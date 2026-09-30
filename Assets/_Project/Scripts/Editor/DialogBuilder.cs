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
    /// only: the 9-sliced large panel, the crimson primary button and the ivory secondary button.
    /// Its proportions and type follow the Game Menu panel of mockup 13 (panel 567 wide, buttons
    /// 448 x 69, serif title, stacked buttons), which is the only modal the mockups show; the dim
    /// behind it is the one measured there (the table drops to 46% brightness).
    /// </summary>
    public static class DialogBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Overlays/Dialog.prefab";

        private const int SortingOrder = 50;

        // Canvas pixels (mockup pixels x 1920 / 1672).
        private const float PanelWidth = 652f;
        private const float PanelStartHeight = 470f;
        private const float TextWidth = 540f;
        private const float ButtonWidth = 514f;
        private const float ButtonHeight = 79f;
        private const float PanelShadowSpread = 27.5f;
        private const float PanelShadowDrop = 3.5f;

        private const float TitleSize = 60f;
        private const float BodySize = 28f;
        private const float ConfirmLabelSize = 40f;
        private const float CancelLabelSize = 32f;

        // Measured on mockup 13: black at this opacity, blended in linear light.
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.817f);

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
            UiBuildKit.AddImage(panelFace, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Common/UI_Panel_Large.png", 0f), true);

            TMP_FontAsset serif = UiBuildKit.LoadFontRole("SerifBold");
            TextMeshProUGUI title = Text("Title", panel, serif, TitleSize, "Leave match?");
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UiBuildKit.Hex("#8E0A17"), UiBuildKit.Hex("#8E0A17"), UiBuildKit.Hex("#750A14"), UiBuildKit.Hex("#750A14"));
            title.color = Color.white;

            TextMeshProUGUI body = Text("Body", panel, UiBuildKit.LoadFontRole("SansRegular"), BodySize, "A bot will take your seat and it counts as a loss.");
            body.color = UiBuildKit.Hex("#7A593F");
            body.lineSpacing = 8f;

            Button confirm = KitButton("ConfirmButton", panel, "UI/Buttons/UI_ButtonPrimary_Normal.png", "UI/Buttons/UI_Shadow_Primary.png", 6.9f, 0.45f);
            TextMeshProUGUI confirmLabel = ButtonLabel(confirm.transform, serif, ConfirmLabelSize, "Leave Match", UiBuildKit.Hex("#FEFCFC"));
            confirmLabel.fontSharedMaterial = UiBuildKit.LoadShadowPreset(serif, "ButtonLabel", new Color(0.29f, 0.03f, 0.04f, 0.9f), new Vector2(0.25f, -0.35f), 0.2f);

            Button cancel = KitButton("CancelButton", panel, "UI/Buttons/UI_ButtonSecondary_Normal.png", "UI/Buttons/UI_Shadow_Secondary.png", 2.3f, 0.4f);
            TextMeshProUGUI cancelLabel = ButtonLabel(cancel.transform, UiBuildKit.LoadFontRole("SansSemiBold"), CancelLabelSize, "Cancel", UiBuildKit.Hex("#222424"));

            var view = root.AddComponent<DialogView>();
            var so = new SerializedObject(view);
            so.FindProperty("_panel").objectReferenceValue = panel;
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
        /// A kit pill button at the dialog's button size. The sprite is sliced only horizontally, so
        /// it is scaled as a whole to the height (its woven caps keep their proportions) and its
        /// shadow keeps the same spread relative to it.
        /// </summary>
        private static Button KitButton(string name, Transform parent, string facePath, string shadowPath, float shadowDrop, float shadowOpacity)
        {
            RectTransform root = Child(name, parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            Sprite face = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{facePath}", 0f);
            Sprite shadow = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{shadowPath}", 0f);
            Vector2 faceSize = UiBuildKit.NativeSize(face);
            float scale = ButtonHeight / faceSize.y;
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
