using System.Linq;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 23, Play Online (mockups 23 and 24): what the layout alone does not make.
    /// <list type="bullet">
    /// <item>The stake chips become one <see cref="SelectableGroup"/>: each chip's crimson look shows
    /// on the chosen one and its ivory look on the others, the amount ivory on crimson and dark on
    /// ivory (the colours measured on 24). 100's coin and amount sit halfway between where 24 and 23
    /// draw them (23's ivory 100 chip is narrower).</item>
    /// <item>The prize popover grows out of its pointer (its pivot is the pointer's tip) and fades as
    /// a whole. The pointer is drawn over the box (its sprite carries a stretch of the box's top edge),
    /// and its shadow under the box, with the box's own. Under every control of the screen lies an
    /// invisible catcher, shown only while the popover is open, that closes it on a tap anywhere else.</item>
    /// <item>Touch targets of at least 88 px for the chips, the "i" and the popover's ×, and widths
    /// that the dynamic labels shrink into rather than run past.</item>
    /// </list>
    /// </summary>
    public static class PlayOnlineCustomizer
    {
        /// <summary>23 draws 100's amount this far right of 24 (mockup pixels); it sits halfway.</summary>
        private const float Chip1Shift = 1.1f;

        /// <summary>The pointer's tip, under the "i"'s centre, where the popover grows from (mockup pixels).</summary>
        private static readonly Vector2 PointerTip = new Vector2(1250.5f, 543.0f);

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);
            BuildStakeSelector(ctx);
            BuildPopover(ctx);

            ScreenExtras.UnifyStyle(ctx, new[] { "Place0", "Place1", "Place2", "Place3" });
            ScreenExtras.UnifyStyle(ctx, new[] { "PrizeAmount0", "PrizeAmount1", "PrizeAmount2", "PrizeAmount3" });

            // Canvas pixels: chips 82 tall grow to 88; the "i" (61) to 88 without reaching the 100
            // chip; the × to 88.
            ScreenExtras.GrowHit(ctx, "StakeChip0", 3f, 3f);
            ScreenExtras.GrowHit(ctx, "StakeChip1", 3f, 3f);
            Pad(ctx, "InfoButton", left: 4f, right: 14f, up: 14f, down: 14f);
            Pad(ctx, "CloseButton", left: 17f, right: 4f, up: 6f, down: 17f);

            // Longer amounts or translations shrink inside their places.
            ScreenExtras.LimitWidth(ctx, "Balance", 80f);
            ScreenExtras.LimitWidth(ctx, "PlayStakeLabel", 290f);
            ScreenExtras.LimitWidth(ctx, "PlayQuickLabel", 270f);
            ScreenExtras.LimitWidth(ctx, "PrizeHeading", 205f);
            for (int i = 0; i < 4; i++)
            {
                ScreenExtras.LimitWidth(ctx, $"PrizeAmount{i}", 70f);
            }
        }

        /// <summary>
        /// The chips as one choice: options, the two looks of each, and the amounts' colours. The
        /// chip 24 draws chosen (100) is chosen as built; the presenter chooses at run time.
        /// </summary>
        private static void BuildStakeSelector(ScreenBuildContext ctx)
        {
            TMP_Text dark = Flatten(ctx, "ChipLabel0");
            TMP_Text light = Flatten(ctx, "ChipLabel1");
            if (dark == null || light == null)
            {
                return;
            }

            ctx.Nodes["ChipLabel1"].anchoredPosition += new Vector2(Chip1Shift * UiBuildKit.ScaleX, 0f);
            ScreenExtras.LimitWidth(ctx, "ChipLabel0", 80f);
            ScreenExtras.LimitWidth(ctx, "ChipLabel1", 80f);

            var group = ctx.Nodes["Panel"].gameObject.AddComponent<SelectableGroup>();
            var so = new SerializedObject(group);
            SetArray(so.FindProperty("_options"), ButtonOf(ctx, "StakeChip0"), ButtonOf(ctx, "StakeChip1"));
            SetArray(so.FindProperty("_selectedLooks"), ctx.Nodes["ChipSelected0"].gameObject, ctx.Nodes["ChipSelected1"].gameObject);
            SetArray(so.FindProperty("_unselectedLooks"), ctx.Nodes["ChipNormal0"].gameObject, ctx.Nodes["ChipNormal1"].gameObject);
            SetArray(so.FindProperty("_labels"), dark, light);
            so.FindProperty("_selectedLabelColor").colorValue = light.color;
            so.FindProperty("_labelColor").colorValue = dark.color;
            so.FindProperty("_selectedIndex").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_stakeSelector"] = group;
        }

        /// <summary>One flat colour (the measured mean), so the selection can recolour the label.</summary>
        private static TMP_Text Flatten(ScreenBuildContext ctx, string id)
        {
            if (!ctx.Nodes.TryGetValue(id, out RectTransform rect) || !rect.TryGetComponent(out TextMeshProUGUI label))
            {
                ctx.Warnings.Add($"{id}: the stake chips need their amount labels");
                return null;
            }

            label.enableVertexGradient = false;
            label.color = UiBuildKit.Hex(ctx.Node(id).color);
            return label;
        }

        /// <summary>
        /// The popover: pivot on the pointer's tip (it grows from there; nothing moves), its own
        /// CanvasGroup for the fade, the pointer's shadow moved under the box (next to the box's own
        /// shadow), and the catcher under the screen's controls that closes it.
        /// </summary>
        private static void BuildPopover(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("Popover", out RectTransform popover))
            {
                ctx.Warnings.Add("Popover: missing");
                return;
            }

            float[] r = ctx.Node("Popover").rect;
            var pivot = new Vector2((PointerTip.x - r[0]) / r[2], (r[1] + r[3] - PointerTip.y) / r[3]);
            Vector2 size = popover.rect.size;
            popover.anchoredPosition += Vector2.Scale(pivot - popover.pivot, size);
            popover.pivot = pivot;
            if (popover.GetComponent<CanvasGroup>() == null)
            {
                popover.gameObject.AddComponent<CanvasGroup>();
            }

            Transform boxShadow = popover.Find("Shadow");
            Transform pointerShadow = ctx.Nodes.TryGetValue("PopoverPointer", out RectTransform pointer) ? pointer.Find("Shadow") : null;
            if (boxShadow != null && pointerShadow != null)
            {
                pointerShadow.SetParent(popover, true);
                pointerShadow.name = "PointerShadow";
                pointerShadow.SetSiblingIndex(boxShadow.GetSiblingIndex() + 1);
            }
            else
            {
                ctx.Warnings.Add("PopoverPointer: its shadow and the box's are needed to draw it under the box");
            }

            // Under everything else on the screen (the first thing in the content), over the whole
            // screen; invisible, so it only answers taps. No tap sound: it is not a control.
            var catcher = new GameObject("PopoverDismiss", typeof(RectTransform));
            var rect = (RectTransform)catcher.transform;
            rect.SetParent(ctx.Content, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = catcher.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            var button = catcher.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            catcher.SetActive(false);
            ctx.Nodes["PopoverDismiss"] = rect;
        }

        /// <summary>Touch padding beyond what a control draws, canvas pixels on each side.</summary>
        private static void Pad(ScreenBuildContext ctx, string id, float left, float right, float up, float down)
        {
            if (!ctx.Nodes.TryGetValue(id, out RectTransform rect))
            {
                ctx.Warnings.Add($"{id}: no node to pad");
                return;
            }

            foreach (Graphic graphic in rect.GetComponents<Graphic>().Concat(rect.Cast<Transform>().Select(t => t.GetComponent<Graphic>())))
            {
                if (graphic != null && graphic.raycastTarget)
                {
                    graphic.raycastPadding = new Vector4(-left, -down, -right, -up);
                }
            }
        }

        private static Button ButtonOf(ScreenBuildContext ctx, string id) => ctx.Nodes[id].GetComponent<Button>();

        private static void SetArray(SerializedProperty property, params Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
