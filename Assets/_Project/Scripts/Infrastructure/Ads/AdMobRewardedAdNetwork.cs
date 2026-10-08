using System;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Callbreak.Infrastructure.Ads
{
    /// <summary>
    /// Google AdMob behind <see cref="IRewardedAdNetwork"/>, through the Google Mobile Ads Unity
    /// plugin (com.google.ads.mobile 11.5.0): Google's consent form (the User Messaging Platform)
    /// first, then the SDK, then rewarded ads of one ad unit (<see cref="AdMobIds"/>; Google's test
    /// unit in development builds).
    /// <para>
    /// Consent follows Google's flow. Each start updates the consent information and shows the
    /// consent form when the law asks for it (the messages set up in AdMob's Privacy &amp;
    /// messaging). The SDK starts, and an ad is requested, only while
    /// <see cref="ConsentInformation.CanRequestAds"/>; a player whose consent from an earlier session
    /// still holds gets ads at once while it is checked again. After the update, and after the
    /// consent form, the privacy options (<see cref="AdMobAdPrivacy"/>) read again whether Settings
    /// must offer them.
    /// </para>
    /// The plugin calls back on other threads (on Android often only once the game resumes after the
    /// ad), so every callback is handed to the main thread through the plugin's
    /// <see cref="MobileAdsEventExecutor"/>, which the plugin recommends since its
    /// RaiseAdEventsOnUnityMainThread became obsolete. Nothing here throws into the game: an SDK
    /// error is logged and becomes a false, a null or the failed callback.
    /// </summary>
    public sealed class AdMobRewardedAdNetwork : IRewardedAdNetwork
    {
        internal const string LogPrefix = "[AdMob] ";

        private readonly string _adUnitId;
        private readonly AdMobAdPrivacy _privacy;
        private Task<bool> _initialized;
        private Task _consent;

        /// <param name="adUnitId">The rewarded ad unit; null or empty where there is none, and then no ad is ever requested.</param>
        /// <param name="privacy">The privacy options, told whenever the consent information may have changed; none when null.</param>
        public AdMobRewardedAdNetwork(string adUnitId, AdMobAdPrivacy privacy = null)
        {
            _adUnitId = adUnitId;
            _privacy = privacy;
        }

        public async Task<bool> StartAsync()
        {
            if (string.IsNullOrEmpty(_adUnitId))
            {
                Debug.LogWarning(LogPrefix + "No rewarded ad unit on this platform, so no ads.");
                return false;
            }

            try
            {
                // The callbacks below reach the main thread through the plugin's executor.
                MobileAdsEventExecutor.Initialize();
                Task consent = GatherConsent();
                if (!ConsentInformation.CanRequestAds())
                {
                    await consent;
                    if (!ConsentInformation.CanRequestAds())
                    {
                        Debug.Log(LogPrefix + "Ads may not be requested yet (consent: " + ConsentInformation.ConsentStatus + ").");
                        return false;
                    }
                }

                return await InitializeOnce();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public Task<IRewardedAd> LoadAsync()
        {
            var loaded = new TaskCompletionSource<IRewardedAd>();
            try
            {
                if (string.IsNullOrEmpty(_adUnitId) || !ConsentInformation.CanRequestAds())
                {
                    loaded.TrySetResult(null);
                    return loaded.Task;
                }

                RewardedAd.Load(_adUnitId, new AdRequest(), (ad, error) => OnMainThread(
                    () =>
                    {
                        if (ad == null || error != null)
                        {
                            Debug.Log(LogPrefix + "No rewarded ad: " + Describe(error));
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

        /// <summary>
        /// Updates the consent information, then shows the consent form if the law asks for it.
        /// Completes when that is over, whatever happened; one runs at a time.
        /// </summary>
        private Task GatherConsent()
        {
            if (_consent != null && !_consent.IsCompleted)
            {
                return _consent;
            }

            var done = new TaskCompletionSource<bool>();
            _consent = done.Task;
            try
            {
                ConsentInformation.Update(new ConsentRequestParameters(), updateError => OnMainThread(
                    () =>
                    {
                        if (updateError != null)
                        {
                            Debug.LogWarning(LogPrefix + "Consent information not updated: " + Describe(updateError));
                            done.TrySetResult(false);
                            return;
                        }

                        // From here on UMP knows whether Settings must offer the privacy options.
                        _privacy?.Refresh();
                        if (ConsentInformation.CanRequestAds())
                        {
                            done.TrySetResult(true);
                            return;
                        }

                        ConsentForm.LoadAndShowConsentFormIfRequired(formError => OnMainThread(
                            () =>
                            {
                                if (formError != null)
                                {
                                    Debug.LogWarning(LogPrefix + "Consent form not shown: " + Describe(formError));
                                }

                                _privacy?.Refresh();
                                done.TrySetResult(true);
                            },
                            () => done.TrySetResult(false)));
                    },
                    () => done.TrySetResult(false)));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                done.TrySetResult(false);
            }

            return done.Task;
        }

        /// <summary>Starts the SDK the first time; later starts wait for that one. False when it failed (tried again next start).</summary>
        private Task<bool> InitializeOnce()
        {
            if (_initialized != null)
            {
                return _initialized;
            }

            var done = new TaskCompletionSource<bool>();
            _initialized = done.Task;
            try
            {
                // The game pauses on iOS while a full-screen ad shows. Android keeps it running, so
                // QuietRewardedAds and QuietInterstitialAds silence it there.
                MobileAds.SetiOSAppPauseOnBackground(true);
                MobileAds.Initialize(status => OnMainThread(
                    () =>
                    {
                        if (status == null)
                        {
                            Debug.LogWarning(LogPrefix + "The SDK did not start.");
                            _initialized = null;
                            done.TrySetResult(false);
                            return;
                        }

                        done.TrySetResult(true);
                    },
                    () =>
                    {
                        _initialized = null;
                        done.TrySetResult(false);
                    }));
            }
            catch
            {
                // Tried again on the next start; StartAsync logs the error.
                _initialized = null;
                throw;
            }

            return done.Task;
        }

        /// <summary>Runs a plugin callback's work on the main thread; when it throws, logs it and runs <paramref name="onError"/>.</summary>
        internal static void OnMainThread(Action action, Action onError = null)
        {
            try
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        onError?.Invoke();
                    }
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static string Describe(AdError error) => error == null ? "no ad, and no error" : error.GetCode() + " " + error.GetMessage();

        internal static string Describe(FormError error) => error.ErrorCode + " " + error.Message;

        /// <summary>One loaded AdMob rewarded ad.</summary>
        private sealed class LoadedAd : IRewardedAd
        {
            private readonly RewardedAd _ad;
            private bool _shown;

            public LoadedAd(RewardedAd ad)
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

            public void Show(Action rewarded, Action closed, Action<string> failed)
            {
                _shown = true;
                _ad.OnAdFullScreenContentClosed += () => OnMainThread(closed);
                _ad.OnAdFullScreenContentFailed += error => OnMainThread(() => failed(Describe(error)));
                _ad.Show(reward => OnMainThread(rewarded));
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
