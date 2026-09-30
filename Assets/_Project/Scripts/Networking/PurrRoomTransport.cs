using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using PurrNet;
using PurrNet.Transports;

namespace Callbreak.Networking
{
    /// <summary>
    /// Private rooms over a PurrNet transport (GDD §24 Phase 1: host and relay). With
    /// <see cref="PurrTransport"/> the room code is the relay's room name: the host listens under
    /// it, guests connect to it, and no phone needs an open port. The room and table logic never
    /// sees PurrNet; this class only carries its bytes, reliably and in order, and reports who
    /// connected and who dropped. It pumps the transport itself, so no NetworkManager is needed.
    /// </summary>
    public sealed class PurrRoomTransport : IRoomTransport, IDisposable
    {
        /// <summary>How long hosting or joining may take before it counts as failed.</summary>
        public const double ConnectTimeoutSeconds = 15.0;

        /// <summary>A host that lost the relay asks for its room again this often.</summary>
        private const double RelistenSeconds = 3.0;

        /// <summary>Keeps this game's rooms apart from any other game on the shared relay.</summary>
        private const string RoomPrefix = "callbreak-";

        private readonly GenericTransport _component;
        private readonly ITransport _transport;

        private HostLink _host;
        private ClientLink _client;
        private TaskCompletionSource<HostLinkResult> _hosting;
        private TaskCompletionSource<ClientLinkResult> _joining;
        private CancellationTokenRegistration _hostingCancel;
        private CancellationTokenRegistration _joiningCancel;
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

            SetRoom(roomCode);
            _transport.Listen(0);
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
        /// Moves the transport's traffic and checks the timeouts. Call once per frame with unscaled time.
        /// </summary>
        public void Tick(float deltaTime, double now)
        {
            _now = now;
            if (_disposed)
            {
                return;
            }

            _transport.ReceiveMessages(deltaTime);
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
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
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
                    _client.Close();
                }
            }
        }

        private void OnConnected(Connection conn, bool asServer)
        {
            if (asServer && _host != null && !_host.IsDisposed)
            {
                _host.RaiseConnected(PeerOf(conn));
            }
        }

        private void OnDisconnected(Connection conn, DisconnectReason reason, bool asServer)
        {
            if (asServer && _host != null && !_host.IsDisposed)
            {
                _host.RaiseDisconnected(PeerOf(conn));
            }
        }

        private void OnDataReceived(Connection conn, ByteData data, bool asServer)
        {
            // The transport reuses its buffer; the room logic keeps what it is given.
            var payload = new byte[data.length];
            Buffer.BlockCopy(data.data, data.offset, payload, 0, data.length);
            if (asServer)
            {
                if (_host != null && !_host.IsDisposed)
                {
                    _host.RaiseReceived(PeerOf(conn), payload);
                }
            }
            else if (_client != null && _client.IsConnected)
            {
                _client.RaiseReceived(payload);
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

            public void RaiseConnected(PeerId peer)
            {
                if (_peers.Add(peer))
                {
                    PeerConnected?.Invoke(peer);
                }
            }

            public void RaiseDisconnected(PeerId peer)
            {
                if (_peers.Remove(peer))
                {
                    PeerDisconnected?.Invoke(peer);
                }
            }

            public void RaiseReceived(PeerId peer, byte[] payload)
            {
                if (_peers.Add(peer))
                {
                    PeerConnected?.Invoke(peer);
                }

                Received?.Invoke(peer, payload);
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
                    PeerDisconnected?.Invoke(peer);
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

            public ClientLink(PurrRoomTransport owner)
            {
                _owner = owner;
            }

            public event Action<byte[]> Received;

            public event Action Closed;

            public bool IsConnected { get; private set; } = true;

            public void RaiseReceived(byte[] payload) => Received?.Invoke(payload);

            public void Send(byte[] payload)
            {
                if (!IsConnected || payload == null || _owner._transport.clientState != ConnectionState.Connected)
                {
                    return;
                }

                _owner._transport.SendToServer(new ByteData(payload, 0, payload.Length), Channel.ReliableOrdered);
            }

            /// <summary>The connection was lost, or the host dropped it.</summary>
            public void Close()
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

                Closed?.Invoke();
            }

            public void Dispose()
            {
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
