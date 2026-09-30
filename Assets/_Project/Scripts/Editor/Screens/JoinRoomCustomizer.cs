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
    /// them, a focus look and an error look per box) and the error line that replaces the footer.
    /// All six letters take the size fitted on the mockup's "B" and are centred in their boxes.
    /// </summary>
    public static class JoinRoomCustomizer
    {
        private const int ReferenceLetter = 1;                                  // the mockup's "B"
        private const float FocusRimGrow = 1.2f;                                // mockup px around the box
        private const float ErrorRimGrow = 2f;                                  // mockup px around the box
        private const float HeaderGap = 20f;                                    // mockup px kept free between the player name and the logo
        private const float SpinnerArc = 0.75f;                                 // the part of the ring that is drawn
        private const float ErrorGap = 10f;                                     // mockup px between the error line and the footer rules
        private static readonly Color FocusRim = new Color32(0x8C, 0x6C, 0x53, 0xFF);   // the boxes' own outline brown
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
            var focusLooks = new GameObject[CodeInput.Length];
            var errorLooks = new GameObject[CodeInput.Length];
            for (int i = 0; i < CodeInput.Length; i++)
            {
                if (!ctx.Nodes.TryGetValue($"CodeBox{i}", out RectTransform box) || !ctx.Nodes.TryGetValue($"CodeLetter{i}", out RectTransform letter))
                {
                    ctx.Warnings.Add($"CodeInput: CodeBox{i} or CodeLetter{i} missing");
                    return;
                }

                letters[i] = letter.GetComponent<TMP_Text>();
                focusLooks[i] = AddRim(ctx, box, $"CodeBox{i}", "FocusRim", FocusRim, FocusRimGrow);
                errorLooks[i] = AddRim(ctx, box, $"CodeBox{i}", "ErrorRim", ErrorColor, ErrorRimGrow);
            }

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
            SetArray(so.FindProperty("_focusLooks"), focusLooks);
            SetArray(so.FindProperty("_errorLooks"), errorLooks);
            so.FindProperty("_shakeTarget").objectReferenceValue = root;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A rim look: the box's own sprite in one colour and a little larger, behind the box, so
        /// that colour shows around it. The focus look uses the boxes' outline brown (the rim reads
        /// thicker) and shows on the next box to fill; the error look uses Heading Burgundy and
        /// shows on all six. The mockup shows neither state. Hidden by default.
        /// </summary>
        private static GameObject AddRim(ScreenBuildContext ctx, RectTransform box, string id, string name, Color color, float grow)
        {
            var face = box.Find("Face") as RectTransform;
            Image faceImage = face != null ? face.GetComponent<Image>() : null;
            if (faceImage == null)
            {
                ctx.Warnings.Add($"{id}: no face for {name}");
                return null;
            }

            var rimObject = new GameObject(name, typeof(RectTransform));
            var rim = (RectTransform)rimObject.transform;
            rim.SetParent(box, false);
            rim.anchorMin = face.anchorMin;
            rim.anchorMax = face.anchorMax;
            rim.pivot = face.pivot;
            rim.anchoredPosition = face.anchoredPosition;
            rim.sizeDelta = face.sizeDelta + UiBuildKit.FromMock(2f * grow, 2f * grow);
            rim.SetSiblingIndex(face.GetSiblingIndex());
            Image image = UiBuildKit.AddImage(rim, faceImage.sprite, false);
            image.color = color;
            rimObject.SetActive(false);
            return rimObject;
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
