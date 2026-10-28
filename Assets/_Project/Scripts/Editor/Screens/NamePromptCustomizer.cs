using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// The name screen at the start (ADR 022, ADR 043), built from the owner's mockup 22: the Profile
    /// screen's title, panel, field and Save, with the player's portrait and a grid of the six to
    /// choose from. As the owner asked, the name field sits at the top of the panel, so the keyboard
    /// never hides it. The screen built from the layout becomes the <see cref="NamePromptView"/>, on
    /// a canvas of its own above the menu and its dialogs; a transparent blocker under its content
    /// takes every touch. Built here, not measured on the mockup: the name field with its
    /// placeholder; the big portrait in its gold ring; and the grid, three over three, each portrait
    /// in its gold ring, the chosen one's ring crimson with V2's check badge (V2, 22 Name Entry).
    /// </summary>
    public static class NamePromptCustomizer
    {
        /// <summary>Above the dialog (50) and below the toast (60), as the name question always was.</summary>
        private const int SortingOrder = 55;

        /// <summary>GDD §11 screen 14: names are 3-16 characters.</summary>
        private const int NameCharacterLimit = 16;

        private const string PlaceholderKey = "ui.name_prompt.placeholder";
        private const string PlaceholderText = "Enter your name";
        private const float PlaceholderAlpha = 0.45f;

        /// <summary>TextMeshPro's channels (TexCoord1, Normal, Tangent); set here so the text never has to change the canvas.</summary>
        private const AdditionalCanvasShaderChannels TextChannels =
            AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

        // The big portrait, mockup pixels: where V2 draws it above the name (22 Name Entry, "Portrait":
        // 220 canvas units wide with its gold ring).
        private static readonly Vector2 PortraitCentre = new Vector2(576.5f, 475.7f);
        private const float PortraitSize = 191.6f;

        // The grid, mockup pixels, as V2 lays out its characters: portraits 132 canvas units wide,
        // 170 apart, rows 162 apart (each choice a little larger than its portrait, which fills
        // AvatarPickerBuilder.FramedPictureShare of it), centred under the header.
        private const float GridCentreX = 1023.2f;
        private const float GridTop = 426.8f;
        private const float GridCell = 122.3f;
        private const float GridGap = 25.8f;
        private const float GridRowGap = 18.8f;
        private const int GridColumns = 3;

        /// <summary>Warm Sand #DFC8AE (GDD §12 palette) behind selected text, as on the Profile screen.</summary>
        private static readonly Color SelectionColor = new Color(0.875f, 0.784f, 0.682f, 0.75f);

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);   // "mirror" on the left-hand ornament lines
            AddCanvas(ctx);
            AddTouchBlocker(ctx);
            BuildNameField(ctx);
            BuildPortraits(ctx);

            ctx.Bound["_content"] = ctx.Content;
            ctx.Bound["_message"] = ctx.Nodes["NameMessage"].GetComponent<TMP_Text>();
            ctx.ViewType = typeof(NamePromptView);
        }

        /// <summary>
        /// A canvas of its own over the menu (written into the serialized canvas: a nested canvas
        /// keeps World Space and order 0 while the screen is built under the temporary one), and a
        /// group to fade, hidden until the view shows it.
        /// </summary>
        private static void AddCanvas(ScreenBuildContext ctx)
        {
            GameObject root = ctx.Root.gameObject;
            var canvas = root.AddComponent<Canvas>();
            var settings = new SerializedObject(canvas);
            settings.FindProperty("m_RenderMode").intValue = (int)RenderMode.ScreenSpaceOverlay;
            settings.FindProperty("m_SortingOrder").intValue = SortingOrder;
            settings.FindProperty("m_AdditionalShaderChannelsFlag").intValue = (int)TextChannels;
            settings.ApplyModifiedPropertiesWithoutUndo();
            SceneSetup.ConfigureScaler(root.AddComponent<CanvasScaler>());
            root.AddComponent<GraphicRaycaster>();
            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        /// <summary>A fully transparent image over the whole screen, just above the background: nothing under the screen can be tapped.</summary>
        private static void AddTouchBlocker(ScreenBuildContext ctx)
        {
            var blocker = new GameObject("TouchBlocker", typeof(RectTransform));
            var rect = (RectTransform)blocker.transform;
            rect.SetParent(ctx.Root, false);
            rect.SetSiblingIndex(ctx.Background != null ? ctx.Background.GetSiblingIndex() + 1 : 0);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = blocker.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
        }

        /// <summary>
        /// The display-name <see cref="TMP_InputField"/> over the Profile screen's field art: a
        /// clipped text area from just left of the fitted text to the field's inner right edge, the
        /// text itself empty, and a dimmed copy of it saying "Enter your name" while it is empty.
        /// Typed in place, so a phone draws the caret.
        /// </summary>
        private static void BuildNameField(ScreenBuildContext ctx)
        {
            RectTransform field = ctx.Nodes["NameField"];
            RectTransform textRect = ctx.Nodes["FieldText"];
            LayoutNode fieldNode = ctx.Node("NameField");
            LayoutNode textNode = ctx.Node("FieldText");
            var text = textRect.GetComponent<TMP_Text>();

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

            var placeholderObject = Object.Instantiate(textRect.gameObject, viewport, true);
            placeholderObject.name = "Placeholder";
            placeholderObject.transform.SetSiblingIndex(textRect.GetSiblingIndex());
            var placeholder = placeholderObject.GetComponent<TMP_Text>();
            placeholder.text = PlaceholderText;
            Color dim = text.color;
            dim.a *= PlaceholderAlpha;
            placeholder.color = dim;
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
            so.FindProperty("m_Text").stringValue = string.Empty;
            so.FindProperty("m_CharacterLimit").intValue = NameCharacterLimit;
            so.FindProperty("m_LineType").enumValueIndex = (int)TMP_InputField.LineType.SingleLine;
            so.FindProperty("m_ContentType").enumValueIndex = (int)TMP_InputField.ContentType.Standard;
            so.FindProperty("m_RichText").boolValue = false;

            // Typed in place, not in the keyboard's own box: only then does a phone draw the field's
            // caret (the owner, 3 Oct 2026). The keyboard's Done only puts the keyboard away (ADR 043).
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

            // The component rewrote the labels while it was being wired; put them back.
            text.richText = false;
            text.text = string.Empty;
            placeholder.text = PlaceholderText;
            ctx.Bound["_field"] = input;
        }

        /// <summary>The big portrait left of the divider, and the grid of the six right of it, both on the panel.</summary>
        private static void BuildPortraits(ScreenBuildContext ctx)
        {
            RectTransform panel = ctx.Nodes["Panel"];
            LayoutNode panelNode = ctx.Node("Panel");
            Vector2 panelCentre = panelNode.Centre;

            Vector2 offset = new Vector2((PortraitCentre.x - panelCentre.x) * UiBuildKit.ScaleX, -(PortraitCentre.y - panelCentre.y) * UiBuildKit.ScaleY);
            Image portrait = AvatarPickerBuilder.BuildFramedPortrait(panel, "Portrait", PortraitSize * UiBuildKit.ScaleX, offset);
            ctx.Bound["_avatar"] = portrait;

            AvatarPicker grid = AvatarPickerBuilder.BuildFramedGrid(
                panel, "Avatars", GridCell * UiBuildKit.ScaleX, GridGap * UiBuildKit.ScaleX, GridRowGap * UiBuildKit.ScaleY, GridColumns);
            ((RectTransform)grid.transform).anchoredPosition = new Vector2(
                (GridCentreX - panelCentre.x) * UiBuildKit.ScaleX, -(GridTop - panelNode.rect[1]) * UiBuildKit.ScaleY);
            ctx.Bound["_avatars"] = grid;
        }
    }
}
