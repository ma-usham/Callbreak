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
    /// 15 sec pill is the same sprite at the mirrored position (the track is symmetric).
    /// </summary>
    public static class CreateRoomCustomizer
    {
        private const string PlaceholderKey = "ui.create_room.room_name";     // the caption's own key: same words
        private const string PlaceholderText = "Room name";
        private const float PlaceholderAlpha = 0.45f;
        private const int RoomNameMaxLength = 20;   // GDD §11 screen 02: 3-20 characters
        private const float HeaderGap = 20f;        // mockup px kept free between the player name and the logo
        private const float SpinnerArc = 0.75f;     // the part of the ring that is drawn

        public static void Customize(ScreenBuildContext ctx)
        {
            BuildRoomNameField(ctx);
            BuildTurnSelector(ctx);
            BuildSpinner(ctx, "CreateSpinner");
            BoundPlayerName(ctx);
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
