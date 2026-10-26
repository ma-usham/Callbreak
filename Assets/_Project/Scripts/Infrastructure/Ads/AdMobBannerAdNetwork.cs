using System;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Callbreak.Infrastructure.Ads
{
    /// <summary>
    /// Google AdMob behind <see cref="IBannerAdNetwork"/>, through the Google Mobile Ads Unity plugin
    /// (com.google.ads.mobile 11.5.0): the table's banner, a standard 320 x 50 banner
    /// (<see cref="AdSize.Banner"/>) of one ad unit (<see cref="AdMobIds"/>; Google's test unit in
    /// development builds).
    /// <para>
    /// AdMob starts once for every kind of ad: getting ready is the rewarded ads' start
    /// (<see cref="AdMobRewardedAdNetwork.StartAsync"/>, given in), which gathers the player's consent
    /// through Google's User Messaging Platform and then starts the SDK, so the consent form never
    /// shows twice. Once it has started, later tables don't ask again. A banner is requested only
    /// while <see cref="ConsentInformation.CanRequestAds"/>.
    /// </para>
    /// <para>
    /// The plugin draws the banner as a view of its own over the game, placed in density-independent
    /// pixels; the table's places are in screen pixels, turned into those with the device's scale
    /// (<see cref="MobileAds.Utils.GetDeviceScale"/>). A banner is made off the screen and stays off
    /// until it is placed; AdMob then refreshes what it shows by itself, and a refresh never brings a
    /// hidden banner back.
    /// </para>
    /// The plugin calls back on other threads, so every callback is handed to the main thread through
    /// the plugin's executor, as the rewarded ads' are. Nothing here throws into the game: an SDK error
    /// is logged and becomes a false or a null.
    /// </summary>
    public sealed class AdMobBannerAdNetwork : IBannerAdNetwork
    {
        private readonly string _adUnitId;
        private readonly Func<Task<bool>> _start;
        private bool _started;

        /// <param name="adUnitId">The banner ad unit; null or empty where there is none, and then no banner is ever requested.</param>
        /// <param name="start">Gets AdMob ready, consent first, then the SDK: the rewarded ads' start, shared so that both run once.</param>
        public AdMobBannerAdNetwork(string adUnitId, Func<Task<bool>> start)
        {
            _adUnitId = adUnitId;
            _start = start ?? throw new ArgumentNullException(nameof(start));
        }

        public async Task<bool> StartAsync()
        {
            if (string.IsNullOrEmpty(_adUnitId))
            {
                Debug.LogWarning(AdMobRewardedAdNetwork.LogPrefix + "No banner ad unit on this platform, so no banner on the table.");
                return false;
            }

            if (_started)
            {
                return true;
            }

            try
            {
                _started = await _start();
                return _started;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public Task<IBannerAd> LoadAsync()
        {
            var loaded = new TaskCompletionSource<IBannerAd>();
            BannerView view = null;
            try
            {
                if (string.IsNullOrEmpty(_adUnitId) || !ConsentInformation.CanRequestAds())
                {
                    loaded.TrySetResult(null);
                    return loaded.Task;
                }

                view = new BannerView(_adUnitId, AdSize.Banner, 0, 0);

                // Off the screen until the table places it.
                view.Hide();
                LoadedBanner banner = null;
                BannerView madeView = view;
                view.OnBannerAdLoaded += () => AdMobRewardedAdNetwork.OnMainThread(
                    () =>
                    {
                        if (banner == null)
                        {
                            madeView.Hide();
                            banner = new LoadedBanner(madeView);
                            loaded.TrySetResult(banner);
                            return;
                        }

                        // AdMob's own refresh: a banner the table hid stays hidden.
                        banner.KeepHiddenIfOff();
                    },
                    () => loaded.TrySetResult(null));
                view.OnBannerAdLoadFailed += error => AdMobRewardedAdNetwork.OnMainThread(
                    () =>
                    {
                        if (banner != null)
                        {
                            // A refresh that failed: the banner keeps what it showed.
                            return;
                        }

                        Debug.Log(AdMobRewardedAdNetwork.LogPrefix + "No banner: " + Describe(error));
                        Destroy(madeView);
                        loaded.TrySetResult(null);
                    },
                    () => loaded.TrySetResult(null));
                view.LoadAd(new AdRequest());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Destroy(view);
                loaded.TrySetResult(null);
            }

            return loaded.Task;
        }

        private static string Describe(AdError error) => error == null ? "no banner, and no error" : error.GetCode() + " " + error.GetMessage();

        private static void Destroy(BannerView view)
        {
            if (view == null)
            {
                return;
            }

            try
            {
                view.Destroy();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>One loaded AdMob banner.</summary>
        private sealed class LoadedBanner : IBannerAd
        {
            private readonly BannerView _view;
            private bool _onScreen;
            private bool _destroyed;

            public LoadedBanner(BannerView view)
            {
                _view = view;
            }

            public int Width => Mathf.RoundToInt(_view.GetWidthInPixels());

            public int Height => Mathf.RoundToInt(_view.GetHeightInPixels());

            public void ShowAt(int x, int y)
            {
                if (_destroyed)
                {
                    return;
                }

                // The plugin places views in density-independent pixels. Rounded down across, so the
                // banner never edges right, towards the Scores button.
                float scale = MobileAds.Utils.GetDeviceScale();
                if (scale <= 0f)
                {
                    scale = 1f;
                }

                _view.SetPosition(Mathf.FloorToInt(x / scale), Mathf.RoundToInt(y / scale));
                _view.Show();
                _onScreen = true;
            }

            public void Hide()
            {
                if (_destroyed)
                {
                    return;
                }

                _onScreen = false;
                _view.Hide();
            }

            public void Destroy()
            {
                if (_destroyed)
                {
                    return;
                }

                _destroyed = true;
                _onScreen = false;
                _view.Destroy();
            }

            /// <summary>After AdMob's own refresh: hides the banner again if the table had it off the screen.</summary>
            public void KeepHiddenIfOff()
            {
                if (_destroyed || _onScreen)
                {
                    return;
                }

                try
                {
                    _view.Hide();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
