using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 02: turns the room-name field into a <see cref="TMP_InputField"/> (the fitted label
    /// is its text, a copy of it in a dimmed colour its placeholder) and the 15 sec | 30 sec control
    /// into a <see cref="SelectableGroup"/>. The mockup draws the crimson pill on 30 sec only; the
    /// 15 sec pill is the same sprite at the mirrored position (the track is symmetric). It also
    /// adds the error line that takes the "Private room" line's place when creating a room fails.
    /// </summary>
    public static class CreateRoomCustomizer
    {
        private const string PlaceholderKey = "ui.create_room.room_name";     // the caption's own key: same words
        private const string PlaceholderText = "Room name";
        private const float PlaceholderAlpha = 0.45f;
        private const int RoomNameMaxLength = 20;   // GDD §11 screen 02: 3-20 characters
        private const float HeaderGap = 20f;        // mockup px kept free between the player name and the logo
        private const float SpinnerArc = 0.75f;     // the part of the ring that is drawn
        private const float ErrorInset = 12f;       // mockup px kept free between the error line and the button's ends
        private const float ChevronGap = 16f;       // mockup px kept free between the button's words and its chevron
        private static readonly Color ErrorColor = new Color32(0xA3, 0x12, 0x1F, 0xFF);   // V2's error crimson ("Messages & Errors")

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);   // "mirror" on the title divider's left line
            BuildRoomNameField(ctx);
            BuildTurnSelector(ctx);
            BuildSpinner(ctx, "CreateSpinner");
            BuildErrorLabel(ctx);
            BoundPlayerName(ctx);
            BoundCreateLabel(ctx);
        }

        /// <summary>
        /// The error line (GDD §11 screen 02: "inline message with Retry"): the "Private room"
        /// note's fitted style in Heading Burgundy, on the note's baseline, centred on the button
        /// below it and no wider than it. Hidden until ShowError, which hides the lock and the note.
        /// </summary>
        private static void BuildErrorLabel(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("PrivateNote", out RectTransform note) || !ctx.Nodes.ContainsKey("CreateButton"))
            {
                ctx.Warnings.Add("PrivateNote: needs the PrivateNote and CreateButton nodes; no error line");
                return;
            }

            var errorObject = Object.Instantiate(note.gameObject, note.parent, false);
            errorObject.name = "ErrorLabel";
            errorObject.transform.SetSiblingIndex(note.GetSiblingIndex() + 1);
            var localized = errorObject.GetComponent<LocalizedText>();
            if (localized != null)
            {
                Object.DestroyImmediate(localized);
            }

            var label = errorObject.GetComponent<TMP_Text>();
            label.text = "Could not create the room";       // design-time sample; the presenter sets the message
            label.enableVertexGradient = false;
            label.color = ErrorColor;
            label.alignment = TextAlignmentOptions.Baseline;

            // The note is set from its left edge, after the lock; the message is centred instead.
            LayoutNode noteNode = ctx.Node("PrivateNote");
            LayoutNode button = ctx.Node("CreateButton");
            float parentCentreX = !string.IsNullOrEmpty(noteNode.parent) && ctx.Nodes.ContainsKey(noteNode.parent)
                ? ctx.Node(noteNode.parent).Centre.x
                : UiBuildKit.ScreenCentre.x;
            var rect = (RectTransform)errorObject.transform;
            rect.pivot = new Vector2(0.5f, rect.pivot.y);
            rect.anchoredPosition = new Vector2((button.Centre.x - parentCentreX) * UiBuildKit.ScaleX, rect.anchoredPosition.y);
            Bound(label, button.rect[2] - 2f * ErrorInset, 0.75f);

            errorObject.SetActive(false);
            ctx.Bound["_errorLabel"] = label;
        }

        /// <summary>
        /// The button's words change (Retry while an error shows) and differ by language: they
        /// shrink a little, then end in an ellipsis, before they reach the chevron.
        /// </summary>
        private static void BoundCreateLabel(ScreenBuildContext ctx)
        {
            if (ctx.Nodes.TryGetValue("CreateButtonLabel", out RectTransform label) && ctx.Nodes.ContainsKey("CreateChevron"))
            {
                float[] ink = ctx.Node("CreateButtonLabel").inkBox;
                float centre = (ink[0] + ink[2]) / 2f;
                Bound(label.GetComponent<TMP_Text>(), 2f * (ctx.Node("CreateChevron").rect[0] - ChevronGap - centre), 0.7f);
            }
        }

        /// <summary>
        /// The busy spinner: the white ring drawn as a three-quarter arc, turning (GDD §11 screen 02:
        /// "Create shows a spinner on the button"). It sits where the chevron is, hidden until busy.
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

        private static void BuildRoomNameField(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("RoomNameField", out RectTransform field) ||
                !ctx.Nodes.TryGetValue("RoomNameText", out RectTransform textRect))
            {
                ctx.Warnings.Add("RoomNameField: needs RoomNameField and RoomNameText nodes");
                return;
            }

            LayoutNode fieldNode = ctx.Node("RoomNameField");
            LayoutNode textNode = ctx.Node("RoomNameText");
            var text = textRect.GetComponent<TMP_Text>();

            // Viewport: from just left of the text's first glyph to the field's inner right edge,
            // the field's inner height. It clips long names and the caret.
            float left = textNode.inkBox[0] - 4f;
            float right = fieldNode.rect[0] + fieldNode.rect[2] - 12f;
            float top = fieldNode.rect[1] + 4f;
            float bottom = fieldNode.rect[1] + fieldNode.rect[3] - 4f;
            RectTransform viewport = UiBuildKit.CreateAt(
                "TextArea", field, new Vector2((left + right) / 2f, (top + bottom) / 2f), fieldNode.Centre, UiBuildKit.FromMock(right - left, bottom - top));
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.SetSiblingIndex(textRect.GetSiblingIndex());
            textRect.SetParent(viewport, true);
            UiBuildKit.FillTextArea(viewport, text);

            // Placeholder: the same fitted style, dimmed, shown only while the field is empty.
            var placeholderObject = Object.Instantiate(textRect.gameObject, viewport, true);
            placeholderObject.name = "Placeholder";
            placeholderObject.transform.SetSiblingIndex(textRect.GetSiblingIndex());
            var placeholder = placeholderObject.GetComponent<TMP_Text>();
            placeholder.text = PlaceholderText;
            Color dim = text.color;
            dim.a *= PlaceholderAlpha;
            placeholder.color = dim;
            placeholder.enabled = false;
            var localized = placeholderObject.AddComponent<LocalizedText>();
            var localizedSo = new SerializedObject(localized);
            localizedSo.FindProperty("_key").stringValue = PlaceholderKey;
            localizedSo.ApplyModifiedPropertiesWithoutUndo();

            var input = field.gameObject.AddComponent<TMP_InputField>();
            Image face = field.Find("Face")?.GetComponent<Image>();
            if (face != null)
            {
                face.raycastTarget = true;
            }

            var so = new SerializedObject(input);
            so.FindProperty("m_TextViewport").objectReferenceValue = viewport;
            so.FindProperty("m_TextComponent").objectReferenceValue = text;
            so.FindProperty("m_Placeholder").objectReferenceValue = placeholder;
            so.FindProperty("m_TargetGraphic").objectReferenceValue = face;
            so.FindProperty("m_Transition").enumValueIndex = (int)Selectable.Transition.None;
            so.FindProperty("m_Text").stringValue = text.text;
            so.FindProperty("m_CharacterLimit").intValue = RoomNameMaxLength;
            so.FindProperty("m_LineType").enumValueIndex = (int)TMP_InputField.LineType.SingleLine;
            so.FindProperty("m_ContentType").enumValueIndex = (int)TMP_InputField.ContentType.Standard;
            so.FindProperty("m_RichText").boolValue = false;

            // Typed in place, not in the keyboard's own box: only then does a phone draw the field's
            // caret. The screen rises above the keyboard (CreateRoomView), so the field stays in sight.
            so.FindProperty("m_HideMobileInput").boolValue = true;
            so.FindProperty("m_CaretColor").colorValue = new Color(text.color.r, text.color.g, text.color.b, 1f);
            so.FindProperty("m_CustomCaretColor").boolValue = true;
            so.FindProperty("m_CaretWidth").intValue = 2;
            so.FindProperty("m_SelectionColor").colorValue = new Color(0.875f, 0.784f, 0.682f, 0.75f);   // Warm Sand #DFC8AE
            so.FindProperty("m_OnFocusSelectAll").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            Navigation navigation = input.navigation;
            navigation.mode = Navigation.Mode.None;
            input.navigation = navigation;
        }

        private static void BuildTurnSelector(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("TurnTrack", out RectTransform track) ||
                !ctx.Nodes.TryGetValue("TurnSelected30", out RectTransform selected30) ||
                !ctx.Nodes.TryGetValue("TurnOption15", out RectTransform option15) ||
                !ctx.Nodes.TryGetValue("TurnOption30", out RectTransform option30) ||
                !ctx.Nodes.TryGetValue("TurnLabel15", out RectTransform label15) ||
                !ctx.Nodes.TryGetValue("TurnLabel30", out RectTransform label30))
            {
                ctx.Warnings.Add("TurnTrack: needs TurnTrack, TurnSelected30, TurnOption15/30 and TurnLabel15/30 nodes");
                return;
            }

            // The 15 sec pill: the 30 sec one mirrored about the track's centre (same sprite).
            LayoutNode trackNode = ctx.Node("TurnTrack");
            LayoutNode pillNode = ctx.Node("TurnSelected30");
            float mirroredCentreX = 2f * trackNode.Centre.x - pillNode.Centre.x;
            var selected15Object = Object.Instantiate(selected30.gameObject, track, false);
            selected15Object.name = "TurnSelected15";
            var selected15 = (RectTransform)selected15Object.transform;
            selected15.anchoredPosition = selected30.anchoredPosition + new Vector2((mirroredCentreX - pillNode.Centre.x) * UiBuildKit.ScaleX, 0f);
            selected15.SetSiblingIndex(selected30.GetSiblingIndex());
            selected15Object.SetActive(false);

            var text15 = label15.GetComponent<TMP_Text>();
            var text30 = label30.GetComponent<TMP_Text>();
            var group = track.gameObject.AddComponent<SelectableGroup>();
            var so = new SerializedObject(group);
            SetArray(so.FindProperty("_options"), option15.GetComponent<Button>(), option30.GetComponent<Button>());
            SetArray(so.FindProperty("_selectedLooks"), selected15Object, selected30.gameObject);
            SetArray(so.FindProperty("_labels"), text15, text30);
            so.FindProperty("_selectedLabelColor").colorValue = text30.color;
            so.FindProperty("_labelColor").colorValue = text15.color;
            so.FindProperty("_selectedIndex").intValue = 1;     // 30 sec, the GDD default and what the mockup shows
            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_turnSelector"] = group;

            foreach (RectTransform option in new[] { option15, option30 })
            {
                var button = option.GetComponent<Button>();
                if (button == null)
                {
                    continue;
                }

                button.transition = Selectable.Transition.None;
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
            }
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

        private static void SetArray(SerializedProperty property, params Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
