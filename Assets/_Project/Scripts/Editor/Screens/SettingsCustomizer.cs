using System.IO;
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
    /// Screen 11: turns every "toggle" node into a <see cref="ToggleSwitch"/> drawn by the Controls
    /// kit's ON and OFF looks (both fill the same box), assembles the volume <see cref="Slider"/>
    /// from the kit's track, fill and knob, and gives the controls that are lower than the GDD's
    /// 88 px touch target a taller one where the layout has room.
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

            GrowTouchTargets(ctx);
        }

        /// <summary>
        /// GDD: touch targets of at least 88 px, 16 px apart. The switches are drawn 54 px high,
        /// the dropdown 62 and the slider's track 22; each grows away from its neighbour to 88,
        /// and no two touch areas come closer than 16 px. The vibration switch sits between the
        /// dropdown (19 px above it) and the reduce-motion switch (24 px below), so it can only
        /// take the 3 and 8 px those gaps can spare: 65 px.
        /// </summary>
        private static void GrowTouchTargets(ScreenBuildContext ctx)
        {
            ScreenExtras.GrowHit(ctx, "MusicToggle", 33.9f, 0f);
            ScreenExtras.GrowHit(ctx, "SoundEffectsToggle", 0f, 33.9f);
            ScreenExtras.GrowHit(ctx, "VolumeTrack", 33.2f, 33.2f);
            ScreenExtras.GrowHit(ctx, "LanguageButton", 25.6f, 0f);
            ScreenExtras.GrowHit(ctx, "VibrationToggle", 3f, 8f);
            ScreenExtras.GrowHit(ctx, "ReduceMotionToggle", 0f, 33.9f);
        }

        private static void BuildToggle(ScreenBuildContext ctx, LayoutNode node)
        {
            RectTransform root = ctx.Nodes[node.id];
            var face = root.Find("Face") as RectTransform;
            if (face == null || string.IsNullOrEmpty(node.on) || string.IsNullOrEmpty(node.off))
            {
                ctx.Warnings.Add($"{node.id}: toggle needs a face and on/off sprites");
                return;
            }

            Image faceImage = face.GetComponent<Image>();
            bool faceIsOn = faceImage.sprite != null && faceImage.sprite.name == Path.GetFileNameWithoutExtension(node.on);
            Sprite other = LoadLook(ctx, faceIsOn ? node.off : node.on);
            if (other == null)
            {
                ctx.Warnings.Add($"{node.id}: sprite {(faceIsOn ? node.off : node.on)} missing");
                return;
            }

            // The other look is centred where the drawn one is and scaled the way the drawn one is.
            // The kit's two looks have one size, so they fill the same box.
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
        /// and at the mockup's 70% it is exactly where the mockup draws it. The fill starts where
        /// the layout has its left edge (the kit's fill carries a thin rim and starts a little
        /// left of the track) and always ends the same small distance past the knob's centre,
        /// under the knob.
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

            // Fill Area: the knob's travel, the fill's height; the slider anchors the fill 0..value
            // across it, and the fill keeps its two offsets: how far it starts before the travel
            // and how far it ends past the knob's centre.
            RectTransform fillArea = UiBuildKit.CreateAt(
                "Fill Area", root, new Vector2(rootNode.Centre.x, fillNode.Centre.y), rootNode.Centre, UiBuildKit.FromMock(rootNode.rect[2], fillNode.rect[3]));
            fillArea.SetSiblingIndex(fill.GetSiblingIndex());
            fill.SetParent(fillArea, false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(value, 1f);
            fill.pivot = new Vector2(0.5f, 0.5f);
            fill.offsetMin = new Vector2((fillNode.rect[0] - rootNode.rect[0]) * UiBuildKit.ScaleX, 0f);
            fill.offsetMax = new Vector2((fillNode.rect[0] + fillNode.rect[2] - knobNode.Centre.x) * UiBuildKit.ScaleX, 0f);

            // The sliced face follows the fill's rectangle (a sprite cut to a shape keeps the
            // margin its pixel box has round that shape; a kit sprite has none).
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

        /// <summary>
        /// A switch look: a sprite under the art root ("UI/Controls/UI_Toggle_On.png"), or the name
        /// of one of the screen's own sprites.
        /// </summary>
        private static Sprite LoadLook(ScreenBuildContext ctx, string look)
        {
            string file = look.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ? look : look + ".png";
            string path = look.Contains("/") ? $"{UiBuildKit.ArtRoot}/{file}" : $"{UiBuildKit.ArtRoot}/Screens/{ctx.Layout.name}/{file}";
            return UiBuildKit.LoadSpriteAt(path, 0f);
        }
    }
}
