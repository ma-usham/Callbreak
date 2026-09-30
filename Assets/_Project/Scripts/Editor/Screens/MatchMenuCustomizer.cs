using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 13, the match menu: adds the dim between the table and the panel (the whole screen on
    /// every aspect ratio; it also keeps taps off the table), turns the Sound switch into a
    /// <see cref="ToggleSwitch"/> drawn by the Settings screen's ON and OFF looks, and makes every
    /// control answer to at least 88 px (GDD §16) without drawing it any larger.
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
        }

        /// <summary>
        /// Resume (79 px high) and the Scorecard button (85) grow upwards: above Resume there is only
        /// the mode tag, and the Scorecard button then keeps 16 px from Resume's area. Leave (74)
        /// grows downwards, where there is only the "paused" note. The switch (85 × 48) grows more
        /// down than up, so that it too keeps 16 px from the Scorecard button. The close × is 87.
        /// </summary>
        private static void GrowTouchAreas(ScreenBuildContext ctx)
        {
            const float min = TableCustomizer.MinTouch;
            TableCustomizer.MinimumTouch(TargetOf(ctx, "CloseButton"), min, min);
            TableCustomizer.MinimumTouch(TargetOf(ctx, "ResumeButton"), 0f, min, 1f);
            TableCustomizer.MinimumTouch(TargetOf(ctx, "ScorecardButton"), 0f, min, 1f);
            TableCustomizer.MinimumTouch(TargetOf(ctx, "LeaveButton"), 0f, min, 0f);
            foreach (Image look in ctx.Nodes["SoundToggle"].GetComponentsInChildren<Image>(true))
            {
                TableCustomizer.MinimumTouch(look, min, min, 0.38f);
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

            // The mockup draws it ON. The OFF look is centred on it and scaled the same way (the two
            // cuts differ by a pixel or two: the ON look keeps its light rim).
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
