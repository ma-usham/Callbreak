using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// <see cref="IBannerAds"/> over a real ad network (<see cref="IBannerAdNetwork"/>; AdMob on
    /// phones), for one screen: the Table scene and the Menu scene each make one and dispose it, which
    /// releases the banner. It gets the network ready at once (the player's consent, then the SDK) and loads one banner, off
    /// the screen; the network refreshes what the banner shows by itself. A start that may not request
    /// ads yet, or a load that brings no banner, is tried again after <see cref="FirstRetryDelay"/>,
    /// each wait twice the last, up to <see cref="MaxRetryDelay"/>, as the full-screen ads are.
    /// <para>
    /// The banner shows only in the slot the screen gives (<see cref="ShowIn"/>): on its top or bottom
    /// edge, centred on the slot's middle line as far as the slot allows (a line on the slot's edge
    /// puts it in that corner). One that doesn't fit its
    /// slot stays off the screen rather than cover the game. <see cref="Hide"/> takes it off and keeps
    /// it loaded. While No Ads isn't owned a banner <see cref="MayShow"/>, so the table makes room.
    /// </para>
    /// <para>
    /// A player who owns No Ads (<see cref="INoAds"/>) gets nothing requested, not even consent and
    /// the SDK, and sees nothing: buying it releases the banner and stops loading, a refund loads one
    /// again. A No Ads that can't be asked counts as owned, so a paying player never sees an ad.
    /// </para>
    /// Nothing here throws into the game. Every call, and every callback of the network and of No
    /// Ads, comes on the main thread.
    /// </summary>
    public sealed class BannerAds : IBannerAds, IDisposable
    {
        /// <summary>The wait before the first retry; each retry after it waits twice as long.</summary>
        public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(5);

        /// <summary>The longest wait between two tries.</summary>
        public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(2);

        private readonly IBannerAdNetwork _network;
        private readonly INoAds _noAds;
        private readonly Action<string> _log;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private IBannerAd _banner;
        private CancellationTokenSource _retry;
        private BannerSlot? _slot;
        private BannerSlot? _placed;
        private bool _showing;
        private bool _ready;
        private bool _loading;
        private bool _disposed;

        /// <param name="network">The banner network: AdMob on phones.</param>
        /// <param name="noAds">The purchase that removes the banner: while it is owned, nothing is requested.</param>
        /// <param name="log">Where the banner's progress goes, a line each (no player data); nowhere when null.</param>
        /// <param name="delay">The retry waits; <see cref="Task.Delay(TimeSpan, CancellationToken)"/> when null.</param>
        public BannerAds(
            IBannerAdNetwork network,
            INoAds noAds,
            Action<string> log = null,
            Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _noAds = noAds ?? throw new ArgumentNullException(nameof(noAds));
            _log = log ?? (_ => { });
            _delay = delay ?? Task.Delay;
            _noAds.Changed += OnNoAdsChanged;
            Load();
        }

        public bool IsShowing => _showing;

        public bool MayShow => !_disposed && Wanted;

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
                    _log("Banner ads: No Ads could not be checked, so none are requested: " + e.Message);
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

        public void ShowIn(BannerSlot slot)
        {
            if (_disposed)
            {
                return;
            }

            _slot = slot;
            Apply();
        }

        public void Hide()
        {
            if (_disposed)
            {
                return;
            }

            _slot = null;
            Apply();
        }

        /// <summary>Stops loading, stops listening to No Ads and releases the banner.</summary>
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
                _log("Banner ads: No Ads could not be let go: " + e.Message);
            }

            _lifetime.Cancel();
            Release();
        }

        /// <summary>No Ads was bought (the banner goes and loading stops) or refunded (loading starts again).</summary>
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

            if (_banner != null)
            {
                _log("Banner ads: No Ads is owned; the banner is taken away.");
            }

            Release();
            _retry?.Cancel();
        }

        /// <summary>
        /// Puts the banner where the table wants it: in its slot when it is loaded and fits there,
        /// otherwise off the screen. Places it only when that changed, as the table asks every frame.
        /// </summary>
        private void Apply()
        {
            if (!Wanted)
            {
                Release();
                return;
            }

            if (_banner == null)
            {
                return;
            }

            if (!_slot.HasValue)
            {
                TakeOff();
                return;
            }

            BannerSlot slot = _slot.Value;
            if (_placed.HasValue && _placed.Value.Equals(slot))
            {
                return;
            }

            _placed = slot;
            int width;
            int height;
            try
            {
                width = _banner.Width;
                height = _banner.Height;
            }
            catch (Exception e)
            {
                _log("Banner ads: the banner's size could not be read: " + e.Message);
                TakeOff();
                return;
            }

            if (width > slot.Width || height > slot.Height)
            {
                _log("Banner ads: the banner (" + width + "x" + height + ") does not fit its slot (" + slot + "); it stays off the screen.");
                TakeOff();
                return;
            }

            // On the middle line, but never past either end of the slot (the Home button, the round line),
            // so a line on an edge puts it in that corner; on the slot's top or bottom edge.
            int x = Math.Max(slot.X, Math.Min(slot.CentreX - (width / 2), slot.X + slot.Width - width));
            int y = slot.AtBottom ? slot.Y + slot.Height - height : slot.Y;
            try
            {
                _banner.ShowAt(x, y);
                _showing = true;
            }
            catch (Exception e)
            {
                _log("Banner ads: the banner could not be shown: " + e.Message);
                _showing = false;
            }
        }

        /// <summary>Takes the banner off the screen, keeping it loaded.</summary>
        private void TakeOff()
        {
            if (!_slot.HasValue)
            {
                _placed = null;
            }

            if (!_showing)
            {
                return;
            }

            _showing = false;
            try
            {
                _banner.Hide();
            }
            catch (Exception e)
            {
                _log("Banner ads: the banner could not be hidden: " + e.Message);
            }
        }

        /// <summary>Releases the banner, if any.</summary>
        private void Release()
        {
            IBannerAd banner = _banner;
            _banner = null;
            _showing = false;
            _placed = null;
            Destroy(banner);
        }

        /// <summary>Starts loading a banner, unless one is loaded or loading (or waiting to try again), or No Ads is owned.</summary>
        private void Load()
        {
            if (_banner != null || _loading || _disposed || !Wanted)
            {
                return;
            }

            _loading = true;
            _ = LoadUntilReadyAsync();
        }

        /// <summary>
        /// Gets the network ready, then loads one banner, waiting longer after each failure; stops as
        /// soon as No Ads is owned, and releases a banner that comes after that.
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

                    IBannerAd banner = _ready && !_disposed && Wanted ? await _network.LoadAsync() : null;
                    if (_disposed || !Wanted)
                    {
                        Destroy(banner);
                        return;
                    }

                    if (banner != null)
                    {
                        _banner = banner;
                        _log("Banner ads: a banner is ready.");
                        Apply();
                        return;
                    }

                    failures++;
                    TimeSpan wait = RetryDelay(failures);
                    _log(_ready
                        ? "Banner ads: no banner came; trying again in " + wait.TotalSeconds.ToString("0") + " s."
                        : "Banner ads: ads may not be requested yet; trying again in " + wait.TotalSeconds.ToString("0") + " s.");
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
                // A broken network: the table goes on without a banner.
                _log("Banner ads: loading stopped: " + e.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private void Destroy(IBannerAd banner)
        {
            if (banner == null)
            {
                return;
            }

            try
            {
                banner.Destroy();
            }
            catch (Exception e)
            {
                _log("Banner ads: the banner could not be released: " + e.Message);
            }
        }
    }
}
