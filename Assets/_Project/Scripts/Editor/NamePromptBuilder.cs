using System.IO;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor
{
    /// <summary>
    /// Builds the name question asked when the game starts (ADR 022) from the UI kit only, in the
    /// shared dialog's style (<see cref="DialogBuilder"/>): the same dim, modal panel, serif crimson
    /// title and crimson button, with the avatar presets to pick from (ADR 041) and the light field
    /// of the Profile screen between them and a line for the note or the reason a name cannot be
    /// used. No mockup draws it. Its canvas sits above the dialog's, so a dialog that opens at the
    /// same time waits underneath.
    /// </summary>
    public static class NamePromptBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Overlays/NamePrompt.prefab";

        private const int SortingOrder = 55;

        // Canvas pixels, from the dialog (panel 652 wide, buttons 514 wide).
        private const float PanelWidth = 652f;
        private const float ContentWidth = 514f;
        private const float PaddingTop = 58f;
        private const float PaddingBottom = 60f;
        private const float TitleSize = 60f;
        private const float FieldHeight = 72f;
        private const float FieldTextSize = 32f;
        private const float MessageSize = 26f;
        private const float ConfirmHeight = 89.7f;
        private const float ConfirmLabelSize = 40f;
        private const float TitleGap = 30f;
        private const float MessageGap = 12f;

        // The five avatar presets fill the content width: 5 x 90 + 4 x 16 = 514.
        private const float AvatarCell = 90f;
        private const float AvatarSpacing = 16f;
        private const float AvatarGap = 26f;
        private const float ButtonGap = 30f;
        private const float PanelShadowSpread = 27.5f;
        private const float PanelShadowDrop = 3.5f;

        /// <summary>GDD §11 screen 14: names are 3-16 characters.</summary>
        private const int NameCharacterLimit = 16;

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.817f);
        private static readonly Color Ink = UiBuildKit.Hex("#2B2420");
        private static readonly Color PlaceholderInk = UiBuildKit.Hex("#A08A78");
        private static readonly Color NoteInk = UiBuildKit.Hex("#7A593F");
        private static readonly Color ErrorInk = UiBuildKit.Hex("#7A1016");

        /// <summary>Warm Sand #DFC8AE (GDD §12 palette) behind selected text, as on the Profile screen.</summary>
        private static readonly Color SelectionColor = new Color(0.875f, 0.784f, 0.682f, 0.75f);

        [MenuItem("Callbreak/Build/Name Prompt")]
        public static void Build()
        {
            GameObject root = BuildHierarchy();
            try
            {
                UiBuildKit.EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[NamePromptBuilder] Built {PrefabPath}");
        }

        private static GameObject BuildHierarchy()
        {
            var root = new GameObject("NamePrompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiBuildKit.CanvasWidth, UiBuildKit.CanvasHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            // The dim takes every tap outside the panel: the question cannot be dismissed.
            RectTransform backdrop = Child("Backdrop", root.transform);
            Stretch(backdrop);
            Image dim = UiBuildKit.AddImage(backdrop, null, true);
            dim.color = Dim;

            RectTransform safeArea = Child("SafeArea", root.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            RectTransform panel = Child("Panel", safeArea);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);

            RectTransform panelShadow = Child("Shadow", panel);
            Stretch(panelShadow, new Vector2(-PanelShadowSpread, -PanelShadowSpread - PanelShadowDrop), new Vector2(PanelShadowSpread, PanelShadowSpread - PanelShadowDrop));
            UiBuildKit.AddImage(panelShadow, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Common/UI_Panel_Large_Shadow.png", 0f), false);

            RectTransform panelFace = Child("Face", panel);
            Stretch(panelFace);
            UiBuildKit.AddImage(panelFace, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Modal_Menu.png", 0f), true);

            TMP_FontAsset serif = UiBuildKit.LoadFontRole("SerifBold");
            float y = PaddingTop;

            // Two lines at this size, as the dialog's longer titles; the view fits the panel to the text at runtime.
            TextMeshProUGUI title = Text("Title", panel, serif, TitleSize, "What's your name?", ref y, TitleSize * 2.5f);
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UiBuildKit.Hex("#8E0A17"), UiBuildKit.Hex("#8E0A17"), UiBuildKit.Hex("#750A14"), UiBuildKit.Hex("#750A14"));
            title.color = Color.white;
            Localize(title, "ui.name_prompt.title");

            y += TitleGap;
            AvatarPicker avatars = AvatarPickerBuilder.Build(panel, "Avatars", AvatarCell, AvatarSpacing);
            ((RectTransform)avatars.transform).anchoredPosition = new Vector2(0f, -y);
            y += AvatarCell + AvatarGap;
            TMP_InputField field = Field(panel, ref y);

            y += MessageGap;
            TextMeshProUGUI message = Text("Message", panel, UiBuildKit.LoadFontRole("SansRegular"), MessageSize, "Your name is visible at the table.", ref y, MessageSize * 1.4f);
            message.color = NoteInk;

            y += ButtonGap;
            Button confirm = DialogBuilder.KitButton(
                "ContinueButton", panel, new Vector2(ContentWidth, ConfirmHeight),
                "UI/Controls/UI_Button_Crimson_Box.png", "UI/Controls/UI_Button_Crimson_Box_Shadow.png", 1.7f, 0.82f);
            ((RectTransform)confirm.transform).anchoredPosition = new Vector2(0f, -y);
            y += ConfirmHeight;
            TextMeshProUGUI confirmLabel = ButtonLabel(confirm.transform, serif, ConfirmLabelSize, "Continue", UiBuildKit.Hex("#FEFCFC"));
            confirmLabel.fontSharedMaterial = UiBuildKit.LoadShadowPreset(serif, "ButtonLabel", new Color(0.29f, 0.03f, 0.04f, 0.9f), new Vector2(0.25f, -0.35f), 0.2f);
            Localize(confirmLabel, "ui.name_prompt.confirm");

            panel.sizeDelta = new Vector2(PanelWidth, y + PaddingBottom);

            var view = root.AddComponent<NamePromptView>();
            var so = new SerializedObject(view);
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_avatars").objectReferenceValue = avatars;
            so.FindProperty("_field").objectReferenceValue = field;
            so.FindProperty("_message").objectReferenceValue = message;
            so.FindProperty("_continueButton").objectReferenceValue = confirm;
            so.FindProperty("_noteColor").colorValue = NoteInk;
            so.FindProperty("_errorColor").colorValue = ErrorInk;
            so.FindProperty("_paddingTop").floatValue = PaddingTop;
            so.FindProperty("_paddingBottom").floatValue = PaddingBottom;
            so.FindProperty("_titleGap").floatValue = TitleGap;
            so.FindProperty("_avatarGap").floatValue = AvatarGap;
            so.FindProperty("_messageGap").floatValue = MessageGap;
            so.FindProperty("_buttonGap").floatValue = ButtonGap;
            so.ApplyModifiedPropertiesWithoutUndo();

            int uiLayer = LayerMask.NameToLayer("UI");
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = uiLayer;
            }

            return root;
        }

        /// <summary>
        /// The Profile screen's light field (9-sliced), a clipped text area inside its rim, the
        /// placeholder and the typed text, left-aligned and vertically centred.
        /// </summary>
        private static TMP_InputField Field(RectTransform panel, ref float y)
        {
            RectTransform root = Child("NameField", panel);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(ContentWidth, FieldHeight);
            root.anchoredPosition = new Vector2(0f, -y);
            y += FieldHeight;

            RectTransform faceRect = Child("Face", root);
            Stretch(faceRect);
            Image face = UiBuildKit.AddImage(faceRect, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Field_Light.png", 0f), true);

            // The kit sprite's 36 px rim is drawn at half size, so a 72 px field keeps it round.
            face.pixelsPerUnitMultiplier = 2f;

            RectTransform area = Child("TextArea", root);
            Stretch(area, new Vector2(26f, 6f), new Vector2(-26f, -6f));
            area.gameObject.AddComponent<RectMask2D>();

            TMP_FontAsset sans = UiBuildKit.LoadFontRole("SansBold");
            TextMeshProUGUI placeholder = FieldText("Placeholder", area, UiBuildKit.LoadFontRole("SansRegular"), PlaceholderInk, "Enter your name");
            Localize(placeholder, "ui.name_prompt.placeholder");
            TextMeshProUGUI text = FieldText("Text", area, sans, Ink, string.Empty);

            var input = root.gameObject.AddComponent<TMP_InputField>();
            var so = new SerializedObject(input);
            so.FindProperty("m_TextViewport").objectReferenceValue = area;
            so.FindProperty("m_TextComponent").objectReferenceValue = text;
            so.FindProperty("m_Placeholder").objectReferenceValue = placeholder;
            so.FindProperty("m_TargetGraphic").objectReferenceValue = face;
            so.FindProperty("m_Transition").enumValueIndex = (int)Selectable.Transition.None;
            so.FindProperty("m_Text").stringValue = string.Empty;
            so.FindProperty("m_CharacterLimit").intValue = NameCharacterLimit;
            so.FindProperty("m_LineType").enumValueIndex = (int)TMP_InputField.LineType.SingleLine;
            so.FindProperty("m_ContentType").enumValueIndex = (int)TMP_InputField.ContentType.Standard;
            so.FindProperty("m_RichText").boolValue = false;

            // Typed in place, not in the keyboard's own box: only then does a phone draw the field's
            // caret (the owner, 3 Oct 2026: no cursor showed). The panel rises above the keyboard.
            so.FindProperty("m_HideMobileInput").boolValue = true;
            so.FindProperty("m_CaretColor").colorValue = Ink;
            so.FindProperty("m_CustomCaretColor").boolValue = true;
            so.FindProperty("m_CaretWidth").intValue = 2;
            so.FindProperty("m_SelectionColor").colorValue = SelectionColor;
            so.FindProperty("m_OnFocusSelectAll").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            Navigation navigation = input.navigation;
            navigation.mode = Navigation.Mode.None;
            input.navigation = navigation;

            // The component rewrote the labels while it was being wired; put them back.
            placeholder.text = "Enter your name";
            text.text = string.Empty;
            return input;
        }

        private static TextMeshProUGUI FieldText(string name, RectTransform area, TMP_FontAsset font, Color color, string sample)
        {
            RectTransform rect = Child(name, area);
            Stretch(rect);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = FieldTextSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.richText = false;
            label.text = sample;
            return label;
        }

        /// <summary>A centred, wrapping text block hung from the top of the panel at <paramref name="y"/>.</summary>
        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, string sample, ref float y, float height)
        {
            RectTransform rect = Child(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(ContentWidth, height);
            rect.anchoredPosition = new Vector2(0f, -y);
            y += height;
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Top;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.text = sample;
            return label;
        }

        private static TextMeshProUGUI ButtonLabel(Transform button, TMP_FontAsset font, float size, string sample, Color color)
        {
            RectTransform rect = Child("Label", button);
            Stretch(rect);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.text = sample;
            return label;
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
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect) => Stretch(rect, Vector2.zero, Vector2.zero);

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
