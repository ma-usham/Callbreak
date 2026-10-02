using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Bots;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Views;

namespace Callbreak.Application.Online
{
    /// <summary>What the hand-over needs of the service whose room it keeps (ADR 030).</summary>
    internal interface IHandOverHolder
    {
        /// <summary>This device's member of the room; the same object, whoever hosts.</summary>
        RoomClient Client { get; }

        /// <summary>The room this device hosts; null while it is a guest.</summary>
        RoomHost Host { get; }

        IHostLink HostLink { get; }

        /// <summary>This device's connection to a host elsewhere; null while it hosts.</summary>
        IClientLink ClientLink { get; }

        /// <summary>
        /// The hand-over moved this device: it hosts <paramref name="host"/> now (or nothing), and it is
        /// connected to a host elsewhere over <paramref name="clientLink"/> (or not). What it held before
        /// is let go of already.
        /// </summary>
        void Hold(RoomHost host, IHostLink hostLink, IClientLink clientLink);
    }

    /// <summary>How the room is hosted on this device, should it take the match over (ADR 030).</summary>
    internal sealed class HandOverRoom
    {
        public string Code { get; set; }

        /// <summary>A private room is listed under its code; a Quick Match table is not.</summary>
        public bool Listed { get; set; }

        public bool QuickTable { get; set; }

        public MatchMode Mode { get; set; }

        public RoomSettings Settings { get; set; }

        /// <summary>The table's clocks; null runs the room on the turn time of <see cref="Settings"/>.</summary>
        public TableClocks Clocks { get; set; }

        public IBotFactory Bots { get; set; }

        public NameFilter Names { get; set; }

        public IReadOnlyList<string> DeckIds { get; set; }

        public Func<ulong> Seeds { get; set; }

        public BotTiming Timing { get; set; }

        public BotLevel FillLevel { get; set; } = BotLevel.Normal;

        public bool BotsChat { get; set; } = true;

        public double HeartbeatSeconds { get; set; } = Protocol.HeartbeatSeconds;

        /// <summary>Where the host is when no host has said so (a Quick Match table's host, from the directory); null looks the code up.</summary>
        public string HostAddress { get; set; }
    }

    /// <summary>
    /// Keeps this device's seat in an online match when hosts come and go (ADR 030). One per device and
    /// match, driven from the service's tick.
    /// <list type="bullet">
    /// <item>A guest that hears nothing from its host for <see cref="Protocol.HostLostSeconds"/>, or loses
    /// the connection, takes the host as lost. If it is the next host in line and holds the package for
    /// that generation, it takes the match over: it hosts the room's next generation and waits for the
    /// other players' reports (<see cref="TakeOverCollector"/>), up to
    /// <see cref="Protocol.HandOverWaitSeconds"/>, then rebuilds the table (<see cref="TableRebuild"/>) and
    /// hosts it. Otherwise it tries the next host in line with its report and the old host with a hello,
    /// in turn, each for <see cref="Protocol.HandOverAttemptSeconds"/>, until one seats it or the seat
    /// hold is over: a player whose own connection dropped finds its old host still there.</item>
    /// <item>A player taking over also tries the old host meanwhile, and while its new table has nobody
    /// else at it: if the old host answers with other people still at its table, this player's
    /// connection had dropped, not the host: it gives the take-over up and stays where it was.</item>
    /// <item>A host whose players have all dropped tries the next host in line it last named, one
    /// generation up: if that player hosts the room now, the match was taken over while this host was
    /// away; it lets its own table go and rejoins the new host as a player, with its report. The newer
    /// generation always wins, so two hosts of one room never both carry on.</item>
    /// </list>
    /// </summary>
    internal sealed class HandOver : IDisposable
    {
        /// <summary>The pause after a try that failed at once (the device refused), before the next.</summary>
        private const double RetrySeconds = 0.5;

        /// <summary>A guest whose connection went tries its host again after this pause, as a room member reconnecting does (GDD §27).</summary>
        private const double ReconnectSeconds = 1.0;

        /// <summary>How often a host whose players all dropped, or a lone new host, looks for the other one.</summary>
        private const double ProbeSeconds = 2.0;

        /// <summary>Every device refusing, for this long, means the room is gone: nobody took the match over.</summary>
        private const double RefusedForSeconds = Protocol.HostLostSeconds + Protocol.HandOverWaitSeconds;

