using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Profile;
using Callbreak.Application.Settings;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;

namespace Callbreak.Application.Sessions
{
    /// <summary>Makes the strategy for a bot level (bound to <c>Callbreak.Bots</c> in the bootstrap).</summary>
    public interface IBotFactory
    {
        IBotStrategy Create(BotLevel level);
    }

    /// <summary>
    /// An offline match against three computer players (GDD §29): the rules engine and the bots run
    /// on the device, with no network and no turn clock. Bots move after a human-looking pause,
    /// the match pauses with the match menu, and a snapshot is saved after every trick, on pause
    /// and on leave, so an interrupted match can be resumed from the main menu. Given a bot
    /// worker (the game passes the thread pool, GDD §20), Hard bots think off the main thread.
    /// A new match is dealt from the seed, of a few fair draws, that gives the player the strongest
    /// hands (<see cref="FriendlyDeal"/>, ADR 056), so offline play is friendly at every level.
    /// </summary>
    public sealed class OfflineMatchSession : IMatchSession
    {
        private readonly MatchEngine _engine;
        private readonly BotDriver _bots;
        private readonly IOfflineMatchStore _store;
        private readonly IStatsService _stats;
        private readonly ISettingsService _settings;
        private readonly Pcg32Random _rng;
        private readonly ulong _seed;
        private readonly BotDifficulty _difficulty;
        private readonly bool _resumed;
        private readonly List<MatchEvent> _outbox = new List<MatchEvent>();
        private readonly SessionSeat[] _seats = new SessionSeat[4];

        private bool _started;
        private bool _ended;
        private double _now;
        private double _pausedAt;
        private SessionStatus _status = SessionStatus.Starting;

        public OfflineMatchSession(
            MatchLaunchRequest request,
            IOfflineMatchStore store,
            IBotFactory botFactory,
            IProfileService profile,
            ICosmeticsService cosmetics,
            ISettingsService settings,
            IStatsService stats,
            BotTiming timing = null,
            BotDriver.IWorker botWorker = null,
            Func<ulong> seeds = null)
        {
            _store = store;
            _stats = stats;
            _settings = settings;
            _difficulty = request.Difficulty;
            timing ??= BotTiming.Offline;
            RuleSet rules = OfflineMatchStore.Rules;

            // The engine keeps dealing from the generator it was restored with, so that one, and
            // no copy of it, is the generator whose state the next save must hold.
            if (request.ResumeSaved && store.TryLoad(out OfflineMatchSave save) &&
                OfflineMatchStore.TryRestore(save, out MatchEngine restored, out Pcg32Random restoredRng))
            {
                _engine = restored;
                _rng = restoredRng;
                _seed = save.seed;
                _difficulty = (BotDifficulty)save.difficulty;
                _resumed = true;
            }
            else
            {
                // A new match gets the seed, of a few fair draws, that deals the player the
                // strongest hands; the harder the bots, the more draws (ADR 056).
                _seed = FriendlyDeal.ChooseSeed(seeds ?? new CryptoRandom().NextSeed, FriendlyDeal.Candidates(_difficulty), rules, LocalSeat);
                _rng = new Pcg32Random(_seed);
                SeatProfile[] seats = SeatRoster.Offline(
                    profile.DisplayName, profile.AvatarId, cosmetics.SelectedDeckId,
                    new Pcg32Random(_seed, 7), settings.CompletedMatches == 0);
                _engine = new MatchEngine(rules, _rng, new MatchSetup("offline-" + _seed.ToString("x16"), seats));
            }

            var botSeats = new Dictionary<Seat, IBotStrategy>();
            IReadOnlyList<SeatProfile> profiles = _engine.ViewFor(Seat.South).Seats;

            // ADR 031: the player sees every card in its own deck, the one it had when the match was
            // dealt ("Choose a deck for your next match"), also in a match saved while bots still drew
            // backs of their own.
            string deck = profiles[(int)LocalSeat].DeckThemeId;
            for (int s = 0; s < 4; s++)
            {
                SeatProfile p = profiles[s];
                if (p.IsBot)
                {
                    botSeats[(Seat)s] = botFactory.Create(LevelOf(_difficulty));
                }

                int.TryParse(p.AvatarId, out int avatar);
                _seats[s] = new SessionSeat((Seat)s, p.Name, avatar, deck, p.IsBot, (Seat)s == LocalSeat, SeatConnection.Connected);
            }

            _bots = new BotDriver(botSeats, botFactory.Create(BotLevel.Normal), new Pcg32Random(_seed, 11), new Pcg32Random(_seed, 13), timing, botWorker);
            View = _engine.ViewFor(LocalSeat);
        }

        public event Action<MatchEvent> EventReceived;

        public event Action<PlayerView> Resynced;

        // Offline seats never change hands and there is no turn clock.
        public event Action SeatsChanged
        {
            add { }
            remove { }
        }

        public event Action StatusChanged;

        public event Action TimerChanged
        {
            add { }
            remove { }
        }

        public event Action<Seat, int> EmoteReceived;

        public MatchMode Mode => MatchMode.Offline;

        public Seat LocalSeat => Seat.South;

        public RuleSet Rules => _engine.Rules;

        public IReadOnlyList<SessionSeat> Seats => _seats;

        public PlayerView View { get; private set; }

        public SessionStatus Status => _status;

        public TurnTimerState Timer => TurnTimerState.None;

        public bool CanPause => true;

