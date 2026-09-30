using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// A relay in memory: rooms are hosted under a code, members join by it, and nothing is
    /// delivered until <see cref="Pump"/> runs, like a real network that answers later. Tests can
    /// drop a connection or lose the next message to a client.
    /// </summary>
    public sealed class InMemoryNetwork : IRoomTransport
    {
        private readonly Dictionary<string, HostLink> _hosts = new Dictionary<string, HostLink>();
        private readonly Queue<Action> _queue = new Queue<Action>();
        private ulong _nextPeer = 1;

        /// <summary>Set to make <see cref="HostAsync"/> fail, as when the relay is unreachable.</summary>
        public RoomError HostError { get; set; } = RoomError.None;

        /// <summary>Set to give the next connection this id, as a relay that hands out a freed id again would.</summary>
        public ulong? NextPeerId { get; set; }

        public int Delivered { get; private set; }

        /// <summary>How often a device asked the relay to host a room, whatever the answer was.</summary>
        public int HostAttempts { get; private set; }

        /// <summary>Every payload a host sent to a guest, in order: all that leaves the host's device.</summary>
        public List<byte[]> HostPayloads { get; } = new List<byte[]>();

        /// <summary>Every payload a guest sent, in order, with the code of the room it went to: all a host is handed.</summary>
        public List<(string Code, byte[] Payload)> ClientPayloads { get; } = new List<(string Code, byte[] Payload)>();

        /// <summary>
        /// When set, an exception thrown by whatever handles a delivery is recorded in
        /// <see cref="Faults"/> and the next delivery goes ahead, as the real transport logs it and
        /// carries on. When not set it leaves <see cref="Pump"/>, so no test misses one.
        /// </summary>
        public bool CollectFaults { get; set; }

        public List<Exception> Faults { get; } = new List<Exception>();

        public Task<HostLinkResult> HostAsync(string roomCode, CancellationToken ct)
        {
            HostAttempts++;
            if (HostError != RoomError.None)
            {
                return Task.FromResult(new HostLinkResult(null, HostError));
            }

            if (_hosts.TryGetValue(roomCode, out HostLink live) && !live.IsDisposed)
            {
                // Someone is hosting this code right now: a second host never takes it over.
                return Task.FromResult(new HostLinkResult(null, RoomError.AlreadyStarted));
            }

            var link = new HostLink(this, roomCode);
            _hosts[roomCode] = link;
            return Task.FromResult(new HostLinkResult(link, RoomError.None));
        }

        public Task<ClientLinkResult> JoinAsync(string roomCode, CancellationToken ct)
        {
            if (!_hosts.TryGetValue(roomCode, out HostLink host) || host.IsDisposed)
            {
                return Task.FromResult(new ClientLinkResult(null, RoomError.NotFound));
            }

            var peer = new PeerId(NextPeerId ?? _nextPeer++);
            NextPeerId = null;
            var client = new ClientLink(this, host, peer);
            host.Add(peer, client);
            _queue.Enqueue(() => host.RaiseConnected(peer));
            return Task.FromResult(new ClientLinkResult(client, RoomError.None));
        }

        /// <summary>Delivers everything in flight, including what the deliveries themselves send.</summary>
        public void Pump()
        {
            int guard = 0;
            while (_queue.Count > 0)
            {
                if (++guard > 100000)
                {
                    throw new InvalidOperationException("The network never settles.");
                }

                Action delivery = _queue.Dequeue();
                Delivered++;
                if (!CollectFaults)
                {
                    delivery();
                    continue;
                }

                try
                {
                    delivery();
                }
                catch (Exception e)
                {
                    Faults.Add(e);
                }
            }
        }

        /// <summary>The connection of <paramref name="link"/> is lost on both ends.</summary>
        public void Drop(IClientLink link)
        {
            var client = (ClientLink)link;
            client.Host.Remove(client.Peer, notifyHost: true);
            client.Close();
        }

        /// <summary>The id the host knows the connection of <paramref name="link"/> by.</summary>
        public PeerId PeerOf(IClientLink link) => ((ClientLink)link).Peer;

        /// <summary>The next <paramref name="count"/> messages to this client never arrive.</summary>
        public void LoseNext(IClientLink link, int count = 1)
        {
            ((ClientLink)link).Lose += count;
        }

        private void Enqueue(Action delivery) => _queue.Enqueue(delivery);

        private sealed class HostLink : IHostLink
        {
            private readonly InMemoryNetwork _network;
            private readonly Dictionary<PeerId, ClientLink> _clients = new Dictionary<PeerId, ClientLink>();

            public HostLink(InMemoryNetwork network, string code)
            {
                _network = network;
                Code = code;
            }

            public event Action<PeerId> PeerConnected;

            public event Action<PeerId> PeerDisconnected;

            public event Action<PeerId, byte[]> Received;

            public string Code { get; }

            public bool IsDisposed { get; private set; }

            public void Add(PeerId peer, ClientLink client) => _clients[peer] = client;

            public void RaiseConnected(PeerId peer) => PeerConnected?.Invoke(peer);

            public void Remove(PeerId peer, bool notifyHost)
            {
                if (_clients.Remove(peer) && notifyHost)
                {
                    _network.Enqueue(() => PeerDisconnected?.Invoke(peer));
                }
            }

            public void Deliver(PeerId peer, byte[] payload)
            {
                if (!IsDisposed && _clients.ContainsKey(peer))
                {
                    Received?.Invoke(peer, payload);
                }
            }

            public void Send(PeerId peer, byte[] payload)
            {
                if (IsDisposed || !_clients.TryGetValue(peer, out ClientLink client))
                {
                    return;
                }

                _network.HostPayloads.Add(payload);
                _network.Enqueue(() => client.Deliver(payload));
            }

            public void Disconnect(PeerId peer)
            {
                if (_clients.TryGetValue(peer, out ClientLink client))
                {
                    _clients.Remove(peer);
                    _network.Enqueue(client.Close);
                    _network.Enqueue(() => PeerDisconnected?.Invoke(peer));
                }
            }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                foreach (ClientLink client in _clients.Values)
                {
                    _network.Enqueue(client.Close);
                }

                _clients.Clear();
            }
        }

        private sealed class ClientLink : IClientLink
        {
            private readonly InMemoryNetwork _network;

            public ClientLink(InMemoryNetwork network, HostLink host, PeerId peer)
            {
                _network = network;
                Host = host;
                Peer = peer;
            }

            public event Action<byte[]> Received;

            public event Action Closed;

            public HostLink Host { get; }

            public PeerId Peer { get; }

            public int Lose { get; set; }

            public bool IsConnected { get; private set; } = true;

            public void Send(byte[] payload)
            {
                if (IsConnected)
                {
                    _network.ClientPayloads.Add((Host.Code, payload));
                    _network.Enqueue(() => Host.Deliver(Peer, payload));
                }
            }

            public void Deliver(byte[] payload)
            {
                if (!IsConnected)
                {
                    return;
                }

                if (Lose > 0)
                {
                    Lose--;
                    return;
                }

                Received?.Invoke(payload);
            }

            public void Close()
            {
                if (!IsConnected)
                {
                    return;
                }

                IsConnected = false;
                Closed?.Invoke();
            }

            public void Dispose()
            {
                if (IsConnected)
                {
                    IsConnected = false;
                    Host.Remove(Peer, notifyHost: true);
                }
            }
        }
    }
}
