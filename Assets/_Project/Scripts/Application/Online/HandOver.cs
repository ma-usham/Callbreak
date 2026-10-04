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
    /// Keeps this device's seat in an online match when hosts come and go (ADR 030; the owner's backup
    /// host, 3 Oct 2026). One per device and match, driven from the service's tick.
    /// <list type="bullet">
    /// <item>A guest that hears nothing from its host for <see cref="Protocol.HostLostSeconds"/>, or loses
    /// the connection, takes the host as lost. The host named its line of succession
    /// (<see cref="NextHostNotice"/>). The next host in line takes the match over at once, one generation
    /// up, from the same trick. The k-th in line (k ≥ 2) keeps its package, which holds no cards, and
    /// takes the match over itself, k generations up, if no host has seated it after (k − 1) ×
    /// <see cref="Protocol.BackupTakeOverSeconds"/>: the round in play is then dealt again. Taking over, a
    /// player hosts that generation and waits for the other players' reports
    /// (<see cref="TakeOverCollector"/>), up to <see cref="Protocol.HandOverWaitSeconds"/>, then rebuilds
    /// the table (<see cref="TableRebuild"/>) and hosts it. Until a host seats it, a player tries every
    /// successor in the line with its report and the old host with a hello, one at a time, each for
    /// <see cref="Protocol.HandOverAttemptSeconds"/>, until the seat hold is over: a player whose own
    /// connection dropped finds its old host still there.</item>
    /// <item>A player taking over also looks at the room's other hosts. The old host and the successors
    /// before it answer with other people at their table only if this player's own connection was what
    /// dropped: it gives the take-over up and stays there. A successor after it whose connection opens
    /// hosts a newer generation already: it gives the take-over up and goes there with its report.</item>
    /// <item>A host whose players have all dropped looks the same way at the successors it named, each at
    /// the generation it would host, and, for a seat hold after it took the match over, at the hosts
    /// before it. Between tables nobody else is at, the newer generation wins; a table with other people
    /// at it wins over a take-over nobody is at; so two hosts of one room never both carry on.</item>
    /// <item>A player who gives up at the end of the seat hold while its own device is online is not
    /// charged the match: it was left behind, not the one who dropped out (the owner, 3 Oct 2026).</item>
    /// </list>
    /// </summary>
    internal sealed class HandOver : IDisposable
    {
        /// <summary>The pause after a try that failed at once (the device refused), before the next.</summary>
        private const double RetrySeconds = 0.5;

        /// <summary>A guest whose connection went tries its host again after this pause, as a room member reconnecting does (GDD §27).</summary>
        private const double ReconnectSeconds = 1.0;

        /// <summary>How often a host whose players all dropped, or a player taking over, looks at the room's other hosts.</summary>
        private const double ProbeSeconds = 2.0;

        /// <summary>Every device refusing, for this long, means the room is gone: nobody took the match over.</summary>
        private const double RefusedForSeconds = Protocol.HostLostSeconds + Protocol.HandOverWaitSeconds;

        private readonly IHandOverTransport _transport;
        private readonly IHandOverHolder _holder;
        private readonly HandOverRoom _room;
        private readonly INetworkStatus _network;
        private readonly HandlerFaults _faults = new HandlerFaults();
        private readonly List<Target> _targets = new List<Target>();
        private readonly List<Target> _probes = new List<Target>();
        private readonly List<Place> _named = new List<Place>();

        private Mode _mode = Mode.Watching;
        private double _now;
        private int _heardCount = -1;
        private double _heardAt;
        private double _giveUpAt;
        private double _nextTryAt;
        private double _refusedSince = double.NaN;
        private Try _try;
        private bool _declared;
        private double _declaredAt;

        // The host taken as lost: its line of succession as it last said it (null when it never did),
        // and where it is (null once it left: it hosts nothing any more).
        private NextHostNotice _line;
        private Place _oldHost;
        private int _nextTarget;

        // Taking over: this player's place in the line (1 for the next host in line, 0 out of it), and
        // a take-over still to come (a backup's, or one whose room did not open) with its turn, which
        // comes no sooner than its wait after this device was last offline.
        private int _place;
        private SuccessionPackage _standby;
        private double _turnAt;
        private double _turnWait;
        private SuccessionPackage _package;
        private int _generation;
        private CancellationTokenSource _openAbort;
        private Task<HostLinkResult> _opening;
        private IHostLink _openedLink;
        private TakeOverCollector _collector;

        // Hosting.
        private NextHostNotice _remembered;
        private double _probeUntil;
        private int _nextProbe;

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
            /// <summary>A successor in the line, with this player's report.</summary>
            NextHost,

            /// <summary>The old host, with a hello: this player's own connection may be what dropped.</summary>
            OldHost,

            /// <summary>A host before this device's own generation, only to see whether other people are at its table.</summary>
            Probe,

            /// <summary>A successor after this device's own generation: its connection alone says it hosts the room there.</summary>
            Newer,
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

                    // A host just found lost (or leaving) is gone after at once, not a frame later.
                    if (_mode == Mode.Rehoming)
                    {
                        Rehome(client);
                    }
                    else if (_mode == Mode.TakingOver)
                    {
                        TakeOver(client);
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
        /// lost (<see cref="DeclareLost"/>); and one whose host says it is leaving goes to the next host
        /// in line at once, without waiting for anything (<see cref="RoomClient.HostLeaving"/>).
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
            bool leaving = client.HostLeaving;
            if (!dropped && !silent && !leaving)
            {
                return;
            }

            NextHostNotice notice = client.NextHost;
            client.Unlink();
            Dispose(link);
            _holder.Hold(null, null, null);
            _giveUpAt = _now + Protocol.SeatHoldSeconds;
            _refusedSince = double.NaN;
            _oldHost = leaving ? null
                : notice != null ? new Place(notice.Generation, notice.HostAddress, notice.HostSeat)
                : new Place(0, _room.HostAddress, NextHostNotice.NoSeat);
            _declared = false;
            _line = null;
            _standby = null;
            _mode = Mode.Rehoming;
            _nextTryAt = _now + ReconnectSeconds;
            Look(client, null);
            if (silent || leaving)
            {
                DeclareLost(client, leaving);
            }
        }

        /// <summary>
        /// The host is lost: nothing came from it for <see cref="Protocol.HostLostSeconds"/>, or it said
        /// it leaves (<paramref name="hostLeft"/>). The next host in line takes the match over at once; a
        /// later successor that holds its package waits for its turn (the owner's backup host, 3 Oct
        /// 2026); meanwhile everyone tries the places <see cref="Look"/> names, in turn.
        /// </summary>
        private void DeclareLost(RoomClient client, bool hostLeft)
        {
            _declared = true;
            _declaredAt = _now;
            NextHostNotice notice = client.NextHost;
            SuccessionPackage package = client.Package;
            _line = notice;
            if (hostLeft)
            {
                _oldHost = null;
            }

            _place = notice != null ? notice.PlaceOf(client.LocalSeatIndex) : 0;
            _standby = null;
            if (_place >= 1 && package != null && package.Generation == notice.Generation)
            {
                if (_place == 1)
                {
                    DropTry();
                    BeginTakeOver(package);
                    return;
                }

                // A backup: the successors before it may have gone with the host. It takes the match
                // over itself should no host have seated it by its turn, each place one wait after the
                // place before it.
                _standby = package;
                _turnWait = (_place - 1) * Protocol.BackupTakeOverSeconds;
                _turnAt = _now + _turnWait;
            }

            Look(client, null);
            _nextTryAt = _now;
        }

        /// <summary>
        /// A host whose players all dropped looks at the room's other hosts (<see cref="Probe"/>): a
        /// successor it named may have taken the match over while it was away, and a host before it may
        /// still have people at its table.
        /// </summary>
        private void WatchAsHost(RoomClient client, RoomHost host)
        {
            Remember(host.NextHost);
            if (!host.InMatch || host.HasConnectedGuests)
            {
                DropTry();
                return;
            }

            Probe(client, host);
        }

        /// <summary>
        /// Keeps every successor this host has named, each at the generation it would host the room at:
        /// one that took the match over while this host was away is found there.
        /// </summary>
        private void Remember(NextHostNotice named)
        {
            if (named == null || named == _remembered)
            {
                return;
            }

            _remembered = named;
            for (int j = 0; j < named.Line.Count; j++)
            {
                var place = new Place(named.Generation + j + 1, named.Line[j].Address, named.Line[j].Seat);
                if (!_named.Exists(known => known.Same(place)))
                {
                    _named.Add(place);
                }
            }
        }

        /// <summary>
        /// Looks at the room's other hosts (<see cref="ProbeTargets"/>), one at a time, every
        /// <see cref="ProbeSeconds"/>: a newer one whose connection opens is gone to (<see cref="Yield"/>);
        /// one before this device's own that answers is stayed at if other people are there
        /// (<see cref="Settle"/>).
        /// </summary>
        /// <param name="host">The table this device hosts; null while it takes the match over.</param>
        private void Probe(RoomClient client, RoomHost host)
        {
            if (_try == null)
            {
                if (_now >= _nextTryAt && Online())
                {
                    ProbeTargets(client, host);
                    if (_probes.Count > 0)
                    {
                        Target next = _probes[_nextProbe % _probes.Count];
                        _nextProbe++;
                        StartTry(next.Aim, next.Place, client);
                    }
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
            if (outcome == Outcome.Connected)
            {
                Yield(client, done);
                return;
            }

            if (outcome == Outcome.Answered)
            {
                Settle(client, done);
                return;
            }

            Dispose(done);
        }

        /// <summary>
        /// The room's other hosts this device looks at. First the successors after its own generation,
        /// newest first, from the line of the host taken as lost and from the lines this device named
        /// itself, leaving out one whose seat at its table is no longer a person's. Then, while it takes
        /// the match over and for a seat hold after, the hosts before it, newest first: the successors
        /// before it in the line, and the lost host unless it left.
        /// </summary>
        private void ProbeTargets(RoomClient client, RoomHost host)
        {
            _probes.Clear();
            int own = host != null ? host.Generation : _generation;
            bool older = host == null || _now < _probeUntil;
            var newer = new List<Target>();
            var before = new List<Target>();
            if (_line != null)
            {
                for (int j = 0; j < _line.Line.Count; j++)
                {
                    NextHostNotice.Successor s = _line.Line[j];
                    var place = new Place(_line.Generation + j + 1, s.Address, s.Seat);
                    if (s.Seat == client.LocalSeatIndex)
                    {
                        continue;
                    }

                    if (place.Generation > own && IsPerson(host, s.Seat))
                    {
                        newer.Add(new Target(Aim.Newer, place));
                    }
                    else if (place.Generation < own && older)
                    {
                        before.Add(new Target(Aim.Probe, place));
                    }
                }
            }

            foreach (Place place in _named)
            {
                if (place.Generation > own && IsPerson(host, place.Seat))
                {
                    newer.Add(new Target(Aim.Newer, place));
                }
            }

            if (_oldHost != null && _oldHost.Generation < own && older)
            {
                before.Add(new Target(Aim.Probe, _oldHost));
            }

            newer.Sort(NewestFirst);
            before.Sort(NewestFirst);
            foreach (Target target in newer)
            {
                Add(_probes, target);
            }

            foreach (Target target in before)
            {
                Add(_probes, target);
            }
        }

        private static int NewestFirst(Target a, Target b) => b.Place.Generation.CompareTo(a.Place.Generation);

        /// <summary>Whether <paramref name="seat"/> is still a person's at <paramref name="host"/>'s table; true when there is no table to ask.</summary>
        private static bool IsPerson(RoomHost host, int seat) =>
            host == null || host.Table == null || seat < 0 || seat >= SeatExtensions.Count || host.Table.IsHuman((Seat)seat);

        /// <summary>
        /// A host of the room at a newer generation was found while this device took the match over, or
        /// hosted it with nobody else at its table: the match went on there. It lets its own take-over or
        /// table go and goes there as a player, with what it holds.
        /// </summary>
        private void Yield(RoomClient client, Try done)
        {
            RoomHost host = _holder.Host;
            IHostLink hostLink = _holder.HostLink;
            SuccessionPackage package = _package;
            DropTakeOver();
            Task<RoomError> report = client.ReportAsync(done.Link, done.Abort.Token);
            _holder.Hold(null, null, done.Link);
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

            _declared = true;
            KeepForLater(package);
            _giveUpAt = _now + Protocol.SeatHoldSeconds;
            _refusedSince = double.NaN;
            _mode = Mode.Rehoming;
            done.Aim = Aim.NextHost;
            done.Hello = report;
            _try = done;
            Look(client, new Target(Aim.NextHost, done.Place));
        }

        /// <summary>
        /// A host before this device's own answered a probe. With other people still at its table, this
        /// player's own connection was what dropped: it gives up its take-over, or its new table, and
        /// stays a player there. With nobody else there, that host will come here instead.
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
            SuccessionPackage package = _package;
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

            _declared = true;
            KeepForLater(package);
            _giveUpAt = _now + Protocol.SeatHoldSeconds;
            _refusedSince = double.NaN;
            _mode = Mode.Rehoming;
            _try = new Try(Aim.OldHost, done.Place) { Link = link, Hello = hello, Abort = done.Abort };
            Look(client, new Target(Aim.OldHost, done.Place));
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
        /// <see cref="Protocol.HostLostSeconds"/>, it is lost (<see cref="DeclareLost"/>): every place
        /// <see cref="Look"/> names, in turn, until a host seats this player, its own turn to take the
        /// match over comes, or the seat hold is over.
        /// </summary>
        private void Rehome(RoomClient client)
        {
            if (!_declared && _now - _heardAt >= Protocol.HostLostSeconds && (_try == null || _try.Link == null))
            {
                // A try that has reached the old host is let finish: the host is there after all.
                DeclareLost(client, hostLeft: false);
                if (_mode != Mode.Rehoming)
                {
                    return;
                }
            }

            if (_standby != null && TurnCame())
            {
                // No host seated this player in time: it takes the match over itself.
                DropTry();
                BeginTakeOver(_standby);
                return;
            }

            if (_try == null)
            {
                if (_targets.Count == 0 && _standby == null)
                {
                    // The host left, and nobody here can take the match over: there is nowhere to go.
                    GiveUp(client, RoomError.HostLeft);
                    return;
                }

                if (_now >= _giveUpAt)
                {
                    // Nobody seated this player for the whole seat hold. One whose own device was online
                    // all along was left behind, not the one who dropped out: it is not charged the match
                    // (the owner, 3 Oct 2026).
                    GiveUp(client, RoomError.ConnectionLost, countsAsLeft: !Online());
                    return;
                }

                if (_standby == null && !double.IsNaN(_refusedSince) && _now - _refusedSince >= RefusedForSeconds && _now >= RefusalFloor())
                {
                    // Every device turned this player down, again and again, since the last backup could
                    // have hosted: the host left, and nobody took the match over.
                    GiveUp(client, RoomError.HostLeft);
                    return;
                }

                if (_targets.Count > 0 && _now >= _nextTryAt && Online())
                {
                    Target next = _targets[_nextTarget % _targets.Count];
                    _nextTarget++;
                    StartTry(next.Aim, next.Place, client);
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
                    Seated(client);
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

        /// <summary>
        /// The turn of the take-over still to come (<see cref="_standby"/>) has come: this device is
        /// online, and no try is about to seat this player. Offline, nothing can be tried, so the turn
        /// comes no sooner than its wait after this device is back online.
        /// </summary>
        private bool TurnCame()
        {
            if (!Online())
            {
                _turnAt = Math.Max(_turnAt, _now + _turnWait);
                return false;
            }

            return _now >= _turnAt && !Seating(_try);
        }

        /// <summary>
        /// A take-over given up, or one whose room did not open, keeps its package (null: none was under
        /// way) until a host seats this player: if none has by then, it takes the match over again
        /// <see cref="Protocol.BackupTakeOverSeconds"/> later.
        /// </summary>
        private void KeepForLater(SuccessionPackage package)
        {
            _standby = package;
            _turnWait = Protocol.BackupTakeOverSeconds;
            _turnAt = _now + _turnWait;
        }

        /// <summary>A try that reached a host and waits for its answer: this player may be seated any moment.</summary>
        private static bool Seating(Try attempt) =>
            attempt != null && attempt.Hello != null && !attempt.Hello.IsCompleted && (attempt.Aim == Aim.NextHost || attempt.Aim == Aim.OldHost);

        /// <summary>
        /// The earliest a player that every device turns down gives up: <see cref="RefusedForSeconds"/>
        /// after the turn of the last backup in the line, so a live backup is never given up on before it
        /// could host.
        /// </summary>
        private double RefusalFloor() =>
            _line == null ? 0.0 : _declaredAt + ((Math.Max(1, _line.Line.Count) - 1) * Protocol.BackupTakeOverSeconds) + RefusedForSeconds;

        /// <summary>A host seated this player: it plays at that table now, with nothing kept for a take-over.</summary>
        private void Seated(RoomClient client)
        {
            _mode = Mode.Watching;
            _heardCount = client.Heard;
            _heardAt = _now;
            _declared = false;
            _line = null;
            _oldHost = null;
            _standby = null;
            _targets.Clear();
            _named.Clear();
            _remembered = null;
        }

        /// <summary>
        /// The places this player tries in turn while it looks for a host. Until the host is taken as
        /// lost, the old host alone. Then every successor in the line but this player, each at the
        /// generation it would host, with this player's report, and the old host unless it left, with a
        /// hello; and, once this device hosted, the successors it named. <paramref name="first"/>, a host
        /// this device found itself, comes first.
        /// </summary>
        private void Look(RoomClient client, Target first)
        {
            _targets.Clear();
            _nextTarget = 0;
            if (first != null)
            {
                Add(_targets, first);
            }

            if (_declared && _line != null)
            {
                for (int j = 0; j < _line.Line.Count; j++)
                {
                    NextHostNotice.Successor s = _line.Line[j];
                    if (s.Seat != client.LocalSeatIndex)
                    {
                        Add(_targets, new Target(Aim.NextHost, new Place(_line.Generation + j + 1, s.Address, s.Seat)));
                    }
                }
            }

            if (_oldHost != null)
            {
                Add(_targets, new Target(Aim.OldHost, _oldHost));
            }

            if (_declared)
            {
                foreach (Place place in _named)
                {
                    Add(_targets, new Target(Aim.NextHost, place));
                }
            }
        }

        /// <summary>
        /// Adds <paramref name="target"/> unless it is this device, or a place of the same generation and
        /// device is there already. A place without an address is looked up by the room's code, under
        /// which only a private room is listed: a Quick Match table's hosts are reached by address alone.
        /// </summary>
        private void Add(List<Target> targets, Target target)
        {
            Place place = target.Place;
            if (place.Address == null ? !_room.Listed : place.Address == _transport.LocalAddress)
            {
                return;
            }

            foreach (Target known in targets)
            {
                if (known.Place.Same(place))
                {
                    return;
                }
            }

            targets.Add(target);
        }

        /// <summary>Hosts the generation of this player's place in the line, to take the match over with <paramref name="package"/>.</summary>
        private void BeginTakeOver(SuccessionPackage package)
        {
            _package = package;
            _standby = null;
            _generation = _line.Generation + _place;
            _mode = Mode.TakingOver;
            _nextProbe = 0;
            _nextTryAt = _now;
            _openAbort = new CancellationTokenSource();
            CancellationToken token = _openAbort.Token;
            string code = _room.Code;
            int generation = _generation;
            bool listed = _room.Listed;
            _opening = Call(() => _transport.TakeOverAsync(code, generation, listed, token));
        }

        /// <summary>Opening the room's generation, then taking the reports while looking at its other hosts, then rebuilding the table.</summary>
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
                    // The room cannot be taken over from here for now: this player looks for a host as any
                    // player would, and tries again a little later while nobody has seated it.
                    SuccessionPackage package = _package;
                    DropTry();
                    DropTakeOver();
                    KeepForLater(package);
                    _mode = Mode.Rehoming;
                    Look(client, null);
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

            // Meanwhile it looks at the room's other hosts: this player's own connection may be what
            // dropped, or a successor after it may host the room already.
            Probe(client, null);
        }

        /// <summary>
        /// The people the rebuild waits for: everyone at the table besides this player who was there as
        /// far as it knows, but the lost host and, for a backup, the successors before it in the line:
        /// they are why it took the match over, and come back as any returning player does.
        /// </summary>
        private IEnumerable<Seat> Expected(RoomClient client)
        {
            IMatchSession match = client.Match;
            var skipped = new bool[SeatExtensions.Count];
            skipped[client.LocalSeatIndex] = true;
            if (_line != null)
            {
                if (_line.HostSeat >= 0 && _line.HostSeat < SeatExtensions.Count)
                {
                    skipped[_line.HostSeat] = true;
                }

                for (int j = 0; j < _place - 1 && j < _line.Line.Count; j++)
                {
                    skipped[_line.Line[j].Seat] = true;
                }
            }

            var expected = new List<Seat>();
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                var seat = (Seat)s;
                SessionSeat at = match.Seats[s];
                if (skipped[s] || at.IsBot || _package.IsBot(seat))
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

        /// <summary>
        /// Rebuilds the table from what everyone brought and hosts it: the match goes on, from the same
        /// trick with the bots' cards the next host in line was sent, or with the round in play dealt
        /// again by a backup, which has none (the owner, 3 Oct 2026).
        /// </summary>
        private void Commit(RoomClient client)
        {
            IMatchSession match = client.Match;
            var reports = new List<SeatReport> { new SeatReport((Seat)client.LocalSeatIndex, ((OnlineMatchSession)match).LastSeq, match.View) };
            reports.AddRange(_collector.Reports);
            RebuiltTable rebuilt = _package.HasCards ? TableRebuild.Rebuild(reports, _package) : TableRebuild.DealAgain(reports);
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
            _named.Clear();
            _remembered = null;
            _probeUntil = _now + Protocol.SeatHoldSeconds;
            _nextTryAt = _now + ProbeSeconds;
            _nextProbe = 0;
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
                if (attempt.Aim == Aim.Newer)
                {
                    // The connection alone says that device hosts the room at a newer generation.
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

        /// <param name="countsAsLeft">Whether a lost connection records the match as left (<see cref="RoomClient.Close(RoomError, bool)"/>).</param>
        private void GiveUp(RoomClient client, RoomError reason, bool countsAsLeft = true)
        {
            Stop();
            try
            {
                client.Close(reason, countsAsLeft);
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

            /// <summary>The connection to a newer host opened: that alone says it hosts the room.</summary>
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

        /// <summary>A host of the room: its generation, its device's address (null: look the code up), and its player's seat when known.</summary>
        private sealed class Place
        {
            public Place(int generation, string address, int seat)
            {
                Generation = generation;
                Address = string.IsNullOrEmpty(address) ? null : address;
                Seat = seat;
            }

            public int Generation { get; }

            public string Address { get; }

            /// <summary>The seat of the player whose device it is; <see cref="NextHostNotice.NoSeat"/> when not known.</summary>
            public int Seat { get; }

            public bool Same(Place other) => other != null && other.Generation == Generation && other.Address == Address;
        }

        /// <summary>A place to try, and how: with a report, a hello, or only a look.</summary>
        private sealed class Target
        {
            public Target(Aim aim, Place place)
            {
                Aim = aim;
                Place = place;
            }

            public Aim Aim { get; }

            public Place Place { get; }
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
