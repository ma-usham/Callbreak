using Callbreak.Infrastructure.Platform;
using UnityEditor;

namespace Callbreak.Editor
{
    /// <summary>
    /// Callbreak &gt; Editor Play &gt; Festival Offers (ADR 073): whether a game played in the Editor reads the
    /// festival offers from Firebase Remote Config, as a phone does. On until it is turned off; it counts
    /// from the next Play. Builds are not affected.
    /// </summary>
    public static class EditorPhoneServicesMenu
    {
        private const string OffersPath = "Callbreak/Editor Play/Festival Offers";

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
