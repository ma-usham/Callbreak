using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Profile;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// When an ad shows between matches (the owner, 5 Oct 2026). As the player leaves the results of
    /// a finished match, a full-screen ad (<see cref="IInterstitialAds"/>) shows before the next
    /// screen: before a new offline match is dealt, before a Quick or Stake Match search starts, and
    /// before the main menu. So it does when the player leaves a match before its end from the match
    /// menu (<see cref="AfterMatch.LeftEarly"/>), once the leave is done: the ad then waits
    /// <see cref="LeaveSettle"/>, so the leave (and a host's hand-over) reaches the other players
    /// before the ad pauses the game. None shows:
    /// <list type="bullet">
    /// <item>on the way back to a private room's lobby, where the friends wait;</item>
    /// <item>for a player who owns No Ads (<see cref="INoAds"/>);</item>
    /// <item>after the player's very first match: while the statistics count one match or none
    /// (<see cref="PlayerStats.gamesPlayed"/>, every match played, finished or left early, the one
    /// just finished or left included; an offline match left for later is not counted yet);</item>
    /// <item>within <see cref="MinInterval"/> of the last ad coming up, on the wall clock;</item>
    /// <item>when no ad is loaded at that moment: the game goes on without waiting for one.</item>
    /// </list>
    /// Only the match results and the match menu's Leave Match ask, so no ad shows during play, on a
    /// round's results, in matchmaking or in the lobby, nor after a match called off (the host lost),
    /// which the player didn't leave. The time of the last ad lives in memory, so each launch starts
    /// afresh; a clock found set back to before it starts the three minutes again from then.
    /// </summary>
    public sealed class BetweenMatchAds : IBetweenMatchAds
    {
        /// <summary>The least time from one ad coming up to the next: at most one every three minutes.</summary>
        public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(3);

        /// <summary>
        /// How long an ad after a match left early waits before it comes up: the leave just sent (and
        /// a host's hand-over) goes out while the game still runs, as a full-screen ad pauses it.
        /// </summary>
        public static readonly TimeSpan LeaveSettle = TimeSpan.FromSeconds(1);

        private readonly IInterstitialAds _ads;
        private readonly IStatsService _stats;
        private readonly INoAds _noAds;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<string> _log;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;

        private DateTime? _lastAdUtc;

        /// <param name="ads">The full-screen ads.</param>
        /// <param name="stats">The player's statistics, which count the matches played.</param>
        /// <param name="noAds">The purchase that removes these ads.</param>
        /// <param name="utcNow">The wall clock.</param>
        /// <param name="log">Where each decision goes, a line each (no player data); nowhere when null.</param>
        /// <param name="delay">Waits the given time (<see cref="LeaveSettle"/>); <see cref="Task.Delay(TimeSpan, CancellationToken)"/> when null.</param>
        public BetweenMatchAds(IInterstitialAds ads, IStatsService stats, INoAds noAds, Func<DateTime> utcNow, Action<string> log = null, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _noAds = noAds ?? throw new ArgumentNullException(nameof(noAds));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _log = log ?? (_ => { });
            _delay = delay ?? ((time, ct) => Task.Delay(time, ct));
        }

        public async Task<bool> ShowIfDueAsync(AfterMatch next, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            DateTime now;
            try
            {
                now = _utcNow();
                if (!IsDue(next, now))
                {
                    return false;
                }

                if (!_ads.IsAvailable)
                {
                    _log("Between-match ads: one is due, but none is loaded; going on without it.");
                    return false;
                }
            }
            catch (Exception e)
            {
                _log("Between-match ads: none, as the check failed: " + e.Message);
                return false;
            }

            // Counted from now, so an exit while it is up finds it there.
            DateTime? before = _lastAdUtc;
            _lastAdUtc = now;
            if (next == AfterMatch.LeftEarly)
            {
                try
                {
                    await _delay(LeaveSettle, ct);
                }
                catch (OperationCanceledException)
                {
                    // The caller stopped waiting before any ad came up: the three minutes don't start.
                    _lastAdUtc = before;
                    throw;
                }
                catch (Exception e)
                {
                    _log("Between-match ads: the wait before the ad failed: " + e.Message);
                }
            }

            bool shown;
            try
            {
                shown = await _ads.ShowAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // The caller stopped waiting; the ad may still be up, so it counts.
                throw;
            }
            catch (Exception e)
            {
                _log("Between-match ads: the ad failed: " + e.Message);
                shown = false;
            }

            if (!shown)
            {
                // None came up after all: the three minutes don't start.
                _lastAdUtc = before;
                _log("Between-match ads: the ad could not be shown; going on without it.");
                return false;
            }

            _log("Between-match ads: an ad was shown.");
            return true;
        }

        private bool IsDue(AfterMatch next, DateTime now)
        {
            if (next == AfterMatch.PrivateLobby)
            {
                _log("Between-match ads: none on the way back to the lobby.");
                return false;
            }

            if (_noAds.Owned)
            {
                _log("Between-match ads: none, No Ads is owned.");
                return false;
            }

            if (_stats.Current.gamesPlayed <= 1)
            {
                _log("Between-match ads: none after the player's first match.");
                return false;
            }

            if (_lastAdUtc.HasValue)
            {
                if (now < _lastAdUtc.Value)
                {
                    // The clock went back past the last ad: how long ago it was can't be told.
                    _lastAdUtc = now;
                    _log("Between-match ads: none, the clock was set back; the three minutes start again.");
                    return false;
                }

                if (now - _lastAdUtc.Value < MinInterval)
                {
                    _log("Between-match ads: none, the last one came up less than three minutes ago.");
                    return false;
                }
            }

            return true;
        }
    }
}
