using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Callbreak.Editor
{
    /// <summary>
    /// Shared helpers for the screen builders: converts measurements taken on the 1672 × 941
    /// mockups to the 1920 × 1080 canvas (GDD §12), and creates the images, labels, sprites and
    /// font assets the builders place.
    /// </summary>
    public static class UiBuildKit
    {
        public const float CanvasWidth = 1920f;
        public const float CanvasHeight = 1080f;
        public const float MockWidth = 1672f;
        public const float MockHeight = 941f;

        public const float ScaleX = CanvasWidth / MockWidth;
        public const float ScaleY = CanvasHeight / MockHeight;

        public const string ArtRoot = "Assets/_Project/Art";
        public const string FontRoot = "Assets/_Project/Fonts";

        // Basic Latin plus Latin-1 punctuation and letters: everything the English UI uses.
        private const string LatinCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "\u00A0\u00A9\u00AB\u00B7\u00BB\u2013\u2014\u2018\u2019\u201C\u201D\u2022\u2026";

        /// <summary>Which corner or edge of the parent a mockup rectangle stays attached to.</summary>
        public enum Attach
        {
            TopLeft,
            TopRight,
            Centre,
        }

        /// <summary>Centre of the mockup: where a design group's own centre sits.</summary>
        public static readonly Vector2 ScreenCentre = new Vector2(MockWidth / 2f, MockHeight / 2f);

        /// <summary>
        /// Creates a child centred on a point measured on the mockup (mockup pixels, top-left
        /// origin). <paramref name="parentCentre"/> is the parent's own centre on the mockup, so
        /// nested elements are measured in screen coordinates too. The size is in canvas units.
        /// </summary>
        public static RectTransform CreateAt(string name, Transform parent, Vector2 centre, Vector2 parentCentre, Vector2 canvasSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = canvasSize;
            rect.anchoredPosition = new Vector2((centre.x - parentCentre.x) * ScaleX, -(centre.y - parentCentre.y) * ScaleY);
            return rect;
        }

        /// <summary>Size of a mockup measurement in canvas units.</summary>
        public static Vector2 FromMock(float width, float height)
        {
            return new Vector2(width * ScaleX, height * ScaleY);
        }

        /// <summary>The size a sprite has on the canvas when shown pixel for pixel at 1080p.</summary>
        public static Vector2 NativeSize(Sprite sprite)
        {
            return sprite == null ? new Vector2(100f, 100f) : sprite.rect.size / sprite.pixelsPerUnit * 100f;
        }

        /// <summary>
        /// Creates a full-screen-sized group that holds part of the design and keeps it attached
        /// to one side of the screen when the canvas is wider or taller than 16:9 (Expand mode).
        /// </summary>
        public static RectTransform CreateDesignGroup(string name, Transform parent, Attach attach)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Vector2 anchor = attach switch
            {
                Attach.TopLeft => new Vector2(0f, 1f),
                Attach.TopRight => new Vector2(1f, 1f),
                _ => new Vector2(0.5f, 0.5f),
            };
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        public static UnityEngine.UI.Image AddImage(RectTransform rect, Sprite sprite, bool raycastTarget)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.raycastTarget = raycastTarget;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }

            return image;
        }

        /// <summary>
        /// Creates a single-line label whose baseline sits at <paramref name="baselineY"/> and
        /// whose text starts at, or is centred on, <paramref name="x"/> (mockup pixels).
        /// </summary>
        public static TextMeshProUGUI CreateLabel(
            string name,
            Transform parent,
            Vector2 parentCentre,
            string text,
            TMP_FontAsset font,
            float fontSize,
            Color color,
            float x,
            float baselineY,
            bool centred)
        {
            // TextMeshPro's Baseline alignment puts the baseline on the box's vertical centre.
            RectTransform rect = CreateAt(name, parent, new Vector2(x, baselineY), parentCentre, new Vector2(900f, fontSize * 2f));
            rect.pivot = new Vector2(centred ? 0.5f : 0f, 0.5f);

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = centred ? TextAlignmentOptions.Baseline : TextAlignmentOptions.BaselineLeft;
            return label;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }

        /// <summary>
        /// Loads a sprite under the art root after making sure its import settings follow the
        /// Developer Handbook recipe E. Returns null, with a warning, while the file is missing.
        /// </summary>
        public static Sprite LoadSprite(string relativePath, float pixelsPerUnit, Vector4 border = default)
        {
            string path = $"{ArtRoot}/{relativePath}";
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[UiBuildKit] Missing art: {path}");
                return null;
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = (TextureImporter)AssetImporter.GetAtPath(path);
            }

            bool changed = false;
            changed |= Set(importer.textureType, TextureImporterType.Sprite, v => importer.textureType = v);
            changed |= Set(importer.spriteImportMode, SpriteImportMode.Single, v => importer.spriteImportMode = v);
            changed |= Set(importer.spritePixelsPerUnit, pixelsPerUnit, v => importer.spritePixelsPerUnit = v);
            changed |= Set(importer.mipmapEnabled, false, v => importer.mipmapEnabled = v);
            changed |= Set(importer.alphaIsTransparency, true, v => importer.alphaIsTransparency = v);
            changed |= Set(importer.wrapMode, TextureWrapMode.Clamp, v => importer.wrapMode = v);
            changed |= Set(importer.maxTextureSize, 4096, v => importer.maxTextureSize = v);
            changed |= Set(importer.spriteBorder, border, v => importer.spriteBorder = v);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Returns the TextMeshPro font asset for a font file in the font root, creating it with a
        /// static Latin atlas on first use (GDD §12).
        /// </summary>
        public static TMP_FontAsset LoadFont(string fontFileName)
        {
            string sourcePath = $"{FontRoot}/{fontFileName}.ttf";
            string assetPath = $"{FontRoot}/{fontFileName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"[UiBuildKit] Missing font file: {sourcePath}");
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            fontAsset.name = $"{fontFileName} SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            fontAsset.material.name = $"{fontFileName} SDF Material";
            fontAsset.atlasTexture.name = $"{fontFileName} SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);

            if (!fontAsset.TryAddCharacters(LatinCharacters, out string missing) && !string.IsNullOrEmpty(missing))
            {
                Debug.Log($"[UiBuildKit] {fontFileName} has no glyph for: {missing}");
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        /// <summary>
        /// Returns a material preset of <paramref name="font"/> with a soft drop shadow under the
        /// glyphs, creating it on first use.
        /// </summary>
        public static Material LoadShadowPreset(TMP_FontAsset font, string presetName, Color shadow, Vector2 offset, float softness)
        {
            string path = $"{FontRoot}/{font.name} {presetName}.mat";
            var preset = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (preset == null)
            {
                preset = new Material(font.material) { name = $"{font.name} {presetName}" };
                AssetDatabase.CreateAsset(preset, path);
            }

            preset.EnableKeyword("UNDERLAY_ON");
            preset.SetColor(ShaderUtilities.ID_UnderlayColor, shadow);
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset.x);
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, offset.y);
            preset.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            preset.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
            EditorUtility.SetDirty(preset);
            return preset;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static bool Set<T>(T current, T wanted, System.Action<T> apply)
        {
            if (Equals(current, wanted))
            {
                return false;
            }

            apply(wanted);
            return true;
        }
    }
}
