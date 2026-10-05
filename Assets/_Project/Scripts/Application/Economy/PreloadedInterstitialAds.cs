using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// <see cref="IInterstitialAds"/> over a real ad network (<see cref="IInterstitialAdNetwork"/>;
    /// AdMob on phones), kept as <see cref="PreloadedRewardedAds"/> keeps the Earn Coins ads. It gets
    /// the network ready at once (the player's consent, then the SDK) and keeps one ad loaded, so an
    /// ad can show the moment a match's results are left; after each show it loads the next. A start
    /// that may not request ads yet, or a load that brings no ad, is tried again after
    /// <see cref="FirstRetryDelay"/>, each wait twice the last, up to <see cref="MaxRetryDelay"/>. A
    /// loaded ad the network can no longer show, or one older than <see cref="MaxAdAge"/> (AdMob's
    /// ads expire after an hour), is replaced with a fresh one.
    /// <para>
    /// A player who owns No Ads (<see cref="INoAds"/>) gets nothing requested, not even consent and
    /// the SDK: buying it lets the loaded ad go and stops loading, a refund starts loading again. A No
    /// Ads that can't be asked counts as owned, so a paying player never sees an ad.
    /// </para>
    /// <para>
    /// <see cref="ShowAsync"/> completes once the ad has gone away: true when it was shown; false
    /// when no ad is ready, it can't be shown, or another ad is showing. It never throws, but for the
    /// <see cref="OperationCanceledException"/> of a cancelled token, as the port says.
    /// </para>
    /// Every call, and every callback of the network and of No Ads, comes on the main thread.
    /// </summary>
    public sealed class PreloadedInterstitialAds : IInterstitialAds, IDisposable
    {
        /// <summary>The wait before the first retry; each retry after it waits twice as long.</summary>
        public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(5);

        /// <summary>The longest wait between two tries.</summary>
        public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(2);

        /// <summary>A loaded ad older than this is replaced (AdMob's ads expire after an hour).</summary>
        public static readonly TimeSpan MaxAdAge = TimeSpan.FromMinutes(55);

        private readonly IInterstitialAdNetwork _network;
        private readonly INoAds _noAds;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<string> _log;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private IInterstitialAd _ad;
        private CancellationTokenSource _retry;
        private DateTime _loadedAtUtc;
        private bool _ready;
        private bool _loading;
        private bool _showing;
        private bool _disposed;

        /// <param name="noAds">The purchase that removes these ads: while it is owned, nothing is requested.</param>
        /// <param name="utcNow">The clock that ages a loaded ad.</param>
        /// <param name="log">Where the ads' progress goes, a line each (no player data); nowhere when null.</param>
        /// <param name="delay">The retry waits; <see cref="Task.Delay(TimeSpan, CancellationToken)"/> when null.</param>
        public PreloadedInterstitialAds(
            IInterstitialAdNetwork network,
            INoAds noAds,
            Func<DateTime> utcNow,
            Action<string> log = null,
            Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _noAds = noAds ?? throw new ArgumentNullException(nameof(noAds));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _log = log ?? (_ => { });
            _delay = delay ?? Task.Delay;
            _noAds.Changed += OnNoAdsChanged;
            Load();
        }

        /// <summary>An ad is loaded and fresh. Asking replaces an expired ad, and restarts loading if it stopped.</summary>
        public bool IsAvailable
        {
            get
            {
                EnsureFreshAd();
                return _ad != null && !_showing;
            }
        }

        /// <summary>Ads may be requested: the player doesn't own No Ads.</summary>
        private bool Wanted
        {
            get
            {
                try
                {
                    return !_noAds.Owned;
                }
                catch (Exception e)
                {
                    _log("Interstitial ads: No Ads could not be checked, so none are requested: " + e.Message);
                    return false;
                }
            }
        }

        /// <summary>The wait after <paramref name="failures"/> failed tries in a row: 5 s, 10 s, 20 s ... at most two minutes.</summary>
        public static TimeSpan RetryDelay(int failures)
        {
            int doublings = Math.Max(0, Math.Min(failures - 1, 16));
            double seconds = FirstRetryDelay.TotalSeconds * Math.Pow(2, doublings);
            return TimeSpan.FromSeconds(Math.Min(seconds, MaxRetryDelay.TotalSeconds));
        }

        public async Task<bool> ShowAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            EnsureFreshAd();
            if (_ad == null || _showing || _disposed)
            {
                return false;
            }

            IInterstitialAd ad = _ad;
            _ad = null;
            _showing = true;
            var done = new TaskCompletionSource<bool>();
            try
            {
                try
                {
                    ad.Show(
                        () => done.TrySetResult(true),
                        reason =>
                        {
                            if (done.TrySetResult(false))
                            {
                                _log("Interstitial ads: the ad could not be shown: " + reason);
                            }
                        });
                }
                catch (Exception e)
                {
                    _log("Interstitial ads: the ad could not be shown: " + e.Message);
                    done.TrySetResult(false);
                }

                using (ct.Register(() => done.TrySetCanceled()))
                {
                    return await done.Task;
                }
            }
            finally
            {
                _showing = false;
                Release(ad);
                Load();
            }
        }

        /// <summary>Stops loading, stops listening to No Ads and releases the loaded ad.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _noAds.Changed -= OnNoAdsChanged;
            }
            catch (Exception e)
            {
                _log("Interstitial ads: No Ads could not be let go: " + e.Message);
            }

            _lifetime.Cancel();
            Release(_ad);
            _ad = null;
        }

        /// <summary>No Ads was bought (the loaded ad goes and loading stops) or refunded (loading starts again).</summary>
        private void OnNoAdsChanged()
        {
            if (_disposed)
            {
                return;
            }

            if (Wanted)
            {
                Load();
                return;
            }

            DropForNoAds();
            _retry?.Cancel();
        }

        /// <summary>Lets the loaded ad go while No Ads is owned.</summary>
        private void DropForNoAds()
        {
            if (_ad == null)
            {
                return;
            }

            _log("Interstitial ads: No Ads is owned; the loaded ad is let go.");
            Release(_ad);
            _ad = null;
        }

        /// <summary>
        /// Replaces a loaded ad that has expired, and starts loading when none is loaded; lets it go
        /// while No Ads is owned.
        /// </summary>
        private void EnsureFreshAd()
        {
            if (!Wanted)
            {
                DropForNoAds();
                return;
            }

            if (_ad != null && (!CanShow(_ad) || _utcNow() - _loadedAtUtc >= MaxAdAge))
            {
                _log("Interstitial ads: the loaded ad expired; loading a fresh one.");
                Release(_ad);
                _ad = null;
            }

            Load();
        }

        /// <summary>
        /// Starts loading an ad, unless one is loaded, loading (or waiting to try again) or showing,
        /// or No Ads is owned.
        /// </summary>
        private void Load()
        {
            if (_ad != null || _loading || _showing || _disposed || !Wanted)
            {
                return;
            }

            _loading = true;
            _ = LoadUntilReadyAsync();
        }

        /// <summary>
        /// Gets the network ready, then loads one ad, waiting longer after each failure; stops as soon
        /// as No Ads is owned, and lets go an ad that comes after that.
        /// </summary>
        private async Task LoadUntilReadyAsync()
        {
            int failures = 0;
            try
            {
                while (!_disposed && Wanted)
                {
                    if (!_ready)
                    {
                        _ready = await _network.StartAsync();
                    }

                    IInterstitialAd ad = _ready && !_disposed && Wanted ? await _network.LoadAsync() : null;
                    if (_disposed || !Wanted)
                    {
                        Release(ad);
                        return;
                    }

                    if (ad != null)
                    {
                        _ad = ad;
                        _loadedAtUtc = _utcNow();
                        _log("Interstitial ads: an ad is ready.");
                        return;
                    }

                    failures++;
                    TimeSpan wait = RetryDelay(failures);
                    _log(_ready
                        ? "Interstitial ads: no ad came; trying again in " + wait.TotalSeconds.ToString("0") + " s."
                        : "Interstitial ads: ads may not be requested yet; trying again in " + wait.TotalSeconds.ToString("0") + " s.");
                    using (var retry = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
                    {
                        _retry = retry;
                        try
                        {
                            await _delay(wait, retry.Token);
                        }
                        finally
                        {
                            _retry = null;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Disposed, or No Ads bought, while waiting.
            }
            catch (Exception e)
            {
                // A broken network: the next IsAvailable or ShowAsync starts over.
                _log("Interstitial ads: loading stopped: " + e.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private bool CanShow(IInterstitialAd ad)
        {
            try
            {
                return ad.CanShow;
            }
            catch (Exception e)
            {
                _log("Interstitial ads: the ad could not be checked: " + e.Message);
                return false;
            }
        }

        private void Release(IInterstitialAd ad)
        {
            if (ad == null)
            {
                return;
            }

            try
            {
                ad.Destroy();
            }
            catch (Exception e)
            {
                _log("Interstitial ads: the ad could not be released: " + e.Message);
            }
        }
    }
}