        private readonly IHandOverTransport _transport;
        private readonly IHandOverHolder _holder;
        private readonly HandOverRoom _room;
        private readonly INetworkStatus _network;
        private readonly HandlerFaults _faults = new HandlerFaults();

        private Mode _mode = Mode.Watching;
        private double _now;
        private int _heardCount = -1;
        private double _heardAt;
        private double _giveUpAt;
        private double _nextTryAt;
        private double _refusedSince = double.NaN;
        private Try _try;
        private Place _nextHost;
        private Place _oldHost;
        private bool _nextFirst;
        private bool _declared;

        // Taking over.
        private SuccessionPackage _package;
        private CancellationTokenSource _openAbort;
        private Task<HostLinkResult> _opening;
        private IHostLink _openedLink;
        private TakeOverCollector _collector;

        // Hosting.
        private NextHostNotice _lastNext;
        private Place _previousHost;
        private double _probeUntil;

        public HandOver(IHandOverTransport transport, IHandOverHolder holder, HandOverRoom room, INetworkStatus network, double now)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _holder = holder ?? throw new ArgumentNullException(nameof(holder));
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _network = network;
            _now = now;
            _heardAt = now;
        }

        private enum Mode : byte
        {
            /// <summary>Connected to the host, or hosting: watching for a host that went quiet, or for players who all dropped.</summary>
            Watching,

            /// <summary>The host was lost: trying the next host in line and the old host in turn.</summary>
            Rehoming,

            /// <summary>Taking the match over: hosting the next generation, waiting for the players' reports.</summary>
            TakingOver,

            /// <summary>The match is over for this device, or the room is: nothing more to do.</summary>
            Done,
        }

        private enum Aim : byte
        {
            /// <summary>The next host in line, with this player's report.</summary>
            NextHost,

            /// <summary>The old host, with a hello: this player's own connection may be what dropped.</summary>
            OldHost,

            /// <summary>The other host of the room (the old one while taking over, the newer one while hosting alone), only to see whether it is there.</summary>
            Probe,
        }

        /// <summary>True once the hand-over has nothing left to do: the match ended, or this device left it.</summary>
        public bool IsDone => _mode == Mode.Done;

        public void Tick(double now)
        {
            _now = now;
            RoomClient client = _holder.Client;
            if (_mode == Mode.Done)
            {
                return;
            }

            if (client == null || client.IsClosed || client.Match == null || IsOver(client.Match))
            {
                Stop();
                return;
            }

            switch (_mode)
            {
                case Mode.Watching:
                    if (_holder.Host != null)
                    {
                        WatchAsHost(client, _holder.Host);
                    }
                    else
                    {
                        WatchAsGuest(client);
                    }

                    break;
                case Mode.Rehoming:
                    Rehome(client);
                    break;
                case Mode.TakingOver:
                    TakeOver(client);
                    break;
            }

            _faults.Rethrow();
        }

        public void Dispose()
        {
            Stop();
            _faults.Rethrow();
        }

        private static bool IsOver(IMatchSession match) => match.Status == SessionStatus.Ended || match.Status == SessionStatus.Failed;

        /// <summary>
        /// A guest whose connection goes tries its host again at once (GDD §27); one that hears nothing
        /// from its host for <see cref="Protocol.HostLostSeconds"/>, connection or not, takes the host as
        /// lost (<see cref="DeclareLost"/>).
        /// </summary>
        private void WatchAsGuest(RoomClient client)
        {
            if (client.Heard != _heardCount)
            {
                _heardCount = client.Heard;
                _heardAt = _now;
            }

            IClientLink link = _holder.ClientLink;
            bool dropped = link == null || !link.IsConnected;
            bool silent = _now - _heardAt >= Protocol.HostLostSeconds;
            if (!dropped && !silent)
            {
                return;
            }

            NextHostNotice notice = client.NextHost;
            client.Unlink();
            Dispose(link);
            _holder.Hold(null, null, null);
            _giveUpAt = _now + Protocol.SeatHoldSeconds;
            _refusedSince = double.NaN;
            _oldHost = notice != null
                ? new Place(notice.Generation, notice.HostAddress)
                : new Place(0, _room.HostAddress);
            _nextHost = null;
            _declared = false;
            _mode = Mode.Rehoming;
            _nextTryAt = _now + ReconnectSeconds;
            if (silent)
            {
                DeclareLost(client);
            }
        }

