using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 14: builds the display-name <see cref="TMP_InputField"/> over the field art cut from
    /// the mockup, with the fitted "Player" label as its text (so the text sits exactly where the
    /// mockup has it), and gives the note line under it its <see cref="ProfileNameMessage"/>. The big
    /// name under the avatar may shrink: the mockup shows "Player", a 16-character name at that size
    /// would run into the stats column. The avatar picker (ADR 041), which no mockup draws, is built
    /// from the kit over the whole screen, in the name question's style.
    /// </summary>
    public static class ProfileCustomizer
    {
        /// <summary>GDD §11 screen 14: names are 3-16 characters.</summary>
        private const int NameCharacterLimit = 16;

        // The viewport that clips long names and the caret, in mockup pixels: from just left of
        // the first glyph to the field's inner right edge, inside its top and bottom rim.
        private const float ViewportLeftPad = 4f;
        private const float ViewportRightInset = 12f;
        private const float ViewportInsetY = 4f;

        /// <summary>Gap kept between the big name and the stats divider, mockup pixels.</summary>
        private const float NameSideMargin = 10f;

        /// <summary>A long name shrinks to no less than this share of the mockup's size.</summary>
        private const float MinNameScale = 0.5f;

        /// <summary>Validation messages use Heading Burgundy (GDD §12 palette); the mockup shows no error state.</summary>
        private const string ErrorColor = "#7A1016";

        /// <summary>Warm Sand #DFC8AE (GDD §12 palette) behind selected text.</summary>
        private static readonly Color SelectionColor = new Color(0.875f, 0.784f, 0.682f, 0.75f);

        // The avatar picker's panel, canvas pixels: the dialog's look, wide enough for six
        // 112-wide portraits 20 apart (772) inside its margins.
        private const float PickerPanelWidth = 912f;
        private const float PickerPaddingTop = 54f;
        private const float PickerPaddingBottom = 58f;
        private const float PickerTitleSize = 56f;
        private const float PickerTitleGap = 34f;
        private const float PickerCell = 112f;
        private const float PickerSpacing = 20f;
        private const float PickerButtonGap = 40f;
        private const float PickerDoneWidth = 400f;
        private const float PickerDoneHeight = 89.7f;
        private const float PickerDoneLabelSize = 40f;
        private const float PickerShadowSpread = CommonKit.PanelShadowSpread;
        private const float PickerShadowDrop = CommonKit.PanelShadowDrop;

        /// <summary>The name question's dim.</summary>
        private static readonly Color PickerDim = new Color(0f, 0f, 0f, 0.817f);

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);   // "mirror" on the title divider's left line
            BuildNameField(ctx);
            BuildNameMessage(ctx);
            LetPlayerNameShrink(ctx);
            BuildAvatarPicker(ctx);
        }

        /// <summary>
        /// ADR 041: over the whole screen, the dim (a tap on it closes the picker) and the modal
        /// panel with the serif title, the six portraits (<see cref="AvatarPickerBuilder"/>) and a
        /// crimson Done. Hidden until the pencil or the avatar opens it.
        /// </summary>
        private static void BuildAvatarPicker(ScreenBuildContext ctx)
        {
            RectTransform sheet = Child("AvatarSheet", ctx.Root);
            Stretch(sheet, Vector2.zero, Vector2.zero);
            sheet.SetAsLastSibling();
            var group = sheet.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            RectTransform backdrop = Child("Backdrop", sheet);
            Stretch(backdrop, Vector2.zero, Vector2.zero);
            Image dim = UiBuildKit.AddImage(backdrop, null, true);
            dim.color = PickerDim;
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = dim;
            backdropButton.transition = Selectable.Transition.None;
            Navigation navigation = backdropButton.navigation;
            navigation.mode = Navigation.Mode.None;
            backdropButton.navigation = navigation;

            RectTransform panel = Child("Panel", sheet);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);

            RectTransform shadow = Child("Shadow", panel);
            Stretch(shadow, new Vector2(-PickerShadowSpread, -PickerShadowSpread - PickerShadowDrop), new Vector2(PickerShadowSpread, PickerShadowSpread - PickerShadowDrop));
            UiBuildKit.AddImage(shadow, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Common/UI_Panel_Large_Shadow.png", 0f), false);

            // The panel takes the taps on it, so only a tap beside it closes the picker.
            RectTransform face = Child("Face", panel);
            Stretch(face, Vector2.zero, Vector2.zero);
            UiBuildKit.AddImage(face, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Modal_Menu.png", 0f), true);

            TMP_FontAsset serif = UiBuildKit.LoadFontRole("SerifBold");
            float y = PickerPaddingTop;
            float titleHeight = PickerTitleSize * 1.3f;
            RectTransform titleRect = Child("Title", panel);
            titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(PickerPanelWidth - 2f * 60f, titleHeight);
            titleRect.anchoredPosition = new Vector2(0f, -y);
            var title = titleRect.gameObject.AddComponent<TextMeshProUGUI>();
            title.font = serif;
            title.fontSize = PickerTitleSize;
            title.alignment = TextAlignmentOptions.Top;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Overflow;
            title.enableAutoSizing = true;
            title.fontSizeMax = PickerTitleSize;
            title.fontSizeMin = PickerTitleSize * 0.6f;
            title.raycastTarget = false;
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UiBuildKit.Hex("#8E0A17"), UiBuildKit.Hex("#8E0A17"), UiBuildKit.Hex("#750A14"), UiBuildKit.Hex("#750A14"));
            title.color = Color.white;
            title.text = "Choose your avatar";
            Localize(title, "ui.profile.choose_avatar");
            y += titleHeight + PickerTitleGap;

            AvatarPicker picker = AvatarPickerBuilder.Build(panel, "Avatars", PickerCell, PickerSpacing);
            ((RectTransform)picker.transform).anchoredPosition = new Vector2(0f, -y);
            y += PickerCell + PickerButtonGap;

            Button done = DialogBuilder.KitButton(
                "AvatarDoneButton", panel, new Vector2(PickerDoneWidth, PickerDoneHeight),
                "UI/Controls/UI_Button_Crimson_Box.png", "UI/Controls/UI_Button_Crimson_Box_Shadow.png", CommonKit.HeroShadowDrop, 1f);
            ((RectTransform)done.transform).anchoredPosition = new Vector2(0f, -y);
            y += PickerDoneHeight;
            RectTransform labelRect = Child("Label", done.transform);
            Stretch(labelRect, Vector2.zero, Vector2.zero);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = serif;
            label.fontSize = PickerDoneLabelSize;
            label.color = UiBuildKit.Hex("#FEFCFC");
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.fontSharedMaterial = UiBuildKit.LoadShadowPreset(serif, "ButtonLabel", new Color(0.29f, 0.03f, 0.04f, 0.9f), new Vector2(0.25f, -0.35f), 0.2f);
            label.text = "Done";
            Localize(label, "ui.profile.avatar_done");

            panel.sizeDelta = new Vector2(PickerPanelWidth, y + PickerPaddingBottom);
            sheet.gameObject.SetActive(false);

            ctx.Bound["_avatarSheet"] = group;
            ctx.Bound["_avatarPanel"] = panel;
            ctx.Bound["_avatarPicker"] = picker;
            ctx.Bound["_avatarDone"] = done;
            ctx.Bound["_avatarBackdrop"] = backdropButton;
        }

        private static void Localize(TMP_Text label, string key)
        {
            var localized = label.gameObject.AddComponent<LocalizedText>();
            var so = new SerializedObject(localized);
            so.FindProperty("_key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void BuildNameField(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("NameField", out RectTransform field) ||
                !ctx.Nodes.TryGetValue("FieldText", out RectTransform textRect))
            {
                ctx.Warnings.Add("NameField: needs NameField and FieldText nodes");
                return;
            }

            LayoutNode fieldNode = ctx.Node("NameField");
            LayoutNode textNode = ctx.Node("FieldText");
            var text = textRect.GetComponent<TMP_Text>();
            string sample = text.text;   // read before the input field exists: it rewrites its label

            float left = textNode.inkBox[0] - ViewportLeftPad;
            float right = fieldNode.rect[0] + fieldNode.rect[2] - ViewportRightInset;
            float top = fieldNode.rect[1] + ViewportInsetY;
            float bottom = fieldNode.rect[1] + fieldNode.rect[3] - ViewportInsetY;
            RectTransform viewport = UiBuildKit.CreateAt(
                "TextArea", field, new Vector2((left + right) / 2f, (top + bottom) / 2f), fieldNode.Centre, UiBuildKit.FromMock(right - left, bottom - top));
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.SetSiblingIndex(textRect.GetSiblingIndex());
            textRect.SetParent(viewport, true);
            UiBuildKit.FillTextArea(viewport, text);

            Image face = field.Find("Face")?.GetComponent<Image>();
            if (face != null)
            {
                face.raycastTarget = true;
            }

            // A tap on the field while the name has to stay (ADR 041) nudges the line under it.
            ctx.Bound["_nameFieldTap"] = field.gameObject.AddComponent<TapRelay>();

            var input = field.gameObject.AddComponent<TMP_InputField>();
            var so = new SerializedObject(input);
            so.FindProperty("m_TextViewport").objectReferenceValue = viewport;
            so.FindProperty("m_TextComponent").objectReferenceValue = text;
            so.FindProperty("m_TargetGraphic").objectReferenceValue = face;
            so.FindProperty("m_Transition").enumValueIndex = (int)Selectable.Transition.None;
            so.FindProperty("m_Text").stringValue = sample;
            so.FindProperty("m_CharacterLimit").intValue = NameCharacterLimit;
            so.FindProperty("m_LineType").enumValueIndex = (int)TMP_InputField.LineType.SingleLine;
            so.FindProperty("m_ContentType").enumValueIndex = (int)TMP_InputField.ContentType.Standard;
            so.FindProperty("m_RichText").boolValue = false;

            // Typed in place, not in the keyboard's own box: only then does a phone draw the field's
            // caret. The screen rises above the keyboard (ProfileView), so the field stays in sight.
            so.FindProperty("m_HideMobileInput").boolValue = true;
            so.FindProperty("m_CaretColor").colorValue = new Color(text.color.r, text.color.g, text.color.b, 1f);
            so.FindProperty("m_CustomCaretColor").boolValue = true;
            so.FindProperty("m_CaretWidth").intValue = 2;
            so.FindProperty("m_SelectionColor").colorValue = SelectionColor;
            so.FindProperty("m_OnFocusSelectAll").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            Navigation navigation = input.navigation;
            navigation.mode = Navigation.Mode.None;
            input.navigation = navigation;

            // The component rewrote the label while it was being wired; restore the fitted text.
            text.richText = false;
            text.text = sample;
            ctx.Bound["_nameField"] = input;
        }

        /// <summary>The name keeps the mockup's size while it fits between the panel edge and the stats divider, then shrinks.</summary>
        private static void LetPlayerNameShrink(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("PlayerName", out RectTransform rect) || !ctx.Nodes.ContainsKey("StatsDivider"))
            {
                ctx.Warnings.Add("PlayerName: needs PlayerName and StatsDivider nodes");
                return;
            }

            float[] ink = ctx.Node("PlayerName").inkBox;
            float centre = (ink[0] + ink[2]) / 2f;
            float halfWidth = ctx.CentreOf("StatsDivider").x - NameSideMargin - centre;
            rect.sizeDelta = new Vector2(UiBuildKit.FromMock(2f * halfWidth, 0f).x, rect.sizeDelta.y);

            var label = rect.GetComponent<TMP_Text>();
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = label.fontSize * MinNameScale;
            label.enableAutoSizing = true;
        }

        private static void BuildNameMessage(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("NameMessage", out RectTransform noteRect))
            {
                ctx.Warnings.Add("NameMessage: node missing");
                return;
            }

            var note = noteRect.GetComponent<TMP_Text>();
            var message = note.gameObject.AddComponent<ProfileNameMessage>();
            var so = new SerializedObject(message);
            so.FindProperty("_label").objectReferenceValue = note;
            so.FindProperty("_noteColor").colorValue = note.color;
            so.FindProperty("_errorColor").colorValue = UiBuildKit.Hex(ErrorColor);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
