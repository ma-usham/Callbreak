using Callbreak.Infrastructure.Platform;
using UnityEditor;

namespace Callbreak.Editor
{
    /// <summary>
    /// Callbreak &gt; Editor Play (ADR 073): whether a game played in the Editor shows Google's AdMob test
    /// ads (the plugin's placeholders for the banner, the ads between matches, the rewarded and the app
    /// open ads) and the festival offers from Firebase Remote Config, as a phone does. Both on until they
    /// are turned off; each counts from the next Play. Builds are not affected.
    /// </summary>
    public static class EditorPhoneServicesMenu
    {
        private const string AdsPath = "Callbreak/Editor Play/AdMob Test Ads";
        private const string OffersPath = "Callbreak/Editor Play/Festival Offers";

        [MenuItem(AdsPath)]
        private static void ToggleAds()
        {
            EditorPhoneServices.AdMobTestAds = !EditorPhoneServices.AdMobTestAds;
        }

        [MenuItem(AdsPath, true)]
        private static bool ToggleAdsValidate()
        {
            Menu.SetChecked(AdsPath, EditorPhoneServices.AdMobTestAds);
            return true;
        }

        [MenuItem(OffersPath)]
        private static void ToggleOffers()
        {
            EditorPhoneServices.FestivalOffers = !EditorPhoneServices.FestivalOffers;
        }

        [MenuItem(OffersPath, true)]
        private static bool ToggleOffersValidate()
        {
            Menu.SetChecked(OffersPath, EditorPhoneServices.FestivalOffers);
            return true;
        }
    }
}
