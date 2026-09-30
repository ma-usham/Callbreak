using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using PurrNet;
using PurrNet.Transports;
using UnityEngine.Networking;

namespace Callbreak.Networking
{
    /// <summary>
    /// Private rooms over a PurrNet transport (GDD §24 Phase 1: host and relay). With
    /// <see cref="PurrTransport"/> the room code is the relay's room name: the host listens under
    /// it, guests connect to it, and no phone needs an open port. The room and table logic never
    /// sees PurrNet; this class only carries its bytes, reliably and in order, and reports who
    /// connected and who dropped. It pumps the transport itself, so no NetworkManager is needed.
    /// <para>
    /// The transport's callbacks only note what happened. The room logic hears of it from
    /// <see cref="Tick"/>, in the order it happened, each event in its own try/catch: an exception
    /// in a handler is logged and the next event goes ahead, instead of escaping into the
    /// transport's receive loop, which swallows it and delivers nothing more on that connection.
    /// </para>
    /// </summary>
    public sealed class PurrRoomTransport : IRoomTransport, IDisposable
    {
        /// <summary>How long hosting or joining may take before it counts as failed.</summary>
        public const double ConnectTimeoutSeconds = 15.0;

        /// <summary>A host that lost the relay asks for its room again this often.</summary>
        private const double RelistenSeconds = 3.0;

        /// <summary>How long the relay may take to say whether a room name is live.</summary>
        private const int RoomCheckTimeoutSeconds = 5;

        /// <summary>A handler that keeps causing events never holds a frame for longer than this many.</summary>
        private const int MaxNoticesPerDrain = 10000;

        /// <summary>Keeps this game's rooms apart from any other game on the shared relay.</summary>
        private const string RoomPrefix = "callbreak-";

        private readonly GenericTransport _component;
        private readonly ITransport _transport;
        private readonly Queue<Notice> _notices = new Queue<Notice>();

        private HostLink _host;
        private ClientLink _client;
        private TaskCompletionSource<HostLinkResult> _hosting;
        private TaskCompletionSource<ClientLinkResult> _joining;
        private CancellationTokenRegistration _hostingCancel;
        private CancellationTokenRegistration _joiningCancel;
        private UnityWebRequest _roomCheck;
        private string _hostCode;
        private double _now;
        private double _hostingDeadline;
        private double _joiningDeadline;
        private double _relistenAt;
        private bool _disposed;

        public PurrRoomTransport(GenericTransport transport)
        {
            _component = transport != null ? transport : throw new ArgumentNullException(nameof(transport));

            // The relay transport reads and writes its control frames with PurrNet's packers, which a
            // NetworkManager normally registers when it wakes. There is none here, so do it once.
            NetworkManager.LoadOrGenerateHashes();
            _transport = transport.transport;
            _transport.onConnectionState += OnConnectionState;
            _transport.onConnected += OnConnected;
            _transport.onDisconnected += OnDisconnected;
            _transport.onDataReceived += OnDataReceived;
        }

        private enum NoticeKind : byte
        {
            PeerConnected,
            PeerDisconnected,
            HostReceived,
            ClientReceived,
            ClientClosed,
        }

        public Task<HostLinkResult> HostAsync(string roomCode, CancellationToken ct)
        {
            if (_disposed)
            {
                return Task.FromResult(new HostLinkResult(null, RoomError.Unknown));
            }

            FinishHosting(new HostLinkResult(null, RoomError.Unknown));
            if (_host != null)
            {
                _host.Dispose();
            }

            var hosting = new TaskCompletionSource<HostLinkResult>();
            _hosting = hosting;
            _hostCode = roomCode;
            _hostingDeadline = double.NaN;
            if (ct.CanBeCanceled)
            {
                _hostingCancel = ct.Register(() =>
                {
                    if (_hosting == hosting)
                    {
                        _transport.StopListening();
                        FinishHosting(new HostLinkResult(null, RoomError.Unknown));
                    }
                });
            }

            ListenUnlessTaken(roomCode, hosting);
            return hosting.Task;
        }

        public Task<ClientLinkResult> JoinAsync(string roomCode, CancellationToken ct)
        {
            if (_disposed)
            {
                return Task.FromResult(new ClientLinkResult(null, RoomError.Unknown));
            }

            FinishJoining(new ClientLinkResult(null, RoomError.Unknown));
            if (_client != null)
            {
                _client.Dispose();
            }

            var joining = new TaskCompletionSource<ClientLinkResult>();
            _joining = joining;
            _joiningDeadline = double.NaN;
            if (ct.CanBeCanceled)
            {
                _joiningCancel = ct.Register(() =>
                {
                    if (_joining == joining)
                    {
                        _transport.Disconnect();
                        FinishJoining(new ClientLinkResult(null, RoomError.Unknown));
                    }
                });
            }

            SetRoom(roomCode);
            _transport.Connect(null, 0);
            return joining.Task;
        }

