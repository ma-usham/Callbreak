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
    /// Quick Match without a matchmaker (GDD §11 screen 05, §27 "Phase 1 fallback"): this device
    /// hosts a table that the search cannot yet fill with other people, so after the bot-fill
    /// delay the empty seats are taken by Normal bots, flagged as bots, and the match starts
    /// after the "Table found" beat. The table then runs exactly like an online one (turn
    /// clock, away handling, no pause). When a real queue exists, only this class changes.
    /// </summary>
    public sealed class QuickMatchService : IMatchmakingService, IDisposable
    {
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
        private readonly HandlerFaults _faults = new HandlerFaults();

        private RoomHost _host;
        private RoomClient _client;
        private Search _search;
        private double _now;

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
            BotTiming timing = null)
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
        }

        /// <summary>The match the last successful search found; the table plays through it.</summary>
        public IMatchSession Match => _client != null ? _client.Match : null;

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
            string playerId = _auth != null && !string.IsNullOrEmpty(_auth.PlayerId) ? _auth.PlayerId : "guest";
            var identity = new HelloMessage(
                Protocol.Version, RoomIdentity.For(playerId, string.Empty), _profile.DisplayName, _profile.AvatarId, _cosmetics.SelectedDeckId);
            // The room is never shown; its one turn time stands for the card timer. The table
            // itself runs on the Quick Match clocks: 20 s to call, 15 s to play a card (GDD §6).
            var settings = new RoomSettings("Quick Match", _config.Clocks.PlaySeconds);

            // ADR 020, "any card can appear": the whole table is in one deck drawn from the
            // catalog, the same for all four seats, whatever deck this player chose.
            var rng = new Pcg32Random(_seedSource());
            IReadOnlyList<string> decks = _cosmetics.DeckIds;
            string tableDeck = decks.Count > 0 ? decks[rng.NextInt(decks.Count)] : null;
            _host = new RoomHost(
                string.Empty, settings, null, _bots, _names, rng, decks, _seedSource, _timing, clocks: _config.Clocks, tableDeck: tableDeck);
            _host.Tick(_now);
            _client = new RoomClient(_host.Local, identity, _host, MatchMode.QuickMatch, _stats, _settings);
            _client.Tick(_now);
            Task<RoomError> joined = _client.JoinAsync(CancellationToken.None);
            if (!joined.IsCompleted || joined.Result != RoomError.None)
            {
                Close();
                return Task.FromResult(new MatchmakingResult(MatchmakingError.Unavailable));
            }

            var search = new Search(progress, _now);
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
            }

            Report(search, revealed: 0, tableFound: false);
            return search.Completion.Task;
        }

        /// <summary>
        /// Drives the search and then the hosted table. Call every frame. Every part runs whatever a
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
                Advance(search, now);
            }

            _faults.Rethrow();
        }

        /// <summary>The search's own time: bots fill the table at the delay, then the seats are revealed one by one.</summary>
        private void Advance(Search search, double now)
        {
            double elapsed = now - search.StartedAt;
            if (!search.Filled)
            {
                if (now - search.ReportedAt >= _config.ProgressIntervalSeconds)
                {
                    Report(search, revealed: 0, tableFound: false);
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
                Report(search, revealed: 2, tableFound: false);
                return;
            }

            int seats = SeatExtensions.Count;
            int revealed = Math.Min(seats, 2 + (int)((now - search.FilledAt) / _config.SeatRevealSeconds));
            if (revealed != search.Revealed || now - search.ReportedAt >= _config.ProgressIntervalSeconds)
            {
                Report(search, revealed, tableFound: revealed == seats);
            }

            double allSeatedAt = search.FilledAt + ((seats - 2) * _config.SeatRevealSeconds);
            if (revealed == seats && now >= allSeatedAt + _config.TableFoundSeconds)
            {
                Finish(MatchmakingError.None);
            }
        }

        public void Dispose()
        {
            if (_search != null)
            {
                Finish(MatchmakingError.Cancelled);
            }

            Close();
            _faults.Rethrow();
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
            if (result != MatchmakingError.None)
            {
                // GDD: cancelling withdraws the ticket; nothing is left running.
                Close();
            }

            search.Completion.TrySetResult(new MatchmakingResult(result));
        }

        private void Close()
        {
            RoomClient client = _client;
            RoomHost host = _host;
            _client = null;
            _host = null;
            if (client != null)
            {
                client.Dispose();
            }

            if (host != null)
            {
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
        }

        /// <summary>Four seats: this player first, then the seats found so far; the rest are still searching.</summary>
        private void Report(Search search, int revealed, bool tableFound)
        {
            search.ReportedAt = _now;
            search.Revealed = revealed;
            if (search.Progress == null)
            {
                return;
            }

            var seats = new List<RoomSeat>(SeatExtensions.Count);
            IMatchSession match = _client != null ? _client.Match : null;
            for (int i = 0; i < SeatExtensions.Count; i++)
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

            search.Progress.Report(new MatchmakingProgress(seats, _now - search.StartedAt, tableFound));
        }

        private sealed class Search
        {
            public Search(IProgress<MatchmakingProgress> progress, double startedAt)
            {
                Progress = progress;
                StartedAt = startedAt;
                ReportedAt = startedAt;
            }

            public IProgress<MatchmakingProgress> Progress { get; }

            public TaskCompletionSource<MatchmakingResult> Completion { get; } = new TaskCompletionSource<MatchmakingResult>();

            public CancellationTokenRegistration Cancellation { get; set; }

            public double StartedAt { get; }

            public double ReportedAt { get; set; }

            public bool Filled { get; set; }

            public double FilledAt { get; set; }

            public int Revealed { get; set; }
        }
    }
}
