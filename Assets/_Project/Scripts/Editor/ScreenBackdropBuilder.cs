using System.IO;
using System.Linq;
using System.Text;
using Callbreak.Editor.Screens;
using Callbreak.Presentation.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor
{
    /// <summary>
    /// One background for every menu screen, and the game behind the pages over a match (the owner,
    /// 31 Oct 2026; ADR 058's addendum). A screen the routers swap has no picture of its own: the menu
    /// screens stand on the main menu's, which the Menu scene lifts out to stand behind them all
    /// (<see cref="MenuBackdrop"/>), and the pages over a match stand on the table, through a dim the
    /// table's scope shows (<see cref="OverTheTable"/>). ScreenLayoutBuilder builds the screens that way;
    /// <see cref="PatchAll"/> brings the saved ones in line without building them again.
    /// Run: <c>Callbreak.Editor.ScreenBackdropBuilder.PatchAll()</c>.
    /// </summary>
    public static class ScreenBackdropBuilder
    {
        /// <summary>The name of a page's dim over the table.</summary>
        public const string DimName = "TableDim";

        // The match menu's dim (MatchMenuCustomizer): the game shows through, calm enough that the page's
        // header and light words read over its seats and cards, and the table keeps its look between the
        // match menu and the pages it opens.
        private const float DimOpacity = 0.83f;

        // How far the dim reaches past the screen's edges, whatever the screen's shape.
        private const float DimBleed = 64f;

        // Screens with a picture of their own: the main menu holds the menus' one (the Menu scene lifts
        // it out); the loading curtain and the name prompt each cover the screen on a canvas of their own.
        private static readonly string[] OwnPicture = { "MainMenu", "SceneLoading", "NamePrompt" };

        // The pages that open over a match (the Table scene's overlays with a page of their own).
        private static readonly string[] OverTheMatch = { "Scorecard", "RoundResults", "MatchResults", "Settings", "HowToPlay" };

        // Of those, the ones that open only over a match.
        private static readonly string[] OnlyOverTheMatch = { "Scorecard", "RoundResults", "MatchResults" };

        public static bool HasOwnPicture(string screen) => OwnPicture.Contains(screen);

        /// <summary>Whether the screen's layout lays it on the equipped background (its own, the menus' one, or the table's dim).</summary>
        public static bool LaidOnPicture(string screen) =>
            File.Exists(ScreenLayout.PathFor(screen)) && ScreenLayout.Load(screen).background == ScreenLayout.EquippedBackground;

        /// <summary>Whether the screen stands on the menus' one background: not on a picture of its own, nor only over a match.</summary>
        public static bool OnTheMenusPicture(string screen) => LaidOnPicture(screen) && !HasOwnPicture(screen) && !OnlyOverTheMatch.Contains(screen);

        /// <summary>
        /// Gives a page over the match its dim (off, under everything the page draws) and its
        /// <see cref="OverTheTable"/>, anew; false for any other screen.
        /// </summary>
        public static bool AddOverTheTable(GameObject screen)
        {
            if (!OverTheMatch.Contains(screen.name))
            {
                return false;
            }

            foreach (OverTheTable old in screen.GetComponents<OverTheTable>())
            {
                Object.DestroyImmediate(old);
            }

            Transform oldDim = screen.transform.Find(DimName);
            if (oldDim != null)
            {
                Object.DestroyImmediate(oldDim.gameObject);
            }

            var dim = new GameObject(DimName, typeof(RectTransform));
            dim.layer = screen.layer;
            var rect = (RectTransform)dim.transform;
            rect.SetParent(screen.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = -Vector2.one * DimBleed;
            rect.offsetMax = Vector2.one * DimBleed;
            var image = dim.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, DimOpacity);
            image.raycastTarget = true;
            dim.SetActive(false);

            var over = screen.AddComponent<OverTheTable>();
            over.Set(image, screen.GetComponent<BackgroundInk>());
            EditorUtility.SetDirty(over);
            return true;
        }

        /// <summary>Takes away the picture of its own from a screen that stands on another; true when it had one.</summary>
        public static bool RemoveOwnPicture(GameObject screen)
        {
            if (HasOwnPicture(screen.name))
            {
                return false;
            }

            bool removed = false;
            foreach (EquippedBackground picture in screen.GetComponentsInChildren<EquippedBackground>(true))
            {
                Object.DestroyImmediate(picture.gameObject);
                removed = true;
            }

            return removed;
        }

        [MenuItem("Callbreak/Screens/One Background For The Menus")]
        private static void PatchAllFromMenu() => Debug.Log(PatchAll());

        /// <summary>Takes the pictures of their own away from the saved screens and gives the pages over a match their dims.</summary>
        public static string PatchAll()
        {
            var report = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ScreenLayoutBuilder.PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool removed = RemoveOwnPicture(root);
                    bool over = AddOverTheTable(root);
                    if (!removed && !over)
                    {
                        continue;
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    report.AppendLine($"{root.name}:{(removed ? " picture taken away" : string.Empty)}{(over ? " dim over the table" : string.Empty)}{(saved ? string.Empty : " (NOT SAVED)")}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return report.ToString();
        }
    }
}
