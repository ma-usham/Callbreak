using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Profile;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// When an app open ad shows (the owner, 15 Oct 2026). As the player comes back to the game on
    /// the bare main menu (the caller checks that: no match, no shop, no dialog, no store sheet), a
    /// full-screen ad (<see cref="IInterstitialAds"/> over AdMob's app open unit) shows. None shows:
    /// <list type="bullet">
    /// <item>on a start: the game was never away;</item>
    /// <item>after less than <see cref="MinAway"/> away, a quick look at another app, or a time away
    /// that can't be told because the clock went back;</item>
    /// <item>for a player who owns No Ads (<see cref="INoAds"/>);</item>
    /// <item>before the player's second match: while the statistics count one match or none, as for
    /// the ads between matches;</item>
    /// <item>within <see cref="FullScreenAdPacing.MinInterval"/> of the last full-screen ad, this kind or
    /// between matches (<see cref="FullScreenAdPacing"/>), on the wall clock; a clock found set back
    /// to before it starts the three minutes again from then;</item>
    /// <item>when no ad is loaded at that moment: the game goes on without waiting for one.</item>
    /// </list>
    /// Each time away gives one chance, whether an ad came or not.
    /// </summary>
    public sealed class ReturnToGameAds : IReturnToGameAds, IDisposable
    {
        /// <summary>The least time away before an ad may greet the player back.</summary>
        public static readonly TimeSpan MinAway = TimeSpan.FromSeconds(30);

        private readonly IInterstitialAds _ads;
        private readonly IStatsService _stats;
        private readonly INoAds _noAds;
        private readonly FullScreenAdPacing _pacing;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<string> _log;

        private DateTime? _leftUtc;

        /// <param name="ads">The app open ads.</param>
        /// <param name="stats">The player's statistics, which count the matches played.</param>
        /// <param name="noAds">The purchase that removes these ads.</param>
        /// <param name="pacing">When the last full-screen ad came up, shared with the ads between matches.</param>
        /// <param name="utcNow">The wall clock.</param>
        /// <param name="log">Where each decision goes, a line each (no player data); nowhere when null.</param>
        public ReturnToGameAds(IInterstitialAds ads, IStatsService stats, INoAds noAds, FullScreenAdPacing pacing, Func<DateTime> utcNow, Action<string> log = null)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _noAds = noAds ?? throw new ArgumentNullException(nameof(noAds));
            _pacing = pacing ?? throw new ArgumentNullException(nameof(pacing));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _log = log ?? (_ => { });
        }

        public void Left(DateTime leftUtc)
        {
            _leftUtc = leftUtc;
        }

        public async Task<bool> ShowIfDueAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // One chance for each time away.
            DateTime? left = _leftUtc;
            _leftUtc = null;
            DateTime now;
            try
            {
                now = _utcNow();
                if (!IsDue(left, now))
                {
                    return false;
                }

                if (!_ads.IsAvailable)
                {
                    _log("App open ads: one is due, but none is loaded; going on without it.");
                    return false;
                }
            }
            catch (Exception e)
            {
                _log("App open ads: none, as the check failed: " + e.Message);
                return false;
            }

            // Counted from now, so an exit while it is up finds it there.
            DateTime? before = _pacing.LastAdUtc;
            _pacing.LastAdUtc = now;
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
                _log("App open ads: the ad failed: " + e.Message);
                shown = false;
            }

            if (!shown)
            {
                // None came up after all: the three minutes don't start.
                _pacing.LastAdUtc = before;
                _log("App open ads: the ad could not be shown; going on without it.");
                return false;
            }

            _log("App open ads: an ad was shown.");
            return true;
        }

        /// <summary>Disposes the ads, when they can be.</summary>
        public void Dispose() => (_ads as IDisposable)?.Dispose();

        private bool IsDue(DateTime? left, DateTime now)
        {
            if (!left.HasValue)
            {
                _log("App open ads: none, the game was not away.");
                return false;
            }

            if (now - left.Value < MinAway)
            {
                _log("App open ads: none, the game was away less than half a minute (or the clock went back).");
                return false;
            }

            if (_noAds.Owned)
            {
                _log("App open ads: none, No Ads is owned.");
                return false;
            }

            if (_stats.Current.gamesPlayed <= 1)
            {
                _log("App open ads: none before the player's second match.");
                return false;
            }

            if (_pacing.LastAdUtc.HasValue)
            {
                if (now < _pacing.LastAdUtc.Value)
                {
                    // The clock went back past the last ad: how long ago it was can't be told.
                    _pacing.LastAdUtc = now;
                    _log("App open ads: none, the clock was set back; the three minutes start again.");
                    return false;
                }

                if (now - _pacing.LastAdUtc.Value < FullScreenAdPacing.MinInterval)
                {
                    _log("App open ads: none, the last full-screen ad came up less than three minutes ago.");
                    return false;
                }
            }

            return true;
        }
    }
}
