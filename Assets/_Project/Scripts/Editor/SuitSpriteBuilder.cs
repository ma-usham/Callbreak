using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace Callbreak.Editor
{
    /// <summary>
    /// The four suit symbols as a TextMeshPro sprite asset. The UI fonts have no ♠ ♦ ♣ (Inter has
    /// only ♥; Source Serif 4 has none), so a label that can name a card or a suit ("Your turn ·
    /// Beat Q♥") is given this asset: TextMeshPro then draws a suit character it cannot find in
    /// the label's font from here, because every sprite carries its Unicode value. The text itself
    /// stays plain ("Q♥"); the tags <c>&lt;sprite name="spade"&gt;</c>, "heart", "diamond" and
    /// "club" work too. Red suits are drawn in the deck's red and black suits in the dark text
    /// colour (GDD §13), baked into the sheet, each as tall as a capital letter and standing on
    /// the baseline, so a line with a suit is no taller than one without.
    /// The shapes come from the kit's icons (<see cref="KitFolder"/>, Icon_Suit_Spade.png and so
    /// on) once they are published; until then from placeholders of the same names that this
    /// builder draws into <see cref="PlaceholderFolder"/>. Only an icon's outline (its alpha) is
    /// used. Rebuild after the icons change: Callbreak/Build/Suit Sprites.
    /// </summary>
    public static class SuitSpriteBuilder
    {
        public const string AssetPath = "Assets/_Project/Config/UI/SuitSprites.asset";
        public const string SheetPath = "Assets/_Project/Art/UI/Table/UI_SuitSprites.png";
        public const string KitFolder = "Assets/_Project/Art/UI/Icons";
        public const string PlaceholderFolder = "Assets/_Project/Art/UI/Table";

        /// <summary>♥ ♦ (GDD §13 FIG 13.1, Classic Crimson).</summary>
        public const string RedSuitColour = "#B7191D";

        /// <summary>♠ ♣: the colour of the text on the table's pills (the status label of layout 08).</summary>
        public const string BlackSuitColour = "#272019";

        private const int GlyphHeight = 96;
        private const int Padding = 4;

        // A suit is as tall as this share of the font size (Inter's capitals are 0.727 em).
        private const float HeightInEm = 0.74f;
        private const float SideBearing = 0.1f;
        private const int PlaceholderSize = 192;

        private static readonly (string name, string file, uint unicode, bool red)[] Suits =
        {
            ("spade", "Icon_Suit_Spade", 0x2660, false),
            ("heart", "Icon_Suit_Heart", 0x2665, true),
            ("diamond", "Icon_Suit_Diamond", 0x2666, true),
            ("club", "Icon_Suit_Club", 0x2663, false),
        };

        [MenuItem("Callbreak/Build/Suit Sprites")]
        private static void BuildFromMenu() => Debug.Log(Build(out _));

        /// <summary>The sprite asset, built on first use.</summary>
        public static TMP_SpriteAsset Ensure()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
            if (asset != null && asset.spriteSheet != null && asset.spriteCharacterTable.Count == Suits.Length && !IconsChanged())
            {
                return asset;
            }

            Build(out asset);
            return asset;
        }

        /// <summary>An icon was published or replaced after the sheet was made (the kit's icons taking over from the placeholders).</summary>
        private static bool IconsChanged()
        {
            if (!File.Exists(SheetPath))
            {
                return true;
            }

            DateTime built = File.GetLastWriteTimeUtc(SheetPath);
            foreach ((string _, string file, uint _, bool _) in Suits)
            {
                string source = SourceOf(file);
                if (!File.Exists(source) || File.GetLastWriteTimeUtc(source) > built)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Where a suit's icon is read from: the kit's once it exists, else the placeholder.</summary>
        public static string SourceOf(string file)
        {
            string kit = $"{KitFolder}/{file}.png";
            return File.Exists(kit) ? kit : $"{PlaceholderFolder}/{file}.png";
        }

        /// <summary>Builds the sheet and the sprite asset from the suit icons; returns a report.</summary>
        public static string Build(out TMP_SpriteAsset asset)
        {
            // 1. Each icon's outline, scaled to the glyph height.
            var shapes = new float[Suits.Length][];
            var widths = new int[Suits.Length];
            var sources = new string[Suits.Length];
            int sheetWidth = Padding;
            for (int i = 0; i < Suits.Length; i++)
            {
                sources[i] = SourceOf(Suits[i].file);
                if (!File.Exists(sources[i]))
                {
                    WritePlaceholder(sources[i], Suits[i].name);
                }

                shapes[i] = LoadOutline(sources[i], GlyphHeight, out widths[i]);
                sheetWidth += widths[i] + Padding;
            }

            // 2. The sheet: the four outlines side by side, each filled with its colour.
            int sheetHeight = GlyphHeight + 2 * Padding;
            var pixels = new Color32[sheetWidth * sheetHeight];
            var rects = new GlyphRect[Suits.Length];
            int x = Padding;
            for (int i = 0; i < Suits.Length; i++)
            {
                Color32 ink = UiBuildKit.Hex(Suits[i].red ? RedSuitColour : BlackSuitColour);

                // The colour also fills the transparent pixels around the shape, so filtering never
                // pulls in a dark fringe.
                for (int py = 0; py < sheetHeight; py++)
                {
                    for (int px = x - Padding / 2; px < x + widths[i] + Padding / 2; px++)
                    {
                        pixels[py * sheetWidth + px] = new Color32(ink.r, ink.g, ink.b, 0);
                    }
                }

                for (int py = 0; py < GlyphHeight; py++)
                {
                    for (int px = 0; px < widths[i]; px++)
                    {
                        byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(shapes[i][py * widths[i] + px]) * 255f);

                        // Rows are stored top first; textures are bottom first.
                        int row = Padding + (GlyphHeight - 1 - py);
                        pixels[row * sheetWidth + x + px] = new Color32(ink.r, ink.g, ink.b, alpha);
                    }
                }

                rects[i] = new GlyphRect(x, Padding, widths[i], GlyphHeight);
                x += widths[i] + Padding;
            }

            var sheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGBA32, false);
            sheet.SetPixels32(pixels);
            sheet.Apply();
            byte[] png = sheet.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(sheet);
            UiBuildKit.EnsureFolder(Path.GetDirectoryName(SheetPath).Replace('\\', '/'));
            if (!File.Exists(SheetPath) || !SameBytes(File.ReadAllBytes(SheetPath), png))
            {
                File.WriteAllBytes(SheetPath, png);
            }
            else
            {
                // Same picture: only note that it is up to date with the icons.
                File.SetLastWriteTimeUtc(SheetPath, DateTime.UtcNow);
            }

            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureSheet();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);

            // 3. The sprite asset.
            UiBuildKit.EnsureFolder(Path.GetDirectoryName(AssetPath).Replace('\\', '/'));
            asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            // A current version keeps TextMeshPro from "upgrading" (and emptying) the tables.
            var so = new SerializedObject(asset);
            so.FindProperty("m_Version").stringValue = "1.1.0";
            so.ApplyModifiedPropertiesWithoutUndo();

            asset.spriteSheet = texture;
            asset.spriteGlyphTable.Clear();
            asset.spriteCharacterTable.Clear();
            for (int i = 0; i < Suits.Length; i++)
            {
                float width = rects[i].width, height = rects[i].height;
                float bearing = Mathf.Round(height * SideBearing);
                var metrics = new GlyphMetrics(width, height, bearing, height, width + 2f * bearing);
                var glyph = new TMP_SpriteGlyph((uint)i, metrics, rects[i], 1f, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(Suits[i].unicode, asset, glyph) { name = Suits[i].name, scale = 1f });
            }

            // Its own face: a glyph of GlyphHeight pixels is HeightInEm of the font size tall and
            // stands on the baseline, below the ascent of any font it sits in.
            FaceInfo face = asset.faceInfo;
            face.familyName = "Callbreak Suits";
            face.pointSize = GlyphHeight / HeightInEm;
            face.scale = 1f;
            face.lineHeight = GlyphHeight;
            face.ascentLine = GlyphHeight;
            face.capLine = GlyphHeight;
            face.meanLine = GlyphHeight * 0.7f;
            face.baseline = 0f;
            face.descentLine = 0f;
            asset.faceInfo = face;

            Material material = asset.material;
            if (material == null)
            {
                material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "SuitSprites Material" };
                AssetDatabase.AddObjectToAsset(material, asset);
                asset.material = material;
            }

            material.mainTexture = texture;
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return $"[SuitSpriteBuilder] {AssetPath}: {string.Join(", ", sources)} -> {SheetPath} ({sheetWidth} x {sheetHeight})";
        }

        private static void ConfigureSheet()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            bool changed = importer.textureType != TextureImporterType.Default
                || importer.mipmapEnabled
                || !importer.alphaIsTransparency
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.filterMode != FilterMode.Bilinear
                || importer.npotScale != TextureImporterNPOTScale.None
                || importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!changed)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// An icon's coverage (0-1, rows top first) cropped to its outline and scaled to
        /// <paramref name="height"/> rows by area averaging.
        /// </summary>
        private static float[] LoadOutline(string path, int height, out int width)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    throw new InvalidOperationException($"[SuitSpriteBuilder] Could not read {path}");
                }

                Color32[] source = texture.GetPixels32();
                int sw = texture.width, sh = texture.height;
                int left = sw, right = -1, bottom = sh, top = -1;
                for (int y = 0; y < sh; y++)
                {
                    for (int x = 0; x < sw; x++)
                    {
                        if (source[y * sw + x].a > 12)
                        {
                            left = Math.Min(left, x);
                            right = Math.Max(right, x);
                            bottom = Math.Min(bottom, y);
                            top = Math.Max(top, y);
                        }
                    }
                }

                if (right < left)
                {
                    throw new InvalidOperationException($"[SuitSpriteBuilder] {path} is empty");
                }

                int cw = right - left + 1, ch = top - bottom + 1;
                width = Math.Max(1, Mathf.RoundToInt(cw * (float)height / ch));
                var shape = new float[width * height];
                const int samples = 4;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float sum = 0f;
                        for (int sy = 0; sy < samples; sy++)
                        {
                            for (int sx = 0; sx < samples; sx++)
                            {
                                float u = (x + (sx + 0.5f) / samples) / width * cw + left - 0.5f;

                                // Row 0 is the top of the icon.
                                float v = top + 0.5f - (y + (sy + 0.5f) / samples) / height * ch;
                                sum += Bilinear(source, sw, sh, u, v);
                            }
                        }

                        shape[y * width + x] = sum / (samples * samples);
                    }
                }

                return shape;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static float Bilinear(Color32[] pixels, int width, int height, float u, float v)
        {
            int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
            float fx = u - x0, fy = v - y0;
            float a = Alpha(pixels, width, height, x0, y0), b = Alpha(pixels, width, height, x0 + 1, y0);
            float c = Alpha(pixels, width, height, x0, y0 + 1), d = Alpha(pixels, width, height, x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Alpha(Color32[] pixels, int width, int height, int x, int y)
        {
            return x < 0 || y < 0 || x >= width || y >= height ? 0f : pixels[y * width + x].a / 255f;
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ placeholders

        /// <summary>A plain drawn suit, white on transparent: stands in until the kit's icon is published.</summary>
        private static void WritePlaceholder(string path, string suit)
        {
            const int size = PlaceholderSize, samples = 4;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / samples) / size) * 2f - 1f;
                            float v = ((y + (sy + 0.5f) / samples) / size) * 2f - 1f;
                            if (Inside(suit, u, v))
                            {
                                inside++;
                            }
                        }
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / (samples * samples)));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            UiBuildKit.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Whether (x, y) in [-1, 1] x [-1, 1], y up, lies inside the suit's shape.</summary>
        private static bool Inside(string suit, float x, float y)
        {
            switch (suit)
            {
                case "heart":
                    return Heart(x * 1.22f, (y + 0.12f) * 1.22f);
                case "diamond":
                    return Mathf.Abs(x) / 0.68f + Mathf.Abs(y) / 0.96f <= 1f;
                case "spade":
                    return Heart(x * 1.28f, (0.3f - y) * 1.34f) || Stem(x, y, -0.2f);
                default:
                    return Disc(x, y, 0f, 0.46f, 0.4f) || Disc(x, y, -0.45f, -0.1f, 0.4f) || Disc(x, y, 0.45f, -0.1f, 0.4f)
                        || Disc(x, y, 0f, 0.05f, 0.3f) || Stem(x, y, -0.1f);
            }
        }

        /// <summary>The classic heart curve, point up-side-up with the lobes at the top.</summary>
        private static bool Heart(float x, float y)
        {
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        private static bool Disc(float x, float y, float cx, float cy, float radius)
        {
            return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius;
        }

        /// <summary>The foot of a spade or a club: a stalk that flares towards the bottom.</summary>
        private static bool Stem(float x, float y, float top)
        {
            return y <= top && y >= -0.96f && Mathf.Abs(x) <= 0.07f + (top - y) * 0.34f;
        }
    }
}
