using Callbreak.Infrastructure.Platform;
using UnityEditor;

namespace Callbreak.Editor
{
    /// <summary>
    /// Callbreak &gt; Ads &gt; Test Banner in Play Mode (ADR 071): whether a game played in the Editor shows
    /// the drawn test banner where a phone shows AdMob's, so the table and the main menu make room for
    /// it. On until it is turned off; it counts from the next Play.
    /// </summary>
    public static class EditorTestBannerMenu
    {
        private const string MenuPath = "Callbreak/Ads/Test Banner in Play Mode";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            EditorTestBanner.On = !EditorTestBanner.On;
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorTestBanner.On);
            return true;
        }
    }
}