        /// <summary>
        /// Nothing from the host for <see cref="Protocol.HostLostSeconds"/>: the host is lost. The next
        /// host in line takes the match over; everyone else tries the next host in line with its report,
        /// and the old host with a hello, in turn.
        /// </summary>
        private void DeclareLost(RoomClient client)
        {
            _declared = true;
            NextHostNotice notice = client.NextHost;
            SuccessionPackage package = client.Package;
            if (notice != null && notice.HasNext && notice.NextSeat == client.LocalSeatIndex
                && package != null && package.Generation == notice.Generation)
            {
                DropTry();
                BeginTakeOver(client, notice, package);
                return;
            }

            _nextHost = notice != null && notice.HasNext && notice.NextSeat != client.LocalSeatIndex
                ? new Place(notice.Generation + 1, notice.NextAddress)
                : null;
            _nextFirst = true;
            _nextTryAt = _now;
        }

        /// <summary>
        /// A host whose players all dropped looks for the next host in line it last named, one
        /// generation up; a host that took the match over and has nobody at its table yet looks for the
        /// old host. Either way the one it finds settles who goes on.
        /// </summary>
        private void WatchAsHost(RoomClient client, RoomHost host)
        {
            NextHostNotice named = host.NextHost;
            if (named != null && named.HasNext)
            {
                _lastNext = named;
            }

            bool alone = host.InMatch && !host.HasConnectedGuests;
            Place target = null;
            if (alone && _lastNext != null && host.Table != null && host.Table.IsHuman((Seat)_lastNext.NextSeat))
            {
                target = new Place(_lastNext.Generation + 1, _lastNext.NextAddress);
            }
            else if (alone && _previousHost != null && _now < _probeUntil)
            {
                target = _previousHost;
            }

            if (target == null)
            {
                DropTry();
                return;
            }

            if (_try == null)
            {
                if (_now >= _nextTryAt && Online())
                {
                    StartTry(target == _previousHost ? Aim.Probe : Aim.NextHost, target, client);
                }

                return;
            }

            Outcome outcome = Advance(_try, client);
            if (outcome == Outcome.Pending)
            {
                return;
            }

            Try done = _try;
            _try = null;
            _nextTryAt = _now + ProbeSeconds;
            if (outcome == Outcome.Connected && done.Aim == Aim.NextHost)
            {
                // The next host in line hosts the room one generation up: the match went on without
                // this host. It lets its own table go and goes there as a player, with what it holds.
                Yield(client, host, done, target);
                return;
            }

            if (outcome == Outcome.Answered && done.Aim == Aim.Probe)
            {
                Settle(client, done);
                return;
            }

            Dispose(done);
        }

        /// <summary>This host was taken over while it was away: it becomes a player at the new host's table.</summary>
        private void Yield(RoomClient client, RoomHost host, Try done, Place newHost)
        {
            IHostLink hostLink = _holder.HostLink;
            Task<RoomError> report = client.ReportAsync(done.Link, done.Abort.Token);
            _holder.Hold(null, null, done.Link);
            try
            {
                host.Dispose();
            }
            catch (Exception e)
            {
                _faults.Keep(e);
            }

            Dispose(hostLink);
            _lastNext = null;
            _previousHost = null;
            _oldHost = newHost;
            _nextHost = null;
            _declared = true;
            _giveUpAt = _now + Protocol.SeatHoldSeconds;
            _refusedSince = double.NaN;
            _mode = Mode.Rehoming;
            _try = done;
            done.Aim = Aim.OldHost;
            done.Hello = report;
        }

        /// <summary>
        /// The old host answered a probe. With other people still at its table, this player's own
        /// connection was what dropped: it gives up its new table, or its take-over, and stays a
        /// player there. With nobody else there, the old host will come here instead.
        /// </summary>
        private void Settle(RoomClient client, Try done)
        {
            if (!OthersAt(done.Prober, client.LocalSeatIndex))
            {
                Dispose(done);
                return;
            }

            IClientLink link = done.Link;
            done.Link = null;
            done.Prober.Dispose();
            done.Prober = null;
            RoomHost host = _holder.Host;
            IHostLink hostLink = _holder.HostLink;
            DropTakeOver();
            Task<RoomError> hello = client.RejoinAsync(link, done.Abort.Token);
            _holder.Hold(null, null, link);
            if (host != null)
            {
                try
                {
                    host.Dispose();
                }
                catch (Exception e)
                {
                    _faults.Keep(e);
                }

                Dispose(hostLink);
            }

            _previousHost = null;
            _lastNext = null;
            _nextHost = null;
            _declared = true;
            _giveUpAt = _now + Protocol.SeatHoldSeconds;
            _mode = Mode.Rehoming;
            _try = new Try(Aim.OldHost, _oldHost) { Link = link, Hello = hello, Abort = done.Abort };
        }

