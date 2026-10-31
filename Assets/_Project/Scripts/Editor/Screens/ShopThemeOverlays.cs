using System.IO;
using System.Text;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// The theme preview's pictures of the menu and of the table (ADR 064), taken from the game's own
    /// screens without what lies under them: the main menu without its background (its small words by
    /// day and by night, <see cref="BackgroundInk"/>), and the table at mockup 08's moment
    /// (<see cref="TablePreview"/>) without its cloth. Each is drawn at 1920 × 1080 on black and on
    /// white, its see-through parts recovered from the two, and halved to 960 × 540 (the preview shows
    /// each at 632 canvas units across): Art/Shop/Themes/Preview_Menu_Day.png, Preview_Menu_Night.png
    /// and Preview_Table.png. Run again when the main menu or the table changes.
    /// Run: <c>Callbreak.Editor.Screens.ShopThemeOverlays.Capture()</c>, then rebuild the shop.
    /// </summary>
    public static class ShopThemeOverlays
    {
        private const int Width = 1920;
        private const int Height = 1080;

        public static string Capture()
        {
            var report = new StringBuilder();
            report.AppendLine(Overlay(MainMenuBuilder.PrefabPath, ShopCustomizer.MenuOverlayDayPath, go => PrepareMenu(go, false)));
            report.AppendLine(Overlay(MainMenuBuilder.PrefabPath, ShopCustomizer.MenuOverlayNightPath, go => PrepareMenu(go, true)));
            report.AppendLine(Overlay(ScreenLayoutBuilder.PrefabPath("Table"), ShopCustomizer.TableOverlayPath, PrepareTable));
            return report.ToString();
        }

        private static void PrepareMenu(GameObject menu, bool night)
        {
            foreach (EquippedBackground background in menu.GetComponentsInChildren<EquippedBackground>(true))
            {
                background.GetComponent<Image>().enabled = false;
            }

            // The menu's ink as its builders give it (so a menu saved before ADR 064 shows its night words too).
            BackgroundInkBuilder.Add(menu);
            BackgroundInk ink = menu.GetComponentInChildren<BackgroundInk>(true);
            if (ink != null)
            {
                ink.Show(night);
            }
        }

        private static void PrepareTable(GameObject table)
        {
            TableView view = table.GetComponentInChildren<TableView>(true);
            foreach (Image image in table.GetComponentsInChildren<Image>(true))
            {
                if (image == view.ClothFrame || image.name == "Table")
                {
                    image.enabled = false;
                }
            }
        }

        private static string Overlay(string prefab, string output, System.Action<GameObject> prepare)
        {
            string black = $"{ScreenCaptureTool.OutputFolder}/Overlay_Black.png";
            string white = $"{ScreenCaptureTool.OutputFolder}/Overlay_White.png";
            if (ScreenCaptureTool.Capture(prefab, Width, Height, black, prepare, Color.black) == null
                || ScreenCaptureTool.Capture(prefab, Width, Height, white, prepare, Color.white) == null)
            {
                return $"FAILED to capture {prefab}";
            }

            var onBlack = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var onWhite = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var halved = new Texture2D(Width / 2, Height / 2, TextureFormat.RGBA32, false);
            try
            {
                onBlack.LoadImage(File.ReadAllBytes(black));
                onWhite.LoadImage(File.ReadAllBytes(white));
                Color[] b = onBlack.GetPixels();
                Color[] w = onWhite.GetPixels();
                var recovered = new Color[b.Length];
                for (int i = 0; i < b.Length; i++)
                {
                    float alpha = Mathf.Clamp01(1f - ((w[i].r - b[i].r) + (w[i].g - b[i].g) + (w[i].b - b[i].b)) / 3f);
                    recovered[i] = alpha > 0.001f
                        ? new Color(Mathf.Clamp01(b[i].r / alpha), Mathf.Clamp01(b[i].g / alpha), Mathf.Clamp01(b[i].b / alpha), alpha)
                        : new Color(0f, 0f, 0f, 0f);
                }

                // Halved: each pixel the average of four, weighted by their cover so edges keep their colour.
                var half = new Color[(Width / 2) * (Height / 2)];
                for (int y = 0; y < Height / 2; y++)
                {
                    for (int x = 0; x < Width / 2; x++)
                    {
                        float r = 0f, g = 0f, bl = 0f, a = 0f;
                        for (int dy = 0; dy < 2; dy++)
                        {
                            for (int dx = 0; dx < 2; dx++)
                            {
                                Color c = recovered[(y * 2 + dy) * Width + x * 2 + dx];
                                r += c.r * c.a;
                                g += c.g * c.a;
                                bl += c.b * c.a;
                                a += c.a;
                            }
                        }

                        half[y * (Width / 2) + x] = a > 0.0001f ? new Color(r / a, g / a, bl / a, a / 4f) : new Color(0f, 0f, 0f, 0f);
                    }
                }

                halved.SetPixels(half);
                halved.Apply();
                UiBuildKit.EnsureFolder(Path.GetDirectoryName(output).Replace('\\', '/'));
                File.WriteAllBytes(output, halved.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(onBlack);
                Object.DestroyImmediate(onWhite);
                Object.DestroyImmediate(halved);
            }

            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
            UiBuildKit.LoadSpriteAt(output, 0f);
            return $"wrote {output}";
        }
    }
}
