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
            "\u00A0\u00A9\u00AB\u00B7\u00BB\u00D7\u2013\u2014\u2018\u2019\u201C\u201D\u2022\u2026\u2212";

        /// <summary>Which corner or edge of the parent a mockup rectangle stays attached to.</summary>
        public enum Attach
        {
            TopLeft,
            TopRight,
            Centre,
            BottomLeft,
            BottomRight,
            Top,
            Bottom,
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

        /// <summary>
        /// Lays an input field's text across its typing area. TMP_InputField scrolls a long text by
        /// moving it, and after a deletion moves it back towards x = 0, as if the text filled the
        /// area; a fitted label resting anywhere else was pulled there, to the middle of the field.
        /// The label now spans the area from edge to edge; its words stay exactly where they were,
        /// the gap from the area's left edge becoming the label's left margin. The height is kept.
        /// Call before copying the label into a placeholder, so that the two start together.
        /// </summary>
        public static void FillTextArea(RectTransform textArea, TMP_Text label)
        {
            var rect = label.rectTransform;
            float left = rect.localPosition.x + rect.rect.xMin * rect.localScale.x;
            float margin = (left - textArea.rect.xMin) / rect.localScale.x;
            rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
            rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
            rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
            Vector4 margins = label.margin;
            label.margin = new Vector4(margin, margins.y, margins.z, margins.w);
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
                Attach.BottomLeft => new Vector2(0f, 0f),
                Attach.BottomRight => new Vector2(1f, 0f),
                Attach.Top => new Vector2(0.5f, 1f),
                Attach.Bottom => new Vector2(0.5f, 0f),
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
        /// Gives a text field (a layout node with its art on a "Face" child) V2's error look
        /// (UI/Controls/UI_Field_Error.png, Tools/ArtPipeline/V2/pieces.py), which its view shows
        /// while the text cannot be used. Null when the field has no face.
        /// </summary>
        public static Callbreak.Presentation.Widgets.FieldErrorLook AddFieldErrorLook(RectTransform field)
        {
            UnityEngine.UI.Image face = field.Find("Face")?.GetComponent<UnityEngine.UI.Image>();
            if (face == null)
            {
                return null;
            }

            var look = field.gameObject.AddComponent<Callbreak.Presentation.Widgets.FieldErrorLook>();
            var so = new SerializedObject(look);
            so.FindProperty("_face").objectReferenceValue = face;
            so.FindProperty("_errorSprite").objectReferenceValue = LoadSpriteAt($"{ArtRoot}/UI/Controls/UI_Field_Error.png", 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return look;
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
            return LoadSpriteAt($"{ArtRoot}/{relativePath}", pixelsPerUnit, border);
        }

        /// <summary>
        /// Loads a sprite by asset path, applying the recipe E import settings. A pixels-per-unit
        /// of zero or less leaves the texture's existing size settings alone (shared sprites that
        /// are stretched to a rectangle).
        /// </summary>
        public static Sprite LoadSpriteAt(string path, float pixelsPerUnit, Vector4 border = default)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[UiBuildKit] Missing art: {path}");
                return null;
            }

            // Card art (Art/Cards) is imported by the card sprite library builder, which keeps mip
            // maps for the small table cards; a screen that shows a card uses it as it is.
            if (path.StartsWith($"{ArtRoot}/Cards/", System.StringComparison.Ordinal))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
            if (pixelsPerUnit > 0f)
            {
                changed |= Set(importer.spritePixelsPerUnit, pixelsPerUnit, v => importer.spritePixelsPerUnit = v);
                changed |= Set(importer.spriteBorder, border, v => importer.spriteBorder = v);
            }

            // The avatar portraits (ADR 041) keep mip maps: 512 px, shown from ~240 px on the Profile
            // screen down to the scorecard's rows.
            bool mipmaps = path.StartsWith($"{ArtRoot}/UI/Avatars/Avatar_", System.StringComparison.Ordinal);
            changed |= Set(importer.mipmapEnabled, mipmaps, v => importer.mipmapEnabled = v);
            changed |= Set(importer.alphaIsTransparency, true, v => importer.alphaIsTransparency = v);
            changed |= Set(importer.wrapMode, TextureWrapMode.Clamp, v => importer.wrapMode = v);
            changed |= Set(importer.maxTextureSize, 4096, v => importer.maxTextureSize = v);

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

        /// <summary>The font asset for a typography role used in the screen layouts (GDD §12).</summary>
        public static TMP_FontAsset LoadFontRole(string role)
        {
            string file = role switch
            {
                "SerifBold" => "SourceSerif4-Bold",
                "SerifSemiBold" => "SourceSerif4-SemiBold",
                "SansRegular" => "Inter-Regular",
                "SansMedium" => "Inter-Medium",
                "SansSemiBold" => "Inter-SemiBold",
                "SansBold" => "Inter-Bold",
                _ => "Inter-Medium",
            };
            TMP_FontAsset font = LoadFont(file);
            EnsureCharacters(font, LatinCharacters);
            return font;
        }

        /// <summary>
        /// Adds any missing characters to a static font atlas (e.g. the true minus sign of a
        /// negative score). A static atlas has let go of its source font, so the font file is found
        /// again through the GUID the asset keeps, used for this addition, and let go again.
        /// Returns the characters the font file itself has no glyph for.
        /// </summary>
        public static string EnsureCharacters(TMP_FontAsset font, string characters)
        {
            if (font == null || string.IsNullOrEmpty(characters))
            {
                return string.Empty;
            }

            var missing = new System.Text.StringBuilder();
            foreach (char c in characters)
            {
                if (!font.HasCharacter(c, false, false) && missing.ToString().IndexOf(c) < 0)
                {
                    missing.Append(c);
                }
            }

            if (missing.Length == 0)
            {
                return string.Empty;
            }

            var so = new SerializedObject(font);
            Font source = font.sourceFontFile;
            if (source == null)
            {
                string guid = so.FindProperty("m_SourceFontFileGUID").stringValue;
                source = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
            }

            if (source == null)
            {
                Debug.LogWarning($"[UiBuildKit] {font.name}: no source font to add '{missing}' from.");
                return missing.ToString();
            }

            so.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Dynamic;
            so.FindProperty("m_SourceFontFile").objectReferenceValue = source;
            so.ApplyModifiedPropertiesWithoutUndo();
            font.TryAddCharacters(missing.ToString(), out string notAdded);
            so.Update();
            so.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Static;
            so.FindProperty("m_SourceFontFile").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return notAdded ?? string.Empty;
        }

        [MenuItem("Callbreak/Build/Font Coverage")]
        public static void EnsureFontCoverageMenu() => EnsureFontCoverage();

        /// <summary>
        /// Makes sure each of the game's six font atlases holds the whole Latin set the English UI
        /// uses, and reports what a font file has no glyph for (menu Callbreak/Build/Font Coverage).
        /// </summary>
        public static string EnsureFontCoverage()
        {
            var report = new System.Text.StringBuilder();
            foreach (string file in new[] { "SourceSerif4-Bold", "SourceSerif4-SemiBold", "Inter-Regular", "Inter-Medium", "Inter-SemiBold", "Inter-Bold" })
            {
                TMP_FontAsset font = LoadFont(file);
                if (font == null)
                {
                    report.Append(file).Append(": missing; ");
                    continue;
                }

                string notInFont = EnsureCharacters(font, LatinCharacters);
                report.Append(file).Append(notInFont.Length == 0 ? ": complete; " : ": the font has no glyph for '" + notInFont + "'; ");
            }

            Debug.Log("[UiBuildKit] Font coverage: " + report);
            return report.ToString();
        }

        /// <summary>
        /// Creates a single-line label whose rendered ink box matches <paramref name="inkBox"/>
        /// (left, top, right, bottom in mockup pixels, measured on the mockup): the font size
        /// matches the ink height, then either the horizontal scale (lettering drawn narrower than
        /// the real font) or, for letter-spaced capitals, the character spacing matches the width.
        /// The label keeps its baseline, and <paramref name="align"/> decides which edge stays put
        /// when the text changes at runtime.
        /// </summary>
        public static TextMeshProUGUI FitLabel(
            string name,
            Transform parent,
            Vector2 parentCentre,
            string text,
            TMP_FontAsset font,
            float[] inkBox,
            string align,
            bool tracked,
            Color top,
            Color bottom)
        {
            float targetW = (inkBox[2] - inkBox[0]) * ScaleX;
            float targetH = (inkBox[3] - inkBox[1]) * ScaleY;
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            float pivotX = align == "left" ? 0f : align == "right" ? 1f : 0.5f;
            rect.pivot = new Vector2(pivotX, 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(targetW * 1.8f, 240f), Mathf.Max(targetH * 3f, 60f));
            rect.anchoredPosition = new Vector2(
                ((align == "left" ? inkBox[0] : align == "right" ? inkBox[2] : (inkBox[0] + inkBox[2]) / 2f) - parentCentre.x) * ScaleX,
                -(((inkBox[1] + inkBox[3]) / 2f) - parentCentre.y) * ScaleY);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = align == "left" ? TextAlignmentOptions.BaselineLeft
                : align == "right" ? TextAlignmentOptions.BaselineRight
                : TextAlignmentOptions.Baseline;
            label.fontSize = Mathf.Max(8f, targetH * 1.3f);
            label.characterHorizontalScale = 1f;
            label.characterSpacing = 0f;
            if (top == bottom)
            {
                label.color = top;
            }
            else
            {
                label.color = Color.white;
                label.enableVertexGradient = true;
                label.colorGradient = new VertexGradient(top, top, bottom, bottom);
            }

            for (int pass = 0; pass < 4; pass++)
            {
                if (!InkBounds(label, out Rect ink, out int visible))
                {
                    break;
                }

                label.fontSize *= targetH / Mathf.Max(0.01f, ink.height);
                if (!InkBounds(label, out ink, out visible))
                {
                    break;
                }

                if (tracked && visible > 1)
                {
                    float perGap = (targetW - ink.width) / (visible - 1);
                    label.characterSpacing += perGap / (label.fontSize * 0.01f);
                }
                else
                {
                    label.characterHorizontalScale = Mathf.Clamp(label.characterHorizontalScale * targetW / Mathf.Max(0.01f, ink.width), 0.3f, 2.5f);
                }
            }

            // Move the label so its ink box lands exactly on the measured one.
            if (InkBounds(label, out Rect final, out _))
            {
                var target = new Vector2(
                    ((inkBox[0] + inkBox[2]) / 2f - parentCentre.x) * ScaleX,
                    -(((inkBox[1] + inkBox[3]) / 2f) - parentCentre.y) * ScaleY);
                rect.anchoredPosition += target - (rect.anchoredPosition + final.center);
            }

            return label;
        }

        /// <summary>
        /// Creates a single-line label at a given size and character spacing (V2's type), placed so
        /// that its rendered ink lands on <paramref name="inkBox"/> (left, top, right, bottom in mockup
        /// pixels): the edge <paramref name="align"/> keeps when the text changes at runtime, and the
        /// vertical centre. The ink is measured with <paramref name="measure"/> (the text the box was
        /// measured on) where it is given, and the label then shows <paramref name="text"/> on the
        /// same baseline.
        /// </summary>
        public static TextMeshProUGUI PlaceLabel(
            string name,
            Transform parent,
            Vector2 parentCentre,
            string text,
            string measure,
            TMP_FontAsset font,
            float size,
            float spacing,
            float[] inkBox,
            string align,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(align == "left" ? 0f : align == "right" ? 1f : 0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max((inkBox[2] - inkBox[0]) * ScaleX * 1.8f, 240f), Mathf.Max(size * 3f, 60f));
            float anchorX = align == "left" ? inkBox[0] : align == "right" ? inkBox[2] : (inkBox[0] + inkBox[2]) / 2f;
            var target = new Vector2((anchorX - parentCentre.x) * ScaleX, -((inkBox[1] + inkBox[3]) / 2f - parentCentre.y) * ScaleY);
            rect.anchoredPosition = target;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.characterSpacing = spacing;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = align == "left" ? TextAlignmentOptions.BaselineLeft
                : align == "right" ? TextAlignmentOptions.BaselineRight
                : TextAlignmentOptions.Baseline;
            label.text = string.IsNullOrEmpty(measure) ? text : measure;
            if (InkBounds(label, out Rect ink, out _))
            {
                float inkX = align == "left" ? ink.xMin : align == "right" ? ink.xMax : ink.center.x;
                rect.anchoredPosition += target - (rect.anchoredPosition + new Vector2(inkX, ink.center.y));
            }

            label.text = text;
            return label;
        }

        /// <summary>
        /// The union of the visible glyphs' ink boxes, in the label's local space. TMP's character
        /// corners are the quad vertices, which include the SDF padding, so each quad is shrunk to
        /// the glyph's own metrics (the padding is symmetric around the glyph).
        /// </summary>
        public static bool InkBounds(TMP_Text label, out Rect ink, out int visible)
        {
            label.ForceMeshUpdate(true, true);
            TMP_TextInfo info = label.textInfo;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            visible = 0;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible || c.textElement == null || c.textElement.glyph == null)
                {
                    continue;
                }

                UnityEngine.TextCore.GlyphMetrics gm = c.textElement.glyph.metrics;
                float glyphW = gm.width * c.scale * label.characterHorizontalScale;
                float glyphH = gm.height * c.scale;
                float padX = Mathf.Max(0f, (c.topRight.x - c.bottomLeft.x - glyphW) / 2f);
                float padY = Mathf.Max(0f, (c.topRight.y - c.bottomLeft.y - glyphH) / 2f);
                visible++;
                minX = Mathf.Min(minX, c.bottomLeft.x + padX);
                minY = Mathf.Min(minY, c.bottomLeft.y + padY);
                maxX = Mathf.Max(maxX, c.topRight.x - padX);
                maxY = Mathf.Max(maxY, c.topRight.y - padY);
            }

            ink = visible > 0 ? Rect.MinMaxRect(minX, minY, maxX, maxY) : default;
            return visible > 0;
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
