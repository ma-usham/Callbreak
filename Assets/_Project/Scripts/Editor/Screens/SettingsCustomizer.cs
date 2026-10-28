using System.IO;
using System.Linq;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 11: turns every "toggle" node into a <see cref="ToggleSwitch"/> drawn by the Controls
    /// kit's ON and OFF looks (both fill the same box), assembles the volume <see cref="Slider"/>
    /// from the kit's track, fill and knob, gives the controls that are lower than the GDD's 88 px
    /// touch target a taller one where the layout has room, and adds the hidden "Privacy options"
    /// button under the panel.
    /// </summary>
    public static class SettingsCustomizer
    {
        private const string PrivacyOptionsKey = "ui.settings.privacy_options";
        private const string PrivacyOptionsText = "Privacy options";
        private const string PrivacyOptionsIcon = "UI/Icons/Icon_Lock.png";

        /// <summary>The privacy button is How to Play's at this size, to fit the band between the panel and the bottom of the screen.</summary>
        private const float PrivacyOptionsScale = 0.68f;

        /// <summary>The lock's height on How to Play's button (mockup px): a little under the book's 52, as it is a solid shape.</summary>
        private const float LockHeight = 48f;

        /// <summary>Mockup px a longer translation keeps from the chevron.</summary>
        private const float MinWordsToChevron = 16f;

        /// <summary>GDD: touch targets of at least 88 px (canvas).</summary>
        private const float TouchTarget = 88f;

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
            BuildPrivacyOptionsButton(ctx);
        }

        /// <summary>
        /// "Privacy options" (Google's consent rules for ads; the owner, 5 Oct 2026). No mockup draws
        /// it, so it is How to Play's button at <see cref="PrivacyOptionsScale"/>: the same ivory
        /// pill, shadow, lettering and chevron, the kit's lock for its icon, and as wide as its words
        /// need. It is centred under the panel, in the band between the panel and the bottom of the
        /// screen (below Done), and its touch target grows to 88 px, still more than 16 px from
        /// Done's. It is built hidden and moves nothing else: the presenter shows it only while the
        /// ad network requires it, and hidden the page is exactly mockup 11. Its words start after
        /// the lock, and a longer translation runs on towards the chevron, then shrinks.
        /// </summary>
        private static void BuildPrivacyOptionsButton(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("HowToPlayButton", out RectTransform source) || !ctx.Nodes.TryGetValue("Panel", out RectTransform panel))
            {
                ctx.Warnings.Add("PrivacyOptionsButton: needs the HowToPlayButton and Panel nodes; no privacy options");
                return;
            }

            Sprite lockSprite = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{PrivacyOptionsIcon}", 0f);
            var root = (RectTransform)Object.Instantiate(source.gameObject, panel.parent, false).transform;
            root.name = "PrivacyOptionsButton";
            root.SetSiblingIndex(panel.GetSiblingIndex() + 1);
            var shadow = root.Find("Shadow") as RectTransform;
            var face = root.Find("Face") as RectTransform;
            var icon = root.Find("HowToPlayIcon") as RectTransform;
            var label = root.Find("HowToPlayLabel") as RectTransform;
            var chevron = root.Find("HowToPlayChevron") as RectTransform;
            var iconFace = icon != null ? icon.Find("Face") as RectTransform : null;
            TMP_Text words = label != null ? label.GetComponent<TMP_Text>() : null;
            LocalizedText localized = label != null ? label.GetComponent<LocalizedText>() : null;
            if (shadow == null || face == null || iconFace == null || words == null || localized == null || chevron == null || lockSprite == null)
            {
                Object.DestroyImmediate(root.gameObject);
                ctx.Warnings.Add("PrivacyOptionsButton: How to Play's button lacks a part, or the lock is missing; no privacy options");
                return;
            }

            icon.name = "PrivacyOptionsIcon";
            label.name = "PrivacyOptionsLabel";
            chevron.name = "PrivacyOptionsChevron";

            // How to Play's measures (mockup px), which the copy keeps from its own ends.
            LayoutNode button = ctx.Node("HowToPlayButton");
            LayoutNode book = ctx.Node("HowToPlayIcon");
            LayoutNode arrow = ctx.Node("HowToPlayChevron");
            float[] bookWords = ctx.Node("HowToPlayLabel").inkBox;
            float iconStart = book.rect[0] - button.rect[0];
            float iconToWords = bookWords[0] - (book.rect[0] + book.rect[2]);
            float chevronToEnd = button.rect[0] + button.rect[2] - (arrow.rect[0] + arrow.rect[2]);
            float wordsToChevron = arrow.rect[0] - bookWords[2];

            // The lock in the book's place, on the same line.
            var lockSize = new Vector2(LockHeight * lockSprite.rect.width / lockSprite.rect.height, LockHeight);
            iconFace.GetComponent<Image>().sprite = lockSprite;
            icon.sizeDelta = UiBuildKit.FromMock(lockSize.x, lockSize.y);
            iconFace.sizeDelta = icon.sizeDelta;
            iconFace.anchoredPosition = Vector2.zero;

            // The words, set as How to Play's are, from their left edge.
            var key = new SerializedObject(localized);
            key.FindProperty("_key").stringValue = PrivacyOptionsKey;
            key.ApplyModifiedPropertiesWithoutUndo();
            words.text = PrivacyOptionsText;
            words.alignment = TextAlignmentOptions.BaselineLeft;
            label.pivot = new Vector2(0f, label.pivot.y);
            if (!UiBuildKit.InkBounds(words, out Rect ink, out _))
            {
                Object.DestroyImmediate(root.gameObject);
                ctx.Warnings.Add("PrivacyOptionsButton: its words have no ink; no privacy options");
                return;
            }

            // As wide as the words need, every part as far from its end as on How to Play.
            float wordsWidth = ink.width / UiBuildKit.ScaleX;
            float width = iconStart + lockSize.x + iconToWords + wordsWidth + wordsToChevron + arrow.rect[2] + chevronToEnd;
            float half = width / 2f;
            float grow = (width - button.rect[2]) * UiBuildKit.ScaleX;
            root.sizeDelta = new Vector2(width * UiBuildKit.ScaleX, root.sizeDelta.y);
            face.sizeDelta += new Vector2(grow, 0f);
            shadow.sizeDelta += new Vector2(grow, 0f);
            icon.anchoredPosition = new Vector2((iconStart + (lockSize.x / 2f) - half) * UiBuildKit.ScaleX, icon.anchoredPosition.y);
            label.anchoredPosition = new Vector2(((iconStart + lockSize.x + iconToWords - half) * UiBuildKit.ScaleX) - ink.xMin, label.anchoredPosition.y);
            chevron.anchoredPosition = new Vector2((half - chevronToEnd - (arrow.rect[2] / 2f)) * UiBuildKit.ScaleX, chevron.anchoredPosition.y);

            // A longer translation runs on towards the chevron, then shrinks, then ends in an ellipsis.
            label.sizeDelta = new Vector2((wordsWidth + wordsToChevron - MinWordsToChevron) * UiBuildKit.ScaleX, label.sizeDelta.y);
            words.enableAutoSizing = true;
            words.fontSizeMax = words.fontSize;
            words.fontSizeMin = words.fontSize * 0.7f;
            words.overflowMode = TextOverflowModes.Ellipsis;

            // Everything smaller about the button's centre. The root keeps its own scale: the press
            // feedback animates it.
            float scale = PrivacyOptionsScale;
            foreach (RectTransform part in root)
            {
                part.localScale = new Vector3(scale, scale, 1f);
                part.anchoredPosition *= scale;
            }

            root.sizeDelta *= scale;

            // Centred under the panel, halfway between its edge and the bottom of the screen.
            LayoutNode panelNode = ctx.Node("Panel");
            var centre = new Vector2(panelNode.Centre.x, (panelNode.rect[1] + panelNode.rect[3] + UiBuildKit.MockHeight) / 2f);
            root.anchoredPosition = new Vector2((centre.x - UiBuildKit.ScreenCentre.x) * UiBuildKit.ScaleX, -(centre.y - UiBuildKit.ScreenCentre.y) * UiBuildKit.ScaleY);

            // GDD: an 88 px touch target. The face is drawn scaled, so its padding is in its own units.
            float drawn = face.sizeDelta.y * scale;
            float pad = Mathf.Max(0f, (TouchTarget - drawn) / 2f) / scale;
            face.GetComponent<Image>().raycastPadding = new Vector4(0f, -pad, 0f, -pad);

            root.gameObject.SetActive(false);
            ctx.Bound["_privacyOptionsButton"] = root.GetComponent<Button>();
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