        /// <summary>
        /// Moves the transport's traffic, tells the room logic what arrived, and checks the timeouts.
        /// Call once per frame with unscaled time.
        /// </summary>
        public void Tick(float deltaTime, double now)
        {
            _now = now;
            if (_disposed)
            {
                return;
            }

            _transport.ReceiveMessages(deltaTime);
            RaiseNotices();
            _transport.SendMessages(deltaTime);

            // The clocks of an attempt start on the first tick after it was asked for, so a caller
            // that has not been ticked yet cannot time out at once.
            if (_hosting != null && double.IsNaN(_hostingDeadline))
            {
                _hostingDeadline = now + ConnectTimeoutSeconds;
            }

            if (_joining != null && double.IsNaN(_joiningDeadline))
            {
                _joiningDeadline = now + ConnectTimeoutSeconds;
            }

            if (_hosting != null && now >= _hostingDeadline)
            {
                _transport.StopListening();
                FinishHosting(new HostLinkResult(null, RoomError.Unknown));
            }

            if (_joining != null && now >= _joiningDeadline)
            {
                _transport.Disconnect();
                FinishJoining(new ClientLinkResult(null, RoomError.Unknown));
            }

            // A host that lost the relay keeps its table (bots cover the guests) and asks for the
            // same room again, so guests that are reconnecting can find it.
            if (_host != null && !_host.IsDisposed && _hosting == null
                && _transport.listenerState == ConnectionState.Disconnected && now >= _relistenAt)
            {
                _relistenAt = now + RelistenSeconds;
                SetRoom(_hostCode);
                _transport.Listen(0);
            }

            // Whatever the timeouts and the relisten caused is heard in the same frame.
            RaiseNotices();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _notices.Clear();
            if (_roomCheck != null)
            {
                _roomCheck.Abort();
            }

            FinishHosting(new HostLinkResult(null, RoomError.Unknown));
            FinishJoining(new ClientLinkResult(null, RoomError.Unknown));
            _transport.onConnectionState -= OnConnectionState;
            _transport.onConnected -= OnConnected;
            _transport.onDisconnected -= OnDisconnected;
            _transport.onDataReceived -= OnDataReceived;
            if (_host != null)
            {
                _host.Dispose();
            }

            if (_client != null)
            {
                _client.Dispose();
            }
        }

        private void SetRoom(string roomCode)
        {
            if (_component is PurrTransport relay)
            {
                relay.roomName = RoomPrefix + roomCode;
            }
        }

        /// <summary>
        /// GDD Appendix A: codes are drawn at random, so two hosts can draw the same one. The relay
        /// says whether a room name is live through the endpoint guests join by (PurrNet's own
        /// RoomExistsAsync asks it the same way); a live one is answered AlreadyStarted, and the
        /// caller draws another code. Any other answer, or none, goes on to listen, which fails by
        /// itself when the relay cannot be reached.
        /// </summary>
        private void ListenUnlessTaken(string roomCode, TaskCompletionSource<HostLinkResult> hosting)
        {
            string master = _component is PurrTransport relay ? relay.masterServer : null;
            if (string.IsNullOrEmpty(master))
            {
                SetRoom(roomCode);
                _transport.Listen(0);
                return;
            }

            UnityWebRequest request = UnityWebRequest.Get(master.EndsWith("/") ? master + "join" : master + "/join");
            request.SetRequestHeader("name", RoomPrefix + roomCode);
            request.SetRequestHeader("Cache-Control", "no-cache");
            request.useHttpContinue = false;
            request.timeout = RoomCheckTimeoutSeconds;
            _roomCheck = request;
            request.SendWebRequest().completed += _ => OnRoomChecked(request, roomCode, hosting);
        }

        private void OnRoomChecked(UnityWebRequest request, string roomCode, TaskCompletionSource<HostLinkResult> hosting)
        {
            bool taken = request.result == UnityWebRequest.Result.Success && NamesAHost(request.downloadHandler.text);
            if (_roomCheck == request)
            {
                _roomCheck = null;
            }

            request.Dispose();
            if (_disposed || _hosting != hosting)
            {
                // Cancelled, timed out, or replaced by a newer attempt meanwhile.
                return;
            }

            if (taken)
            {
                FinishHosting(new HostLinkResult(null, RoomError.AlreadyStarted));
                return;
            }

            SetRoom(roomCode);
            _transport.Listen(0);
        }

