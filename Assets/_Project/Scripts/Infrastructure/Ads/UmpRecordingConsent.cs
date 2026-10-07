using System;
using Callbreak.Application.Recording;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Callbreak.Infrastructure.Ads
{
    /// <summary>
    /// The player's privacy choice behind <see cref="IRecordingConsent"/>, read from Google's User
    /// Messaging Platform (UMP), the consent flow the ads already run (ADR 034, 044): its
    /// <see cref="ConsentInformation.ConsentStatus"/>, and the IAB TCF v2 signals its consent form
    /// stores in the app's default SharedPreferences where the GDPR applies (IABTCF_gdprApplies,
    /// IABTCF_PurposeConsents). Both outlive the session, so the choice of an earlier session is known
    /// at launch, before the ads update it.
    /// <para>
    /// It reads again whenever <see cref="AdMobAdPrivacy"/> is refreshed, which the ads do after each
    /// update of the consent information and after each of Google's forms (the consent form, and the
    /// privacy options form behind Settings' "Privacy options"), and raises <see cref="Changed"/> when
    /// what it read differs. Main thread only. Nothing here throws into the game: what can't be read
    /// is unknown (no state, no signals), which records nothing.
    /// </para>
    /// </summary>
    public sealed class UmpRecordingConsent : IRecordingConsent, IDisposable
    {
        private const string GdprAppliesKey = "IABTCF_gdprApplies";
        private const string PurposeConsentsKey = "IABTCF_PurposeConsents";

        private readonly AdMobAdPrivacy _privacy;
        private ConsentSignals _current;

        public UmpRecordingConsent(AdMobAdPrivacy privacy)
        {
            _privacy = privacy ?? throw new ArgumentNullException(nameof(privacy));
            _current = Read();
            _privacy.Refreshed += OnRefreshed;
        }

        public event Action Changed;

        public ConsentSignals Current => _current;

        public void Dispose()
        {
            _privacy.Refreshed -= OnRefreshed;
        }

        private void OnRefreshed()
        {
            ConsentSignals read = Read();
            if (read.Equals(_current))
            {
                return;
            }

            _current = read;
            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static ConsentSignals Read()
        {
            ConsentState state = ReadState();
            bool? gdprApplies = null;
            string purposeConsents = string.Empty;
            if (UnityEngine.Application.platform == RuntimePlatform.Android)
            {
                ReadTcf(out gdprApplies, out purposeConsents);
            }

            return new ConsentSignals(state, gdprApplies, purposeConsents);
        }

        private static ConsentState ReadState()
        {
            try
            {
                switch (ConsentInformation.ConsentStatus)
                {
                    case ConsentStatus.NotRequired:
                        return ConsentState.NotRequired;
                    case ConsentStatus.Required:
                        return ConsentState.Required;
                    case ConsentStatus.Obtained:
                        return ConsentState.Obtained;
                    default:
                        return ConsentState.Unknown;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(AdMobRewardedAdNetwork.LogPrefix + "Consent status not read: " + e.Message);
                return ConsentState.Unknown;
            }
        }

        /// <summary>
        /// The TCF signals from the default SharedPreferences, where the IAB's spec has every consent
        /// platform on Android store them. A value of an unexpected type is left out, as if never written.
        /// </summary>
        private static void ReadTcf(out bool? gdprApplies, out string purposeConsents)
        {
            gdprApplies = null;
            purposeConsents = string.Empty;
            try
            {
                // Unity owns and caches the activity object: not disposed here.
                AndroidJavaObject activity = UnityEngine.Android.AndroidApplication.currentActivity;
                using (var manager = new AndroidJavaClass("android.preference.PreferenceManager"))
                using (AndroidJavaObject preferences = manager.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", activity))
                {
                    if (preferences == null)
                    {
                        return;
                    }

                    try
                    {
                        if (preferences.Call<bool>("contains", GdprAppliesKey))
                        {
                            gdprApplies = preferences.Call<int>("getInt", GdprAppliesKey, 0) == 1;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(AdMobRewardedAdNetwork.LogPrefix + GdprAppliesKey + " not read: " + e.Message);
                    }

                    try
                    {
                        purposeConsents = preferences.Call<string>("getString", PurposeConsentsKey, string.Empty) ?? string.Empty;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning(AdMobRewardedAdNetwork.LogPrefix + PurposeConsentsKey + " not read: " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(AdMobRewardedAdNetwork.LogPrefix + "TCF consent signals not read: " + e.Message);
            }
        }
    }
}
