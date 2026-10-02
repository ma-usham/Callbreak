using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Rooms;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// Private rooms over a peer-to-peer network with a directory (<see cref="IPeerNetwork"/>; Epic
    /// Online Services in the app). The host lists its room code and accepts connections on the
    /// room's channel; a guest looks the code up, accepts the host's address on that channel and
    /// opens the connection with an empty packet (<see cref="PacketFramer.OpenPacket"/>), which the
    /// room never sees. Room messages are cut to the network's small packets and put back together
    /// per sender (<see cref="PacketFramer"/>); a guest's message longer than
    /// <see cref="Protocol.MaxClientPayloadBytes"/> is dropped as soon as its size is known. In
    /// everything the room logic sees it behaves like the relay's transport (PurrRoomTransport).
    /// <para>
    /// The network's events only note what happened. The room logic hears of it from
    /// <see cref="Tick"/>, in the order it happened (what arrived before a connection dropped is
    /// handed over before the drop), each notice in its own try/catch: a handler that throws is
    /// reported to the fault handler and the next notice goes ahead. Hosting and joining finish
    /// from <see cref="Tick"/> too: a step that takes longer than <see cref="ConnectTimeoutSeconds"/>
    /// fails, and a guest looks a code up again for <see cref="LookUpBudgetSeconds"/> before it is
    /// not found, as a directory shows a new listing only after a while. A host whose listing is
    /// lost lists its code again every <see cref="RelistSeconds"/> until it is back, so guests that
    /// reconnect find the table; its guests' connections do not depend on the listing.
    /// </para>
    /// <para>
    /// Quick Match (<see cref="IQuickMatchTransport"/>, ADR 029) opens tables that are listed in its
    /// own directory instead (<see cref="OpenAsync"/>), and joins a table's host at the address that
    /// directory gave (<see cref="JoinAtAsync(string, string, CancellationToken)"/>).
    /// </para>
    /// <para>
    /// A player who takes a room over from a lost host (<see cref="IHandOverTransport"/>, ADR 030)
    /// hosts the room's next generation, on a channel of its own (the code and the generation), so
    /// that it never mixes with the generation before; a private room is listed again at that
    /// generation, and a lookup finds the latest. A host of a later generation takes members'
    /// messages up to <see cref="Protocol.MaxReportBytes"/>, for the reports they bring it.
    /// </para>
    /// </summary>
    public sealed class PeerRoomTransport : ITickedRoomTransport, IQuickMatchTransport, IHandOverTransport, IDisposable
    {
        /// <summary>
        /// How long hosting may take, and each step of joining: signing in, one lookup, and
        /// connecting once the host is found.
        /// </summary>
        public const double ConnectTimeoutSeconds = 15.0;

        /// <summary>
        /// How long a guest keeps looking up a code nobody lists before it counts as not found. The
        /// directory takes a while to show a new listing (EOS about 9 s), and a guest may type the
        /// code before that; the relay, too, took about 10 s to say a room was not there.
        /// </summary>
        public const double LookUpBudgetSeconds = 12.0;

        /// <summary>The pause between one lookup that found nothing and the next.</summary>
        public const double LookUpPauseSeconds = 1.0;

        /// <summary>A host whose listing was lost lists its code again this often until it is back.</summary>
        public const double RelistSeconds = 3.0;

        /// <summary>
        /// The longest message a member puts together from its host: far more than any message of
        /// the protocol (the longest in a whole test match was 629 bytes; four long names add a few
        /// hundred), so only a broken or hostile host's is dropped, unread, before it costs more
        /// memory than this.
        /// </summary>
        public const int MaxHostMessageBytes = 1 << 20;

        /// <summary>The start of every room's channel; the room code follows. Codes are A-Z and 2-9, so the name is alphanumeric.</summary>
        public const string ChannelPrefix = "cb";

        /// <summary>A handler that keeps causing notices never holds a frame for longer than this many.</summary>
        private const int MaxNoticesPerDrain = 10000;

        /// <summary>A peer that floods the network never holds a frame for longer than this many packets; the rest wait.</summary>
        private const int MaxPacketsPerDrain = 10000;

        private readonly IPeerNetwork _network;
        private readonly Action<Exception> _onFault;
        private readonly Queue<Notice> _notices = new Queue<Notice>();
        private readonly List<StrayListing> _strays = new List<StrayListing>();

        private HostAttempt _hosting;
        private JoinAttempt _joining;
        private HostLink _host;
        private ClientLink _client;
        private ulong _lastPeer;
        private double _now;
        private bool _absorbing;
        private bool _disposed;

        /// <param name="network">The device's peer-to-peer network; its events are raised on the main thread.</param>
        /// <param name="onFault">
        /// Hears every exception a room handler threw while a notice was raised, and every failure
        /// of the network itself, for the app to log. Nothing is thrown out of <see cref="Tick"/>.
        /// </param>
        public PeerRoomTransport(IPeerNetwork network, Action<Exception> onFault)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _onFault = onFault ?? throw new ArgumentNullException(nameof(onFault));
            if (network.MaxPacketSize < PacketFramer.MinPacketSize)
            {
                throw new ArgumentException("The network's packets are too small to carry room messages.", nameof(network));
            }

            _network.ConnectionRequested += OnConnectionRequested;
            _network.Connected += OnConnected;
            _network.Disconnected += OnDisconnected;
            _network.ListingLost += OnListingLost;
        }

        private enum NoticeKind : byte
        {
            PeerConnected,
            PeerDisconnected,
            HostReceived,
            ClientReceived,
            ClientClosed,
            ListingLost,
        }

        /// <summary>
        /// The directory dropped a listing this transport does not keep itself, such as a Quick Match
        /// table's (the room code). Raised from <see cref="Tick"/>, in order with everything else.
        /// </summary>
        public event Action<string> ListingLost;

        /// <summary>This device's address once it is signed in; null before.</summary>
        public string LocalAddress => _network.LocalAddress;

        /// <summary>Signs this device in if it is not yet (the table directory needs it); false when the network cannot be reached.</summary>
        public Task<bool> SignInAsync(CancellationToken ct)
        {
            return _disposed ? Task.FromResult(false) : Call(() => _network.SignInAsync(ct));
        }

        /// <summary>
        /// Signs in and lists <paramref name="roomCode"/>. AlreadyStarted when another device lists
        /// it (the caller draws another code); Offline when the device cannot sign in or the
        /// directory says so; Unknown otherwise, after <see cref="ConnectTimeoutSeconds"/> at most, or
        /// at once when <paramref name="ct"/> is cancelled. One room is hosted at a time: a new call
        /// ends the attempt before it and lets go of the room hosted before.
        /// </summary>
        public Task<HostLinkResult> HostAsync(string roomCode, CancellationToken ct) => Host(roomCode, listed: true, 0, ct);

        /// <summary>
        /// Signs in and hosts <paramref name="roomCode"/> without listing it: only a guest that knows
        /// this device's address joins (Quick Match lists its tables in a directory of their own).
        /// Offline when the device cannot sign in; Unknown otherwise, as <see cref="HostAsync"/>.
        /// </summary>
        public Task<HostLinkResult> OpenAsync(string roomCode, CancellationToken ct) => Host(roomCode, listed: false, 0, ct);

        /// <summary>
        /// Signs in and hosts generation <paramref name="generation"/> of <paramref name="roomCode"/>
        /// (ADR 030), on that generation's channel; <paramref name="listed"/>, the code is listed at
        /// that generation. AlreadyStarted when another device lists the code at that generation or a
        /// later one; otherwise as <see cref="HostAsync"/>.
        /// </summary>
        public Task<HostLinkResult> TakeOverAsync(string roomCode, int generation, bool listed, CancellationToken ct)
        {
            return generation < 1 ? Task.FromResult(HostFailed(RoomError.Unknown)) : Host(roomCode, listed, generation, ct);
        }

        /// <summary>
        /// Signs in, looks <paramref name="roomCode"/> up and connects to its host. A code not found
        /// is looked up again, after a pause, until <see cref="LookUpBudgetSeconds"/> have gone by:
        /// then it is NotFound. NotFound also when the device that lists the code turns the
        /// connection down; Offline when the device cannot sign in or the directory says so, at
        /// once; Unknown otherwise, when a step takes longer than <see cref="ConnectTimeoutSeconds"/>
        /// (the connection's own clock starts once the host is found), or at once when
        /// <paramref name="ct"/> is cancelled. One connection to a host at a time: a new call ends
        /// the attempt before it and lets go of the connection made before.
        /// </summary>
        public Task<ClientLinkResult> JoinAsync(string roomCode, CancellationToken ct) => Join(roomCode, null, 0, ct);

        /// <summary>
        /// Signs in and connects to the host at <paramref name="hostAddress"/> on
        /// <paramref name="roomCode"/>'s channel, with no lookup. NotFound when that device turns
        /// the connection down or is this one; otherwise as <see cref="JoinAsync"/>.
        /// </summary>
        public Task<ClientLinkResult> JoinAtAsync(string roomCode, string hostAddress, CancellationToken ct)
        {
            return JoinAtAsync(roomCode, 0, hostAddress, ct);
        }

        /// <summary>
        /// Signs in and connects to the host of generation <paramref name="generation"/> of
        /// <paramref name="roomCode"/> at <paramref name="hostAddress"/>, on that generation's channel,
        /// with no lookup (ADR 030). NotFound when that device does not host that generation of the
        /// room, or is this one; otherwise as <see cref="JoinAsync"/>.
        /// </summary>
        public Task<ClientLinkResult> JoinAtAsync(string roomCode, int generation, string hostAddress, CancellationToken ct)
        {
            return string.IsNullOrEmpty(hostAddress) || generation < 0 ? Task.FromResult(JoinFailed(RoomError.NotFound)) : Join(roomCode, hostAddress, generation, ct);
        }

        /// <summary>The channel of generation <paramref name="generation"/> of a room: the prefix and the code, and the generation after a dot from 1 on.</summary>
        internal static string ChannelOf(string roomCode, int generation)
        {
            return generation > 0
                ? ChannelPrefix + roomCode + "." + generation.ToString(CultureInfo.InvariantCulture)
                : ChannelPrefix + roomCode;
        }

        private Task<HostLinkResult> Host(string roomCode, bool listed, int generation, CancellationToken ct)
        {
            if (_disposed || !RoomCode.IsValid(roomCode))
            {
                return Task.FromResult(HostFailed(RoomError.Unknown));
            }

            EndHosting(_hosting, HostFailed(RoomError.Unknown));
            if (_host != null)
            {
                _host.Dispose();
            }

            var attempt = new HostAttempt(roomCode, listed, generation);
            _hosting = attempt;
            if (ct.IsCancellationRequested)
            {
                EndHosting(attempt, HostFailed(RoomError.Unknown));
                return attempt.Result.Task;
            }

            if (ct.CanBeCanceled)
            {
                attempt.Registration = ct.Register(() => EndHosting(attempt, HostFailed(RoomError.Unknown)));
                if (attempt.Ended)
                {
                    // Cancelled while it was being registered.
                    return attempt.Result.Task;
                }
            }

            attempt.SignIn = Call(() => _network.SignInAsync(attempt.Abort.Token));
            return attempt.Result.Task;
        }

        /// <param name="knownAddress">The host's address when it is known already; null looks the code up.</param>
        /// <param name="generation">The generation hosted at <paramref name="knownAddress"/>; a lookup finds it out.</param>
        private Task<ClientLinkResult> Join(string roomCode, string knownAddress, int generation, CancellationToken ct)
        {
            if (_disposed)
            {
                return Task.FromResult(JoinFailed(RoomError.Unknown));
            }

            if (!RoomCode.IsValid(roomCode))
            {
                return Task.FromResult(JoinFailed(RoomError.NotFound));
            }

            EndJoining(_joining, JoinFailed(RoomError.Unknown));
            if (_client != null)
            {
                _client.Dispose();
            }

            var attempt = new JoinAttempt(roomCode, knownAddress, generation);
            _joining = attempt;
            if (ct.IsCancellationRequested)
            {
                EndJoining(attempt, JoinFailed(RoomError.Unknown));
                return attempt.Result.Task;
            }

            if (ct.CanBeCanceled)
            {
                attempt.Registration = ct.Register(() => EndJoining(attempt, JoinFailed(RoomError.Unknown)));
                if (attempt.Ended)
                {
                    // Cancelled while it was being registered.
                    return attempt.Result.Task;
                }
            }

            attempt.SignIn = Call(() => _network.SignInAsync(attempt.Abort.Token));
            return attempt.Result.Task;
        }

        /// <summary>
        /// Takes what the network delivered, tells the room logic what happened, moves hosting,
        /// joining and relisting on, and checks the timeouts. Call once per frame with unscaled time.
        /// </summary>
        public void Tick(float deltaTime, double now)
        {
            _now = now;
            if (_disposed)
            {
                // A listing still on its way when the transport was disposed is taken down once it arrives.
                SweepStrays();
                return;
            }

            Absorb();
            RaiseNotices();
            AdvanceHosting(now);
            AdvanceJoining(now);
            if (_host != null)
            {
                _host.Relist(now);
            }

            SweepStrays();

            // Whatever the attempts and the timeouts caused is heard in the same frame.
            RaiseNotices();
        }

        /// <summary>Ends what is under way, takes the room's listing down and closes every connection.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _notices.Clear();
            _network.ConnectionRequested -= OnConnectionRequested;
            _network.Connected -= OnConnected;
            _network.Disconnected -= OnDisconnected;
            _network.ListingLost -= OnListingLost;
            EndHosting(_hosting, HostFailed(RoomError.Unknown));
            EndJoining(_joining, JoinFailed(RoomError.Unknown));
            if (_host != null)
            {
                _host.Dispose();
            }

            if (_client != null)
            {
                _client.Dispose();
            }

            // Listings that already arrived are taken down now; one still on its way, by a later tick.
            SweepStrays();
        }

        private static HostLinkResult HostFailed(RoomError error) => new HostLinkResult(null, error);

        private static ClientLinkResult JoinFailed(RoomError error) => new ClientLinkResult(null, error);

        private static bool IsRoomChannel(string channel) => channel.StartsWith(ChannelPrefix, StringComparison.Ordinal);

        private void AdvanceHosting(double now)
        {
            HostAttempt attempt = _hosting;
            if (attempt == null)
            {
                return;
            }

            // The clock of an attempt starts on the first tick after it was asked for, so a caller
            // that has not been ticked yet cannot time out at once.
            if (double.IsNaN(attempt.Deadline))
            {
                attempt.Deadline = now + ConnectTimeoutSeconds;
            }

            if (attempt.List == null && attempt.SignIn.IsCompleted)
            {
                if (!Answered(attempt.SignIn, out bool signedIn))
                {
                    EndHosting(attempt, HostFailed(RoomError.Unknown));
                    return;
                }

                if (!signedIn)
                {
                    EndHosting(attempt, HostFailed(RoomError.Offline));
                    return;
                }

                if (!attempt.Listed)
                {
                    // A room that is not listed is hosted as soon as the device is signed in.
                    var unlisted = new HostLink(this, attempt.Code, attempt.Channel, listed: false, attempt.Generation);
                    _host = unlisted;
                    EndHosting(attempt, new HostLinkResult(unlisted, RoomError.None));
                    return;
                }

                attempt.List = Call(() => ListCode(attempt.Code, attempt.Generation, attempt.Abort.Token));
            }

            if (attempt.List != null && attempt.List.IsCompleted)
            {
                RoomError listed = Answered(attempt.List, out RoomError answer) ? answer : RoomError.Unknown;
                if (listed == RoomError.None)
                {
                    var link = new HostLink(this, attempt.Code, attempt.Channel, listed: true, attempt.Generation);
                    _host = link;
                    if (attempt.ListingLost)
                    {
                        link.LoseListing(now);
                    }

                    EndHosting(attempt, new HostLinkResult(link, RoomError.None));
                    return;
                }

                EndHosting(attempt, HostFailed(listed == RoomError.AlreadyStarted || listed == RoomError.Offline ? listed : RoomError.Unknown));
                return;
            }

            if (now >= attempt.Deadline)
            {
                EndHosting(attempt, HostFailed(RoomError.Unknown));
            }
        }

        private void AdvanceJoining(double now)
        {
            JoinAttempt attempt = _joining;
            if (attempt == null)
            {
                return;
            }

            // Each step has a clock of its own, started on the tick that began it; signing in, on the
            // first tick after the attempt was asked for.
            if (double.IsNaN(attempt.Deadline))
            {
                attempt.Deadline = now + ConnectTimeoutSeconds;
            }

            if (!attempt.SignedIn && attempt.SignIn.IsCompleted)
            {
                if (!Answered(attempt.SignIn, out bool signedIn))
                {
                    EndJoining(attempt, JoinFailed(RoomError.Unknown));
                    return;
                }

                if (!signedIn)
                {
                    EndJoining(attempt, JoinFailed(RoomError.Offline));
                    return;
                }

                attempt.SignedIn = true;
                attempt.NextLookUpAt = now;
            }

            if (attempt.SignedIn && attempt.Address == null && !LookUp(attempt, now))
            {
                return;
            }

            if (attempt.Address != null)
            {
                if (attempt.Refused)
                {
                    // Turned down or lost before it opened, as the relay answers a room it does not know.
                    EndJoining(attempt, JoinFailed(RoomError.NotFound));
                    return;
                }

                if (attempt.Opened)
                {
                    var link = new ClientLink(this, attempt.Address, attempt.Channel);
                    _client = link;
                    EndJoining(attempt, new ClientLinkResult(link, RoomError.None));
                    return;
                }
            }

            if (now >= attempt.Deadline)
            {
                EndJoining(attempt, JoinFailed(RoomError.Unknown));
            }
        }

        /// <summary>
        /// Looks the code up, again after a pause while nobody lists it and the budget lasts, and
        /// asks the host it finds for a connection. False when that ended the attempt.
        /// </summary>
        private bool LookUp(JoinAttempt attempt, double now)
        {
            if (attempt.KnownAddress != null)
            {
                return Connect(attempt, attempt.KnownAddress, now);
            }

            if (attempt.LookUp == null && now >= attempt.NextLookUpAt)
            {
                if (double.IsNaN(attempt.LookUpUntil))
                {
                    attempt.LookUpUntil = now + LookUpBudgetSeconds;
                }

                attempt.LookUp = Call(() => _network.LookUpAsync(attempt.Code, attempt.Abort.Token));
                attempt.Deadline = now + ConnectTimeoutSeconds;
            }

            if (attempt.LookUp == null || !attempt.LookUp.IsCompleted)
            {
                return true;
            }

            PeerLookup lookup = Answered(attempt.LookUp, out PeerLookup answer) ? answer : new PeerLookup(null, RoomError.Unknown);
            attempt.LookUp = null;
            if (lookup.Error == RoomError.NotFound)
            {
                if (now + LookUpPauseSeconds >= attempt.LookUpUntil)
                {
                    EndJoining(attempt, JoinFailed(RoomError.NotFound));
                    return false;
                }

                // A listing takes a while to show: maybe the host has only just opened the room.
                attempt.NextLookUpAt = now + LookUpPauseSeconds;
                attempt.Deadline = double.PositiveInfinity;
                return true;
            }

            if (!lookup.Found)
            {
                EndJoining(attempt, JoinFailed(lookup.Error == RoomError.Offline ? RoomError.Offline : RoomError.Unknown));
                return false;
            }

            // The room as the latest generation hosts it, on that generation's channel.
            attempt.Channel = ChannelOf(attempt.Code, lookup.Generation);
            return Connect(attempt, lookup.Address, now);
        }

        /// <summary>Asks the host at <paramref name="address"/> for a connection. False when that ended the attempt.</summary>
        private bool Connect(JoinAttempt attempt, string address, double now)
        {
            if (address == _network.LocalAddress)
            {
                // This device's own room, which it is leaving: nobody else hosts the code here.
                EndJoining(attempt, JoinFailed(RoomError.NotFound));
                return false;
            }

            // The connection's clock starts now that the host is found.
            attempt.Address = address;
            attempt.Deadline = now + ConnectTimeoutSeconds;
            if (!Accept(attempt.Address, attempt.Channel) || !Send(attempt.Address, attempt.Channel, PacketFramer.OpenPacket()))
            {
                EndJoining(attempt, JoinFailed(RoomError.Unknown));
                return false;
            }

            return true;
        }

        /// <summary>Gives <paramref name="attempt"/> its answer once; a failed attempt lets go of whatever it asked the network for.</summary>
        private void EndHosting(HostAttempt attempt, HostLinkResult result)
        {
            if (attempt == null || attempt.Ended)
            {
                return;
            }

            attempt.Ended = true;
            if (_hosting == attempt)
            {
                _hosting = null;
            }

            attempt.Registration.Dispose();
            if (!result.Succeeded)
            {
                Abandon(attempt.Abort);
                if (attempt.List != null)
                {
                    _strays.Add(new StrayListing(attempt.Code, attempt.List));
                }
            }

            attempt.Result.TrySetResult(result);
        }

        private void EndJoining(JoinAttempt attempt, ClientLinkResult result)
        {
            if (attempt == null || attempt.Ended)
            {
                return;
            }

            attempt.Ended = true;
            if (_joining == attempt)
            {
                _joining = null;
            }

            attempt.Registration.Dispose();
            if (!result.Succeeded)
            {
                Abandon(attempt.Abort);
                if (attempt.Address != null && !attempt.Refused)
                {
                    // The connection it asked for is not wanted any more, even if it opens.
                    Close(attempt.Address, attempt.Channel);
                }
            }

            attempt.Result.TrySetResult(result);
        }

        /// <summary>A listing given up on before it arrived is taken down once it does, unless the code is hosted here again by then.</summary>
        private void SweepStrays()
        {
            for (int i = _strays.Count - 1; i >= 0; i--)
            {
                StrayListing stray = _strays[i];
                if (!stray.Listing.IsCompleted)
                {
                    continue;
                }

                _strays.RemoveAt(i);
                bool listed = stray.Listing.Status == TaskStatus.RanToCompletion && stray.Listing.Result == RoomError.None;
                bool wanted = (_host != null && _host.Code == stray.Code) || (_hosting != null && _hosting.Code == stray.Code);
                if (listed && !wanted)
                {
                    Unlist(stray.Code);
                }
            }
        }

        /// <summary>
        /// Takes everything the network has delivered, so that it is handed over before whatever the
        /// network reports next: called first by every event, and by <see cref="Tick"/>.
        /// </summary>
        private void Absorb()
        {
            if (_absorbing || _disposed)
            {
                return;
            }

            _absorbing = true;
            try
            {
                for (int i = 0; i < MaxPacketsPerDrain; i++)
                {
                    string address;
                    string channel;
                    byte[] packet;
                    try
                    {
                        if (!_network.TryReceive(out address, out channel, out packet))
                        {
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        Report(e);
                        break;
                    }

                    Route(address, channel, packet);
                }
            }
            finally
            {
                _absorbing = false;
            }
        }

        private void Route(string address, string channel, byte[] packet)
        {
            if (address == null || channel == null || packet == null)
            {
                return;
            }

            if (_host != null && channel == _host.Channel)
            {
                _host.MarkReceived(address, packet);
                return;
            }

            if (_client != null && _client.Is(address, channel))
            {
                _client.MarkReceived(packet);
                return;
            }

            JoinAttempt joining = _joining;
            if (joining != null && joining.Is(address, channel))
            {
                // The connection works. The host sends nothing before the member's hello, so there
                // is nothing to keep from before the link exists.
                joining.Opened = true;
            }
        }

        private void OnConnectionRequested(string address, string channel)
        {
            if (_disposed || address == null || channel == null || !IsRoomChannel(channel))
            {
                return;
            }

            Absorb();
            if (_host != null && channel == _host.Channel)
            {
                _host.Accept(address);
                return;
            }

            JoinAttempt joining = _joining;
            if ((joining != null && joining.Is(address, channel)) || (_client != null && _client.Is(address, channel)))
            {
                Accept(address, channel);
                return;
            }

            // Nobody here hosts that room (any more): turned down at once, so the guest hears it
            // instead of waiting out its timeout.
            Close(address, channel);
        }

        private void OnConnected(string address, string channel)
        {
            if (_disposed || address == null || channel == null)
            {
                return;
            }

            Absorb();
            if (_host != null && channel == _host.Channel)
            {
                _host.MarkConnected(address);
                return;
            }

            JoinAttempt joining = _joining;
            if (joining != null && joining.Is(address, channel))
            {
                joining.Opened = true;
                return;
            }

            if ((_client == null || !_client.Is(address, channel)) && IsRoomChannel(channel))
            {
                // A connection an attempt gave up on opened after all.
                Close(address, channel);
            }
        }

        private void OnDisconnected(string address, string channel)
        {
            if (_disposed || address == null || channel == null)
            {
                return;
            }

            // What came before the drop is handed over first.
            Absorb();
            if (_host != null && channel == _host.Channel)
            {
                _host.MarkDisconnected(address);
                return;
            }

            if (_client != null && _client.Is(address, channel))
            {
                _client.MarkClosed();
                return;
            }

            JoinAttempt joining = _joining;
            if (joining != null && joining.Is(address, channel))
            {
                joining.Refused = true;
            }
        }

        private void OnListingLost(string roomCode)
        {
            if (_disposed || roomCode == null)
            {
                return;
            }

            // A room that is not listed has no listing to lose (a Quick Match table's is not this transport's).
            if (_host != null && _host.Listed && roomCode == _host.Code)
            {
                _host.LoseListing(_now);
            }
            else if (_hosting != null && _hosting.Listed && roomCode == _hosting.Code)
            {
                _hosting.ListingLost = true;
            }
            else
            {
                // Not a listing of this transport's: whoever listed it hears of it.
                Enqueue(new Notice(this, roomCode));
            }
        }

        /// <summary>Tells the room logic, in order, everything noted since the last time.</summary>
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
                    Report(e);
                }
            }
        }

        private void Enqueue(Notice notice)
        {
            if (!_disposed)
            {
                _notices.Enqueue(notice);
            }
        }

        private void Report(Exception e)
        {
            try
            {
                _onFault(e);
            }
            catch (Exception)
            {
                // The fault handler failed too; there is nowhere left to report it.
            }
        }

        private void RaiseListingLost(string roomCode) => ListingLost?.Invoke(roomCode);

        /// <summary>Lists the code at its generation; the creator's room (0) as it always was.</summary>
        private Task<RoomError> ListCode(string roomCode, int generation, CancellationToken ct)
        {
            return generation > 0 ? _network.ListAsync(roomCode, generation, ct) : _network.ListAsync(roomCode, ct);
        }

        /// <summary>A peer id for a new connection: never <see cref="PeerId.Local"/>, never one handed out before.</summary>
        private PeerId NextPeer()
        {
            _lastPeer = _lastPeer >= ulong.MaxValue - 1 ? 1 : _lastPeer + 1;
            return new PeerId(_lastPeer);
        }

        /// <summary>Sends one room message as packets. A network that fails is reported, and the rest of the message is not sent.</summary>
        private void SendMessage(string address, string channel, byte[] payload)
        {
            byte[][] packets = PacketFramer.Split(payload, _network.MaxPacketSize);
            foreach (byte[] packet in packets)
            {
                if (!Send(address, channel, packet))
                {
                    return;
                }
            }
        }

        private bool Send(string address, string channel, byte[] packet)
        {
            try
            {
                _network.Send(address, channel, packet);
                return true;
            }
            catch (Exception e)
            {
                Report(e);
                return false;
            }
        }

        private bool Accept(string address, string channel)
        {
            try
            {
                _network.Accept(address, channel);
                return true;
            }
            catch (Exception e)
            {
                Report(e);
                return false;
            }
        }

        private void Close(string address, string channel)
        {
            try
            {
                _network.Close(address, channel);
            }
            catch (Exception e)
            {
                Report(e);
            }
        }

        private void Unlist(string roomCode)
        {
            try
            {
                _network.Unlist(roomCode);
            }
            catch (Exception e)
            {
                Report(e);
            }
        }

        private void Abandon(CancellationTokenSource abort)
        {
            try
            {
                abort.Cancel();
            }
            catch (Exception e)
            {
                Report(e);
            }
        }

        /// <summary>A call into the network that throws instead of answering counts as a failed answer.</summary>
        private Task<T> Call<T>(Func<Task<T>> call)
        {
            try
            {
                return call() ?? Task.FromException<T>(new InvalidOperationException("The network answered with no task."));
            }
            catch (Exception e)
            {
                return Task.FromException<T>(e);
            }
        }

        /// <summary>True with the answer when <paramref name="task"/> completed; a failed one is reported, a cancelled one is not.</summary>
        private bool Answered<T>(Task<T> task, out T answer)
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                answer = task.Result;
                return true;
            }

            if (task.IsFaulted)
            {
                Report(task.Exception.GetBaseException());
            }

            answer = default;
            return false;
        }

        /// <summary>One thing the network reported, to be told to the room logic from <see cref="Tick"/>.</summary>
        private readonly struct Notice
        {
            private readonly NoticeKind _kind;
            private readonly HostLink _host;
            private readonly ClientLink _client;
            private readonly PeerId _peer;
            private readonly byte[] _payload;
            private readonly PeerRoomTransport _transport;
            private readonly string _code;

            public Notice(NoticeKind kind, HostLink host, ClientLink client, PeerId peer, byte[] payload)
            {
                _kind = kind;
                _host = host;
                _client = client;
                _peer = peer;
                _payload = payload;
                _transport = null;
                _code = null;
            }

            /// <summary>A listing that is not the transport's was lost.</summary>
            public Notice(PeerRoomTransport transport, string roomCode)
            {
                _kind = NoticeKind.ListingLost;
                _host = null;
                _client = null;
                _peer = default;
                _payload = null;
                _transport = transport;
                _code = roomCode;
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
                    case NoticeKind.ListingLost:
                        _transport.RaiseListingLost(_code);
                        break;
                }
            }
        }

        /// <summary>A listing an attempt asked for and then gave up on, until the network answers.</summary>
        private readonly struct StrayListing
        {
            public StrayListing(string code, Task<RoomError> listing)
            {
                Code = code;
                Listing = listing;
            }

            public string Code { get; }

            public Task<RoomError> Listing { get; }
        }

        /// <summary>One call to <see cref="HostAsync"/> or <see cref="OpenAsync"/>, until it has its answer.</summary>
        private sealed class HostAttempt
        {
            public HostAttempt(string code, bool listed, int generation)
            {
                Code = code;
                Generation = generation;
                Channel = ChannelOf(code, generation);
                Listed = listed;
            }

            public string Code { get; }

            /// <summary>The generation of the room hosted (ADR 030); 0 for a room as its creator opens it.</summary>
            public int Generation { get; }

            /// <summary>The room is listed under its code (<see cref="HostAsync"/>), not only opened (<see cref="OpenAsync"/>).</summary>
            public bool Listed { get; }

            public string Channel { get; }

            public TaskCompletionSource<HostLinkResult> Result { get; } = new TaskCompletionSource<HostLinkResult>();

            /// <summary>Cancels the network calls of an attempt that ended without them.</summary>
            public CancellationTokenSource Abort { get; } = new CancellationTokenSource();

            public CancellationTokenRegistration Registration { get; set; }

            public Task<bool> SignIn { get; set; }

            public Task<RoomError> List { get; set; }

            public double Deadline { get; set; } = double.NaN;

            /// <summary>The directory dropped the listing before the room was handed over: it is listed again at once.</summary>
            public bool ListingLost { get; set; }

            public bool Ended { get; set; }
        }

        /// <summary>One call to <see cref="JoinAsync"/> or <see cref="JoinAtAsync"/>, until it has its answer.</summary>
        private sealed class JoinAttempt
        {
            public JoinAttempt(string code, string knownAddress, int generation)
            {
                Code = code;
                Channel = ChannelOf(code, generation);
                KnownAddress = knownAddress;
            }

            public string Code { get; }

            /// <summary>The host's address given with <see cref="JoinAtAsync(string, int, string, CancellationToken)"/>: nothing is looked up.</summary>
            public string KnownAddress { get; }

            /// <summary>The room's channel: of the generation given, or of the one the lookup found.</summary>
            public string Channel { get; set; }

            public TaskCompletionSource<ClientLinkResult> Result { get; } = new TaskCompletionSource<ClientLinkResult>();

            public CancellationTokenSource Abort { get; } = new CancellationTokenSource();

            public CancellationTokenRegistration Registration { get; set; }

            public Task<bool> SignIn { get; set; }

            public bool SignedIn { get; set; }

            /// <summary>The lookup under way; null between two lookups.</summary>
            public Task<PeerLookup> LookUp { get; set; }

            /// <summary>When the next lookup goes out, after one that found nothing.</summary>
            public double NextLookUpAt { get; set; } = double.NaN;

            /// <summary>The end of the lookup budget, from the first lookup on.</summary>
            public double LookUpUntil { get; set; } = double.NaN;

            /// <summary>The host's address once the code was found and the connection asked for.</summary>
            public string Address { get; set; }

            /// <summary>The connection opened (or a packet came over it).</summary>
            public bool Opened { get; set; }

            /// <summary>The connection was turned down or lost before the join was answered.</summary>
            public bool Refused { get; set; }

            public double Deadline { get; set; } = double.NaN;

            public bool Ended { get; set; }

            public bool Is(string address, string channel) => Address != null && Address == address && Channel == channel;
        }

        /// <summary>One guest's connection to the hosted room, under a peer id of its own.</summary>
        private sealed class Guest
        {
            public Guest(PeerId peer, string address, int maxMessageBytes)
            {
                Peer = peer;
                Address = address;
                Assembler = new PacketAssembler(maxMessageBytes);
            }

            public PeerId Peer { get; }

            public string Address { get; }

            /// <summary>
            /// No member ever sends more than <see cref="Protocol.MaxClientPayloadBytes"/>, or a host that
            /// took a room over more than <see cref="Protocol.MaxReportBytes"/>; anything longer is dropped unread.
            /// </summary>
            public PacketAssembler Assembler { get; }

            /// <summary>The guest's open packet came: another one means a new connection from the same device.</summary>
            public bool Opened { get; set; }
        }

        private sealed class HostLink : IHostLink, IHostAddresses
        {
            private readonly PeerRoomTransport _owner;
            private readonly Dictionary<string, Guest> _byAddress = new Dictionary<string, Guest>();
            private readonly Dictionary<PeerId, Guest> _byPeer = new Dictionary<PeerId, Guest>();
            private readonly HashSet<string> _accepted = new HashSet<string>();
            private readonly List<Guest> _scratch = new List<Guest>();

            private CancellationTokenSource _relistAbort;
            private Task<bool> _relistSignIn;
            private Task<RoomError> _relist;
            private double _relistAt;
            private bool _unlisted;

            public HostLink(PeerRoomTransport owner, string code, string channel, bool listed, int generation)
            {
                _owner = owner;
                Code = code;
                Channel = channel;
                Listed = listed;
                Generation = generation;
            }

            public event Action<PeerId> PeerConnected;

            public event Action<PeerId> PeerDisconnected;

            public event Action<PeerId, byte[]> Received;

            public string Code { get; }

            public string Channel { get; }

            /// <summary>Listed under its code: a lost listing is listed again, and the listing goes with the room.</summary>
            public bool Listed { get; }

            public int Generation { get; }

            public string LocalAddress => _owner.LocalAddress;

            public bool IsDisposed { get; private set; }

            public string AddressOf(PeerId peer) => _byPeer.TryGetValue(peer, out Guest guest) ? guest.Address : null;

            /// <summary>A connection request on the room's channel: allowed, and the guest counts once it opens.</summary>
            public void Accept(string address)
            {
                if (IsDisposed)
                {
                    return;
                }

                if (!_byAddress.ContainsKey(address))
                {
                    _accepted.Add(address);
                }

                _owner.Accept(address, Channel);
            }

            public void MarkConnected(string address)
            {
                if (IsDisposed || _byAddress.ContainsKey(address))
                {
                    return;
                }

                _accepted.Remove(address);
                Add(address);
            }

            public void MarkDisconnected(string address)
            {
                _accepted.Remove(address);
                if (!IsDisposed && _byAddress.TryGetValue(address, out Guest guest))
                {
                    Remove(guest);
                }
            }

            public void MarkReceived(string address, byte[] packet)
            {
                if (IsDisposed)
                {
                    return;
                }

                if (!_byAddress.TryGetValue(address, out Guest guest))
                {
                    if (!_accepted.Remove(address))
                    {
                        // Nobody this room accepted.
                        return;
                    }

                    // A packet over an accepted connection: it is open, whether or not that was reported yet.
                    guest = Add(address);
                }

                PacketOutcome outcome = guest.Assembler.Add(packet, out byte[] message);
                if (outcome == PacketOutcome.Message)
                {
                    _owner.Enqueue(new Notice(NoticeKind.HostReceived, this, null, guest.Peer, message));
                }
                else if (outcome == PacketOutcome.Opened)
                {
                    if (guest.Opened)
                    {
                        // The device opened a new connection over its old one (its app started again,
                        // or it lost the connection without this side noticing): the old one is gone.
                        Remove(guest);
                        guest = Add(address);
                    }

                    guest.Opened = true;
                }
            }

            /// <summary>The directory dropped the room's listing: it is listed again from the next tick on.</summary>
            public void LoseListing(double now)
            {
                if (IsDisposed || !Listed)
                {
                    return;
                }

                _unlisted = true;
                if (_relistSignIn == null && _relist == null)
                {
                    _relistAt = now;
                }
            }

            /// <summary>Lists the code again while it is not listed, one attempt at a time, every <see cref="RelistSeconds"/>.</summary>
            public void Relist(double now)
            {
                if (IsDisposed || !_unlisted)
                {
                    return;
                }

                if (_relistSignIn == null && _relist == null)
                {
                    if (now < _relistAt)
                    {
                        return;
                    }

                    _relistAt = now + RelistSeconds;
                    _relistAbort = new CancellationTokenSource();
                    CancellationToken token = _relistAbort.Token;
                    _relistSignIn = _owner.Call(() => _owner._network.SignInAsync(token));
                }

                if (_relistSignIn != null && _relistSignIn.IsCompleted)
                {
                    bool signedIn = _owner.Answered(_relistSignIn, out bool answer) && answer;
                    _relistSignIn = null;
                    if (!signedIn)
                    {
                        return;
                    }

                    CancellationToken token = _relistAbort.Token;
                    _relist = _owner.Call(() => _owner.ListCode(Code, Generation, token));
                }

                if (_relist != null && _relist.IsCompleted)
                {
                    bool listed = _owner.Answered(_relist, out RoomError answer) && answer == RoomError.None;
                    _relist = null;
                    if (listed)
                    {
                        _unlisted = false;
                    }
                }
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

            public void Send(PeerId peer, byte[] payload)
            {
                if (IsDisposed || payload == null || !_byPeer.TryGetValue(peer, out Guest guest))
                {
                    return;
                }

                _owner.SendMessage(guest.Address, Channel, payload);
            }

            /// <summary>Closes the peer's connection; the room hears <see cref="PeerDisconnected"/> as from any drop.</summary>
            public void Disconnect(PeerId peer)
            {
                if (IsDisposed || !_byPeer.TryGetValue(peer, out Guest guest))
                {
                    return;
                }

                Remove(guest);
                _owner.Close(guest.Address, Channel);
            }

            /// <summary>Takes the listing down and closes every guest's connection; nothing is raised any more.</summary>
            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                if (_owner._host == this)
                {
                    _owner._host = null;
                }

                if (Listed)
                {
                    _owner.Unlist(Code);
                }

                if (_relistAbort != null)
                {
                    _owner.Abandon(_relistAbort);
                }

                if (_relist != null)
                {
                    _owner._strays.Add(new StrayListing(Code, _relist));
                }

                _scratch.Clear();
                _scratch.AddRange(_byAddress.Values);
                _byAddress.Clear();
                _byPeer.Clear();
                foreach (Guest guest in _scratch)
                {
                    _owner.Close(guest.Address, Channel);
                }

                foreach (string address in _accepted)
                {
                    _owner.Close(address, Channel);
                }

                _accepted.Clear();
                _scratch.Clear();
            }

            private Guest Add(string address)
            {
                var guest = new Guest(_owner.NextPeer(), address, Generation > 0 ? Protocol.MaxReportBytes : Protocol.MaxClientPayloadBytes);
                _byAddress[address] = guest;
                _byPeer[guest.Peer] = guest;
                _owner.Enqueue(new Notice(NoticeKind.PeerConnected, this, null, guest.Peer, null));
                return guest;
            }

            private void Remove(Guest guest)
            {
                _byAddress.Remove(guest.Address);
                _byPeer.Remove(guest.Peer);
                _owner.Enqueue(new Notice(NoticeKind.PeerDisconnected, this, null, guest.Peer, null));
            }
        }

        private sealed class ClientLink : IClientLink
        {
            private readonly PeerRoomTransport _owner;
            private readonly PacketAssembler _assembler = new PacketAssembler(MaxHostMessageBytes);
            private bool _disposed;

            public ClientLink(PeerRoomTransport owner, string address, string channel)
            {
                _owner = owner;
                Address = address;
                Channel = channel;
            }

            public event Action<byte[]> Received;

            public event Action Closed;

            public string Address { get; }

            public string Channel { get; }

            public bool IsConnected { get; private set; } = true;

            public bool Is(string address, string channel) => Address == address && Channel == channel;

            public void MarkReceived(byte[] packet)
            {
                if (IsConnected && _assembler.Add(packet, out byte[] message) == PacketOutcome.Message)
                {
                    _owner.Enqueue(new Notice(NoticeKind.ClientReceived, null, this, default, message));
                }
            }

            /// <summary>The connection was lost or the host closed it: <see cref="Closed"/> follows everything that arrived before.</summary>
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

                _owner.Enqueue(new Notice(NoticeKind.ClientClosed, null, this, default, null));
            }

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
                if (!IsConnected || payload == null)
                {
                    return;
                }

                _owner.SendMessage(Address, Channel, payload);
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
                    _owner.Close(Address, Channel);
                }
            }
        }
    }
}
