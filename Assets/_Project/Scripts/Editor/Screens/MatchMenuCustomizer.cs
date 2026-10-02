using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 13, the match menu: adds the dim between the table and the panel (the whole screen on
    /// every aspect ratio; it also keeps taps off the table), turns the Sound switch into a
    /// <see cref="ToggleSwitch"/> drawn by the Controls kit's ON and OFF looks, makes every
    /// control answer to at least 88 px (GDD §16) without drawing it any larger, and readies the
    /// mode tag to hug its words.
    /// </summary>
    public static class MatchMenuCustomizer
    {
        // Mockup 13 is mockup 08 at 44.5% of its sRGB values: black at 83% when blended in linear light.
        private const float DimOpacity = 0.83f;

        public static void Customize(ScreenBuildContext ctx)
        {
            var dimObject = new GameObject("Dim", typeof(RectTransform));
            var dim = (RectTransform)dimObject.transform;
            dim.SetParent(ctx.Content, false);
            dim.SetAsFirstSibling();
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = Vector2.zero;
            dim.offsetMax = Vector2.zero;
            var image = dimObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, DimOpacity);
            image.raycastTarget = true;

            BuildToggle(ctx, ctx.Node("SoundToggle"));
            GrowTouchAreas(ctx);

            // The mode tag hugs its words (the owner, 3 Oct 2026): MatchMenuView sets its width.
            TableCustomizer.HugWords(ctx.Nodes["ModePill"], ctx.Nodes["ModeLabel"].GetComponent<TMP_Text>());
        }

        /// <summary>
        /// Resume (the crimson box, 90 px high) needs nothing. The Scorecard button (85) grows
        /// downwards: the box above it is only 14 px away. Leave (74) grows downwards, where there is
        /// only the "paused" note. The switch (92 × 52) grows more down than up, so that it keeps
        /// 16 px from the Scorecard button; below it, it faces Leave over an 8 px wide strip only.
        /// The close × is 87.
        /// </summary>
        private static void GrowTouchAreas(ScreenBuildContext ctx)
        {
            const float min = TableCustomizer.MinTouch;
            TableCustomizer.MinimumTouch(TargetOf(ctx, "CloseButton"), min, min);
            TableCustomizer.MinimumTouch(TargetOf(ctx, "ResumeButton"), 0f, min, 1f);
            TableCustomizer.MinimumTouch(TargetOf(ctx, "ScorecardButton"), 0f, min, 0f);
            TableCustomizer.MinimumTouch(TargetOf(ctx, "LeaveButton"), 0f, min, 0f);
            foreach (Image look in ctx.Nodes["SoundToggle"].GetComponentsInChildren<Image>(true))
            {
                TableCustomizer.MinimumTouch(look, min, min, 0.29f);
            }
        }

        private static Graphic TargetOf(ScreenBuildContext ctx, string buttonId)
        {
            Button button = ctx.Nodes[buttonId].GetComponent<Button>();
            if (button == null || button.targetGraphic == null)
            {
                ctx.Warnings.Add($"{buttonId}: no button to give a touch area");
                return null;
            }

            return button.targetGraphic;
        }

        private static void BuildToggle(ScreenBuildContext ctx, LayoutNode node)
        {
            RectTransform root = ctx.Nodes[node.id];
            var face = root.Find("Face") as RectTransform;
            Sprite on = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{node.on}.png", 0f);
            Sprite off = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{node.off}.png", 0f);
            if (face == null || on == null || off == null)
            {
                ctx.Warnings.Add($"{node.id}: toggle needs a face and its on/off sprites");
                return;
            }

            // The mockup draws it ON. The kit's OFF look fills the same box as its ON look, so it takes
            // ON's place, scaled the same way.
            Image faceImage = face.GetComponent<Image>();
            faceImage.raycastTarget = true;
            face.name = "On";
            Vector2 native = UiBuildKit.NativeSize(on);
            var factor = new Vector2(face.sizeDelta.x / native.x, face.sizeDelta.y / native.y);
            var offObject = new GameObject("Off", typeof(RectTransform));
            var offRect = (RectTransform)offObject.transform;
            offRect.SetParent(root, false);
            offRect.anchorMin = face.anchorMin;
            offRect.anchorMax = face.anchorMax;
            offRect.pivot = face.pivot;
            offRect.anchoredPosition = face.anchoredPosition;
            offRect.sizeDelta = Vector2.Scale(UiBuildKit.NativeSize(off), factor);
            UiBuildKit.AddImage(offRect, off, true);

            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = faceImage;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            var feedback = root.gameObject.AddComponent<ButtonFeedback>();
            var feedbackSo = new SerializedObject(feedback);
            feedbackSo.FindProperty("_selectable").objectReferenceValue = button;
            feedbackSo.ApplyModifiedPropertiesWithoutUndo();

            var toggle = root.gameObject.AddComponent<ToggleSwitch>();
            var so = new SerializedObject(toggle);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_onGraphic").objectReferenceValue = face.gameObject;
            so.FindProperty("_offGraphic").objectReferenceValue = offObject;
            so.FindProperty("_isOn").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            offObject.SetActive(false);
        }
    }
}
