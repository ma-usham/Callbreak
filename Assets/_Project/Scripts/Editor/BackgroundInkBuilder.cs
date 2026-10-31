using System.Collections.Generic;
using System.Linq;
using System.Text;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor
{
    /// <summary>
    /// Gives every menu screen that shows the equipped background its <see cref="BackgroundInk"/>
    /// (ADR 064): the words it draws straight on the background (the title, the subtitle, the
    /// greeting and the player's name, the tagline and its lines, the loading words, the results'
    /// captions) turn light on a night scene. They are found by name, the names the screen builders
    /// give them, and only where nothing opaque is drawn under them: words on a panel keep their
    /// colours. The shop draws its words on its own cream panel, so it has none. Each word's night
    /// colour is a light cream, a title's a warm gold.
    /// Run: <c>Callbreak.Editor.BackgroundInkBuilder.PatchAll()</c> (the builders call <see cref="Add"/>).
    /// </summary>
    public static class BackgroundInkBuilder
    {
        // Titles and headlines: a warm gold; everything else a light cream; the player's name brighter.
        private static readonly Color TitleNight = new Color32(0xF6, 0xDF, 0xA8, 0xFF);
        private static readonly Color WordsNight = new Color32(0xEA, 0xD8, 0xBE, 0xFF);
        private static readonly Color NameNight = new Color32(0xFF, 0xF8, 0xEE, 0xFF);

        private static readonly string[] TitleNames = { "Title", "Headline" };
        private static readonly string[] WordNames = { "Subtitle", "Greeting", "Tagline", "Loading", "ScoresCaption", "SettingsCaption" };
        private static readonly string[] NameNames = { "PlayerName" };
        private static readonly string[] LineNames = { "FooterLineLeft", "FooterLineRight" };

        /// <summary>Adds (or replaces) the screen's ink; returns how many words and lines it colours.</summary>
        public static int Add(GameObject screen)
        {
            var background = screen.GetComponentInChildren<EquippedBackground>(true);
            if (background == null || screen.GetComponent<ShopView>() != null)
            {
                return 0;
            }

            foreach (BackgroundInk old in screen.GetComponents<BackgroundInk>())
            {
                Object.DestroyImmediate(old);
            }

            // Laid out at 1920 × 1080 to tell what lies under what. A saved screen's root has no canvas,
            // and TextMeshPro only lays its words out under one, so it gets a temporary one.
            var root = (RectTransform)screen.transform;
            Vector2 anchorMin = root.anchorMin, anchorMax = root.anchorMax, size = root.sizeDelta;
            GameObject canvas = null;
            if (root.parent == null)
            {
                canvas = new GameObject("InkLayoutCanvas", typeof(RectTransform), typeof(Canvas));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(UiBuildKit.CanvasWidth, UiBuildKit.CanvasHeight);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvas, screen.scene);
                root.SetParent(canvas.transform, false);
                root.anchorMin = root.anchorMax = Vector2.zero;
                root.sizeDelta = new Vector2(UiBuildKit.CanvasWidth, UiBuildKit.CanvasHeight);
            }

            Graphic[] graphics = screen.GetComponentsInChildren<Graphic>(true);
            var targets = new List<Graphic>();
            var day = new List<Color>();
            var night = new List<Color>();
            var gradient = new List<bool>();
            for (int index = 0; index < graphics.Length; index++)
            {
                Graphic graphic = graphics[index];
                string name = graphic.name;
                Color? light = null;
                if (graphic is TMP_Text)
                {
                    if (TitleNames.Contains(name))
                    {
                        light = TitleNight;
                    }
                    else if (WordNames.Contains(name))
                    {
                        light = WordsNight;
                    }
                    else if (NameNames.Contains(name))
                    {
                        light = NameNight;
                    }
                }
                else if (LineNames.Contains(name))
                {
                    light = WordsNight;
                }

                if (light == null || OnAPanel(graphics, index))
                {
                    continue;
                }

                targets.Add(graphic);
                day.Add(graphic.color);
                Color nightColour = light.Value;
                nightColour.a = graphic.color.a;
                night.Add(nightColour);
                gradient.Add(graphic is TMP_Text text && text.enableVertexGradient);
            }

            if (canvas != null)
            {
                root.SetParent(null, false);
                root.anchorMin = anchorMin;
                root.anchorMax = anchorMax;
                root.sizeDelta = size;
                Object.DestroyImmediate(canvas);
            }

            if (targets.Count == 0)
            {
                return 0;
            }

            var ink = screen.AddComponent<BackgroundInk>();
            ink.Set(background.Library, targets.ToArray(), day.ToArray(), night.ToArray(), gradient.ToArray());
            EditorUtility.SetDirty(ink);
            return targets.Count;
        }

        /// <summary>
        /// Whether something opaque (a panel, a plate, a button's face) is drawn before the graphic at
        /// <paramref name="index"/> and under the middle of what it draws; the equipped background
        /// itself doesn't count.
        /// </summary>
        private static bool OnAPanel(Graphic[] graphics, int index)
        {
            // The middle of what is drawn: a label's words (its box may be far wider), a line's rect.
            var corners = new Vector3[4];
            graphics[index].rectTransform.GetWorldCorners(corners);
            Vector3 middle = (corners[0] + corners[2]) / 2f;
            if (graphics[index] is TMP_Text text && !string.IsNullOrEmpty(text.text))
            {
                text.ForceMeshUpdate(true, true);
                if (text.textInfo != null && text.textInfo.characterCount > 0)
                {
                    middle = text.rectTransform.TransformPoint(text.textBounds.center);
                }
            }
            for (int i = 0; i < index; i++)
            {
                if (!(graphics[i] is Image image) || image.sprite == null || image.color.a < 0.5f
                    || image.GetComponentInParent<EquippedBackground>(true) != null)
                {
                    continue;
                }

                image.rectTransform.GetWorldCorners(corners);
                if (middle.x >= corners[0].x && middle.x <= corners[2].x && middle.y >= corners[0].y && middle.y <= corners[2].y)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Adds the ink to every saved menu screen that shows the equipped background; everything else stays as it is.</summary>
        [MenuItem("Callbreak/Screens/Add Background Ink")]
        private static void PatchAllFromMenu() => Debug.Log(PatchAll());

        /// <summary>As <see cref="PatchAll()"/>, leaving out the screens named in <paramref name="skip"/>.</summary>
        public static string PatchAll(params string[] skip)
        {
            var report = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI/Screens" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (skip.Contains(System.IO.Path.GetFileNameWithoutExtension(path)))
                {
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponentInChildren<EquippedBackground>(true) == null)
                    {
                        continue;
                    }

                    bool had = root.GetComponent<BackgroundInk>() != null;
                    int count = Add(root);
                    if (count == 0 && !had)
                    {
                        continue;
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    report.AppendLine($"{root.name}: {count}{(saved ? "" : " (NOT SAVED)")}");
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
