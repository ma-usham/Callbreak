#if UNITY_EDITOR
using System;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// What a game played in the Editor takes from a phone (the owner, 2 Nov 2026; ADR 073), each the
    /// Editor's own setting, on until it is turned off (Callbreak &gt; Editor Play), read as the game
    /// starts: Google AdMob with Google's test units, whose banner, interstitial, rewarded and app open
    /// ads the plugin draws as its own placeholders; and the festival offers from Firebase Remote
    /// Config. The PlayMode tests boot with neither unless they ask (<see cref="While"/>). Builds never
    /// read these: a release build has the owner's ad units, a development build Google's test units.
    /// </summary>
    public static class EditorPhoneServices
    {
        public const string AdMobTestAdsKey = "Callbreak.EditorAdMobTestAds";
        public const string FestivalOffersKey = "Callbreak.EditorFestivalOffers";

        public static bool AdMobTestAds
        {
            get => UnityEditor.EditorPrefs.GetBool(AdMobTestAdsKey, true);
            set => UnityEditor.EditorPrefs.SetBool(AdMobTestAdsKey, value);
        }

        public static bool FestivalOffers
        {
            get => UnityEditor.EditorPrefs.GetBool(FestivalOffersKey, true);
            set => UnityEditor.EditorPrefs.SetBool(FestivalOffersKey, value);
        }

        /// <summary>Sets both until the returned handle is disposed, then puts them back as they were.</summary>
        public static IDisposable While(bool on)
        {
            var restore = new Restore(AdMobTestAds, FestivalOffers);
            AdMobTestAds = on;
            FestivalOffers = on;
            return restore;
        }

        private sealed class Restore : IDisposable
        {
            private readonly bool _adMobTestAds;
            private readonly bool _festivalOffers;
            private bool _done;

            public Restore(bool adMobTestAds, bool festivalOffers)
            {
                _adMobTestAds = adMobTestAds;
                _festivalOffers = festivalOffers;
            }

            public void Dispose()
            {
                if (_done)
                {
                    return;
                }

                _done = true;
                AdMobTestAds = _adMobTestAds;
                FestivalOffers = _festivalOffers;
            }
        }
    }
}
#endif
