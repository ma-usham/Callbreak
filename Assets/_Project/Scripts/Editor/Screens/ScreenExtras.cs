using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Extras a screen customizer applies after the layout builder has placed the nodes and fitted
    /// the labels to their ink boxes. <see cref="Apply"/> reads these optional node fields from the
    /// screen's layout (they pass through screen.json untouched):
    /// <para>
    /// <c>"capFit": [capTop, baseline]</c> or <c>"xFit": [xHeightTop, baseline]</c> on a label
    /// (mockup pixels, read on a flat-topped stem with <c>Tools/art profile --col</c>). The mockups'
    /// serif lettering has shorter ascenders and descenders than Source Serif 4, so fitting the whole
    /// ink box of a word such as "Round Complete" makes its capitals up to 12% too small and lifts
    /// its baseline by 3-4 px. With one of these the font size comes from that height, the baseline
    /// sits on the measured one, and the width still matches the ink box. With <c>"dilate": 0.12</c>
    /// as well, the glyphs are drawn heavier (a material preset of the font with that face
    /// dilation): squeezing Source Serif 4 to the mockups' narrow lettering thins its stems by about
    /// 15%, and the dilation gives that weight back; the fit allows for the extra thickness.
    /// <c>"dilate"</c> on its own (no fit) just draws the label with the heavier preset.
    /// </para>
    /// <para>
    /// <c>"lineGradient": ["#capTop", "#baseline"]</c> on a label adds a <see cref="TextLineGradient"/>:
    /// one vertical gradient across the whole line, as the crimson titles have on the mockups
    /// (TextMeshPro's own gradient restarts on every letter, and the builder's two measured colours
    /// are averages of the upper and lower third, which flattens it). The two colours are the ink
    /// colour at the top of the capitals and at the baseline.
    /// </para>
    /// <para>
    /// <c>"fade": [left, right]</c> on a sprite node (a hairline drawn with <c>make</c>) adds a
    /// <see cref="LineFade"/>: the line fades to nothing over that many mockup pixels at each end.
    /// <c>"mirror": true</c> on a sprite node draws its sprite flipped left to right (one kit sprite
    /// for the ornament on the left of a medal and, mirrored, for the one on its right).
    /// </para>
    /// <para>
    /// <see cref="Tabular"/>, <see cref="UnifyStyle"/>, <see cref="AlignColumn"/> and <see cref="AlignRow"/>
    /// make a table of dynamic values regular: the mockups' lettering differs a little from cell to cell (size,
    /// width, baseline), which is exact for the sample values and uneven as soon as a presenter
    /// writes other values into the cells. <see cref="Flank"/> keeps the ornaments either side of
    /// a centred caption next to its text when the text changes.
    /// </para>
    /// <para>
    /// <see cref="LimitWidth"/> gives a dynamic label (a player name, a headline that changes with
    /// the result) a maximum width, so longer text shrinks and finally ends in an ellipsis instead
    /// of running into its neighbours; <see cref="CentreOnCapitals"/> keeps such a label centred
    /// on its line while it shrinks.
    /// </para>
    /// <para>
    /// <see cref="GrowHit"/> gives a control a touch target larger than its art (GDD: 88 px), and
    /// <see cref="Rise"/> gives a results panel its entrance (<see cref="RiseIn"/>).
    /// </para>
    /// </summary>
    public static class ScreenExtras
    {
        [Serializable]
        private sealed class Doc
        {
            public Item[] nodes;
        }

        [Serializable]
        private sealed class Item
        {
            public string id;
            public float[] capFit;
            public float[] xFit;
            public string[] lineGradient;
            public float[] fade;
            public float dilate;
            public bool mirror;
        }

        /// <summary>Applies "capFit" / "xFit", "lineGradient", "dilate", "fade" and "mirror" to every node of the screen that carries one.</summary>
        public static void Apply(ScreenBuildContext ctx)
        {
            string path = ScreenLayout.PathFor(ctx.Layout.name);
            Doc doc = JsonUtility.FromJson<Doc>(File.ReadAllText(path));
            if (doc?.nodes == null)
            {
                return;
            }

            foreach (Item item in doc.nodes)
            {
                bool caps = item.capFit != null && item.capFit.Length == 2;
                bool xHeight = item.xFit != null && item.xFit.Length == 2;
                bool gradient = item.lineGradient != null && item.lineGradient.Length == 2;
                if (item.fade != null && item.fade.Length == 2)
                {
                    Fade(ctx, item.id, item.fade);
                }

                if (item.mirror)
                {
                    Mirror(ctx, item.id);
                }

                if (!caps && !xHeight && !gradient && item.dilate <= 0f)
                {
                    continue;
                }

                if (!ctx.Nodes.TryGetValue(item.id, out RectTransform rect) || !rect.TryGetComponent(out TextMeshProUGUI label))
                {
                    ctx.Warnings.Add($"{item.id}: capFit/xFit/lineGradient/dilate needs a label");
                    continue;
                }

                if (caps || xHeight)
                {
                    Refit(ctx, ctx.Node(item.id), label, caps ? item.capFit : item.xFit, caps ? "H" : "x", item.dilate);
                }
                else if (item.dilate > 0f)
                {
                    label.fontSharedMaterial = HeavyPreset(label.font, item.dilate);
                }

                if (gradient)
                {
                    var line = label.gameObject.AddComponent<TextLineGradient>();
                    var so = new SerializedObject(line);
                    so.FindProperty("_capTop").colorValue = UiBuildKit.Hex(item.lineGradient[0]);
                    so.FindProperty("_baseline").colorValue = UiBuildKit.Hex(item.lineGradient[1]);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void Mirror(ScreenBuildContext ctx, string id)
        {
            Transform face = ctx.Nodes.TryGetValue(id, out RectTransform rect) ? rect.Find("Face") : null;
            if (face == null)
            {
                ctx.Warnings.Add($"{id}: mirror needs a sprite node");
                return;
            }

            face.localScale = new Vector3(-1f, 1f, 1f);
        }

        private static void Fade(ScreenBuildContext ctx, string id, float[] fade)
        {
            Image face = ctx.Nodes.TryGetValue(id, out RectTransform rect) ? rect.Find("Face")?.GetComponent<Image>() : null;
            if (face == null)
            {
                ctx.Warnings.Add($"{id}: fade needs a sprite node");
                return;
            }

            var line = face.gameObject.AddComponent<LineFade>();
            var so = new SerializedObject(line);
            so.FindProperty("_left").floatValue = fade[0] * UiBuildKit.ScaleX;
            so.FindProperty("_right").floatValue = fade[1] * UiBuildKit.ScaleX;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// How far a face dilation moves every glyph edge outward, per unit of font size: the shader
        /// shifts the edge by dilate x ratioA / 2 of the distance field's range, which spans
        /// gradientScale atlas pixels at the font's sampling size.
        /// </summary>
        private static float EdgeShiftPerFontSize(TMP_FontAsset font, float dilate)
        {
            if (dilate <= 0f || font == null)
            {
                return 0f;
            }

            float gradientScale = font.material.GetFloat(ShaderUtilities.ID_GradientScale);
            float ratioA = (gradientScale - 1f) / gradientScale;
            return dilate * ratioA * 0.5f * gradientScale / font.faceInfo.pointSize;
        }

        /// <summary>A material preset of the font whose glyphs are drawn heavier, created on first use.</summary>
        private static Material HeavyPreset(TMP_FontAsset font, float dilate)
        {
            string path = $"{UiBuildKit.FontRoot}/{font.name} Heavy{Mathf.RoundToInt(dilate * 100f):00}.mat";
            var preset = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (preset == null)
            {
                preset = new Material(font.material) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(preset, path);
            }

            preset.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
            ShaderUtilities.UpdateShaderRatios(preset);
            EditorUtility.SetDirty(preset);
            return preset;
        }

        private static void Refit(ScreenBuildContext ctx, LayoutNode node, TextMeshProUGUI label, float[] fit, string probe, float dilate)
        {
            if (node.inkBox == null || node.inkBox.Length != 4 || fit[1] - fit[0] < 1f)
            {
                ctx.Warnings.Add($"{node.id}: capFit/xFit needs an ink box and a height");
                return;
            }

            // Size: the probe glyph (a flat capital, or a flat lowercase letter) gets the measured
            // height; its foot is the baseline. A dilated face draws every edge "shift" further out,
            // so the glyph itself is fitted that much smaller on every side.
            float shiftPerSize = EdgeShiftPerFontSize(label.font, dilate);
            string text = label.text;
            label.text = probe;
            float targetHeight = (fit[1] - fit[0]) * UiBuildKit.ScaleY;
            for (int pass = 0; pass < 2; pass++)
            {
                if (!UiBuildKit.InkBounds(label, out Rect glyph, out _))
                {
                    label.text = text;
                    ctx.Warnings.Add($"{node.id}: the font has no '{probe}' to measure");
                    return;
                }

                float heightPerSize = glyph.height / label.fontSize;
                label.fontSize = targetHeight / Mathf.Max(0.0001f, heightPerSize + 2f * shiftPerSize);
            }

            float shift = shiftPerSize * label.fontSize;
            UiBuildKit.InkBounds(label, out Rect probeInk, out _);
            float baseline = probeInk.yMin - shift;
            label.text = text;
            if (dilate > 0f)
            {
                label.fontSharedMaterial = HeavyPreset(label.font, dilate);
            }

            // Width: as the builder does it, by horizontal scale (or spacing for tracked capitals).
            float targetWidth = (node.inkBox[2] - node.inkBox[0]) * UiBuildKit.ScaleX - 2f * shift;
            for (int pass = 0; pass < 3; pass++)
            {
                if (!UiBuildKit.InkBounds(label, out Rect ink, out int visible))
                {
                    return;
                }

                if (node.tracked && visible > 1)
                {
                    label.characterSpacing += (targetWidth - ink.width) / (visible - 1) / (label.fontSize * 0.01f);
                }
                else
                {
                    label.characterHorizontalScale = Mathf.Clamp(label.characterHorizontalScale * targetWidth / Mathf.Max(0.01f, ink.width), 0.3f, 2.5f);
                }
            }

            // Position: the ink centred on the measured box, the baseline on the measured baseline.
            if (!UiBuildKit.InkBounds(label, out Rect final, out _))
            {
                return;
            }

            Vector2 parentCentre = ParentCentre(ctx, node);
            float targetX = ((node.inkBox[0] + node.inkBox[2]) / 2f - parentCentre.x) * UiBuildKit.ScaleX;
            float targetBaseline = -(fit[1] - parentCentre.y) * UiBuildKit.ScaleY;
            var rect = (RectTransform)label.transform;
            rect.anchoredPosition = new Vector2(targetX - final.center.x, targetBaseline - baseline);
        }

        /// <summary>
        /// Gives score labels tabular figures (<see cref="TabularFigures"/>: every digit as wide as
        /// the widest, as on the mockups) and fits each again with them: its width scale, so that the
        /// ink as drawn is as wide as on the mockup, and its position, so that the ink is centred
        /// where the mockup's is. Call before <see cref="UnifyStyle"/>.
        /// </summary>
        public static void Tabular(ScreenBuildContext ctx, IEnumerable<string> ids)
        {
            foreach (string id in ids)
            {
                if (!ctx.Nodes.TryGetValue(id, out RectTransform rect) || !rect.TryGetComponent(out TextMeshProUGUI label))
                {
                    ctx.Warnings.Add($"{id}: not a label");
                    continue;
                }

                LayoutNode node = ctx.Node(id);
                if (node.inkBox == null || node.inkBox.Length != 4)
                {
                    continue;
                }

                label.gameObject.AddComponent<TabularFigures>();
                float targetWidth = (node.inkBox[2] - node.inkBox[0]) * UiBuildKit.ScaleX;
                for (int pass = 0; pass < 2; pass++)
                {
                    if (!DrawnInk(label, out float left, out float right))
                    {
                        break;
                    }

                    label.characterHorizontalScale = Mathf.Clamp(label.characterHorizontalScale * targetWidth / Mathf.Max(0.01f, right - left), 0.3f, 2.5f);
                }

                if (DrawnInk(label, out float inkLeft, out float inkRight))
                {
                    float target = ((node.inkBox[0] + node.inkBox[2]) / 2f - ParentCentre(ctx, node).x) * UiBuildKit.ScaleX;
                    rect.anchoredPosition = new Vector2(target - (inkLeft + inkRight) / 2f, rect.anchoredPosition.y);
                }
            }
        }

        /// <summary>
        /// The horizontal extent of a label's ink as drawn, in its local space: read from the mesh,
        /// so it includes what effects such as <see cref="TabularFigures"/> do to the glyphs.
        /// </summary>
        private static bool DrawnInk(TextMeshProUGUI label, out float left, out float right)
        {
            label.ForceMeshUpdate(true, true);
            TMP_TextInfo info = label.textInfo;
            left = float.MaxValue;
            right = float.MinValue;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible || c.textElement == null || c.textElement.glyph == null)
                {
                    continue;
                }

                // The quad includes the distance field's padding, symmetric around the glyph.
                Vector3[] vertices = info.meshInfo[c.materialReferenceIndex].vertices;
                float quadLeft = vertices[c.vertexIndex].x, quadRight = vertices[c.vertexIndex + 2].x;
                float glyphWidth = c.textElement.glyph.metrics.width * c.scale * label.characterHorizontalScale;
                float pad = Mathf.Max(0f, (quadRight - quadLeft - glyphWidth) / 2f);
                left = Mathf.Min(left, quadLeft + pad);
                right = Mathf.Max(right, quadRight - pad);
            }

            return right > left;
        }

        private static Vector2 ParentCentre(ScreenBuildContext ctx, LayoutNode node)
        {
            return !string.IsNullOrEmpty(node.parent) && ctx.Nodes.ContainsKey(node.parent) ? ctx.Node(node.parent).Centre : UiBuildKit.ScreenCentre;
        }

        /// <summary>
        /// Gives labels that show the same kind of value (all the scores of a table, all its names)
        /// one font size and one width scale: the medians of what each was fitted to. Each label
        /// keeps its baseline and the edge its alignment pins. Call before <see cref="LimitWidth"/>.
        /// </summary>
        public static void UnifyStyle(ScreenBuildContext ctx, IEnumerable<string> ids)
        {
            List<TextMeshProUGUI> labels = Labels(ctx, ids);
            if (labels.Count < 2)
            {
                return;
            }

            float size = Median(labels.Select(l => l.fontSize));
            float scale = Median(labels.Select(l => l.characterHorizontalScale));
            foreach (TextMeshProUGUI label in labels)
            {
                label.fontSize = size;
                label.characterHorizontalScale = scale;
            }
        }

        /// <summary>
        /// Puts the labels of one table column at the same place in every row: horizontally on one
        /// line (the median of where each was fitted: its left edge, centre or right edge, as its
        /// alignment says) and at one height within the row (the median of their baselines). The
        /// labels' parents must be the table's rows: nodes of one size, one above the other.
        /// </summary>
        public static void AlignColumn(ScreenBuildContext ctx, IEnumerable<string> ids)
        {
            List<TextMeshProUGUI> labels = Labels(ctx, ids);
            if (labels.Count < 2)
            {
                return;
            }

            var position = new Vector2(
                Median(labels.Select(l => l.rectTransform.anchoredPosition.x)),
                Median(labels.Select(l => l.rectTransform.anchoredPosition.y)));
            foreach (TextMeshProUGUI label in labels)
            {
                label.rectTransform.anchoredPosition = position;
            }
        }

        /// <summary>
        /// Puts labels of one table row (children of the same node) that are set alike on one
        /// baseline: the median of theirs. (Cells of different sizes are centred on the row on the
        /// mockups, so their baselines differ; leave those to <see cref="AlignColumn"/>.)
        /// </summary>
        public static void AlignRow(ScreenBuildContext ctx, IEnumerable<string> ids)
        {
            List<TextMeshProUGUI> labels = Labels(ctx, ids);
            if (labels.Count < 2)
            {
                return;
            }

            float y = Median(labels.Select(l => l.rectTransform.anchoredPosition.y));
            foreach (TextMeshProUGUI label in labels)
            {
                label.rectTransform.anchoredPosition = new Vector2(label.rectTransform.anchoredPosition.x, y);
            }
        }

        /// <summary>
        /// Adds a <see cref="LabelFlanks"/> to a centred caption so that the nodes to its left and
        /// right stay at their present distance from its text. The three must share a parent.
        /// Call after the caption's size is final (after <see cref="LimitWidth"/>).
        /// </summary>
        public static LabelFlanks Flank(ScreenBuildContext ctx, string labelId, string leftId, string rightId)
        {
            if (!ctx.Nodes.TryGetValue(labelId, out RectTransform labelRect) || !labelRect.TryGetComponent(out TextMeshProUGUI label) ||
                !ctx.Nodes.TryGetValue(leftId, out RectTransform left) || !ctx.Nodes.TryGetValue(rightId, out RectTransform right))
            {
                ctx.Warnings.Add($"{labelId}: Flank needs a label and the nodes {leftId} and {rightId}");
                return null;
            }

            label.ForceMeshUpdate(true, true);
            var flanks = label.gameObject.AddComponent<LabelFlanks>();
            var so = new SerializedObject(flanks);
            so.FindProperty("_left").objectReferenceValue = left;
            so.FindProperty("_right").objectReferenceValue = right;
            so.FindProperty("_designWidth").floatValue = LabelFlanks.Width(label);
            so.FindProperty("_leftX").floatValue = left.anchoredPosition.x;
            so.FindProperty("_rightX").floatValue = right.anchoredPosition.x;
            so.ApplyModifiedPropertiesWithoutUndo();
            return flanks;
        }

        private static List<TextMeshProUGUI> Labels(ScreenBuildContext ctx, IEnumerable<string> ids)
        {
            var labels = new List<TextMeshProUGUI>();
            foreach (string id in ids)
            {
                if (ctx.Nodes.TryGetValue(id, out RectTransform rect) && rect.TryGetComponent(out TextMeshProUGUI label))
                {
                    labels.Add(label);
                }
                else
                {
                    ctx.Warnings.Add($"{id}: not a label");
                }
            }

            return labels;
        }

        private static float Median(IEnumerable<float> values)
        {
            List<float> sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2f;
        }

        /// <summary>
        /// Enlarges (or, with negative values, trims) what a control answers a touch on, without
        /// changing what it draws: the GDD asks for touch targets of at least 88 px, 16 px apart.
        /// <paramref name="up"/> and <paramref name="down"/> are canvas pixels beyond the top and
        /// the bottom edge of what the node draws; they go on every raycast target the node has
        /// itself (its face, both looks of a switch, a button's own invisible image).
        /// </summary>
        public static void GrowHit(ScreenBuildContext ctx, string id, float up, float down)
        {
            if (!ctx.Nodes.TryGetValue(id, out RectTransform rect))
            {
                ctx.Warnings.Add($"{id}: GrowHit needs a node");
                return;
            }

            var targets = new List<Graphic>();
            if (rect.TryGetComponent(out Graphic own))
            {
                targets.Add(own);
            }

            foreach (Transform child in rect)
            {
                if (child.TryGetComponent(out Graphic graphic))
                {
                    targets.Add(graphic);
                }
            }

            int grown = 0;
            foreach (Graphic target in targets.Where(t => t.raycastTarget))
            {
                // Left, bottom, right, top; a negative padding reaches beyond the rectangle.
                Vector4 padding = target.raycastPadding;
                target.raycastPadding = new Vector4(padding.x, -down, padding.z, -up);
                grown++;
            }

            if (grown == 0)
            {
                ctx.Warnings.Add($"{id}: GrowHit found no raycast target");
            }
        }

        /// <summary>
        /// Adds a <see cref="RiseIn"/> to a node (GDD §14 "Results: panel rises"): the view plays
        /// it when the screen is shown. The node must sit inside the screen's content, not be it.
        /// </summary>
        public static RiseIn Rise(ScreenBuildContext ctx, string id)
        {
            if (!ctx.Nodes.TryGetValue(id, out RectTransform rect) || rect == ctx.Content)
            {
                ctx.Warnings.Add($"{id}: Rise needs a node inside the content");
                return null;
            }

            return rect.gameObject.AddComponent<RiseIn>();
        }

        /// <summary>
        /// Makes a one-line label keep the middle of its capitals where it is now, whatever size it
        /// is drawn at: a headline that <see cref="LimitWidth"/> shrinks then stays centred on the
        /// line the full-size one is set on instead of sinking to its baseline (where the smaller
        /// lettering's descenders would still reach what lies under it). The text as built does
        /// not move. Call before <see cref="LimitWidth"/>.
        /// </summary>
        public static void CentreOnCapitals(ScreenBuildContext ctx, string id)
        {
            if (!ctx.Nodes.TryGetValue(id, out RectTransform rect) || !rect.TryGetComponent(out TextMeshProUGUI label) ||
                !UiBuildKit.InkBounds(label, out Rect before, out _))
            {
                ctx.Warnings.Add($"{id}: CentreOnCapitals needs a label with text");
                return;
            }

            label.verticalAlignment = VerticalAlignmentOptions.Capline;
            if (UiBuildKit.InkBounds(label, out Rect after, out _))
            {
                rect.anchoredPosition += new Vector2(0f, before.center.y - after.center.y);
            }
        }

        /// <summary>
        /// Caps a dynamic label's width (mockup pixels; the edge its alignment pins stays where it
        /// is). Text that does not fit shrinks down to <paramref name="minScale"/> of the design
        /// size and then ends in an ellipsis. Text that fits is drawn exactly as before.
        /// </summary>
        public static void LimitWidth(ScreenBuildContext ctx, string id, float widthInMockPixels, float minScale = 0.6f)
        {
            if (!ctx.Nodes.TryGetValue(id, out RectTransform rect) || !rect.TryGetComponent(out TextMeshProUGUI label))
            {
                ctx.Warnings.Add($"{id}: LimitWidth needs a label");
                return;
            }

            rect.sizeDelta = new Vector2(widthInMockPixels * UiBuildKit.ScaleX, rect.sizeDelta.y);
            float size = label.fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMax = size;
            label.fontSizeMin = size * minScale;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
