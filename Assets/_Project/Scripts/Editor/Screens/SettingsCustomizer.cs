using System.Linq;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 11: turns every "toggle" node into a <see cref="ToggleSwitch"/> drawn by the one ON
    /// and one OFF look cut from the mockup, and assembles the volume <see cref="Slider"/> from its
    /// cut track, crimson fill (9-sliced so it can grow) and knob (with its own shadow).
    /// </summary>
    public static class SettingsCustomizer
    {
        public static void Customize(ScreenBuildContext ctx)
        {
            foreach (LayoutNode node in ctx.Layout.nodes.Where(n => n.kind == "toggle"))
            {
                BuildToggle(ctx, node);
            }

            BuildSlider(ctx);
            ScreenExtras.Apply(ctx);

            // The language's own name must stay left of the dropdown's chevron, and the player's
            // name left of the title.
            ScreenExtras.LimitWidth(ctx, "LanguageValue", 114f);
            ScreenExtras.LimitWidth(ctx, "PlayerName", 330f);
        }

        private static void BuildToggle(ScreenBuildContext ctx, LayoutNode node)
        {
            RectTransform root = ctx.Nodes[node.id];
            var face = root.Find("Face") as RectTransform;
            if (face == null || string.IsNullOrEmpty(node.on) || string.IsNullOrEmpty(node.off))
            {
                ctx.Warnings.Add($"{node.id}: toggle needs a face and on/off sprite names");
                return;
            }

            Image faceImage = face.GetComponent<Image>();
            bool faceIsOn = faceImage.sprite != null && faceImage.sprite.name == node.on;
            Sprite other = LoadScreenSprite(ctx, faceIsOn ? node.off : node.on);
            if (other == null)
            {
                ctx.Warnings.Add($"{node.id}: sprite {(faceIsOn ? node.off : node.on)} missing");
                return;
            }

            // The other look is centred where the drawn one is, scaled the way the drawn one is (the
            // two cuts differ slightly in size: the ON look keeps its thin light rim).
            Vector2 faceNative = UiBuildKit.NativeSize(faceImage.sprite);
            var factor = new Vector2(face.sizeDelta.x / faceNative.x, face.sizeDelta.y / faceNative.y);
            var otherObject = new GameObject(faceIsOn ? "Off" : "On", typeof(RectTransform));
            var otherRect = (RectTransform)otherObject.transform;
            otherRect.SetParent(root, false);
            otherRect.anchorMin = face.anchorMin;
            otherRect.anchorMax = face.anchorMax;
            otherRect.pivot = face.pivot;
            otherRect.anchoredPosition = face.anchoredPosition;
            otherRect.sizeDelta = Vector2.Scale(UiBuildKit.NativeSize(other), factor);
            UiBuildKit.AddImage(otherRect, other, true);
            faceImage.raycastTarget = true;
            face.name = faceIsOn ? "On" : "Off";

            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = faceImage;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            var feedback = root.gameObject.AddComponent<ButtonFeedback>();
            var feedbackSo = new SerializedObject(feedback);
            feedbackSo.FindProperty("_selectable").objectReferenceValue = button;
            feedbackSo.FindProperty("_cue").intValue = (int)SoundCue.Toggle;
            feedbackSo.ApplyModifiedPropertiesWithoutUndo();

            // Added while the node is inactive, so the switch is wired before it is first enabled
            // (when someone has the Editor in Play mode, AddComponent enables it at once).
            root.gameObject.SetActive(false);
            var toggle = root.gameObject.AddComponent<ToggleSwitch>();
            var so = new SerializedObject(toggle);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_onGraphic").objectReferenceValue = faceIsOn ? face.gameObject : otherObject;
            so.FindProperty("_offGraphic").objectReferenceValue = faceIsOn ? otherObject : face.gameObject;
            so.FindProperty("_isOn").boolValue = faceIsOn;
            so.ApplyModifiedPropertiesWithoutUndo();
            otherObject.SetActive(false);       // the prefab shows the state the mockup draws
            root.gameObject.SetActive(true);
        }

        /// <summary>
        /// VolumeSlider spans the knob's travel: at 0 the knob's centre is on the track's left end,
        /// and at the mockup's 70% it is exactly where the mockup draws it. The fill runs from the
        /// track's left end to the knob's centre.
        /// </summary>
        private static void BuildSlider(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("VolumeSlider", out RectTransform root) ||
                !ctx.Nodes.TryGetValue("VolumeFill", out RectTransform fill) ||
                !ctx.Nodes.TryGetValue("VolumeKnob", out RectTransform knob))
            {
                ctx.Warnings.Add("VolumeSlider: needs VolumeSlider, VolumeFill and VolumeKnob nodes");
                return;
            }

            LayoutNode rootNode = ctx.Node("VolumeSlider");
            LayoutNode fillNode = ctx.Node("VolumeFill");
            LayoutNode knobNode = ctx.Node("VolumeKnob");
            const float value = 0.7f;

            // Fill Area: the slider's full width, the fill's height; the fill is anchored 0..value.
            RectTransform fillArea = UiBuildKit.CreateAt(
                "Fill Area", root, new Vector2(rootNode.Centre.x, fillNode.Centre.y), rootNode.Centre, UiBuildKit.FromMock(rootNode.rect[2], fillNode.rect[3]));
            fillArea.SetSiblingIndex(fill.GetSiblingIndex());
            fill.SetParent(fillArea, false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(value, 1f);
            fill.pivot = new Vector2(0.5f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            // The sliced face follows the fill's rectangle; the sprite's pixel box is a little
            // larger than the pill it was cut to, so keep that margin.
            var fillFace = fill.Find("Face") as RectTransform;
            if (fillFace != null)
            {
                float[] r = fillNode.rect, s = fillNode.spriteRect ?? fillNode.rect;
                fillFace.anchorMin = Vector2.zero;
                fillFace.anchorMax = Vector2.one;
                fillFace.offsetMin = new Vector2((s[0] - r[0]) * UiBuildKit.ScaleX, -((s[1] + s[3]) - (r[1] + r[3])) * UiBuildKit.ScaleY);
                fillFace.offsetMax = new Vector2(((s[0] + s[2]) - (r[0] + r[2])) * UiBuildKit.ScaleX, (r[1] - s[1]) * UiBuildKit.ScaleY);
            }

            // Handle Slide Area: the slider's full rectangle; the knob is anchored at the value.
            RectTransform handleArea = UiBuildKit.CreateAt(
                "Handle Slide Area", root, rootNode.Centre, rootNode.Centre, UiBuildKit.FromMock(rootNode.rect[2], rootNode.rect[3]));
            handleArea.SetSiblingIndex(knob.GetSiblingIndex());
            knob.SetParent(handleArea, false);
            knob.anchorMin = new Vector2(value, 0f);
            knob.anchorMax = new Vector2(value, 1f);
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.anchoredPosition = new Vector2(0f, (rootNode.Centre.y - knobNode.Centre.y) * UiBuildKit.ScaleY);
            knob.sizeDelta = new Vector2(knobNode.rect[2] * UiBuildKit.ScaleX, (knobNode.rect[3] - rootNode.rect[3]) * UiBuildKit.ScaleY);

            Image knobFace = knob.Find("Face")?.GetComponent<Image>();
            var slider = root.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fill;
            slider.handleRect = knob;
            slider.targetGraphic = knobFace;
            ColorBlock colors = slider.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            colors.fadeDuration = 0.08f;
            slider.colors = colors;
            Navigation navigation = slider.navigation;
            navigation.mode = Navigation.Mode.None;
            slider.navigation = navigation;
            slider.SetValueWithoutNotify(value);
        }

        private static Sprite LoadScreenSprite(ScreenBuildContext ctx, string name)
        {
            return UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/Screens/{ctx.Layout.name}/{name}.png", 0f);
        }
    }
}
