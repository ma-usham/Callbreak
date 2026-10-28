using System.IO;
using Callbreak.Presentation.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor
{
    /// <summary>
    /// Builds the shared confirmation dialog (GDD §23 popup layer, sorting order 50) from the UI kit
    /// only, as V2 draws it (Figma "V2 · Screens", Dialogs and Overlay · Confirm Dialog): the
    /// 9-sliced plain panel 800 wide; the title in Source Serif 4 Bold 44, crimson, over the
    /// ornament divider; the body in Inter SemiBold 28, brown; and the buttons side by side, the
    /// ivory secondary pill (88 high, Inter Bold 28) left of the crimson primary pill (96 high,
    /// Source Serif 4 Bold 34). DialogView lays them out. The table, or the menu, is dimmed as V2
    /// dims it under its overlays.
    /// </summary>
    public static class DialogBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Overlays/Dialog.prefab";

        private const int SortingOrder = 50;

        // Canvas units.
        private const float PanelWidth = 800f;
        private const float PanelStartHeight = 395f;
        private const float TextWidth = 672f;
        private static readonly Vector2 DividerSize = new Vector2(360f, 16f);
        private static readonly Vector2 ConfirmSize = new Vector2(280f, 96f);
        private static readonly Vector2 CancelSize = new Vector2(200f, 88f);
        private const float PanelShadowSpread = CommonKit.PanelShadowSpread;
        private const float PanelShadowDrop = CommonKit.PanelShadowDrop;

        private const float TitleSize = 44f;
        private const float BodySize = 28f;
        private const float ConfirmLabelSize = 34f;
        private const float CancelLabelSize = 28f;

        // The primary pill's shadow drop (Tools/ArtPipeline/V2/kit.json, "primary").
        private const float PrimaryShadowDrop = 6f;

        // A label in a row too wide for the panel shrinks to no less than this share of its size.
        private const float LabelMinScale = 0.7f;

        private static readonly Color Dim = new Color(0f, 0f, 0f, Screens.TableCustomizer.V2DimOpacity);

        [MenuItem("Callbreak/Build/Dialog")]
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
            Debug.Log($"[DialogBuilder] Built {PrefabPath}");
        }

        private static GameObject BuildHierarchy()
        {
            var root = new GameObject("Dialog", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
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

            // Tapping outside the panel cancels.
            RectTransform backdrop = Child("Backdrop", root.transform);
            Stretch(backdrop);
            Image dim = UiBuildKit.AddImage(backdrop, null, true);
            dim.color = Dim;
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.targetGraphic = dim;

            RectTransform safeArea = Child("SafeArea", root.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            RectTransform panel = Child("Panel", safeArea);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, PanelStartHeight);

            RectTransform panelShadow = Child("Shadow", panel);
            Stretch(panelShadow, new Vector2(-PanelShadowSpread, -PanelShadowSpread - PanelShadowDrop), new Vector2(PanelShadowSpread, PanelShadowSpread - PanelShadowDrop));
            UiBuildKit.AddImage(panelShadow, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Common/UI_Panel_Large_Shadow.png", 0f), false);

            // The face takes raycasts so a tap on the panel never reaches the backdrop.
            RectTransform panelFace = Child("Face", panel);
            Stretch(panelFace);
            UiBuildKit.AddImage(panelFace, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Modal_Menu.png", 0f), true);

            TMP_FontAsset serif = UiBuildKit.LoadFontRole("SerifBold");
            TextMeshProUGUI title = Text("Title", panel, serif, TitleSize, "Leave match?");
            title.color = UiBuildKit.Hex("#8E0E19");

            // The ornament under the title, as the screens draw theirs.
            RectTransform divider = Child("Divider", panel);
            divider.anchorMin = divider.anchorMax = divider.pivot = new Vector2(0.5f, 1f);
            divider.sizeDelta = DividerSize;
            UiBuildKit.AddImage(divider, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Decor/Deco_TitleDivider.png", 0f), false).preserveAspect = true;

            TextMeshProUGUI body = Text("Body", panel, UiBuildKit.LoadFontRole("SansSemiBold"), BodySize, "A bot will take your seat and it counts as a loss.");
            body.color = UiBuildKit.Hex("#6E4F37");
            body.lineSpacing = 8f;

            Button confirm = KitButton(
                "ConfirmButton", panel, ConfirmSize, "UI/Buttons/UI_ButtonPrimary_Normal.png", "UI/Buttons/UI_Shadow_Primary.png", PrimaryShadowDrop, 1f);
            TextMeshProUGUI confirmLabel = ButtonLabel(confirm.transform, serif, ConfirmLabelSize, "Leave Match", UiBuildKit.Hex("#FFFCF7"));
            confirmLabel.fontSharedMaterial = UiBuildKit.LoadShadowPreset(serif, "ButtonLabel", new Color(0.29f, 0.03f, 0.04f, 0.9f), new Vector2(0.25f, -0.35f), 0.2f);

            Button cancel = KitButton("CancelButton", panel, CancelSize, "UI/Buttons/UI_ButtonSecondary_Normal.png", "UI/Buttons/UI_Shadow_Secondary.png", CommonKit.SecondaryShadowDrop, 1f);
            TextMeshProUGUI cancelLabel = ButtonLabel(cancel.transform, UiBuildKit.LoadFontRole("SansBold"), CancelLabelSize, "Cancel", UiBuildKit.Hex("#17140F"));

            var view = root.AddComponent<DialogView>();
            var so = new SerializedObject(view);
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_divider").objectReferenceValue = divider;
            so.FindProperty("_backdrop").objectReferenceValue = backdropButton;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_body").objectReferenceValue = body;
            so.FindProperty("_confirmButton").objectReferenceValue = confirm;
            so.FindProperty("_confirmLabel").objectReferenceValue = confirmLabel;
            so.FindProperty("_cancelButton").objectReferenceValue = cancel;
            so.FindProperty("_cancelLabel").objectReferenceValue = cancelLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            int uiLayer = LayerMask.NameToLayer("UI");
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = uiLayer;
            }

            return root;
        }

        /// <summary>A centred, wrapping text block hung from the top of the panel; DialogView places it.</summary>
        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, string sample)
        {
            RectTransform rect = Child(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(TextWidth, size * 1.3f);
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

        /// <summary>
        /// A kit pill button of any size, hung from the top centre of its parent. The sprite is
        /// sliced only horizontally, so it is scaled as a whole to the height (its woven caps keep
        /// their proportions) and its shadow keeps the same spread relative to it.
        /// </summary>
        public static Button KitButton(string name, Transform parent, Vector2 size, string facePath, string shadowPath, float shadowDrop, float shadowOpacity)
        {
            RectTransform root = Child(name, parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = size;

            Sprite face = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{facePath}", 0f);
            Sprite shadow = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{shadowPath}", 0f);
            Vector2 faceSize = UiBuildKit.NativeSize(face);
            float scale = size.y / faceSize.y;
            Vector2 spread = (UiBuildKit.NativeSize(shadow) - faceSize) / 2f * scale;

            RectTransform shadowRect = Child("Shadow", root);
            Stretch(shadowRect, new Vector2(-spread.x, -spread.y - shadowDrop), new Vector2(spread.x, spread.y - shadowDrop));
            Image shadowImage = UiBuildKit.AddImage(shadowRect, shadow, false);
            shadowImage.pixelsPerUnitMultiplier = 1f / scale;
            shadowImage.color = new Color(1f, 1f, 1f, shadowOpacity);

            RectTransform faceRect = Child("Face", root);
            Stretch(faceRect);
            Image faceImage = UiBuildKit.AddImage(faceRect, face, true);
            faceImage.pixelsPerUnitMultiplier = 1f / scale;

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = faceImage;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var feedback = root.gameObject.AddComponent<ButtonFeedback>();
            var so = new SerializedObject(feedback);
            so.FindProperty("_selectable").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
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

            // DialogView turns auto-sizing on when a long translation's row is too wide.
            label.fontSizeMax = size;
            label.fontSizeMin = size * LabelMinScale;
            label.text = sample;
            return label;
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
