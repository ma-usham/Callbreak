using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 07, the call panel: wires the eight chips, their numbers and the crimson chip of the
    /// chosen call into a <see cref="BidChipRow"/>, and adds what the mockup does not draw: the
    /// "Asha is calling…" line that stands in the middle of the table while the panel is hidden
    /// (it borrows the panel subtitle's type); the line that says why a call was refused, in the
    /// subtitle's place and type, in burgundy; the faint dot of the beginner assist (the kit's
    /// crimson disc, small and half transparent, under the number of the suggested chip); a canvas
    /// group so the panel can fade and slide away; V2's dim over the table behind it; and 88 px
    /// touch areas on the 83 px chips.
    /// </summary>
    public static class BiddingPanelCustomizer
    {
        public const int ChipCount = 8;
        private const int MockupChoice = 4;
        private const float CallingScale = 1.25f;

        // The refusal line: as wide as the panel allows (the panel is 706 mockup px), GDD §12 burgundy.
        private const float ReasonWidth = 640f;
        private const string ReasonColor = "#7A1016";

        // The assist's dot: under the chip's number, above its bottom edge (mockup px from the chip's centre).
        private const float HintDrop = 24f;
        private const float HintSize = 8f;
        private const float HintOpacity = 0.5f;

        // V2's dim over the table while the panel is up (07 Bidding: black at 60%, as the table's other
        // dims), canvas units: it covers the backdrop's 2560 x 1440 frame and the panel's 24-unit slide.
        private const float DimOpacity = TableCustomizer.V2DimOpacity;
        private static readonly Vector2 DimSize = new Vector2(2620f, 1500f);

        public static void Customize(ScreenBuildContext ctx)
        {
            RectTransform panel = ctx.Nodes["Panel"];
            var row = panel.gameObject.AddComponent<BidChipRow>();
            var so = new SerializedObject(row);
            SerializedProperty chips = so.FindProperty("_chips"), labels = so.FindProperty("_labels");
            chips.arraySize = labels.arraySize = ChipCount;
            for (int i = 0; i < ChipCount; i++)
            {
                Button chip = ctx.Nodes[$"Chip{i}"].GetComponent<Button>();
                chips.GetArrayElementAtIndex(i).objectReferenceValue = chip;
                labels.GetArrayElementAtIndex(i).objectReferenceValue = ctx.Nodes[$"ChipLabel{i}"].GetComponent<TMP_Text>();

                // 83 px chips, 15 px apart: each answers to 88 (GDD §16), 10 px clear of the next.
                TableCustomizer.MinimumTouch(chip.targetGraphic, TableCustomizer.MinTouch, TableCustomizer.MinTouch);
            }

            so.FindProperty("_selection").objectReferenceValue = ctx.Nodes["ChipSelected"];
            so.FindProperty("_labelColor").colorValue = UiBuildKit.Hex(ctx.Node("ChipLabel1").color);
            so.FindProperty("_selectedLabelColor").colorValue = UiBuildKit.Hex(ctx.Node($"ChipLabel{MockupChoice - 1}").color);
            so.FindProperty("_firstCall").intValue = 1;
            so.FindProperty("_selected").intValue = MockupChoice;
            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_chips"] = row;

            // The panel leaves as a whole once the call is placed (it fades as it slides).
            ctx.Bound["_panelGroup"] = panel.gameObject.AddComponent<CanvasGroup>();

            BuildDim(ctx, panel);
            BuildReason(ctx);
            BuildHint(ctx, panel);

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

        /// <summary>
        /// "Too late — your turn had ended.": a copy of the subtitle (same type, size and baseline)
        /// that takes its place while a refused call is waiting for another choice.
        /// </summary>
        private static void BuildReason(ScreenBuildContext ctx)
        {
            RectTransform subtitle = ctx.Nodes["Subtitle"];
            GameObject copy = Object.Instantiate(subtitle.gameObject, subtitle.parent);
            copy.name = "Reason";
            foreach (LocalizedText localized in copy.GetComponents<LocalizedText>())
            {
                Object.DestroyImmediate(localized);
            }

            var reason = copy.GetComponent<TextMeshProUGUI>();
            reason.enableVertexGradient = false;
            reason.color = UiBuildKit.Hex(ReasonColor);
            reason.text = "Too late — your turn had ended.";
            reason.raycastTarget = false;
            ctx.Nodes["Reason"] = (RectTransform)copy.transform;
            ScreenExtras.LimitWidth(ctx, "Reason", ReasonWidth);
            copy.SetActive(false);
            ctx.Bound["_reason"] = reason;
        }

        /// <summary>
        /// V2's dim over the table, under the panel's shadow and face: part of the panel, so it comes
        /// and goes, fades and slides with it, and the table is clear again once the call is placed.
        /// It takes no touches, so the HUD's buttons answer as before.
        /// </summary>
        private static void BuildDim(ScreenBuildContext ctx, RectTransform panel)
        {
            RectTransform dim = UiBuildKit.CreateAt("Dim", panel, UiBuildKit.ScreenCentre, ctx.CentreOf("Panel"), DimSize);
            dim.SetAsFirstSibling();
            Image image = dim.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, DimOpacity);
            image.raycastTarget = false;
        }

        /// <summary>The beginner assist's dot, built on the mockup's chosen chip; the view moves it along the row.</summary>
        private static void BuildHint(ScreenBuildContext ctx, RectTransform panel)
        {
            Vector2 chipCentre = ctx.CentreOf($"Chip{MockupChoice - 1}");
            RectTransform hint = UiBuildKit.CreateAt(
                "Hint", panel, chipCentre + new Vector2(0f, HintDrop), ctx.CentreOf("Panel"), UiBuildKit.FromMock(HintSize, HintSize));
            Image dot = UiBuildKit.AddImage(hint, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Disc_Number.png", 0f), false);
            dot.color = new Color(1f, 1f, 1f, HintOpacity);

            // Over the chips, under the crimson chip of the chosen call and the numbers.
            hint.SetSiblingIndex(ctx.Nodes["ChipSelected"].GetSiblingIndex());
            hint.gameObject.SetActive(false);
            ctx.Bound["_hint"] = hint;
        }
    }
}