        /// <summary>Whether the room the probe sees has a person connected besides this player and its host.</summary>
        private static bool OthersAt(RoomClient prober, int ownSeat)
        {
            RoomSnapshot room = prober.Room;
            if (room == null)
            {
                return false;
            }

            for (int i = 0; i < room.Seats.Count; i++)
            {
                RoomSnapshot.Member m = room.Seats[i];
                if (i != ownSeat && !m.IsHost && m.Occupied && !m.IsBot && m.IsConnected)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The connection went: the old host is tried. Once nothing has come from it for
        /// <see cref="Protocol.HostLostSeconds"/>, it is lost (<see cref="DeclareLost"/>): the next host in
        /// line with this player's report, and the old host with a hello, in turn.
        /// </summary>
        private void Rehome(RoomClient client)
        {
            if (!_declared && _now - _heardAt >= Protocol.HostLostSeconds && (_try == null || _try.Link == null))
            {
                // A try that has reached the old host is let finish: the host is there after all.
                DeclareLost(client);
                if (_mode != Mode.Rehoming)
                {
                    return;
                }
            }

            if (_try == null)
            {
                if (_now >= _giveUpAt)
                {
                    GiveUp(client, RoomError.ConnectionLost);
                    return;
                }

                if (!double.IsNaN(_refusedSince) && _now - _refusedSince >= RefusedForSeconds)
                {
                    // Every device turned this player down, again and again: the host left, and nobody
                    // took the match over.
                    GiveUp(client, RoomError.HostLeft);
                    return;
                }

                if (_now >= _nextTryAt && Online())
                {
                    bool next = _nextHost != null && (_nextFirst || _oldHost == null);
                    _nextFirst = !next;
                    StartTry(next ? Aim.NextHost : Aim.OldHost, next ? _nextHost : _oldHost, client);
                }

                return;
            }

            Try current = _try;
            Outcome outcome = Advance(current, client);
            switch (outcome)
            {
                case Outcome.Pending:
                    return;

                case Outcome.Seated:
                    _try = null;
                    _holder.Hold(null, null, current.Link);
                    current.Abort.Dispose();
                    _mode = Mode.Watching;
                    _heardCount = client.Heard;
                    _heardAt = _now;
                    return;

                case Outcome.Fatal:
                    _try = null;
                    client.Unlink();
                    Dispose(current);
                    GiveUp(client, current.Error == RoomError.Kicked ? RoomError.Kicked : RoomError.ConnectionLost);
                    return;

                case Outcome.Refused:
                    if (double.IsNaN(_refusedSince))
                    {
                        _refusedSince = _now;
                    }

                    break;

                default:
                    _refusedSince = double.NaN;
                    break;
            }

            _try = null;
            if (current.Hello != null)
            {
                // The link was this player's: it is reconnecting again.
                client.Unlink();
                _holder.Hold(null, null, null);
            }

            Dispose(current);
            _nextTryAt = _now + RetrySeconds;
        }

        private void BeginTakeOver(RoomClient client, NextHostNotice notice, SuccessionPackage package)
        {
            _package = package;
            _previousHost = new Place(notice.Generation, notice.HostAddress);
            _mode = Mode.TakingOver;
            _openAbort = new CancellationTokenSource();
            CancellationToken token = _openAbort.Token;
            string code = _room.Code;
            int generation = notice.Generation + 1;
            bool listed = _room.Listed;
            _opening = Call(() => _transport.TakeOverAsync(code, generation, listed, token));
        }

        /// <summary>Opening the room's next generation, then taking the reports, then rebuilding the table.</summary>
        private void TakeOver(RoomClient client)
        {
            if (_opening != null)
            {
                if (!_opening.IsCompleted)
                {
                    return;
                }

                HostLinkResult opened = Answer(_opening, new HostLinkResult(null, RoomError.Unknown));
                _opening = null;
                if (!opened.Succeeded)
                {
                    // The room cannot be taken over from here: look for the old host as any player would.
                    DropTakeOver();
                    _mode = Mode.Rehoming;
                    _nextHost = null;
                    _nextTryAt = _now;
                    return;
                }

                _openedLink = opened.Link;
                _collector = new TakeOverCollector(opened.Link, _package, (Seat)client.LocalSeatIndex, Expected(client), _now + Protocol.HandOverWaitSeconds);
            }

            if (_collector.IsComplete(_now))
            {
                DropTry();
                Commit(client);
                return;
            }

            // Meanwhile the old host is tried: this player's own connection may be what dropped.
            if (_try == null)
            {
                if (_now >= _nextTryAt && Online())
                {
                    StartTry(Aim.Probe, _previousHost, client);
                }

                return;
            }

            Try done = _try;
            Outcome outcome = Advance(done, client);
            if (outcome == Outcome.Pending)
            {
                return;
            }

            _try = null;
            _nextTryAt = _now + ProbeSeconds;
            if (outcome == Outcome.Answered)
            {
                Settle(client, done);
                return;
            }

            Dispose(done);
        }

        /// <summary>
        /// The people the rebuild waits for: everyone at the table besides this player and the lost
        /// host who was there as far as this player knows.
        /// </summary>
        private IEnumerable<Seat> Expected(RoomClient client)
        {
            IMatchSession match = client.Match;
            var expected = new List<Seat>();
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                var seat = (Seat)s;
                SessionSeat at = match.Seats[s];
                if (s == client.LocalSeatIndex || seat == (Seat)HostSeatOf(client) || at.IsBot || _package.IsBot(seat))
                {
                    continue;
                }

                if (at.Connection == SeatConnection.Connected || at.Connection == SeatConnection.Away)
                {
                    expected.Add(seat);
                }
            }

            return expected;
        }

        private static int HostSeatOf(RoomClient client) => client.NextHost != null ? client.NextHost.HostSeat : -1;

        /// <summary>Rebuilds the table from what everyone brought and hosts it: the match goes on.</summary>
        private void Commit(RoomClient client)
        {
            IMatchSession match = client.Match;
            var reports = new List<SeatReport> { new SeatReport((Seat)client.LocalSeatIndex, ((OnlineMatchSession)match).LastSeq, match.View) };
            reports.AddRange(_collector.Reports);
            RebuiltTable rebuilt = TableRebuild.Rebuild(reports, _package);
            PlayerView latest = rebuilt.Latest;

            var present = new HashSet<int> { client.LocalSeatIndex };
            foreach (KeyValuePair<PeerId, int> arrival in _collector.Present)
            {
                present.Add(arrival.Value);
            }

            var presence = new SeatConnection[SeatExtensions.Count];
            var humans = new bool[SeatExtensions.Count];
            var profiles = new SeatProfile[SeatExtensions.Count];
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                profiles[s] = latest.Seats[s];
                humans[s] = !latest.Seats[s].IsBot;
                SeatConnection last = match.Seats[s].Connection;
                presence[s] = present.Contains(s) ? SeatConnection.Connected
                    : last == SeatConnection.Left || last == SeatConnection.BotControlled ? last
                    : SeatConnection.Reconnecting;
            }

            TableClocks clocks = _room.Clocks ?? TableClocks.PrivateRoom(_room.Settings.TurnSeconds);
            var setup = new TableHostSetup(latest.Rules, latest.MatchId, profiles, humans, clocks, _room.Seeds, _room.FillLevel, _room.Timing, _room.BotsChat);
            var table = new TableHost(setup, _room.Bots, new TableResume(rebuilt, presence, _now));
            var seats = new RoomTakeOver.SeatInfo[SeatExtensions.Count];
            RoomSnapshot lobby = client.Room;
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                RoomSnapshot.Member m = lobby != null ? lobby.Seats[s] : null;
                string identity = _package.IdentityOf((Seat)s);
                if (identity == null && (m == null || !m.Occupied || !m.IsBot))
                {
                    // A seat a bot plays without sitting in the lobby (a private room's), or one its person left for good.
                    continue;
                }

                SeatProfile profile = latest.Seats[s];
                int.TryParse(profile.AvatarId, out int avatar);
                seats[s] = m != null && m.Occupied
                    ? new RoomTakeOver.SeatInfo(m.Name, m.AvatarId, m.DeckThemeId, identity, identity == null)
                    : new RoomTakeOver.SeatInfo(profile.Name, avatar, profile.DeckThemeId, identity, identity == null);
            }

