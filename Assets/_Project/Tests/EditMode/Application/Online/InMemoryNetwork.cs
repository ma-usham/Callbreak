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

        public int Delivered { get; private set; }

        /// <summary>How often a device asked the relay to host a room, whatever the answer was.</summary>
        public int HostAttempts { get; private set; }

        public Task<HostLinkResult> HostAsync(string roomCode, CancellationToken ct)
        {
            HostAttempts++;
            if (HostError != RoomError.None)
            {
                return Task.FromResult(new HostLinkResult(null, HostError));
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

            var peer = new PeerId(_nextPeer++);
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

                _queue.Dequeue()();
                Delivered++;
            }
        }

        /// <summary>The connection of <paramref name="link"/> is lost on both ends.</summary>
        public void Drop(IClientLink link)
        {
            var client = (ClientLink)link;
            client.Host.Remove(client.Peer, notifyHost: true);
            client.Close();
        }

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
