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
    /// would run into the stats column.
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

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);   // "mirror" on the title divider's left line
            BuildNameField(ctx);
            BuildNameMessage(ctx);
            LetPlayerNameShrink(ctx);
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
