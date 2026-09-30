using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 07, the call panel: wires the eight chips, their numbers and the crimson chip of the
    /// chosen call into a <see cref="BidChipRow"/>, and adds the "Asha is calling…" line that
    /// stands in the middle of the table while the panel is hidden (the mockup shows only the
    /// local turn, so the line borrows the panel subtitle's type).
    /// </summary>
    public static class BiddingCustomizer
    {
        public const int ChipCount = 8;
        private const int MockupChoice = 4;
        private const float CallingScale = 1.25f;

        public static void Customize(ScreenBuildContext ctx)
        {
            RectTransform panel = ctx.Nodes["Panel"];
            var row = panel.gameObject.AddComponent<BidChipRow>();
            var so = new SerializedObject(row);
            SerializedProperty chips = so.FindProperty("_chips"), labels = so.FindProperty("_labels");
            chips.arraySize = labels.arraySize = ChipCount;
            for (int i = 0; i < ChipCount; i++)
            {
                chips.GetArrayElementAtIndex(i).objectReferenceValue = ctx.Nodes[$"Chip{i}"].GetComponent<Button>();
                labels.GetArrayElementAtIndex(i).objectReferenceValue = ctx.Nodes[$"ChipLabel{i}"].GetComponent<TMP_Text>();
            }

            so.FindProperty("_selection").objectReferenceValue = ctx.Nodes["ChipSelected"];
            so.FindProperty("_labelColor").colorValue = UiBuildKit.Hex(ctx.Node("ChipLabel1").color);
            so.FindProperty("_selectedLabelColor").colorValue = UiBuildKit.Hex(ctx.Node($"ChipLabel{MockupChoice - 1}").color);
            so.FindProperty("_firstCall").intValue = 1;
            so.FindProperty("_selected").intValue = MockupChoice;
            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_chips"] = row;

            // "Asha is calling…": on the table, where the panel will appear.
            TMP_Text style = ctx.Nodes["Subtitle"].GetComponent<TMP_Text>();
            RectTransform calling = UiBuildKit.CreateAt("CallingText", ctx.Groups["Centre"], ctx.CentreOf("Panel"), UiBuildKit.ScreenCentre, new Vector2(900f, 80f));
            var label = calling.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = style.font;
            label.fontSharedMaterial = style.fontSharedMaterial;
            label.fontSize = style.fontSize * CallingScale;
            label.characterHorizontalScale = style.characterHorizontalScale;
            label.color = style.color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.text = "Asha is calling…";
            calling.gameObject.SetActive(false);
            ctx.Bound["_callingText"] = label;
        }
    }
}
