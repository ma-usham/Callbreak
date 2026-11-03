using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;

namespace Callbreak.Application.Reviews
{
    /// <summary>
    /// The rule for Google Play's review card (the owner, 3 Nov 2026: "lets implement the google plays
    /// rate review in our game"; ADR 080). An ask is due after a win: the last match recorded in this
    /// launch was won (first place, not left early), and it is at least the player's
    /// <see cref="MinMatchesPlayed"/>th match. Each win gives one chance. At most one ask every
    /// <see cref="MinInterval"/>, and <see cref="MaxAsks"/> in all, remembered across launches
    /// (review-prompt.json). Google's policy forbids asking the player's opinion first or rewarding a
    /// rating, so the game asks nothing itself: the store's card is the whole prompt, and the store
    /// decides whether it shows.
    /// </summary>
    public sealed class ReviewPrompt : IReviewPrompt, IDisposable
    {
        public const string SaveKey = "review-prompt";

        /// <summary>The fewest finished or left matches, the won one included, before the first ask.</summary>
        public const int MinMatchesPlayed = 3;

        /// <summary>The most asks ever.</summary>
        public const int MaxAsks = 3;

        /// <summary>The least time from one ask to the next.</summary>
        public static readonly TimeSpan MinInterval = TimeSpan.FromDays(30);

        private readonly ISaveStore _saves;
        private readonly IStatsService _stats;
        private readonly IStoreReview _store;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<string> _log;
        private readonly ReviewPromptData _data;
        private readonly bool _readOnly;

        private int _matchesSeen;
        private bool _justWon;
        private bool _disposed;

        public ReviewPrompt(ISaveStore saves, IStatsService stats, IStoreReview store, Func<DateTime> utcNow, Action<string> log = null)
        {
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _log = log ?? (_ => { });
            ReviewPromptData loaded = VersionedSave.Load<ReviewPromptData>(saves, SaveKey, ReviewPromptData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new ReviewPromptData();
            _data.schemaVersion = ReviewPromptData.CurrentVersion;

            // Matches recorded before this launch give no chance: the win must be this launch's.
            _matchesSeen = _stats.Current.gamesPlayed;
            _stats.Changed += OnStatsChanged;
        }

        public bool IsDue
        {
            get
            {
                if (!_justWon || _readOnly || _data.asks >= MaxAsks || _stats.Current.gamesPlayed < MinMatchesPlayed)
                {
                    return false;
                }

                return _data.lastAskUtcTicks == 0
                    || _utcNow() - new DateTime(_data.lastAskUtcTicks, DateTimeKind.Utc) >= MinInterval;
            }
        }

        public async Task<bool> AskIfDueAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!IsDue)
            {
                return false;
            }

            _justWon = false;
            _data.asks++;
            _data.lastAskUtcTicks = _utcNow().Ticks;
            try
            {
                _saves.Save(SaveKey, _data);
            }
            catch (Exception e)
            {
                // The ask still counts in memory for this launch.
                _log("Review: the ask could not be saved: " + e.Message);
            }

            _log("Review: asking the store for its review card (ask " + _data.asks + " of " + MaxAsks + ").");
            await _store.RequestAsync(ct);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stats.Changed -= OnStatsChanged;
        }

        private void OnStatsChanged()
        {
            PlayerStats current = _stats.Current;
            if (current.gamesPlayed == _matchesSeen)
            {
                return;
            }

            // A new match was recorded: a win (the streak runs) gives a chance, anything else takes it away.
            _matchesSeen = current.gamesPlayed;
            _justWon = current.winStreak > 0;
        }
    }
}
