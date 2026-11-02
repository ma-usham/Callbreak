using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Callbreak.Editor
{
    /// <summary>
    /// Google's Editor placeholder ads (ADR 073) come on canvases of their own at sorting order 0, under
    /// the game's (its screens at 10, overlays, dialogs and toasts above them): in Play mode the banner,
    /// the ads between matches, the rewarded and the app open ads and the consent form loaded and
    /// showed, unseen and out of reach behind the game. On phones AdMob's views lie over everything, so
    /// here each placeholder's canvas is put over the game as it appears (ADR 075). Editor only: builds
    /// have no placeholders.
    /// </summary>
    [InitializeOnLoad]
    public static class AdMobPlaceholdersOnTop
    {
        /// <summary>Over every canvas of the game, the loading curtain's included.</summary>
        private const int OverTheGame = 30000;

        private const string CloneSuffix = "(Clone)";

        // The plugin's placeholder prefabs (GoogleMobileAds/Editor/Resources/PlaceholderAds and Ump).
        private static readonly HashSet<string> Placeholders = new HashSet<string>
        {
            "BANNER", "ADAPTIVE", "CENTER", "FULL_BANNER", "LARGE_BANNER", "LEADERBOARD", "MEDIUM_RECTANGLE", "SMART_BANNER",
            "1024x768", "768x1024", "ConsentForm",
        };

        static AdMobPlaceholdersOnTop()
        {
            EditorApplication.hierarchyChanged += RaisePlaceholders;
        }

        private static void RaisePlaceholders()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Canvas canvas = IsPlaceholder(root.name) ? root.GetComponentInChildren<Canvas>(true) : null;
                    if (canvas == null || canvas.sortingOrder >= OverTheGame)
                    {
                        continue;
                    }

                    canvas.sortingOrder = OverTheGame;
                }
            }
        }

        private static bool IsPlaceholder(string name)
        {
            if (name.EndsWith(CloneSuffix, System.StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - CloneSuffix.Length);
            }

            return Placeholders.Contains(name.Trim());
        }
    }
}
