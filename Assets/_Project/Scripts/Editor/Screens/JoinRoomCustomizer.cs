using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 03: assembles the <see cref="CodeInput"/> (six boxes, one hidden TMP_InputField over
    /// them, and the kit's focus and error looks of the box) and the error line that replaces the
    /// footer. All six letters take the size fitted on the mockup's "B" and are centred in their
    /// boxes.
    /// </summary>
    public static class JoinRoomCustomizer
    {
        // The box's other two looks (ArtSource/Kit/Controls.map.csv): the same box with a gold
        // outline (GDD §12: focus = gold outline) and with a Heading Burgundy one. Both have the
        // size of the normal box that the layout places, so they are shown in its place.
        private const string FocusBox = "UI/Controls/UI_CodeBox_Focus.png";
        private const string ErrorBox = "UI/Controls/UI_CodeBox_Error.png";

        private const int ReferenceLetter = 1;                                  // the mockup's "B"
        private const float HeaderGap = 20f;                                    // mockup px kept free between the player name and the logo
        private const float SpinnerArc = 0.75f;                                 // the part of the ring that is drawn
        private const float ErrorGap = 10f;                                     // mockup px between the error line and the footer rules
        private static readonly Color ErrorColor = new Color32(0x7A, 0x10, 0x16, 0xFF);  // Heading Burgundy (GDD §12)

        public static void Customize(ScreenBuildContext ctx)
        {
            BuildCodeInput(ctx);
            BuildErrorLabel(ctx);
            BuildSpinner(ctx, "JoinSpinner");
            BoundPlayerName(ctx);
        }

        /// <summary>
        /// The busy spinner (the same white ring as on Create Room): a three-quarter arc, turning,
        /// where the chevron is; hidden until busy.
        /// </summary>
        private static void BuildSpinner(ScreenBuildContext ctx, string id)
        {
            if (!ctx.Nodes.TryGetValue(id, out RectTransform spinner))
            {
                ctx.Warnings.Add($"{id}: node missing; no busy spinner");
                return;
            }

            Image ring = spinner.Find("Face")?.GetComponent<Image>();
            if (ring != null)
            {
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = (int)Image.Origin360.Top;
                ring.fillClockwise = true;
                ring.fillAmount = SpinnerArc;
            }

            spinner.gameObject.AddComponent<Spinner>();
        }

        /// <summary>A long player name ends in an ellipsis before it reaches the logo.</summary>
        private static void BoundPlayerName(ScreenBuildContext ctx)
        {
            if (ctx.Nodes.TryGetValue("PlayerName", out RectTransform name) && ctx.Nodes.ContainsKey("Logo"))
            {
                float room = ctx.Node("Logo").rect[0] - ctx.Node("PlayerName").inkBox[0] - HeaderGap;
                Bound(name.GetComponent<TMP_Text>(), room, 1f);
            }
        }

        private static void BuildCodeInput(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("CodeInput", out RectTransform root))
            {
                ctx.Warnings.Add("CodeInput: node missing");
                return;
            }

            var letters = new TMP_Text[CodeInput.Length];
            var boxes = new Image[CodeInput.Length];
            for (int i = 0; i < CodeInput.Length; i++)
            {
                if (!ctx.Nodes.TryGetValue($"CodeBox{i}", out RectTransform box) || !ctx.Nodes.TryGetValue($"CodeLetter{i}", out RectTransform letter))
                {
                    ctx.Warnings.Add($"CodeInput: CodeBox{i} or CodeLetter{i} missing");
                    return;
                }

                letters[i] = letter.GetComponent<TMP_Text>();
                boxes[i] = box.Find("Face")?.GetComponent<Image>();
                if (boxes[i] == null || boxes[i].sprite == null)
                {
                    ctx.Warnings.Add($"CodeInput: CodeBox{i} has no box sprite");
                    return;
                }
            }

            // The three looks of a box. A missing look falls back to the normal box, so a box never
            // goes blank.
            Sprite normalBox = boxes[0].sprite;
            Sprite focusBox = LoadLook(ctx, FocusBox, normalBox);
            Sprite errorBox = LoadLook(ctx, ErrorBox, normalBox);

            // One size and one baseline for all six, fitted on the reference letter; each character
            // is centred on its box (any character can land in any box).
            TMP_Text reference = letters[ReferenceLetter];
            var referenceRect = (RectTransform)reference.transform;
            float baselineY = referenceRect.anchoredPosition.y;
            for (int i = 0; i < letters.Length; i++)
            {
                var rect = (RectTransform)letters[i].transform;
                rect.pivot = new Vector2(0.5f, referenceRect.pivot.y);
                rect.sizeDelta = referenceRect.sizeDelta;
                rect.anchoredPosition = new Vector2(0f, baselineY);
                letters[i].font = reference.font;
                letters[i].fontSize = reference.fontSize;
                letters[i].characterHorizontalScale = reference.characterHorizontalScale;
                letters[i].characterSpacing = reference.characterSpacing;
                letters[i].alignment = TextAlignmentOptions.Baseline;
                letters[i].color = reference.color;
            }

            TMP_InputField input = AddHiddenInput(ctx, root, reference);
            var codeInput = root.gameObject.AddComponent<CodeInput>();
            var so = new SerializedObject(codeInput);
            so.FindProperty("_input").objectReferenceValue = input;
            SetArray(so.FindProperty("_letters"), letters);
            SetArray(so.FindProperty("_boxes"), boxes);
            so.FindProperty("_normalBox").objectReferenceValue = normalBox;
            so.FindProperty("_focusBox").objectReferenceValue = focusBox;
            so.FindProperty("_errorBox").objectReferenceValue = errorBox;
            so.FindProperty("_shakeTarget").objectReferenceValue = root;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>One of the box's other looks from the kit (its import settings come from KitImporter).</summary>
        private static Sprite LoadLook(ScreenBuildContext ctx, string path, Sprite fallback)
        {
            Sprite look = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{path}", 0f);
            if (look == null)
            {
                ctx.Warnings.Add($"CodeInput: {path} missing; the boxes keep their normal look in that state");
                return fallback;
            }

            return look;
        }

        /// <summary>
        /// A transparent input field over all six boxes: tapping any box focuses it (and opens the
        /// keyboard); its own text and caret are invisible, the boxes show the characters.
        /// </summary>
        private static TMP_InputField AddHiddenInput(ScreenBuildContext ctx, RectTransform root, TMP_Text reference)
        {
            var hiddenObject = new GameObject("HiddenInput", typeof(RectTransform));
            var hidden = (RectTransform)hiddenObject.transform;
            hidden.SetParent(root, false);
            Stretch(hidden);
            var hitArea = hiddenObject.AddComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0f);
            hitArea.raycastTarget = true;

            var viewportObject = new GameObject("TextArea", typeof(RectTransform));
            var viewport = (RectTransform)viewportObject.transform;
            viewport.SetParent(hidden, false);
            Stretch(viewport);
            viewportObject.AddComponent<RectMask2D>();

            var textObject = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textObject.transform;
            textRect.SetParent(viewport, false);
            Stretch(textRect);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = reference.font;
            text.fontSize = 24f;
            text.color = new Color(0f, 0f, 0f, 0f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            text.text = string.Empty;

            var input = hiddenObject.AddComponent<TMP_InputField>();
            var so = new SerializedObject(input);
            so.FindProperty("m_TextViewport").objectReferenceValue = viewport;
            so.FindProperty("m_TextComponent").objectReferenceValue = text;
            so.FindProperty("m_TargetGraphic").objectReferenceValue = hitArea;
            so.FindProperty("m_Transition").enumValueIndex = (int)Selectable.Transition.None;
            so.FindProperty("m_Text").stringValue = string.Empty;
            so.FindProperty("m_CharacterLimit").intValue = CodeInput.Length;
            so.FindProperty("m_LineType").enumValueIndex = (int)TMP_InputField.LineType.SingleLine;
            so.FindProperty("m_ContentType").enumValueIndex = (int)TMP_InputField.ContentType.Standard;
            so.FindProperty("m_RichText").boolValue = false;
            so.FindProperty("m_HideMobileInput").boolValue = true;
            so.FindProperty("m_KeyboardType").enumValueIndex = (int)TouchScreenKeyboardType.ASCIICapable;
            so.FindProperty("m_CustomCaretColor").boolValue = true;
            so.FindProperty("m_CaretColor").colorValue = new Color(0f, 0f, 0f, 0f);
            so.FindProperty("m_SelectionColor").colorValue = new Color(0f, 0f, 0f, 0f);
            so.FindProperty("m_OnFocusSelectAll").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            Navigation navigation = input.navigation;
            navigation.mode = Navigation.Mode.None;
            input.navigation = navigation;
            return input;
        }

        /// <summary>The error line: the footer's fitted style in Heading Burgundy, hidden until ShowError.</summary>
        private static void BuildErrorLabel(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("Footer", out RectTransform footer))
            {
                ctx.Warnings.Add("Footer: node missing; no error line");
                return;
            }

            var errorObject = Object.Instantiate(footer.gameObject, footer.parent, false);
            errorObject.name = "ErrorLabel";
            errorObject.transform.SetSiblingIndex(footer.GetSiblingIndex() + 1);
            var localized = errorObject.GetComponent<LocalizedText>();
            if (localized != null)
            {
                Object.DestroyImmediate(localized);
            }

            var label = errorObject.GetComponent<TMP_Text>();
            label.text = "Room not found";          // design-time sample; the presenter sets the message
            label.enableVertexGradient = false;
            label.color = ErrorColor;

            // The message stays between the two footer rules: it shrinks a little, then ends in an ellipsis.
            if (ctx.Nodes.ContainsKey("FooterLineLeft") && ctx.Nodes.ContainsKey("FooterLineRight"))
            {
                float[] left = ctx.Node("FooterLineLeft").spriteRect, right = ctx.Node("FooterLineRight").spriteRect;
                Bound(label, right[0] - (left[0] + left[2]) - 2f * ErrorGap, 0.75f);
            }

            errorObject.SetActive(false);
            ctx.Bound["_errorLabel"] = label;
        }

        /// <summary>
        /// Caps a dynamic label at a width (mockup px): longer text first shrinks down to
        /// <paramref name="minScale"/> of the fitted size, then ends in an ellipsis. Text that
        /// fits keeps the fitted size, so the mockup sample is unchanged.
        /// </summary>
        private static void Bound(TMP_Text label, float widthMock, float minScale)
        {
            var rect = (RectTransform)label.transform;
            rect.sizeDelta = new Vector2(widthMock * UiBuildKit.ScaleX, rect.sizeDelta.y);
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = label.fontSize * minScale;
            label.enableAutoSizing = minScale < 1f;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