        /// <summary>Only a real join ticket (it names the relay host to connect to) means the room is live.</summary>
        private static bool NamesAHost(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                return !string.IsNullOrEmpty(UnityEngine.JsonUtility.FromJson<ClientJoinInfo>(json).host);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void OnConnectionState(ConnectionState state, bool asServer)
        {
            if (asServer)
            {
                if (state == ConnectionState.Connected)
                {
                    if (_hosting != null)
                    {
                        _host = new HostLink(this);
                        FinishHosting(new HostLinkResult(_host, RoomError.None));
                    }
                }
                else if (state == ConnectionState.Disconnected)
                {
                    if (_hosting != null)
                    {
                        FinishHosting(new HostLinkResult(null, RoomError.Unknown));
                    }
                    else if (_host != null && !_host.IsDisposed)
                    {
                        _host.DropAll();
                        _relistenAt = _now + RelistenSeconds;
                    }
                }

                return;
            }

            if (state == ConnectionState.Connected)
            {
                if (_joining != null)
                {
                    _client = new ClientLink(this);
                    FinishJoining(new ClientLinkResult(_client, RoomError.None));
                }
            }
            else if (state == ConnectionState.Disconnected)
            {
                if (_joining != null)
                {
                    // The relay knows no room under this code (or could not be reached at all).
                    FinishJoining(new ClientLinkResult(null, RoomError.NotFound));
                }
                else if (_client != null)
                {
                    _client.MarkClosed();
                }
            }
        }

        private void OnConnected(Connection conn, bool asServer)
        {
            if (asServer && _host != null && !_host.IsDisposed)
            {
                _host.MarkConnected(PeerOf(conn));
            }
        }

        private void OnDisconnected(Connection conn, DisconnectReason reason, bool asServer)
        {
            if (asServer && _host != null && !_host.IsDisposed)
            {
                _host.MarkDisconnected(PeerOf(conn));
            }
        }

        private void OnDataReceived(Connection conn, ByteData data, bool asServer)
        {
            if (asServer)
            {
                // No member ever sends more than this (GDD §25 "messages are tiny"): anything larger is
                // dropped before it costs a copy.
                if (_host == null || _host.IsDisposed || data.length > Protocol.MaxClientPayloadBytes)
                {
                    return;
                }

                _host.MarkReceived(PeerOf(conn), Copy(data));
            }
            else if (_client != null && _client.IsConnected)
            {
                _notices.Enqueue(new Notice(NoticeKind.ClientReceived, null, _client, default, Copy(data)));
            }
        }

        /// <summary>The transport reuses its buffer; the room logic keeps what it is given.</summary>
        private static byte[] Copy(ByteData data)
        {
            var payload = new byte[data.length];
            Buffer.BlockCopy(data.data, data.offset, payload, 0, data.length);
            return payload;
        }

        /// <summary>Tells the room logic, in order, everything the transport reported since the last time.</summary>
        private void RaiseNotices()
        {
            for (int raised = 0; raised < MaxNoticesPerDrain && _notices.Count > 0 && !_disposed; raised++)
            {
                Notice notice = _notices.Dequeue();
                try
                {
                    notice.Raise();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        private void FinishHosting(HostLinkResult result)
        {
            TaskCompletionSource<HostLinkResult> hosting = _hosting;
            if (hosting == null)
            {
                return;
            }

            _hosting = null;
            _hostingCancel.Dispose();
            _hostingCancel = default;
            hosting.TrySetResult(result);
        }

        private void FinishJoining(ClientLinkResult result)
        {
            TaskCompletionSource<ClientLinkResult> joining = _joining;
            if (joining == null)
            {
                return;
            }

            _joining = null;
            _joiningCancel.Dispose();
            _joiningCancel = default;
            joining.TrySetResult(result);
        }

        private static PeerId PeerOf(Connection conn) => new PeerId((ulong)(uint)conn.connectionId);

        /// <summary>One thing the transport reported, to be told to the room logic from <see cref="Tick"/>.</summary>
        private readonly struct Notice
        {
            private readonly NoticeKind _kind;
            private readonly HostLink _host;
            private readonly ClientLink _client;
            private readonly PeerId _peer;
            private readonly byte[] _payload;

            public Notice(NoticeKind kind, HostLink host, ClientLink client, PeerId peer, byte[] payload)
            {
                _kind = kind;
                _host = host;
                _client = client;
                _peer = peer;
                _payload = payload;
            }

            public void Raise()
            {
                switch (_kind)
                {
                    case NoticeKind.PeerConnected:
                        _host.RaiseConnected(_peer);
                        break;
                    case NoticeKind.PeerDisconnected:
                        _host.RaiseDisconnected(_peer);
                        break;
                    case NoticeKind.HostReceived:
                        _host.RaiseReceived(_peer, _payload);
                        break;
                    case NoticeKind.ClientReceived:
                        _client.RaiseReceived(_payload);
                        break;
                    case NoticeKind.ClientClosed:
                        _client.RaiseClosed();
                        break;
                }
            }
        }

        private sealed class HostLink : IHostLink
        {
            private readonly PurrRoomTransport _owner;
            private readonly HashSet<PeerId> _peers = new HashSet<PeerId>();

            public HostLink(PurrRoomTransport owner)
            {
                _owner = owner;
            }

            public event Action<PeerId> PeerConnected;

            public event Action<PeerId> PeerDisconnected;

            public event Action<PeerId, byte[]> Received;

            public bool IsDisposed { get; private set; }

            public void MarkConnected(PeerId peer)
            {
                if (_peers.Add(peer))
                {
                    _owner._notices.Enqueue(new Notice(NoticeKind.PeerConnected, this, null, peer, null));
                }
            }

            public void MarkDisconnected(PeerId peer)
            {
                if (_peers.Remove(peer))
                {
                    _owner._notices.Enqueue(new Notice(NoticeKind.PeerDisconnected, this, null, peer, null));
                }
            }

            public void MarkReceived(PeerId peer, byte[] payload)
            {
                MarkConnected(peer);
                _owner._notices.Enqueue(new Notice(NoticeKind.HostReceived, this, null, peer, payload));
            }

            public void RaiseConnected(PeerId peer)
            {
                if (!IsDisposed)
                {
                    PeerConnected?.Invoke(peer);
                }
            }

            public void RaiseDisconnected(PeerId peer)
            {
                if (!IsDisposed)
                {
                    PeerDisconnected?.Invoke(peer);
                }
            }

            public void RaiseReceived(PeerId peer, byte[] payload)
            {
                if (!IsDisposed)
                {
                    Received?.Invoke(peer, payload);
                }
            }

            /// <summary>The relay connection is gone: every guest with it.</summary>
            public void DropAll()
            {
                if (_peers.Count == 0)
                {
                    return;
                }

                var gone = new List<PeerId>(_peers);
                _peers.Clear();
                foreach (PeerId peer in gone)
                {
                    _owner._notices.Enqueue(new Notice(NoticeKind.PeerDisconnected, this, null, peer, null));
                }
            }

            public void Send(PeerId peer, byte[] payload)
            {
                if (IsDisposed || payload == null || !_peers.Contains(peer)
                    || _owner._transport.listenerState != ConnectionState.Connected)
                {
                    return;
                }

                _owner._transport.SendToClient(new Connection((int)peer.Value), new ByteData(payload, 0, payload.Length), Channel.ReliableOrdered);
            }

            public void Disconnect(PeerId peer)
            {
                if (IsDisposed || !_peers.Contains(peer))
                {
                    return;
                }

                _owner._transport.CloseConnection(new Connection((int)peer.Value));
            }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                _peers.Clear();
                if (_owner._host == this)
                {
                    _owner._host = null;
                    _owner._transport.StopListening();
                }
            }
        }

        private sealed class ClientLink : IClientLink
        {
            private readonly PurrRoomTransport _owner;
            private bool _disposed;

            public ClientLink(PurrRoomTransport owner)
            {
                _owner = owner;
            }

            public event Action<byte[]> Received;

            public event Action Closed;

            public bool IsConnected { get; private set; } = true;

            /// <summary>What arrived before the connection was lost is still handed over, in order; nothing after a Dispose.</summary>
            public void RaiseReceived(byte[] payload)
            {
                if (!_disposed)
                {
                    Received?.Invoke(payload);
                }
            }

            public void RaiseClosed()
            {
                if (!_disposed)
                {
                    Closed?.Invoke();
                }
            }

            public void Send(byte[] payload)
            {
                if (!IsConnected || payload == null || _owner._transport.clientState != ConnectionState.Connected)
                {
                    return;
                }

                _owner._transport.SendToServer(new ByteData(payload, 0, payload.Length), Channel.ReliableOrdered);
            }

            /// <summary>The connection was lost, or the host dropped it: <see cref="Closed"/> follows everything that arrived before.</summary>
            public void MarkClosed()
            {
                if (!IsConnected)
                {
                    return;
                }

                IsConnected = false;
                if (_owner._client == this)
                {
                    _owner._client = null;
                }

                _owner._notices.Enqueue(new Notice(NoticeKind.ClientClosed, null, this, default, null));
            }

            public void Dispose()
            {
                _disposed = true;
                if (!IsConnected)
                {
                    return;
                }

                IsConnected = false;
                if (_owner._client == this)
                {
                    _owner._client = null;
                    _owner._transport.Disconnect();
                }
            }
        }
    }
}
