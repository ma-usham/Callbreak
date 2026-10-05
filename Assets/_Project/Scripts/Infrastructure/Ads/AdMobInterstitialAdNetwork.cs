using System;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Callbreak.Infrastructure.Ads
{
    /// <summary>
    /// Google AdMob behind <see cref="IInterstitialAdNetwork"/>, through the Google Mobile Ads Unity
    /// plugin (com.google.ads.mobile 11.5.0): the full-screen ads between matches, of one ad unit
    /// (<see cref="AdMobIds"/>; Google's test unit in development builds).
    /// <para>
    /// AdMob starts once for both kinds of ad: getting ready is the rewarded ads' start
    /// (<see cref="AdMobRewardedAdNetwork.StartAsync"/>, given in), which gathers the player's consent
    /// through Google's User Messaging Platform and then starts the SDK, so the consent form never
    /// shows twice. An ad is requested only while <see cref="ConsentInformation.CanRequestAds"/>.
    /// </para>
    /// <para>
    /// No game sound plays over an ad: on Android Unity pauses while a full-screen ad shows, and on
    /// iOS <see cref="MobileAds.SetiOSAppPauseOnBackground"/> makes it pause too.
    /// </para>
    /// The plugin calls back on other threads, so every callback is handed to the main thread through
    /// the plugin's executor, as the rewarded ads' are. Nothing here throws into the game: an SDK error
    /// is logged and becomes a false, a null or the failed callback.
    /// </summary>
    public sealed class AdMobInterstitialAdNetwork : IInterstitialAdNetwork
    {
        private readonly string _adUnitId;
        private readonly Func<Task<bool>> _start;

        /// <param name="adUnitId">The interstitial ad unit; null or empty where there is none, and then no ad is ever requested.</param>
        /// <param name="start">Gets AdMob ready, consent first, then the SDK: the rewarded ads' start, shared so that both run once.</param>
        public AdMobInterstitialAdNetwork(string adUnitId, Func<Task<bool>> start)
        {
            _adUnitId = adUnitId;
            _start = start ?? throw new ArgumentNullException(nameof(start));
        }

        public async Task<bool> StartAsync()
        {
            if (string.IsNullOrEmpty(_adUnitId))
            {
                Debug.LogWarning(AdMobRewardedAdNetwork.LogPrefix + "No interstitial ad unit on this platform, so no ads between matches.");
                return false;
            }

            try
            {
                if (!await _start())
                {
                    return false;
                }

                // As on Android, the game pauses on iOS while a full-screen ad shows: no sound over it.
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

                InterstitialAd.Load(_adUnitId, new AdRequest(), (ad, error) => AdMobRewardedAdNetwork.OnMainThread(
                    () =>
                    {
                        if (ad == null || error != null)
                        {
                            Debug.Log(AdMobRewardedAdNetwork.LogPrefix + "No interstitial ad: " + Describe(error));
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

        /// <summary>One loaded AdMob interstitial ad.</summary>
        private sealed class LoadedAd : IInterstitialAd
        {
            private readonly InterstitialAd _ad;
            private bool _shown;

            public LoadedAd(InterstitialAd ad)
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