            IHostLink link = _openedLink;
            IReadOnlyList<KeyValuePair<PeerId, int>> arrived = _collector.Present;
            _collector.Release();
            _collector = null;
            _openedLink = null;
            var takeOver = new RoomTakeOver(client.LocalSeatIndex, table, seats, arrived, _now);
            var host = new RoomHost(
                takeOver, _room.Code, _room.Settings, link, _room.Bots, _room.Names, new Pcg32Random(_room.Seeds()), _room.DeckIds,
                _room.Seeds, _room.Timing, _room.FillLevel, _room.Clocks, _room.BotsChat, _room.QuickTable, _room.HeartbeatSeconds);
            Task<RoomError> joined = client.HostAsync(host, CancellationToken.None);
            _holder.Hold(host, link, null);
            if (!joined.IsCompleted || joined.Result != RoomError.None)
            {
                _faults.Keep(new InvalidOperationException("The rebuilt room did not seat its own host."));
            }

            _openAbort?.Dispose();
            _openAbort = null;
            _package = null;
            _lastNext = null;
            _probeUntil = _now + Protocol.SeatHoldSeconds;
            _nextTryAt = _now + ProbeSeconds;
            _mode = Mode.Watching;
        }

        private void StartTry(Aim aim, Place place, RoomClient client)
        {
            var attempt = new Try(aim, place);
            CancellationToken token = attempt.Abort.Token;
            string code = _room.Code;
            attempt.Deadline = _now + Protocol.HandOverAttemptSeconds;
            attempt.Connect = string.IsNullOrEmpty(place.Address)
                ? Call(() => _transport.JoinAsync(code, token))
                : Call(() => _transport.JoinAtAsync(code, place.Generation, place.Address, token));
            _try = attempt;
        }

        /// <summary>Moves a try on: connecting, then (but for a probe that only looks) the hello and its answer.</summary>
        private Outcome Advance(Try attempt, RoomClient client)
        {
            if (attempt.Connect != null)
            {
                if (!attempt.Connect.IsCompleted)
                {
                    return _now >= attempt.Deadline ? Outcome.Failed : Outcome.Pending;
                }

                ClientLinkResult linked = Answer(attempt.Connect, new ClientLinkResult(null, RoomError.Unknown));
                attempt.Connect = null;
                if (!linked.Succeeded)
                {
                    return linked.Error == RoomError.NotFound ? Outcome.Refused : Outcome.Failed;
                }

                attempt.Link = linked.Link;
                if (_mode == Mode.Watching && attempt.Aim == Aim.NextHost)
                {
                    // A host looking for the next host in line: the connection alone says it hosts the next generation.
                    return Outcome.Connected;
                }

                if (attempt.Aim == Aim.Probe)
                {
                    attempt.Prober = new RoomClient(linked.Link, client.Identity, null, _room.Mode, null, null);
                    attempt.Prober.Tick(_now);
                    attempt.Hello = attempt.Prober.JoinAsync(attempt.Abort.Token);
                }
                else
                {
                    attempt.Hello = attempt.Aim == Aim.NextHost
                        ? client.ReportAsync(linked.Link, attempt.Abort.Token)
                        : client.RejoinAsync(linked.Link, attempt.Abort.Token);
                }
            }

            if (attempt.Prober != null)
            {
                attempt.Prober.Tick(_now);
            }

            if (attempt.Hello == null || !attempt.Hello.IsCompleted)
            {
                return Outcome.Pending;
            }

            RoomError answer = Answer(attempt.Hello, RoomError.Unknown);
            attempt.Error = answer;
            if (attempt.Aim == Aim.Probe)
            {
                return answer == RoomError.None ? Outcome.Answered : Outcome.Failed;
            }

            if (answer == RoomError.None)
            {
                return Outcome.Seated;
            }

            bool seatGone = answer == RoomError.AlreadyStarted || answer == RoomError.Full || answer == RoomError.UpdateRequired || answer == RoomError.Kicked;
            return seatGone ? Outcome.Fatal : Outcome.Failed;
        }

        private void GiveUp(RoomClient client, RoomError reason)
        {
            Stop();
            try
            {
                client.Close(reason);
            }
            catch (Exception e)
            {
                _faults.Keep(e);
            }
        }

        private bool Online() => _network == null || _network.IsOnline;

        /// <summary>Ends whatever is under way; the room objects the holder has are its own.</summary>
        private void Stop()
        {
            _mode = Mode.Done;
            DropTry();
            DropTakeOver();
        }

        private void DropTry()
        {
            Try attempt = _try;
            _try = null;
            if (attempt != null)
            {
                Dispose(attempt);
            }
        }

        private void DropTakeOver()
        {
            if (_openAbort != null)
            {
                Cancel(_openAbort);
                _openAbort = null;
            }

            Task<HostLinkResult> opening = _opening;
            _opening = null;
            if (opening != null && opening.Status == TaskStatus.RanToCompletion && opening.Result.Succeeded)
            {
                opening.Result.Link.Dispose();
            }

            if (_collector != null)
            {
                _collector.Dispose();
                _collector = null;
            }

            Dispose(_openedLink);
            _openedLink = null;
            _package = null;
        }

        /// <summary>Lets go of a try: its probe, its connection unless the room took it, and its calls.</summary>
        private void Dispose(Try attempt)
        {
            Cancel(attempt.Abort);
            Task<ClientLinkResult> connect = attempt.Connect;
            attempt.Connect = null;
            if (connect != null && connect.Status == TaskStatus.RanToCompletion && connect.Result.Succeeded)
            {
                connect.Result.Link.Dispose();
            }

            if (attempt.Prober != null)
            {
                attempt.Prober.Dispose();
                attempt.Prober = null;
            }

            if (attempt.Link != null && attempt.Link != _holder.ClientLink)
            {
                attempt.Link.Dispose();
            }

            attempt.Link = null;
        }

        private void Dispose(IDisposable disposable)
        {
            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch (Exception e)
            {
                _faults.Keep(e);
            }
        }

        private void Cancel(CancellationTokenSource source)
        {
            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already let go of.
            }
            catch (Exception e)
            {
                _faults.Keep(e);
            }
        }

        /// <summary>A call into the network that throws instead of answering counts as a failed answer.</summary>
        private static Task<T> Call<T>(Func<Task<T>> call)
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

        private static T Answer<T>(Task<T> task, T failed)
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                return task.Result;
            }

            if (task.IsFaulted)
            {
                _ = task.Exception;
            }

            return failed;
        }

        private enum Outcome : byte
        {
            Pending,

            /// <summary>The connection opened (a host looking for the next host in line asks no more).</summary>
            Connected,

            /// <summary>A probe was answered: the room is there, as <see cref="Try.Prober"/> sees it.</summary>
            Answered,

            /// <summary>This player has its seat back.</summary>
            Seated,

            /// <summary>The device turned the connection down: it hosts no such room.</summary>
            Refused,

            /// <summary>No answer in time, or the connection went: try again.</summary>
            Failed,

            /// <summary>The seat is gone (given up, or this player was removed): nothing to go back to.</summary>
            Fatal,
        }

        /// <summary>A host of the room: its generation, and its device's address (null: look the code up).</summary>
        private sealed class Place
        {
            public Place(int generation, string address)
            {
                Generation = generation;
                Address = string.IsNullOrEmpty(address) ? null : address;
            }

            public int Generation { get; }

            public string Address { get; }
        }

        /// <summary>One try to reach a host: connecting, then saying hello (or a report) and waiting for the answer.</summary>
        private sealed class Try
        {
            public Try(Aim aim, Place place)
            {
                Aim = aim;
                Place = place;
            }

            public Aim Aim { get; set; }

            public Place Place { get; }

            public CancellationTokenSource Abort { get; set; } = new CancellationTokenSource();

            public double Deadline { get; set; }

            public Task<ClientLinkResult> Connect { get; set; }

            public IClientLink Link { get; set; }

            /// <summary>A member of its own that only looks at the room a probe found.</summary>
            public RoomClient Prober { get; set; }

            public Task<RoomError> Hello { get; set; }

            public RoomError Error { get; set; }
        }
    }
}
