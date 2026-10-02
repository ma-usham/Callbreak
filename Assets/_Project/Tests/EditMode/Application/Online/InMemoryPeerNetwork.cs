using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// A peer-to-peer network with a directory, in memory, as Epic Online Services offers one
    /// (<see cref="IPeerNetwork"/>): devices sign in, list room codes in one shared directory and
    /// look them up, and send each other small packets on channels, reliably and in order. Like
    /// <see cref="InMemoryNetwork"/>, nothing is delivered until <see cref="Pump"/> runs: answers,
    /// events and packets all wait for it, as on a real network that answers later.
    /// <para>
    /// It keeps the rules of the EOS adapter. A device's first packet to another asks that device to
    /// connect (<see cref="IPeerNetwork.ConnectionRequested"/>); the connection opens once both ends
    /// accepted it (sending counts as accepting), and what was sent before waits until then. A close
    /// is orderly (what was sent before still arrives) and both ends hear it: the other end, and the
    /// device that asked for it once the close is through, if it had allowed the connection; until
    /// then that device's packets to the pair are dropped, as a packet would open it again. Packets longer than
    /// <see cref="MaxPacketSize"/> and channels EOS would not take as a socket name throw
    /// <see cref="ArgumentException"/>. A cancelled call completes at once with its failure (false,
    /// Unknown, a lookup that found nothing) and never throws, and every task runs its continuations
    /// asynchronously, as the adapter's do.
    /// </para>
    /// <para>
    /// Tests can lose a connection or a listing, unplug a device, keep it from signing in, make the
    /// directory unreachable for it, hold every answer back, list a code elsewhere, and make new
    /// listings take a while to show in lookups (with the network's clock, <see cref="Now"/>).
    /// </para>
    /// </summary>
    public sealed class InMemoryPeerNetwork
    {
        /// <summary>The largest packet EOS P2P sends.</summary>
        public const int EosMaxPacketSize = 1170;

        /// <summary>The longest channel (EOS socket name).</summary>
        public const int MaxChannelLength = 32;

        private readonly Dictionary<string, Device> _devices = new Dictionary<string, Device>();
        private readonly Dictionary<string, string> _listings = new Dictionary<string, string>();
        private readonly Dictionary<string, double> _shownAt = new Dictionary<string, double>();
        private readonly Dictionary<string, Link> _links = new Dictionary<string, Link>();
        private readonly HashSet<string> _closing = new HashSet<string>();
        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly Queue<Action> _held = new Queue<Action>();
        private int _nextDevice = 1;
        private bool _holdAnswers;

        public InMemoryPeerNetwork(int maxPacketSize = EosMaxPacketSize)
        {
            MaxPacketSize = maxPacketSize;
        }

        public int MaxPacketSize { get; }

        /// <summary>The network's time in seconds, as the test drives it: a new listing shows in lookups by it.</summary>
        public double Now { get; set; }

        /// <summary>
        /// How long a new listing takes to show in lookups (EOS: about 8.6 s, measured); until then
        /// a lookup of its code finds nothing. A listing taken over with <see cref="Take"/> shows at once.
        /// </summary>
        public double ListingDelaySeconds { get; set; }

        /// <summary>
        /// While set, every answer of the services (sign-in, listing, look-up) waits, as from a
        /// directory that stopped answering; clearing it lets them through on the next
        /// <see cref="Pump"/>.
        /// </summary>
        public bool HoldAnswers
        {
            get => _holdAnswers;
            set
            {
                _holdAnswers = value;
                while (!value && _held.Count > 0)
                {
                    _queue.Enqueue(_held.Dequeue());
                }
            }
        }

        /// <summary>Every packet a device sent, in order: all that went over the network.</summary>
        public List<(string From, string To, string Channel, byte[] Packet)> Sent { get; } = new List<(string From, string To, string Channel, byte[] Packet)>();

        /// <summary>Connections that exist, open or still being asked for.</summary>
        public int Links => _links.Count;

        /// <summary>True when nothing is in flight and every device has taken what it was sent.</summary>
        public bool IsQuiet
        {
            get
            {
                if (_queue.Count > 0)
                {
                    return false;
                }

                foreach (Device device in _devices.Values)
                {
                    if (device.Waiting > 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public Device AddDevice()
        {
            var device = new Device(this, "device" + _nextDevice++);
            _devices[device.Address] = device;
            return device;
        }

        /// <summary>The address that lists <paramref name="roomCode"/>, shown in lookups yet or not; null when nobody does.</summary>
        public string ListedBy(string roomCode) => _listings.TryGetValue(roomCode, out string address) ? address : null;

        /// <summary>True while <paramref name="a"/> and <paramref name="b"/> have an open connection on <paramref name="channel"/>.</summary>
        public bool AreConnected(Device a, Device b, string channel) => _links.TryGetValue(Key(a.Address, b.Address, channel), out Link link) && link.Open;

        /// <summary>Delivers everything in flight, including what the deliveries themselves cause.</summary>
        public void Pump()
        {
            int guard = 0;
            while (_queue.Count > 0)
            {
                if (++guard > 1000000)
                {
                    throw new InvalidOperationException("The network never settles.");
                }

                _queue.Dequeue()();
            }
        }

        /// <summary>The directory drops the listing of <paramref name="roomCode"/>, as when its device lost the network for a moment; that device hears of it.</summary>
        public void LoseListing(string roomCode)
        {
            if (!_listings.TryGetValue(roomCode, out string address))
            {
                return;
            }

            _listings.Remove(roomCode);
            _shownAt.Remove(roomCode);
            if (_devices.TryGetValue(address, out Device owner))
            {
                _queue.Enqueue(() => owner.RaiseListingLost(roomCode));
            }
        }

        /// <summary>
        /// Someone else lists <paramref name="roomCode"/>. By default an address no device answers
        /// at, as a host whose app vanished leaves its listing behind for a while.
        /// </summary>
        public void Take(string roomCode, string address = "elsewhere")
        {
            _listings[roomCode] = address;
            _shownAt[roomCode] = Now;
        }

        /// <summary>Every connection between <paramref name="a"/> and <paramref name="b"/> is lost: both ends hear it, and what was in flight is gone.</summary>
        public void Break(Device a, Device b)
        {
            foreach (Link link in new List<Link>(_links.Values))
            {
                if (link.Joins(a.Address, b.Address))
                {
                    Lose(link, a.Address, b.Address);
                }
            }
        }

        /// <summary>
        /// The device is gone without a word (its app was killed): every connection of it is lost
        /// for the other end, and nothing reaches it any more. Its listings stay, as the directory
        /// has not noticed yet.
        /// </summary>
        public void Unplug(Device device)
        {
            device.IsUnplugged = true;
            device.ClearInbox();
            foreach (Link link in new List<Link>(_links.Values))
            {
                if (link.A.Address == device.Address || link.B.Address == device.Address)
                {
                    Lose(link, link.Other(device.Address).Address);
                }
            }
        }

        private static string Key(string a, string b, string channel) =>
            string.CompareOrdinal(a, b) < 0 ? a + "\n" + b + "\n" + channel : b + "\n" + a + "\n" + channel;

        /// <summary>EOS takes 1 to 32 characters of A-Z, a-z, 0-9, '-', '_', '+', '=', '.' and space as a socket name.</summary>
        private static void CheckChannel(string channel)
        {
            if (channel == null || channel.Length == 0 || channel.Length > MaxChannelLength)
            {
                throw new ArgumentException("A channel is 1 to " + MaxChannelLength + " characters: \"" + channel + "\".", nameof(channel));
            }

            foreach (char c in channel)
            {
                bool allowed = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || "-_+=. ".IndexOf(c) >= 0;
                if (!allowed)
                {
                    throw new ArgumentException("A channel may not hold '" + c + "': \"" + channel + "\".", nameof(channel));
                }
            }
        }

        private void Answer(Action answer)
        {
            if (_holdAnswers)
            {
                _held.Enqueue(answer);
            }
            else
            {
                _queue.Enqueue(answer);
            }
        }

        private RoomError List(Device device, string roomCode)
        {
            if (!device.IsSignedIn)
            {
                return RoomError.Unknown;
            }

            if (device.DirectoryError != RoomError.None)
            {
                return device.DirectoryError;
            }

            if (_listings.TryGetValue(roomCode, out string owner))
            {
                // Listing a code twice is one listing; anyone else's is a clash, shown in lookups yet or not.
                return owner == device.Address ? RoomError.None : RoomError.AlreadyStarted;
            }

            _listings[roomCode] = device.Address;
            _shownAt[roomCode] = Now + ListingDelaySeconds;
            return RoomError.None;
        }

        private void Unlist(Device device, string roomCode)
        {
            if (_listings.TryGetValue(roomCode, out string owner) && owner == device.Address)
            {
                _listings.Remove(roomCode);
                _shownAt.Remove(roomCode);
            }
        }

        private PeerLookup LookUp(Device device, string roomCode)
        {
            if (!device.IsSignedIn)
            {
                return new PeerLookup(null, RoomError.Unknown);
            }

            if (device.DirectoryError != RoomError.None)
            {
                return new PeerLookup(null, device.DirectoryError);
            }

            bool shown = _listings.TryGetValue(roomCode, out string address) && Now >= _shownAt[roomCode];
            return shown ? new PeerLookup(address, RoomError.None) : new PeerLookup(null, RoomError.NotFound);
        }

        private Link LinkOf(string from, string to, string channel)
        {
            string key = Key(from, to, channel);
            if (!_links.TryGetValue(key, out Link link))
            {
                link = new Link(key, from, to, channel);
                _links[key] = link;
            }

            return link;
        }

        private bool IsCurrent(Link link) => _links.TryGetValue(link.Key, out Link current) && current == link;

        private void Accept(Device device, string to, string channel)
        {
            Link link = LinkOf(device.Address, to, channel);
            link.End(device.Address).Allowed = true;
            TryOpen(link);
        }

        private void Send(Device device, string to, string channel, byte[] packet)
        {
            if (_closing.Contains(device.Address + "\n" + Key(device.Address, to, channel)))
            {
                // The device's close of this connection is not through yet.
                return;
            }

            Sent.Add((device.Address, to, channel, packet));
            Link link = LinkOf(device.Address, to, channel);
            End mine = link.End(device.Address);
            mine.Allowed = true;
            if (link.Open)
            {
                Deliver(link, device.Address, to, packet);
                return;
            }

            mine.Held.Enqueue(packet);
            End theirs = link.Other(device.Address);
            if (!theirs.Allowed && !theirs.Asked)
            {
                // The first packet asks the other device to connect; nothing arrives before it accepts.
                theirs.Asked = true;
                string from = device.Address;
                _queue.Enqueue(() =>
                {
                    if (IsCurrent(link) && !link.Open && _devices.TryGetValue(to, out Device other) && !other.IsUnplugged)
                    {
                        other.RaiseConnectionRequested(from, channel);
                    }
                });
            }

            TryOpen(link);
        }

        private void TryOpen(Link link)
        {
            if (link.Open || !link.A.Allowed || !link.B.Allowed)
            {
                return;
            }

            link.Open = true;
            RaiseOnPump(link, link.A.Address, link.B.Address, (device, other, channel) => device.RaiseConnected(other, channel), whileOpen: true);
            RaiseOnPump(link, link.B.Address, link.A.Address, (device, other, channel) => device.RaiseConnected(other, channel), whileOpen: true);
            while (link.A.Held.Count > 0)
            {
                Deliver(link, link.A.Address, link.B.Address, link.A.Held.Dequeue());
            }

            while (link.B.Held.Count > 0)
            {
                Deliver(link, link.B.Address, link.A.Address, link.B.Held.Dequeue());
            }
        }

        /// <summary>
        /// Closing is orderly: what was sent before still arrives. The other end hears of it, and so
        /// does the closer, once the close is through, for a connection it had allowed; a request
        /// it never allowed just goes.
        /// </summary>
        private void Close(Device device, string to, string channel)
        {
            string key = Key(device.Address, to, channel);
            if (!_links.TryGetValue(key, out Link link))
            {
                return;
            }

            _links.Remove(key);
            if (link.Open || link.Other(device.Address).Allowed)
            {
                RaiseOnPump(link, to, device.Address, (other, closer, ch) => other.RaiseDisconnected(closer, ch), whileOpen: false);
            }

            if (link.Open || link.End(device.Address).Allowed)
            {
                string closing = device.Address + "\n" + key;
                _closing.Add(closing);
                _queue.Enqueue(() =>
                {
                    _closing.Remove(closing);
                    if (!device.IsUnplugged)
                    {
                        device.RaiseDisconnected(to, channel);
                    }
                });
            }
        }

        /// <summary>A connection is lost: whatever was in flight on it is gone, and the given ends hear of it.</summary>
        private void Lose(Link link, params string[] told)
        {
            link.Lost = true;
            _links.Remove(link.Key);
            foreach (string address in told)
            {
                End end = link.End(address);
                if (link.Open || end.Allowed)
                {
                    RaiseOnPump(link, address, link.Other(address).Address, (device, other, channel) => device.RaiseDisconnected(other, channel), whileOpen: false);
                }
            }
        }

        private void RaiseOnPump(Link link, string to, string about, Action<Device, string, string> raise, bool whileOpen)
        {
            _queue.Enqueue(() =>
            {
                if (whileOpen && (!IsCurrent(link) || !link.Open))
                {
                    return;
                }

                if (_devices.TryGetValue(to, out Device device) && !device.IsUnplugged)
                {
                    raise(device, about, link.Channel);
                }
            });
        }

        private void Deliver(Link link, string from, string to, byte[] packet)
        {
            _queue.Enqueue(() =>
            {
                if (!link.Lost && _devices.TryGetValue(to, out Device device) && !device.IsUnplugged)
                {
                    device.Take(from, link.Channel, packet);
                }
            });
        }

        /// <summary>One device on the network: what the app's peer-to-peer adapter would be.</summary>
        public sealed class Device : IPeerNetwork
        {
            private readonly InMemoryPeerNetwork _network;
            private readonly Queue<(string Address, string Channel, byte[] Packet)> _inbox = new Queue<(string Address, string Channel, byte[] Packet)>();

            internal Device(InMemoryPeerNetwork network, string address)
            {
                _network = network;
                Address = address;
            }

            public event Action<string, string> ConnectionRequested;

            public event Action<string, string> Connected;

            public event Action<string, string> Disconnected;

            public event Action<string> ListingLost;

            /// <summary>The address the device has once it is signed in.</summary>
            public string Address { get; }

            public int MaxPacketSize => _network.MaxPacketSize;

            public string LocalAddress => IsSignedIn ? Address : null;

            public bool IsSignedIn { get; private set; }

            /// <summary>Clear to make signing in fail, as on a device that cannot reach the service.</summary>
            public bool CanSignIn { get; set; } = true;

            /// <summary>Offline or Unknown makes the directory unreachable from this device: listing and looking up answer that.</summary>
            public RoomError DirectoryError { get; set; } = RoomError.None;

            public bool IsUnplugged { get; internal set; }

            public int SignInCalls { get; private set; }

            public int ListCalls { get; private set; }

            public int LookUpCalls { get; private set; }

            /// <summary>Packets delivered that the device has not taken yet.</summary>
            public int Waiting => _inbox.Count;

            public Task<bool> SignInAsync(CancellationToken ct)
            {
                SignInCalls++;
                var answer = Pending(ct, false);
                _network.Answer(() =>
                {
                    if (answer.Task.IsCompleted || IsUnplugged)
                    {
                        return;
                    }

                    if (CanSignIn)
                    {
                        IsSignedIn = true;
                    }

                    answer.TrySetResult(IsSignedIn);
                });
                return answer.Task;
            }

            public Task<RoomError> ListAsync(string roomCode, CancellationToken ct)
            {
                ListCalls++;
                var answer = Pending(ct, RoomError.Unknown);
                _network.Answer(() =>
                {
                    if (!answer.Task.IsCompleted && !IsUnplugged)
                    {
                        answer.TrySetResult(_network.List(this, roomCode));
                    }
                });
                return answer.Task;
            }

            public void Unlist(string roomCode) => _network.Unlist(this, roomCode);

            public Task<PeerLookup> LookUpAsync(string roomCode, CancellationToken ct)
            {
                LookUpCalls++;
                var answer = Pending(ct, new PeerLookup(null, RoomError.Unknown));
                _network.Answer(() =>
                {
                    if (!answer.Task.IsCompleted && !IsUnplugged)
                    {
                        answer.TrySetResult(_network.LookUp(this, roomCode));
                    }
                });
                return answer.Task;
            }

            public void Accept(string address, string channel)
            {
                CheckChannel(channel);
                if (IsUnplugged)
                {
                    return;
                }

                RequireSignIn();
                _network.Accept(this, address, channel);
            }

            public void Close(string address, string channel)
            {
                CheckChannel(channel);
                if (!IsUnplugged)
                {
                    _network.Close(this, address, channel);
                }
            }

            public void Send(string address, string channel, byte[] packet)
            {
                CheckChannel(channel);
                if (packet == null)
                {
                    throw new ArgumentNullException(nameof(packet));
                }

                if (packet.Length == 0 || packet.Length > MaxPacketSize)
                {
                    throw new ArgumentException("A packet is 1 to " + MaxPacketSize + " bytes, not " + packet.Length + ".", nameof(packet));
                }

                if (IsUnplugged)
                {
                    return;
                }

                RequireSignIn();
                _network.Send(this, address, channel, packet);
            }

            public bool TryReceive(out string address, out string channel, out byte[] packet)
            {
                if (_inbox.Count == 0)
                {
                    address = null;
                    channel = null;
                    packet = null;
                    return false;
                }

                (address, channel, packet) = _inbox.Dequeue();
                return true;
            }

            internal void Take(string from, string channel, byte[] packet) => _inbox.Enqueue((from, channel, packet));

            internal void ClearInbox() => _inbox.Clear();

            internal void RaiseConnectionRequested(string address, string channel) => ConnectionRequested?.Invoke(address, channel);

            internal void RaiseConnected(string address, string channel) => Connected?.Invoke(address, channel);

            internal void RaiseDisconnected(string address, string channel) => Disconnected?.Invoke(address, channel);

            internal void RaiseListingLost(string roomCode) => ListingLost?.Invoke(roomCode);

            /// <summary>An answer to come; cancelled first, it is the call's failure at once.</summary>
            private static TaskCompletionSource<T> Pending<T>(CancellationToken ct, T cancelled)
            {
                var answer = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (ct.CanBeCanceled)
                {
                    ct.Register(() => answer.TrySetResult(cancelled));
                }

                return answer;
            }

            private void RequireSignIn()
            {
                if (!IsSignedIn)
                {
                    throw new InvalidOperationException("The device is not signed in.");
                }
            }
        }

        private sealed class Link
        {
            public Link(string key, string a, string b, string channel)
            {
                Key = key;
                A = new End(a);
                B = new End(b);
                Channel = channel;
            }

            public string Key { get; }

            public string Channel { get; }

            public End A { get; }

            public End B { get; }

            public bool Open { get; set; }

            public bool Lost { get; set; }

            public End End(string address) => A.Address == address ? A : B;

            public End Other(string address) => A.Address == address ? B : A;

            public bool Joins(string a, string b) => (A.Address == a && B.Address == b) || (A.Address == b && B.Address == a);
        }

        private sealed class End
        {
            public End(string address)
            {
                Address = address;
            }

            public string Address { get; }

            /// <summary>This end accepted the connection, or sent over it.</summary>
            public bool Allowed { get; set; }

            /// <summary>This end was asked to connect.</summary>
            public bool Asked { get; set; }

            /// <summary>What this end sent before the connection opened.</summary>
            public Queue<byte[]> Held { get; } = new Queue<byte[]>();
        }
    }
}
