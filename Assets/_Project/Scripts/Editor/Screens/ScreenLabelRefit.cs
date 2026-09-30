using System;
using System.IO;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Label adjustments a screen customizer can apply after the layout builder has fitted the
    /// labels to their ink boxes.
    /// <para>
    /// <see cref="Apply"/>: the mockups' serif lettering has shorter descenders and a lower
    /// i-dot than Source Serif 4, so fitting the whole ink box of a word such as "Round Complete"
    /// or "You Win!" makes its capitals up to 12% too small and lifts its baseline by 3-4 px. A
    /// label node may therefore carry <c>"capFit": [capTop, baseline]</c> or
    /// <c>"xFit": [xHeightTop, baseline]</c> (mockup pixels, read on a flat-topped stem with
    /// <c>Tools/art profile --col</c>): the font size then comes from that height, the baseline
    /// sits on the measured one, and the width still matches the ink box.
    /// </para>
    /// <para>
    /// <c>"lineGradient": ["#capTop", "#baseline"]</c> on a label node adds a <see cref="TextLineGradient"/>:
    /// one vertical gradient across the whole line, as the crimson titles have on the mockups
    /// (TextMeshPro's own gradient restarts on every letter, and the builder's two measured colours
    /// are averages of the upper and lower third, which flattens it). The two colours are the ink
    /// colour at the top of the capitals and at the baseline.
    /// </para>
    /// <para>
    /// <see cref="LimitWidth"/>: gives a dynamic label (a player name, a headline that changes
    /// with the result) a maximum width, so longer text shrinks and finally ends in an ellipsis
    /// instead of running into its neighbours.
    /// </para>
    /// </summary>
    public static class ScreenLabelRefit
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
        }

        /// <summary>Applies "capFit" / "xFit" and "lineGradient" to every label of the screen that carries one.</summary>
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
                if (!caps && !xHeight && !gradient)
                {
                    continue;
                }

                if (!ctx.Nodes.TryGetValue(item.id, out RectTransform rect) || !rect.TryGetComponent(out TextMeshProUGUI label))
                {
                    ctx.Warnings.Add($"{item.id}: capFit/xFit/lineGradient needs a label");
                    continue;
                }

                if (caps || xHeight)
                {
                    Refit(ctx, ctx.Node(item.id), label, caps ? item.capFit : item.xFit, caps ? "H" : "x");
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

        private static void Refit(ScreenBuildContext ctx, LayoutNode node, TextMeshProUGUI label, float[] fit, string probe)
        {
            if (node.inkBox == null || node.inkBox.Length != 4 || fit[1] - fit[0] < 1f)
            {
                ctx.Warnings.Add($"{node.id}: capFit/xFit needs an ink box and a height");
                return;
            }

            // Size: the probe glyph (a flat capital, or a flat lowercase letter) gets the measured
            // height; its foot is the baseline.
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

                label.fontSize *= targetHeight / Mathf.Max(0.01f, glyph.height);
            }

            UiBuildKit.InkBounds(label, out Rect probeInk, out _);
            float baseline = probeInk.yMin;
            label.text = text;

            // Width: as the builder does it, by horizontal scale (or spacing for tracked capitals).
            float targetWidth = (node.inkBox[2] - node.inkBox[0]) * UiBuildKit.ScaleX;
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

            Vector2 parentCentre = !string.IsNullOrEmpty(node.parent) && ctx.Nodes.ContainsKey(node.parent)
                ? ctx.Node(node.parent).Centre
                : UiBuildKit.ScreenCentre;
            float targetX = ((node.inkBox[0] + node.inkBox[2]) / 2f - parentCentre.x) * UiBuildKit.ScaleX;
            float targetBaseline = -(fit[1] - parentCentre.y) * UiBuildKit.ScaleY;
            var rect = (RectTransform)label.transform;
            rect.anchoredPosition = new Vector2(targetX - final.center.x, targetBaseline - baseline);
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
