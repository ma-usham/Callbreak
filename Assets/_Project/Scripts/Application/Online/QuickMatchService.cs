using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// Quick Match (GDD §11 screen 05, §27; ADR 029): real players meet at tables found in a
    /// directory (<see cref="IQuickTableDirectory"/>), and bots take the seats nobody took in time.
    /// <list type="bullet">
    /// <item>A tap starts a 20 s countdown of the player's own. The device signs in, looks for open
    /// tables, oldest first, and joins one; a table that is full or fills first is skipped. With none
    /// to join it opens a table of its own: listed as open with its free seats, kept current as
    /// players come and go, and dated by this player's tap.</item>
    /// <item>A player only ever sits at a table whose host tapped no later than it did, so its own
    /// countdown is never below the table's and runs out no sooner than the table locks. When only
    /// newer tables show (as when its host left and it looks again), it opens a table of its own
    /// instead: that keeps its start by its own 20 s, and a newer table's host that is still alone
    /// then moves to it, as the older table. A newer table that has real guests stays apart.</item>
    /// <item>A host still alone at its table keeps looking every few seconds; when an older table
    /// is open (its host tapped earlier, or on a tie the smaller code), it closes its own and joins
    /// that one. Only a host with no real guests moves, so a skewed clock never sends tables to and
    /// fro.</item>
    /// <item>Until 14 s of the host's countdown only people sit down; then bots fill the empty seats
    /// one by one, about a second apart. Once all four seats are taken the table is locked (hidden
    /// from the directory, nobody new joins) and every screen shows the same 3 s countdown, which the
    /// host sends as time from now; then the match starts. A guest shows its own countdown until the
    /// lock.</item>
    /// <item>A host that leaves before the start sends its guests back to searching, their own
    /// countdowns still running; one whose time runs low opens a table of its own, which bots fill
    /// by its own 20 s. A guest whose connection drops during the match reconnects to the host's
    /// address while its seat is held. A host lost during the match is replaced by the next host in
    /// line, and the match goes on from the same trick (<see cref="HandOver"/>, ADR 030).</item>
    /// <item>A waiting host tells its guests it is still there about every second, up to the start.
    /// A guest that hears nothing for a few seconds (the host went offline, or its app is in the
    /// background) treats the host as gone, even during the 3 s countdown: it leaves, and looks
    /// again without that host. A join that gets no answer for a few seconds (a table that still
    /// shows though its host is gone) gives that table up the same way. A host that comes back finds
    /// its table as the guests left it: free seats are filled again, by people or bots, and past its
    /// own fill time the bots sit down at once and the match starts.</item>
    /// <item>Without a table directory (no peer-to-peer network, sign-in fails, or it cannot be
    /// reached) the device plays at a table of its own, filled with bots after a short delay: Quick
    /// Match as it was before (GDD §27 "Phase 1 fallback").</item>
    /// </list>
    /// A table is the same room protocol as a private room (ADR 016), so it then runs exactly like an
    /// online one (turn clock, away handling, no pause).
    /// </summary>
    public sealed class QuickMatchService : IMatchmakingService, IDisposable, IHandOverHolder
    {
        private const int SeatCount = RoomSnapshot.SeatCount;

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly QuickMatchConfig _config;
        private readonly IProfileService _profile;
        private readonly ICosmeticsService _cosmetics;
        private readonly IAuthService _auth;
        private readonly IBotFactory _bots;
        private readonly IStatsService _stats;
        private readonly ISettingsService _settings;
        private readonly INetworkStatus _network;
        private readonly NameFilter _names;
        private readonly Func<ulong> _seedSource;
        private readonly BotTiming _timing;
        private readonly IQuickMatchTransport _transport;
        private readonly IQuickTableDirectory _directory;
        private readonly Func<DateTime> _utcNow;
        private readonly IRandom _jitter;
        private readonly string _fallbackPlayerId;
        private readonly HandlerFaults _faults = new HandlerFaults();

        private RoomHost _host;
        private IHostLink _hostLink;
        private RoomClient _client;
        private IClientLink _clientLink;
        private string _code;
        private string _hostAddress;
        private bool _listed;
        private int _listedFree = -1;
        private Search _search;
        private HandOver _handOver;
        private WarmUpCall _warmUp;
        private bool _warm;
        private double _now;

        /// <param name="transport">Rooms over the peer-to-peer network; null plays every match at a table of this device's own.</param>
        /// <param name="directory">The Quick Match table directory; null, as for <paramref name="transport"/>.</param>
        /// <param name="utcNow">The wall clock the directory dates tables by; null uses the system's.</param>
        public QuickMatchService(
            QuickMatchConfig config,
            IProfileService profile,
            ICosmeticsService cosmetics,
            IAuthService auth,
            IBotFactory bots,
            IStatsService stats,
            ISettingsService settings,
            INetworkStatus network,
            NameFilter names,
            Func<ulong> seedSource,
            BotTiming timing = null,
            IQuickMatchTransport transport = null,
            IQuickTableDirectory directory = null,
            Func<DateTime> utcNow = null)
        {
            _config = config ?? new QuickMatchConfig();
            _profile = profile;
            _cosmetics = cosmetics;
            _auth = auth;
            _bots = bots;
            _stats = stats;
            _settings = settings;
            _network = network;
            _names = names;
            _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
            _timing = timing;
            _transport = transport;
            _directory = directory;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _jitter = new Pcg32Random(_seedSource());
            _fallbackPlayerId = "guest-" + _seedSource().ToString("x16");
            if (_transport != null)
            {
                _transport.ListingLost += OnListingLost;
            }
        }

        private enum Stage : byte
        {
            /// <summary>A table of this device's own, filled with bots after the short delay.</summary>
            Local,
            SigningIn,
            Searching,
            Joining,

            /// <summary>A guest at a table, until its match starts.</summary>
            Seated,
            Opening,

            /// <summary>The host of a table, until its match starts.</summary>
            Hosting,
        }

        /// <summary>The match the last successful search found; the table plays through it.</summary>
        public IMatchSession Match => _client != null ? _client.Match : null;

        /// <summary>The table this device hosts, for tests: its own, or one it took over (ADR 030); null for a guest.</summary>
        internal RoomHost Host => _host;

        /// <summary>This device's member of its table, for tests.</summary>
        internal RoomClient Client => _client;

        RoomClient IHandOverHolder.Client => _client;

        RoomHost IHandOverHolder.Host => _host;

        IHostLink IHandOverHolder.HostLink => _hostLink;

        IClientLink IHandOverHolder.ClientLink => _clientLink;

        /// <summary>
        /// Gets the network ready ahead of a search: signs in, and makes the first call to the table
        /// directory, which is slow right after signing in (EOS: about 4 s). Call it when the player
        /// is likely to play online soon, such as when the Play Online screen opens. Nothing happens
        /// without a directory, while offline, or once it has been done.
        /// </summary>
        public void WarmUp()
        {
            if (_transport == null || _directory == null || _warm || _warmUp != null || !_network.IsOnline)
            {
                return;
            }

            var warmUp = new WarmUpCall();
            _warmUp = warmUp;
            warmUp.SignIn = Call(() => _transport.SignInAsync(warmUp.Abort.Token));
        }

        public Task<MatchmakingResult> FindMatchAsync(IProgress<MatchmakingProgress> progress, CancellationToken ct)
        {
            if (_search != null)
            {
                return Task.FromResult(new MatchmakingResult(MatchmakingError.Unavailable));
            }

            if (!_network.IsOnline)
            {
                return Task.FromResult(new MatchmakingResult(MatchmakingError.Offline));
            }

            Close();
            var search = new Search(progress, _now, UnixMs());
            _search = search;
            if (ct.CanBeCanceled)
            {
                search.Cancellation = ct.Register(() =>
                {
                    if (_search == search)
                    {
                        Finish(MatchmakingError.Cancelled);
                    }
                });

                if (_search != search)
                {
                    // Cancelled before it began.
                    return search.Completion.Task;
                }
            }

            if (_transport == null || _directory == null)
            {
                StartLocal(search);
            }
            else
            {
                search.Stage = Stage.SigningIn;
                search.SignIn = Call(() => _transport.SignInAsync(search.Abort.Token));
                Report(search);
            }

            return search.Completion.Task;
        }

        /// <summary>
        /// Drives the search and then the table. Call every frame. Every part runs whatever a
        /// screen's handler threw in the part before it; the first exception is thrown last.
        /// </summary>
        public void Tick(double now)
        {
            _now = now;
            if (_host != null)
            {
                try
                {
                    _host.Tick(now);
                }
                catch (Exception e)
                {
                    _faults.Keep(e);
                }
            }

            if (_client != null)
            {
                try
                {
                    _client.Tick(now);
                }
                catch (Exception e)
                {
                    _faults.Keep(e);
                }
            }

            Search search = _search;
            if (search != null)
            {
                try
                {
                    Advance(search, now);
                }
                catch (Exception e)
                {
                    _faults.Keep(e);
                }
            }

            try
            {
                WatchTheLink(now);
            }
            catch (Exception e)
            {
                _faults.Keep(e);
            }

            AdvanceWarmUp();
            _faults.Rethrow();
        }

        public void Dispose()
        {
            if (_search != null)
            {
                Finish(MatchmakingError.Cancelled);
            }

            if (_transport != null)
            {
                _transport.ListingLost -= OnListingLost;
            }

            if (_warmUp != null)
            {
                Cancel(_warmUp.Abort);
                _warmUp = null;
            }

            Close();
            _faults.Rethrow();
        }

        private static double Elapsed(Search search, double now) => now - search.StartedAt;

        /// <summary>Signing in, then the first directory call; its answer itself does not matter.</summary>
        private void AdvanceWarmUp()
        {
            WarmUpCall warmUp = _warmUp;
            if (warmUp == null)
            {
                return;
            }

            if (warmUp.Find == null)
            {
                if (!warmUp.SignIn.IsCompleted)
                {
                    return;
                }

                if (!Answer(warmUp.SignIn, false))
                {
                    // Not now: the next call to WarmUp, or the search, tries again.
                    _warmUp = null;
                    return;
                }

                warmUp.Find = Call(() => _directory.FindQuickTablesAsync(warmUp.Abort.Token));
            }

            if (warmUp.Find.IsCompleted)
            {
                _warm = Answer(warmUp.Find, new QuickTableSearch(null, RoomError.Unknown)).Error == RoomError.None;
                _warmUp = null;
            }
        }

        /// <summary>
        /// The directory dropped this device's table, as after the device lost the network for a
        /// moment: it is listed again on the next tick (listing a table that is still listed changes
        /// nothing), so players still find it (ADR 029).
        /// </summary>
        private void OnListingLost(string roomCode)
        {
            Search search = _search;
            if (search == null || search.Stage != Stage.Hosting || _host == null || _host.IsLocked || roomCode != _code || search.Listing != null)
            {
                return;
            }

            _listed = false;
            search.RelistAt = _now;
        }

        private static int Oldest(QuickTable a, QuickTable b)
        {
            int byAge = a.OpenedAtUnixMs.CompareTo(b.OpenedAtUnixMs);
            if (byAge != 0)
            {
                return byAge;
            }

            int byCode = string.CompareOrdinal(a.RoomCode, b.RoomCode);
            return byCode != 0 ? byCode : string.CompareOrdinal(a.HostAddress, b.HostAddress);
        }

        private void Advance(Search search, double now)
        {
            switch (search.Stage)
            {
                case Stage.Local:
                    AdvanceLocal(search, now);
                    return;
                case Stage.SigningIn:
                    AdvanceSignIn(search, now);
                    break;
                case Stage.Searching:
                    AdvanceSearch(search, now);
                    break;
                case Stage.Joining:
                    AdvanceJoin(search, now);
                    break;
                case Stage.Seated:
                    AdvanceSeated(search, now);
                    break;
                case Stage.Opening:
                    AdvanceOpening(search, now);
                    break;
                case Stage.Hosting:
                    AdvanceHosting(search, now);
                    break;
            }

            if (_search == search && search.Stage != Stage.Local)
            {
                int signature = Signature(search);
                if (search.Dirty || signature != search.ReportedSignature || now - search.ReportedAt >= _config.ProgressIntervalSeconds)
                {
                    Report(search);
                }
            }
        }

        private void AdvanceSignIn(Search search, double now)
        {
            if (!search.SignIn.IsCompleted)
            {
                return;
            }

            bool signedIn = Answer(search.SignIn, false);
            search.SignIn = null;
            if (!signedIn)
            {
                // No peer-to-peer network to play over (ADR 029).
                FallBack(search);
                return;
            }

            BeginSearch(search, now);
        }

        /// <summary>Looks for open tables, unless time is running low: then this player hosts, and bots fill the table in time.</summary>
        private void BeginSearch(Search search, double now)
        {
            search.Dirty = true;
            if (Elapsed(search, now) >= _config.PeopleOnlySeconds)
            {
                BeginHosting(search);
                return;
            }

            search.Stage = Stage.Searching;
            search.Find = Call(() => _directory.FindQuickTablesAsync(search.Abort.Token));
        }

        private void AdvanceSearch(Search search, double now)
        {
            if (!search.Find.IsCompleted)
            {
                if (Elapsed(search, now) >= _config.PeopleOnlySeconds)
                {
                    search.Find = null;
                    BeginHosting(search);
                }

                return;
            }

            QuickTableSearch found = Answer(search.Find, new QuickTableSearch(null, RoomError.Unknown));
            search.Find = null;
            if (found.Error != RoomError.None)
            {
                if (!search.Searched)
                {
                    // The directory cannot be reached at all.
                    FallBack(search);
                    return;
                }

                BeginHosting(search);
                return;
            }

            search.Searched = true;
            List<QuickTable> tables = Joinable(found.Tables, search.TapUnixMs, null, search.Avoid);
            if (tables.Count == 0)
            {
                BeginHosting(search);
                return;
            }

            BeginJoining(search, tables, now);
        }

        /// <summary>
        /// The tables this player may try, oldest first: open with a seat free, not its own, and
        /// opened by a host who tapped no later than this player did (<paramref name="tappedAt"/>),
        /// so the table's countdown ends no later than this player's own. For a host thinking of
        /// moving (<paramref name="mineCode"/> is its table), only tables older than its own: by the
        /// tap, then by code. Hosts in <paramref name="avoid"/> went quiet on this search: not again.
        /// </summary>
        private List<QuickTable> Joinable(IReadOnlyList<QuickTable> tables, long tappedAt, string mineCode, HashSet<string> avoid)
        {
            var joinable = new List<QuickTable>();
            string me = _transport.LocalAddress;
            var mine = new QuickTable(mineCode, me, 0, tappedAt);
            for (int i = 0; i < tables.Count; i++)
            {
                QuickTable table = tables[i];
                if (table.FreeSeats <= 0 || string.IsNullOrEmpty(table.HostAddress) || table.HostAddress == me || !RoomCode.IsValid(table.RoomCode))
                {
                    continue;
                }

                if (table.OpenedAtUnixMs > tappedAt || (mineCode != null && Oldest(table, mine) >= 0) || avoid.Contains(table.HostAddress))
                {
                    continue;
                }

                joinable.Add(table);
            }

            joinable.Sort(Oldest);
            return joinable;
        }

        private void BeginJoining(Search search, List<QuickTable> tables, double now)
        {
            search.Stage = Stage.Joining;
            search.Candidates = new Queue<QuickTable>(tables);
            search.Dirty = true;
            TryNextTable(search, now);
        }

        /// <summary>Connects to the next table on the list; with none left, or with time running low, this player hosts.</summary>
        private void TryNextTable(Search search, double now)
        {
            DropJoin(search);
            if (search.Candidates == null || search.Candidates.Count == 0 || Elapsed(search, now) >= _config.PeopleOnlySeconds)
            {
                BeginHosting(search);
                return;
            }

            QuickTable table = search.Candidates.Dequeue();
            search.Target = table;
            search.AttemptAbort = new CancellationTokenSource();
            search.AttemptDeadline = now + _config.JoinTimeoutSeconds;
            CancellationToken token = search.AttemptAbort.Token;
            search.Link = Call(() => _transport.JoinAtAsync(table.RoomCode, table.HostAddress, token));
        }

        private void AdvanceJoin(Search search, double now)
        {
            if (Elapsed(search, now) >= _config.PeopleOnlySeconds)
            {
                // A join still under way is given up for a table of this player's own, which bots fill in time.
                DropJoin(search);
                BeginHosting(search);
                return;
            }

            if (now >= search.AttemptDeadline)
            {
                // No answer: a table that still shows though its host is gone (or gone quiet). It is
                // not tried again on this search.
                search.Avoid.Add(search.Target.HostAddress);
                TryNextTable(search, now);
                return;
            }

            if (search.Joiner != null)
            {
                search.Joiner.Tick(now);
            }

            if (search.Link != null)
            {
                if (!search.Link.IsCompleted)
                {
                    return;
                }

                ClientLinkResult linked = Answer(search.Link, new ClientLinkResult(null, RoomError.Unknown));
                search.Link = null;
                if (!linked.Succeeded)
                {
                    TryNextTable(search, now);
                    return;
                }

                // Connected: the host has as long again to answer the hello.
                search.AttemptDeadline = now + _config.JoinTimeoutSeconds;
                search.JoinLink = linked.Link;
                search.Joiner = new RoomClient(linked.Link, Hello(search.Target.RoomCode), null, MatchMode.QuickMatch, _stats, _settings);
                search.Joiner.Tick(now);
                search.Hello = search.Joiner.JoinAsync(search.AttemptAbort.Token);
            }

            if (search.Hello == null || !search.Hello.IsCompleted)
            {
                return;
            }

            RoomError joined = Answer(search.Hello, RoomError.Unknown);
            search.Hello = null;
            if (joined != RoomError.None)
            {
                // Full, starting, or racing someone else to the last seat: the next table.
                TryNextTable(search, now);
                return;
            }

            _client = search.Joiner;
            _clientLink = search.JoinLink;
            _code = search.Target.RoomCode;
            _hostAddress = search.Target.HostAddress;
            search.Joiner = null;
            search.JoinLink = null;
            search.AttemptAbort = null;
            search.HeardCount = _client.Heard;
            search.HeardAt = now;
            search.Stage = Stage.Seated;
            search.Dirty = true;
        }

        private void AdvanceSeated(Search search, double now)
        {
            RoomClient client = _client;
            if (client.Match != null)
            {
                Finish(MatchmakingError.None);
                return;
            }

            // Every message from the host, a heartbeat or anything else, says it is still there. Counted
            // rather than timed, so messages that waited while this app was in the background count too.
            if (client.Heard != search.HeardCount)
            {
                search.HeardCount = client.Heard;
                search.HeardAt = now;
            }

            bool silent = now - search.HeardAt >= _config.HostSilenceSeconds;
            if (client.IsClosed || _clientLink == null || !_clientLink.IsConnected || silent)
            {
                // The host left before the start, went quiet (offline, or its app in the background),
                // or the connection went: look again, on this player's own clock, which kept running
                // (ADR 029). A host that went quiet is not tried again on this search.
                if (silent && _hostAddress != null)
                {
                    search.Avoid.Add(_hostAddress);
                }

                Close();
                BeginSearch(search, now);
            }
        }

        private void BeginHosting(Search search)
        {
            DropJoin(search);
            search.Stage = Stage.Opening;
            search.Dirty = true;
            search.Code = DrawCode();
            search.TableAbort = new CancellationTokenSource();
            CancellationToken token = search.TableAbort.Token;
            string code = search.Code;
            search.Open = Call(() => _transport.OpenAsync(code, token));
        }

        private void AdvanceOpening(Search search, double now)
        {
            if (!search.Open.IsCompleted)
            {
                return;
            }

            HostLinkResult opened = Answer(search.Open, new HostLinkResult(null, RoomError.Unknown));
            search.Open = null;
            if (!opened.Succeeded || !OpenTable(search, opened.Link, now))
            {
                // The network will not take a table: one of this device's own (ADR 029).
                FallBack(search);
            }
        }

        /// <summary>This device hosts the table: its own player sits down first, and the table is listed as open.</summary>
        private bool OpenTable(Search search, IHostLink link, double now)
        {
            string code = search.Code;
            // The room is never shown; its one turn time stands for the card timer. The table runs on
            // the Quick Match clocks: 20 s to call, 15 s to play a card (GDD §6).
            var settings = new RoomSettings("Quick Match", _config.Clocks.PlaySeconds);
            var host = new RoomHost(
                code, settings, link, _bots, _names, new Pcg32Random(_seedSource()), _cosmetics.DeckIds, _seedSource, _timing, clocks: _config.Clocks, quickTable: true,
                heartbeatSeconds: _config.HeartbeatSeconds);
            host.Tick(now);
            var client = new RoomClient(host.Local, Hello(code), host, MatchMode.QuickMatch, _stats, _settings);
            client.Tick(now);
            Task<RoomError> joined = client.JoinAsync(CancellationToken.None);
            if (!joined.IsCompleted || joined.Result != RoomError.None)
            {
                client.Dispose();
                DisposeQuietly(host);
                link.Dispose();
                return false;
            }

            _host = host;
            _hostLink = link;
            _client = client;
            _code = code;
            search.Stage = Stage.Hosting;
            search.Dirty = true;
            search.NextSearchAt = now + _config.SearchIntervalSeconds;
            search.NextBotAt = search.StartedAt + _config.PeopleOnlySeconds;
            search.StartsAt = double.NaN;
            ListTable(search);
            return true;
        }

        /// <summary>Lists the table as open with its free seats, dated by this player's tap: a table is as old as its host's countdown.</summary>
        private void ListTable(Search search)
        {
            int free = _host.FreeSeats;
            string code = _code;
            long openedAt = search.TapUnixMs;
            CancellationToken token = search.TableAbort.Token;
            _listed = true;
            _listedFree = free;
            search.Listing = Call(() => _directory.ListQuickAsync(code, free, openedAt, token));
        }

        private void AdvanceHosting(Search search, double now)
        {
            RoomHost host = _host;
            if (search.Listing != null && search.Listing.IsCompleted)
            {
                bool listed = Answer(search.Listing, RoomError.Unknown) == RoomError.None;
                search.Listing = null;
                if (!listed)
                {
                    // The directory did not take it: tried again a little later.
                    _listed = false;
                    search.RelistAt = now + _config.SearchIntervalSeconds;
                }
            }

            if (!_listed && search.Listing == null && !host.IsLocked && now >= search.RelistAt)
            {
                ListTable(search);
            }

            // From the end of the people-only time, bots take the empty seats one by one; a table that
            // is late (its host was away) gets them all at once, so it still starts in time.
            while (!host.IsLocked && Elapsed(search, now) >= _config.PeopleOnlySeconds && now >= search.NextBotAt && host.SeatBot())
            {
                search.NextBotAt = now + NextBotGap(search, now, host.FreeSeats);
                search.Dirty = true;
            }

            // Someone left during the countdown: a bot takes the seat, and the table stays full.
            while (host.IsLocked && host.FreeSeats > 0 && host.SeatBot())
            {
                search.Dirty = true;
            }

            if (!host.IsLocked && host.FreeSeats == 0)
            {
                // Full: locked, hidden from the directory, and the same countdown on every screen,
                // ending by this player's own 20 s.
                double startsAt = now + _config.StartCountdownSeconds;
                double deadline = search.StartedAt + _config.CountdownSeconds;
                if (startsAt > deadline)
                {
                    startsAt = Math.Max(now, deadline);
                }

                host.Lock(startsAt);
                search.StartsAt = startsAt;
                search.Dirty = true;
            }

            SyncFreeSeats(host);
            if (host.IsLocked)
            {
                if (now >= search.StartsAt)
                {
                    Start();
                }

                return;
            }

            MoveIfOlder(search, now, host);
        }

        /// <summary>The directory hears of every change in the free seats; a locked table has none, which hides it.</summary>
        private void SyncFreeSeats(RoomHost host)
        {
            int free = host.IsLocked ? 0 : host.FreeSeats;
            if (!_listed || free == _listedFree)
            {
                return;
            }

            _listedFree = free;
            try
            {
                _directory.SetQuickFreeSeats(_code, free);
            }
            catch (Exception e)
            {
                _faults.Keep(e);
            }
        }

        /// <summary>
        /// A host still alone at its table looks for an older open table every few seconds, and
        /// moves to it (ADR 029): only a host with no real guests ever moves, so nobody else is
        /// sent away.
        /// </summary>
        private void MoveIfOlder(Search search, double now, RoomHost host)
        {
            if (host.Guests > 0)
            {
                search.MergeFind = null;
                return;
            }

            if (search.MergeFind == null)
            {
                if (now >= search.NextSearchAt)
                {
                    CancellationToken token = search.TableAbort.Token;
                    search.MergeFind = Call(() => _directory.FindQuickTablesAsync(token));
                }

                return;
            }

            if (!search.MergeFind.IsCompleted)
            {
                return;
            }

            QuickTableSearch found = Answer(search.MergeFind, new QuickTableSearch(null, RoomError.Unknown));
            search.MergeFind = null;
            search.NextSearchAt = now + _config.SearchIntervalSeconds;
            if (found.Error != RoomError.None)
            {
                return;
            }

            // This table is dated by this player's tap, as the directory has it.
            List<QuickTable> older = Joinable(found.Tables, search.TapUnixMs, _code, search.Avoid);
            if (older.Count == 0)
            {
                return;
            }

            DropTableCalls(search);
            Close();
            BeginJoining(search, older, now);
        }

        /// <summary>The match starts: the table leaves the directory, and every member is told to load it.</summary>
        private void Start()
        {
            UnlistTable();
            bool started = _host.StartMatch(fillWithBots: true);
            Finish(started && _client != null && _client.Match != null ? MatchmakingError.None : MatchmakingError.Unavailable);
        }

        /// <summary>The next bot's pause: about a second, but short enough that the table is full by 17 s of this player's countdown.</summary>
        private double NextBotGap(Search search, double now, int seatsLeft)
        {
            double gap = _config.BotGapMinSeconds + ((_config.BotGapMaxSeconds - _config.BotGapMinSeconds) * _jitter.NextInt(1001) / 1000.0);
            if (seatsLeft <= 0)
            {
                return gap;
            }

            double fillBy = search.StartedAt + _config.CountdownSeconds - _config.StartCountdownSeconds;
            return Math.Min(gap, Math.Max(0.0, fillBy - now) / seatsLeft);
        }

        /// <summary>
        /// No table directory to use (sign-in failed, it cannot be reached, or the network will not
        /// take a table): a table of this device's own, as before (ADR 029).
        /// </summary>
        private void FallBack(Search search)
        {
            DropJoin(search);
            DropTableCalls(search);
            Close();
            StartLocal(search);
        }

        /// <summary>A table of this device's own, filled with bots after the short delay (GDD §27 "Phase 1 fallback").</summary>
        private void StartLocal(Search search)
        {
            search.Stage = Stage.Local;
            // The room is never shown; its one turn time stands for the card timer. The table itself
            // runs on the Quick Match clocks: 20 s to call, 15 s to play a card (GDD §6).
            var settings = new RoomSettings("Quick Match", _config.Clocks.PlaySeconds);

            // The whole table is in the deck this player chose, every face and back of every seat,
            // as offline and in a room this player hosts (ADR 025; ADR 020 drew one from the catalog).
            var rng = new Pcg32Random(_seedSource());
            _host = new RoomHost(
                string.Empty, settings, null, _bots, _names, rng, _cosmetics.DeckIds, _seedSource, _timing, clocks: _config.Clocks);
            _host.Tick(_now);
            _client = new RoomClient(_host.Local, Hello(string.Empty), _host, MatchMode.QuickMatch, _stats, _settings);
            _client.Tick(_now);
            Task<RoomError> joined = _client.JoinAsync(CancellationToken.None);
            if (!joined.IsCompleted || joined.Result != RoomError.None)
            {
                Finish(MatchmakingError.Unavailable);
                return;
            }

            ReportLocal(search, revealed: 0, tableFound: false);
        }

        /// <summary>The search's own time at a table of this device's own: bots fill it at the delay, then the seats are revealed one by one.</summary>
        private void AdvanceLocal(Search search, double now)
        {
            double elapsed = now - search.StartedAt;
            if (!search.Filled)
            {
                if (now - search.ReportedAt >= _config.ProgressIntervalSeconds)
                {
                    ReportLocal(search, revealed: 0, tableFound: false);
                }

                if (elapsed < _config.BotFillDelaySeconds)
                {
                    return;
                }

                // GDD §11 screen 05: at exactly the configured delay the empty seats fill with bots.
                if (!_host.StartMatch(fillWithBots: true) || _client.Match == null)
                {
                    Finish(MatchmakingError.Unavailable);
                    return;
                }

                search.Filled = true;
                search.FilledAt = now;
                ReportLocal(search, revealed: 2, tableFound: false);
                return;
            }

            int revealed = Math.Min(SeatCount, 2 + (int)((now - search.FilledAt) / _config.SeatRevealSeconds));
            if (revealed != search.Revealed || now - search.ReportedAt >= _config.ProgressIntervalSeconds)
            {
                ReportLocal(search, revealed, tableFound: revealed == SeatCount);
            }

            double allSeatedAt = search.FilledAt + ((SeatCount - 2) * _config.SeatRevealSeconds);
            if (revealed == SeatCount && now >= allSeatedAt + _config.TableFoundSeconds)
            {
                Finish(MatchmakingError.None);
            }
        }

        /// <summary>
        /// Once the match is on, every player's seat is kept through hosts that come and go (ADR 030):
        /// a guest whose connection drops reconnects to the host's address while its seat is held, and a
        /// host lost for good is replaced by the next host in line.
        /// </summary>
        private void WatchTheLink(double now)
        {
            if (_handOver == null && _search == null && _client != null && _client.Match != null && _code != null && _transport != null)
            {
                _handOver = new HandOver(_transport, this, HandOverRoom(), _network, now);
            }

            if (_handOver != null)
            {
                _handOver.Tick(now);
            }
        }

        /// <summary>How this table is hosted here, should this player take it over (ADR 030).</summary>
        private HandOverRoom HandOverRoom() => new HandOverRoom
        {
            Code = _code,
            Listed = false,
            QuickTable = true,
            Mode = MatchMode.QuickMatch,
            Settings = new RoomSettings("Quick Match", _config.Clocks.PlaySeconds),
            Clocks = _config.Clocks,
            Bots = _bots,
            Names = _names,
            DeckIds = _cosmetics.DeckIds,
            Seeds = _seedSource,
            Timing = _timing,
            HeartbeatSeconds = _config.HeartbeatSeconds,
            HostAddress = _hostAddress,
        };

        void IHandOverHolder.Hold(RoomHost host, IHostLink hostLink, IClientLink clientLink)
        {
            _host = host;
            _hostLink = hostLink;
            _clientLink = clientLink;
        }

        private void Finish(MatchmakingError result)
        {
            Search search = _search;
            if (search == null)
            {
                return;
            }

            _search = null;
            search.Cancellation.Dispose();
            Cancel(search.Abort);
            DropJoin(search);
            DropTableCalls(search);
            if (result != MatchmakingError.None)
            {
                // GDD: cancelling withdraws the ticket; nothing is left running.
                Close();
            }

            search.Completion.TrySetResult(new MatchmakingResult(result));
        }

        /// <summary>
        /// Lets go of the table this device sits at: a hosted one leaves the directory and its guests
        /// are told (before the start they look again; during the match the next host in line takes it
        /// over, ADR 030); a guest that has not started playing tells the host it leaves.
        /// </summary>
        private void Close()
        {
            if (_handOver != null)
            {
                HandOver handOver = _handOver;
                _handOver = null;
                try
                {
                    handOver.Dispose();
                }
                catch (Exception e)
                {
                    _faults.Keep(e);
                }
            }

            UnlistTable();
            RoomClient client = _client;
            RoomHost host = _host;
            IHostLink hostLink = _hostLink;
            IClientLink clientLink = _clientLink;
            _client = null;
            _host = null;
            _hostLink = null;
            _clientLink = null;
            _code = null;
            _hostAddress = null;
            if (client != null)
            {
                bool waitingGuest = host == null && client.Match == null && !client.IsClosed && clientLink != null && clientLink.IsConnected;

                // A host whose match is on (the app is closing) leaves it as anyone leaving on purpose
                // does (GDD §8): it counts as left, and the match goes on without it (ADR 030).
                bool hostInMatch = host != null && host.InMatch && client.Match != null && !client.IsClosed
                    && client.Match.Status != SessionStatus.Ended && client.Match.Status != SessionStatus.Failed;
                if (waitingGuest || hostInMatch)
                {
                    try
                    {
                        client.LeaveAsync(CancellationToken.None);
                    }
                    catch (Exception e)
                    {
                        _faults.Keep(e);
                    }
                }

                client.Dispose();
            }

            DisposeQuietly(host);
            if (clientLink != null)
            {
                clientLink.Dispose();
            }

            if (hostLink != null)
            {
                hostLink.Dispose();
            }
        }

        private void UnlistTable()
        {
            if (_listed && _code != null)
            {
                try
                {
                    _directory.UnlistQuick(_code);
                }
                catch (Exception e)
                {
                    _faults.Keep(e);
                }
            }

            _listed = false;
            _listedFree = -1;
        }

        /// <summary>Gives up a join under way: what it connected is closed.</summary>
        private void DropJoin(Search search)
        {
            CancellationTokenSource abort = search.AttemptAbort;
            search.AttemptAbort = null;
            if (abort != null)
            {
                Cancel(abort);
            }

            Task<ClientLinkResult> link = search.Link;
            search.Link = null;
            if (link != null && link.Status == TaskStatus.RanToCompletion && link.Result.Succeeded)
            {
                link.Result.Link.Dispose();
            }

            search.Hello = null;
            RoomClient joiner = search.Joiner;
            IClientLink joinLink = search.JoinLink;
            search.Joiner = null;
            search.JoinLink = null;
            if (joiner != null)
            {
                joiner.Dispose();
            }

            if (joinLink != null)
            {
                joinLink.Dispose();
            }
        }

        /// <summary>Cancels what the table asked the network for and has not heard back about (opening it, listing it, looking further).</summary>
        private void DropTableCalls(Search search)
        {
            CancellationTokenSource abort = search.TableAbort;
            search.TableAbort = null;
            if (abort != null)
            {
                Cancel(abort);
            }

            Task<HostLinkResult> open = search.Open;
            search.Open = null;
            if (open != null && open.Status == TaskStatus.RanToCompletion && open.Result.Succeeded)
            {
                open.Result.Link.Dispose();
            }

            search.Listing = null;
            search.MergeFind = null;
        }

        private void DisposeQuietly(RoomHost host)
        {
            if (host == null)
            {
                return;
            }

            try
            {
                host.Dispose();
            }
            catch (Exception e)
            {
                // A screen's handler failed while the table closed; it is closed all the same.
                _faults.Keep(e);
            }
        }

        private void Cancel(CancellationTokenSource source)
        {
            try
            {
                source.Cancel();
            }
            catch (Exception e)
            {
                _faults.Keep(e);
            }
        }

        /// <summary>What changes the seats or the countdown on screen: a new value is reported at once.</summary>
        private int Signature(Search search)
        {
            int occupied = 0;
            if (_client != null && _client.LocalSeatIndex >= 0)
            {
                for (int i = 0; i < _client.Seats.Count; i++)
                {
                    if (_client.Seats[i].Occupied)
                    {
                        occupied++;
                    }
                }
            }

            bool found = _client != null && !double.IsNaN(_client.StartsAt);
            return ((int)search.Stage * 100) + (occupied * 10) + (found ? 1 : 0);
        }

        /// <summary>
        /// The seats in the table's order, this player's marked as its own (the screen puts it first,
        /// ADR 029); this player's countdown until the table is locked, then the table's.
        /// </summary>
        private void Report(Search search)
        {
            search.Dirty = false;
            search.ReportedAt = _now;
            search.ReportedSignature = Signature(search);
            if (search.Progress == null)
            {
                return;
            }

            bool atTable = _client != null && _client.LocalSeatIndex >= 0 && (search.Stage == Stage.Seated || search.Stage == Stage.Hosting);
            var seats = new List<RoomSeat>(SeatCount);
            if (atTable)
            {
                seats.AddRange(_client.Seats);
            }
            else
            {
                // Not at a table yet: this player alone, in the first seat.
                seats.Add(new RoomSeat(0, true, _profile.DisplayName, _profile.AvatarId, _cosmetics.SelectedDeckId, false, true, false, true));
                for (int i = 1; i < SeatCount; i++)
                {
                    seats.Add(new RoomSeat(i, false, string.Empty, 0, string.Empty, false, false, false, false));
                }
            }

            double startsAt = atTable ? _client.StartsAt : double.NaN;
            bool found = !double.IsNaN(startsAt);
            var progress = new MatchmakingProgress(
                seats,
                _now - search.StartedAt,
                found,
                Math.Max(0.0, search.StartedAt + _config.CountdownSeconds - _now),
                found ? Math.Max(0.0, startsAt - _now) : double.NaN);
            _faults.Raise<MatchmakingProgress>(search.Progress.Report, progress);
        }

        /// <summary>A table of this device's own: this player first, then the seats found so far; the rest are still searching.</summary>
        private void ReportLocal(Search search, int revealed, bool tableFound)
        {
            search.ReportedAt = _now;
            search.Revealed = revealed;
            if (search.Progress == null)
            {
                return;
            }

            var seats = new List<RoomSeat>(SeatCount);
            IMatchSession match = _client != null ? _client.Match : null;
            for (int i = 0; i < SeatCount; i++)
            {
                if (i == 0)
                {
                    seats.Add(new RoomSeat(0, true, _profile.DisplayName, _profile.AvatarId, _cosmetics.SelectedDeckId, false, true, false, true));
                }
                else if (match != null && i < revealed)
                {
                    SessionSeat seat = match.Seats[i];
                    seats.Add(new RoomSeat(i, true, seat.Name, seat.AvatarId, seat.DeckThemeId, false, true, seat.IsBot, false));
                }
                else
                {
                    seats.Add(new RoomSeat(i, false, string.Empty, 0, string.Empty, false, false, false, false));
                }
            }

            _faults.Raise<MatchmakingProgress>(search.Progress.Report, new MatchmakingProgress(seats, _now - search.StartedAt, tableFound));
        }

        /// <summary>
        /// This player's hello to the table <paramref name="code"/>, naming it by its identity in that
        /// table alone (GDD §27), as a room member does.
        /// </summary>
        private HelloMessage Hello(string code)
        {
            string playerId = _auth != null && !string.IsNullOrEmpty(_auth.PlayerId) ? _auth.PlayerId : _fallbackPlayerId;
            return new HelloMessage(Protocol.Version, RoomIdentity.For(playerId, code), _profile.DisplayName, _profile.AvatarId, _cosmetics.SelectedDeckId);
        }

        /// <summary>A table's code is never shown; it is drawn from the secure seed source all the same, as a room's is.</summary>
        private string DrawCode() => RoomCode.Generate(new Pcg32Random(_seedSource()).NextInt);

        private long UnixMs() => (long)Math.Round((_utcNow() - UnixEpoch).TotalMilliseconds);

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

        /// <summary>The task's answer; <paramref name="failed"/> when it failed or was cancelled.</summary>
        private static T Answer<T>(Task<T> task, T failed)
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                return task.Result;
            }

            if (task.IsFaulted)
            {
                // Seen, so it is never reported as unobserved.
                _ = task.Exception;
            }

            return failed;
        }

        private sealed class Search
        {
            public Search(IProgress<MatchmakingProgress> progress, double startedAt, long tapUnixMs)
            {
                Progress = progress;
                StartedAt = startedAt;
                ReportedAt = startedAt;
                TapUnixMs = tapUnixMs;
            }

            public IProgress<MatchmakingProgress> Progress { get; }

            public TaskCompletionSource<MatchmakingResult> Completion { get; } = new TaskCompletionSource<MatchmakingResult>();

            public CancellationTokenRegistration Cancellation { get; set; }

            /// <summary>Cancels the search's own calls to the network (signing in, looking for tables) once it ends.</summary>
            public CancellationTokenSource Abort { get; } = new CancellationTokenSource();

            /// <summary>When the player tapped: the countdown counts from here.</summary>
            public double StartedAt { get; }

            /// <summary>When the player tapped, by the wall clock: the age of a table this player hosts, and how old a table it may join must be.</summary>
            public long TapUnixMs { get; }

            public Stage Stage { get; set; }

            public double ReportedAt { get; set; }

            public int ReportedSignature { get; set; } = -1;

            /// <summary>Something on screen changed: reported on this tick.</summary>
            public bool Dirty { get; set; } = true;

            // A table of this device's own.
            public bool Filled { get; set; }

            public double FilledAt { get; set; }

            public int Revealed { get; set; }

            // Looking for a table.
            public Task<bool> SignIn { get; set; }

            /// <summary>A search of the directory has answered once: a later failure no longer means there is none.</summary>
            public bool Searched { get; set; }

            public Task<QuickTableSearch> Find { get; set; }

            // Joining one.
            public Queue<QuickTable> Candidates { get; set; }

            public QuickTable Target { get; set; }

            public CancellationTokenSource AttemptAbort { get; set; }

            public Task<ClientLinkResult> Link { get; set; }

            public IClientLink JoinLink { get; set; }

            public RoomClient Joiner { get; set; }

            public Task<RoomError> Hello { get; set; }

            // Hosting one.
            public string Code { get; set; }

            public CancellationTokenSource TableAbort { get; set; }

            public Task<HostLinkResult> Open { get; set; }

            public Task<RoomError> Listing { get; set; }

            public double RelistAt { get; set; }

            public Task<QuickTableSearch> MergeFind { get; set; }

            public double NextSearchAt { get; set; }

            public double NextBotAt { get; set; }

            public double StartsAt { get; set; } = double.NaN;

            /// <summary>When the join under way gives up on its table unless it hears back.</summary>
            public double AttemptDeadline { get; set; }

            /// <summary>The messages a guest had from its host when it last looked: a count that stops moving means a host that went quiet.</summary>
            public int HeardCount { get; set; }

            public double HeardAt { get; set; }

            /// <summary>Hosts that went quiet on this search, or never answered it: their tables are not tried again.</summary>
            public HashSet<string> Avoid { get; } = new HashSet<string>();
        }

        /// <summary>Signing in and the first directory call, made ahead of a search (<see cref="WarmUp"/>).</summary>
        private sealed class WarmUpCall
        {
            public CancellationTokenSource Abort { get; } = new CancellationTokenSource();

            public Task<bool> SignIn { get; set; }

            public Task<QuickTableSearch> Find { get; set; }
        }
    }
}
