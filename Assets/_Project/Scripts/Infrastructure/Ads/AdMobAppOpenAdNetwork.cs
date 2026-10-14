using System;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Callbreak.Infrastructure.Ads
{
    /// <summary>
    /// Google AdMob's app open ads behind <see cref="IInterstitialAdNetwork"/>, through the Google
    /// Mobile Ads Unity plugin (com.google.ads.mobile 11.5.0): the full-screen ads on the return to the
    /// game (<see cref="ReturnToGameAds"/>), of one ad unit (<see cref="AdMobIds"/>; Google's test
    /// unit in development builds). An app open ad loads, shows and closes as an interstitial does, so
    /// <see cref="PreloadedInterstitialAds"/> keeps one loaded and <see cref="QuietInterstitialAds"/>
    /// silences the game around it, as for the ads between matches (AdMobInterstitialAdNetwork).
    /// <para>
    /// AdMob starts once for every kind of ad: getting ready is the rewarded ads' start
    /// (<see cref="AdMobRewardedAdNetwork.StartAsync"/>, given in), consent first, then the SDK. An ad
    /// is requested only while <see cref="ConsentInformation.CanRequestAds"/>. Every callback is
    /// handed to the main thread, and nothing here throws into the game.
    /// </para>
    /// </summary>
    public sealed class AdMobAppOpenAdNetwork : IInterstitialAdNetwork
    {
        private readonly string _adUnitId;
        private readonly Func<Task<bool>> _start;

        /// <param name="adUnitId">The app open ad unit; null or empty where there is none, and then no ad is ever requested.</param>
        /// <param name="start">Gets AdMob ready, consent first, then the SDK: the rewarded ads' start, shared so that it runs once.</param>
        public AdMobAppOpenAdNetwork(string adUnitId, Func<Task<bool>> start)
        {
            _adUnitId = adUnitId;
            _start = start ?? throw new ArgumentNullException(nameof(start));
        }

        public async Task<bool> StartAsync()
        {
            if (string.IsNullOrEmpty(_adUnitId))
            {
                Debug.LogWarning(AdMobRewardedAdNetwork.LogPrefix + "No app open ad unit on this platform, so no ads on the return to the game.");
                return false;
            }

            try
            {
                if (!await _start())
                {
                    return false;
                }

                // The game pauses on iOS while a full-screen ad shows (Android: QuietInterstitialAds).
                MobileAds.SetiOSAppPauseOnBackground(true);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public Task<IInterstitialAd> LoadAsync()
        {
            var loaded = new TaskCompletionSource<IInterstitialAd>();
            try
            {
                if (string.IsNullOrEmpty(_adUnitId) || !ConsentInformation.CanRequestAds())
                {
                    loaded.TrySetResult(null);
                    return loaded.Task;
                }

                AppOpenAd.Load(_adUnitId, new AdRequest(), (ad, error) => AdMobRewardedAdNetwork.OnMainThread(
                    () =>
                    {
                        if (ad == null || error != null)
                        {
                            Debug.Log(AdMobRewardedAdNetwork.LogPrefix + "No app open ad: " + Describe(error));
                            ad?.Destroy();
                            loaded.TrySetResult(null);
                            return;
                        }

                        loaded.TrySetResult(new LoadedAd(ad));
                    },
                    () => loaded.TrySetResult(null)));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                loaded.TrySetResult(null);
            }

            return loaded.Task;
        }

        private static string Describe(AdError error) => error == null ? "no ad, and no error" : error.GetCode() + " " + error.GetMessage();

        /// <summary>One loaded AdMob app open ad.</summary>
        private sealed class LoadedAd : IInterstitialAd
        {
            private readonly AppOpenAd _ad;
            private bool _shown;

            public LoadedAd(AppOpenAd ad)
            {
                _ad = ad;
            }

            public bool CanShow
            {
                get
                {
                    try
                    {
                        return !_shown && _ad.CanShowAd();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        return false;
                    }
                }
            }

            public void Show(Action closed, Action<string> failed)
            {
                _shown = true;
                _ad.OnAdFullScreenContentClosed += () => AdMobRewardedAdNetwork.OnMainThread(closed);
                _ad.OnAdFullScreenContentFailed += error => AdMobRewardedAdNetwork.OnMainThread(() => failed(Describe(error)));
                _ad.Show();
            }

            public void Destroy()
            {
                try
                {
                    _ad.Destroy();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
