namespace Callbreak.Infrastructure.Ads
{
    /// <summary>
    /// The owner's AdMob identifiers (public: they ship inside the app) and Google's test ad units.
    /// Development builds always show Google's test ads, because clicks on one's own live ads can get
    /// an AdMob account suspended. Only the rewarded unit is used: the owner's banner and
    /// interstitial units are shown nowhere.
    /// </summary>
    public static class AdMobIds
    {
        /// <summary>The AdMob app (Android). The plugin writes it into the Android manifest when it builds (see Callbreak.Editor.AdMobSetup).</summary>
        public const string AndroidAppId = "ca-app-pub-1025159441369051~3176202879";

        /// <summary>The Earn Coins rewarded ad unit (Android), for release builds.</summary>
        public const string AndroidRewardedAdUnit = "ca-app-pub-1025159441369051/8532471334";

        /// <summary>Google's sample rewarded unit for Android, which always serves test ads (developers.google.com/admob/unity/test-ads).</summary>
        public const string AndroidTestRewardedAdUnit = "ca-app-pub-3940256099942544/5224354917";

        /// <summary>Google's sample rewarded unit for iOS, which always serves test ads.</summary>
        public const string IosTestRewardedAdUnit = "ca-app-pub-3940256099942544/1712485313";

        /// <summary>
        /// The rewarded unit of a build: Google's test unit in a development build, the owner's in a
        /// release build; null for an iOS release build, as the owner has no iOS AdMob app yet.
        /// </summary>
        public static string RewardedAdUnit(bool ios, bool developmentBuild)
        {
            if (developmentBuild)
            {
                return ios ? IosTestRewardedAdUnit : AndroidTestRewardedAdUnit;
            }

            return ios ? null : AndroidRewardedAdUnit;
        }
    }
}
