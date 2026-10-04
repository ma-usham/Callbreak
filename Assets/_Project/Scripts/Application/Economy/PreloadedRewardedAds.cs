using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// <see cref="IRewardedAds"/> over a real ad network (<see cref="IRewardedAdNetwork"/>; AdMob on
    /// phones). It gets the network ready at once (the player's consent, then the SDK) and keeps one
    /// ad loaded, so the Earn Coins page can show it at a tap; after each show it loads the next. A
    /// start that may not request ads yet, or a load that brings no ad, is tried again after
    /// <see cref="FirstRetryDelay"/>, each wait twice the last, up to <see cref="MaxRetryDelay"/>.
    /// A loaded ad the network can no longer show, or one older than <see cref="MaxAdAge"/> (AdMob's
    /// ads expire after an hour), is replaced with a fresh one.
    /// <para>
    /// <see cref="ShowAsync"/> completes once the ad has gone away: true only when the player earned
    /// the reward (a reward the network reports just after the close, within
    /// <see cref="LateRewardGrace"/>, still counts); false when no ad is ready, it can't be shown, the
    /// player closed it early, or another ad is showing. It never throws, but for the
    /// <see cref="OperationCanceledException"/> of a cancelled token, as the port says; the ad then
    /// pays nothing.
    /// </para>
    /// Every call, and every callback of the network, comes on the main thread.
    /// </summary>
    public sealed class PreloadedRewardedAds : IRewardedAds, IDisposable
    {
        /// <summary>The wait before the first retry; each retry after it waits twice as long.</summary>
        public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(5);

        /// <summary>The longest wait between two tries.</summary>
        public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(2);

        /// <summary>A loaded ad older than this is replaced (AdMob's ads expire after an hour).</summary>
        public static readonly TimeSpan MaxAdAge = TimeSpan.FromMinutes(55);

        /// <summary>How long an ad closed without its reward waits for a reward the network reports late.</summary>
        public static readonly TimeSpan LateRewardGrace = TimeSpan.FromSeconds(1);

        private readonly IRewardedAdNetwork _network;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<string> _log;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private IRewardedAd _ad;
        private DateTime _loadedAtUtc;
        private bool _ready;
        private bool _loading;
        private bool _showing;
        private bool _disposed;

        /// <param name="utcNow">The clock that ages a loaded ad.</param>
        /// <param name="log">Where the ads' progress goes, a line each (no player data); nowhere when null.</param>
        /// <param name="delay">The retry and grace waits; <see cref="Task.Delay(TimeSpan, CancellationToken)"/> when null.</param>
        public PreloadedRewardedAds(
            IRewardedAdNetwork network,
            Func<DateTime> utcNow,
            Action<string> log = null,
            Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _log = log ?? (_ => { });
            _delay = delay ?? Task.Delay;
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

            IRewardedAd ad = _ad;
            _ad = null;
            _showing = true;
            var show = new AdShow(this);
            try
            {
                try
                {
                    ad.Show(show.Rewarded, show.Closed, show.Failed);
                }
                catch (Exception e)
                {
                    show.Failed(e.Message);
                }

                using (ct.Register(show.Cancel))
                {
                    return await show.Done;
                }
            }
            finally
            {
                _showing = false;
                Release(ad);
                Load();
            }
        }

        /// <summary>Stops loading and releases the loaded ad.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            Release(_ad);
            _ad = null;
        }

        /// <summary>Replaces a loaded ad that has expired, and starts loading when none is loaded.</summary>
        private void EnsureFreshAd()
        {
            if (_ad != null && (!CanShow(_ad) || _utcNow() - _loadedAtUtc >= MaxAdAge))
            {
                _log("Rewarded ads: the loaded ad expired; loading a fresh one.");
                Release(_ad);
                _ad = null;
            }

            Load();
        }

        /// <summary>Starts loading an ad, unless one is loaded, loading (or waiting to try again) or showing.</summary>
        private void Load()
        {
            if (_ad != null || _loading || _showing || _disposed)
            {
                return;
            }

            _loading = true;
            _ = LoadUntilReadyAsync();
        }

        /// <summary>Gets the network ready, then loads one ad, waiting longer after each failure.</summary>
        private async Task LoadUntilReadyAsync()
        {
            int failures = 0;
            try
            {
                while (!_disposed)
                {
                    if (!_ready)
                    {
                        _ready = await _network.StartAsync();
                    }

                    IRewardedAd ad = _ready && !_disposed ? await _network.LoadAsync() : null;
                    if (_disposed)
                    {
                        Release(ad);
                        return;
                    }

                    if (ad != null)
                    {
                        _ad = ad;
                        _loadedAtUtc = _utcNow();
                        _log("Rewarded ads: an ad is ready.");
                        return;
                    }

                    failures++;
                    TimeSpan wait = RetryDelay(failures);
                    _log(_ready
                        ? "Rewarded ads: no ad came; trying again in " + wait.TotalSeconds.ToString("0") + " s."
                        : "Rewarded ads: ads may not be requested yet; trying again in " + wait.TotalSeconds.ToString("0") + " s.");
                    await _delay(wait, _lifetime.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Disposed while waiting.
            }
            catch (Exception e)
            {
                // A broken network: the next IsAvailable or ShowAsync starts over.
                _log("Rewarded ads: loading stopped: " + e.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private bool CanShow(IRewardedAd ad)
        {
            try
            {
                return ad.CanShow;
            }
            catch (Exception e)
            {
                _log("Rewarded ads: the ad could not be checked: " + e.Message);
                return false;
            }
        }

        private void Release(IRewardedAd ad)
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
                _log("Rewarded ads: the ad could not be released: " + e.Message);
            }
        }

        /// <summary>One ad on screen: over once it has gone away (true with its reward), failed, or the wait was cancelled.</summary>
        private sealed class AdShow
        {
            private readonly PreloadedRewardedAds _owner;
            private readonly TaskCompletionSource<bool> _done = new TaskCompletionSource<bool>();
            private bool _rewarded;
            private bool _closed;

            public AdShow(PreloadedRewardedAds owner)
            {
                _owner = owner;
            }

            public Task<bool> Done => _done.Task;

            public void Rewarded()
            {
                _rewarded = true;
                if (_closed)
                {
                    _done.TrySetResult(true);
                }
            }

            public void Closed()
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                if (_rewarded)
                {
                    _done.TrySetResult(true);
                    return;
                }

                _ = AwaitLateRewardAsync();
            }

            public void Failed(string reason)
            {
                _owner._log("Rewarded ads: the ad could not be shown: " + reason);
                _done.TrySetResult(_rewarded);
            }

            /// <summary>The caller stopped waiting; may come from any thread.</summary>
            public void Cancel() => _done.TrySetCanceled();

            private async Task AwaitLateRewardAsync()
            {
                try
                {
                    await _owner._delay(LateRewardGrace, _owner._lifetime.Token);
                }
                catch (Exception)
                {
                    // Disposed, or a broken wait: settle now.
                }

                _done.TrySetResult(_rewarded);
            }
        }
    }
}
