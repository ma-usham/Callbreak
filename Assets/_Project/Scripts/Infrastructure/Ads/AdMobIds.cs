namespace Callbreak.Infrastructure.Ads
{
    /// <summary>
    /// The owner's AdMob identifiers (public: they ship inside the app) and Google's test ad units.
    /// Development builds always show Google's test ads, because clicks on one's own live ads can get
    /// an AdMob account suspended. The rewarded unit pays Earn Coins, the interstitial unit shows
    /// between matches and the banner unit shows on the table, left of the Scores button.
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

        /// <summary>The interstitial ad unit between matches (Android), for release builds.</summary>
        public const string AndroidInterstitialAdUnit = "ca-app-pub-1025159441369051/4319920270";

        /// <summary>Google's sample interstitial unit for Android, which always serves test ads (developers.google.com/admob/unity/test-ads).</summary>
        public const string AndroidTestInterstitialAdUnit = "ca-app-pub-3940256099942544/1033173712";

        /// <summary>Google's sample interstitial unit for iOS, which always serves test ads.</summary>
        public const string IosTestInterstitialAdUnit = "ca-app-pub-3940256099942544/4411468910";

        /// <summary>The banner ad unit on the table (Android), for release builds.</summary>
        public const string AndroidBannerAdUnit = "ca-app-pub-1025159441369051/6484892497";

        /// <summary>Google's sample fixed-size banner unit for Android, which always serves test ads (developers.google.com/admob/unity/test-ads).</summary>
        public const string AndroidTestBannerAdUnit = "ca-app-pub-3940256099942544/6300978111";

        /// <summary>Google's sample fixed-size banner unit for iOS, which always serves test ads.</summary>
        public const string IosTestBannerAdUnit = "ca-app-pub-3940256099942544/2934735716";

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

        /// <summary>
        /// The interstitial unit of a build, as <see cref="RewardedAdUnit"/> picks the rewarded one:
        /// Google's test unit in a development build, the owner's in a release build; null for an iOS
        /// release build.
        /// </summary>
        public static string InterstitialAdUnit(bool ios, bool developmentBuild)
        {
            if (developmentBuild)
            {
                return ios ? IosTestInterstitialAdUnit : AndroidTestInterstitialAdUnit;
            }

            return ios ? null : AndroidInterstitialAdUnit;
        }

        /// <summary>
        /// The banner unit of a build, as <see cref="RewardedAdUnit"/> picks the rewarded one: Google's
        /// test unit in a development build, the owner's in a release build; null for an iOS release
        /// build.
        /// </summary>
        public static string BannerAdUnit(bool ios, bool developmentBuild)
        {
            if (developmentBuild)
            {
                return ios ? IosTestBannerAdUnit : AndroidTestBannerAdUnit;
            }

            return ios ? null : AndroidBannerAdUnit;
        }
    }
}