        public bool IsPaused => _status == SessionStatus.Paused;

        /// <summary>True when this session continued a saved match instead of dealing a new one.</summary>
        public bool IsResumed => _resumed;

        /// <summary>
        /// How strong the bots of this match are: the launch request's choice, or for a resumed
        /// match the one it was started with. "Play Again" starts the next match with it (GDD §11
        /// screen 10), whatever Settings remembers from the offline setup screen.
        /// </summary>
        public BotDifficulty Difficulty => _difficulty;

        public string MatchId => _engine.MatchId;

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            SetStatus(SessionStatus.Running);
            if (_resumed)
            {
                View = _engine.ViewFor(LocalSeat);
                Resynced?.Invoke(View);
                return;
            }

            // A new match replaces whatever was left unfinished (GDD §11 screen 06).
            _store.Clear();
            _engine.Start();
            Flush();
        }

        public void Tick(double now)
        {
            if (!_started || _ended)
            {
                _now = now;
                return;
            }

            // Time keeps being observed while paused, so Resume knows how long the pause lasted.
            _now = now;
            if (_status == SessionStatus.Paused)
            {
                return;
            }

            // Several bot moves can fall due in one frame after a long pause in the table's animations.
            for (int guard = 0; guard < 8; guard++)
            {
                if (!_bots.TryAct(_engine, now, out CommandResult result))
                {
                    break;
                }

                if (!result.IsAccepted)
                {
                    break;
                }

                Flush();
            }
        }

        public Task<CommandResult> PlaceCallAsync(int call, CancellationToken ct)
        {
            if (!CanAct())
            {
                return Task.FromResult(CommandResult.Rejected(RejectReason.WrongPhase));
            }

            CommandResult result = _engine.Execute(new PlaceCall(LocalSeat, call, _engine.CurrentTurn));
            Flush();
            return Task.FromResult(result);
        }

        public Task<CommandResult> PlayCardAsync(Card card, CancellationToken ct)
        {
            if (!CanAct())
            {
                return Task.FromResult(CommandResult.Rejected(RejectReason.WrongPhase));
            }

            CommandResult result = _engine.Execute(new PlayCard(LocalSeat, card, _engine.CurrentTurn));
            Flush();
            return Task.FromResult(result);
        }

        public Task ReadyNextAsync(CancellationToken ct)
        {
            if (CanAct())
            {
                _engine.Execute(new ReadyNext(LocalSeat));
                Flush();
            }

            return Task.CompletedTask;
        }

        public Task SendEmoteAsync(int emoteId, CancellationToken ct)
        {
            EmoteReceived?.Invoke(LocalSeat, emoteId);
            return Task.CompletedTask;
        }

        public void Pause()
        {
            if (_status != SessionStatus.Running)
            {
                return;
            }

            _pausedAt = _now;
            SetStatus(SessionStatus.Paused);
            SaveProgress();
        }

        /// <summary>Resumes play; bot deadlines move on by however long the match was paused.</summary>
        public void Resume()
        {
            if (_status != SessionStatus.Paused)
            {
                return;
            }

            _bots.Shift(_now - _pausedAt);
            SetStatus(SessionStatus.Running);
        }

        public Task LeaveAsync(CancellationToken ct)
        {
            // Offline, leaving keeps the match for "Resume" on the main menu (GDD §11 screen 06).
            if (!_ended)
            {
                SaveProgress();
            }

            SetStatus(SessionStatus.Ended);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        private bool CanAct() => _started && !_ended && _status == SessionStatus.Running;

        /// <summary>Delivers everything the engine emitted that the local seat may see, in order.</summary>
        private void Flush()
        {
            _outbox.Clear();
            _engine.DrainEvents(_outbox);
            View = _engine.ViewFor(LocalSeat);
            bool trickEnded = false;
            foreach (MatchEvent e in _outbox)
            {
                _bots.Observe(e, _now);
                if (e is TrickCompleted)
                {
                    trickEnded = true;
                }

                if (e is MatchEnded ended)
                {
                    FinishMatch(ended);
                }

                if (e.Visibility.IsVisibleTo(LocalSeat))
                {
                    EventReceived?.Invoke(e);
                }
            }

            if (trickEnded && !_ended)
            {
                SaveProgress();
            }
        }

        private void FinishMatch(MatchEnded ended)
        {
            _ended = true;
            foreach (Standing standing in ended.Standings)
            {
                if (standing.Seat == LocalSeat)
                {
                    _stats.RecordFinished(_engine.MatchId, standing, View.Scores);
                }
            }

            _settings.IncrementCompletedMatches();
            _store.Clear();
            SetStatus(SessionStatus.Ended);
        }

        private void SaveProgress()
        {
            if (!_started || _ended)
            {
                return;
            }

            _store.Save(new OfflineMatchSave
            {
                match = _engine.Snapshot(),
                difficulty = (int)_difficulty,
                seed = _seed,
                rngState = _rng.State,
                rngIncrement = _rng.Increment,
            });
        }

        private void SetStatus(SessionStatus status)
        {
            if (_status == status)
            {
                return;
            }

            _status = status;
            StatusChanged?.Invoke();
        }

        private static BotLevel LevelOf(BotDifficulty difficulty) => difficulty switch
        {
            BotDifficulty.Easy => BotLevel.Easy,
            BotDifficulty.Hard => BotLevel.Hard,
            _ => BotLevel.Normal,
        };
    }
}
