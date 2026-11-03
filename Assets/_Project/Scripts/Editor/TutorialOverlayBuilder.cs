using System.IO;
using Callbreak.Bootstrap;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Callbreak.Editor
{
    /// <summary>
    /// Builds the first-hand tutorial's overlay (ADR 084) from the dialog's kit: the modal panel as
    /// a caption card (serif title in the dialog's crimson, the body in its brown), a crimson Next,
    /// an ivory "Skip tutorial", a dim for the centred cards, and the pointing hand designed in Figma
    /// (Art/Tutorial Pointer 446:518; Tools/ArtPipeline/NewUI/README.md). Its canvas sorts above the table's pages (40)
    /// and below dialogs (50). <see cref="AddToTableScene"/> puts it in the Table scene and hands it
    /// to the scope; <see cref="SceneSetup.BuildTable"/> does the same on a full rebuild.
    /// </summary>
    public static class TutorialOverlayBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Overlays/TutorialOverlay.prefab";

        private const int SortingOrder = 45;
        private const string PointerPath = UiBuildKit.ArtRoot + "/UI/Table/UI_TutorialPointer.png";

        // The pointer's fingertip in its 210 x 272 sprite (82, 16 from the top left), and its size on screen.
        private static readonly Vector2 PointerPivot = new Vector2(0.390f, 0.941f);
        private static readonly Vector2 PointerSize = new Vector2(112f, 145f);

        private const float CardWidth = 740f;
        private const float CardStartHeight = 260f;
        private const float PanelShadowSpread = 27.5f;
        private const float PanelShadowDrop = 3.5f;
        private const float TitleSize = 40f;
        private const float BodySize = 26f;
        private const float ButtonHeight = 58f;
        private const float NextWidth = 190f;
        private const float SkipWidth = 230f;
        private const float NextLabelSize = 30f;
        private const float SkipLabelSize = 24f;

        // A lighter dim than the dialog's: the table stays readable behind the welcome and the score.
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);

        [MenuItem("Callbreak/Build/Tutorial Overlay")]
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
            Debug.Log($"[TutorialOverlayBuilder] Built {PrefabPath}");
        }

        /// <summary>Puts the overlay in the Table scene, once, and hands it to the scope; saves the scene.</summary>
        [MenuItem("Callbreak/Build/Tutorial Overlay Into Table Scene")]
        public static string AddToTableScene()
        {
            Scene scene = SceneManager.GetSceneByPath(SceneSetup.TablePath);
            bool opened = !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(SceneSetup.TablePath, OpenSceneMode.Additive);
            }

            try
            {
                string result = Place(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return result;
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>Instantiates the overlay at the scene's root, unless it is there, and sets the scope's <c>_tutorial</c>.</summary>
        internal static string Place(Scene scene)
        {
            TableLifetimeScope scope = null;
            TutorialView view = null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                scope = scope != null ? scope : go.GetComponentInChildren<TableLifetimeScope>(true);
                view = view != null ? view : go.GetComponentInChildren<TutorialView>(true);
            }

            if (scope == null)
            {
                return "no TableLifetimeScope in " + scene.path;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                return "build the overlay first: " + PrefabPath;
            }

            if (view == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                view = instance.GetComponent<TutorialView>();
            }

            var so = new SerializedObject(scope);
            so.FindProperty("_tutorial").objectReferenceValue = view;
            so.ApplyModifiedPropertiesWithoutUndo();
            return "tutorial overlay in " + scene.path;
        }

        private static GameObject BuildHierarchy()
        {
            var root = new GameObject("TutorialOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            SceneSetup.ConfigureScaler(root.GetComponent<CanvasScaler>());
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            // Behind a centred card: takes every touch, so the table under it waits.
            RectTransform dim = Child("Dim", root.transform);
            Stretch(dim);
            UiBuildKit.AddImage(dim, null, true).color = Dim;

            RectTransform safeArea = Child("SafeArea", root.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            RectTransform card = Child("Card", safeArea);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(1f, 1f);
            card.sizeDelta = new Vector2(CardWidth, CardStartHeight);

            RectTransform shadow = Child("Shadow", card);
            Stretch(shadow, new Vector2(-PanelShadowSpread, -PanelShadowSpread - PanelShadowDrop), new Vector2(PanelShadowSpread, PanelShadowSpread - PanelShadowDrop));
            UiBuildKit.AddImage(shadow, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Common/UI_Panel_Large_Shadow.png", 0f), false);

            // The face takes touches, so a tap on the card never reaches the table.
            RectTransform face = Child("Face", card);
            Stretch(face);
            UiBuildKit.AddImage(face, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Modal_Menu.png", 0f), true);

            TMP_FontAsset serif = UiBuildKit.LoadFontRole("SerifBold");
            TextMeshProUGUI title = Text("Title", card, serif, TitleSize, "Follow the suit");
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(UiBuildKit.Hex("#8E0A17"), UiBuildKit.Hex("#8E0A17"), UiBuildKit.Hex("#750A14"), UiBuildKit.Hex("#750A14"));
            title.color = Color.white;

            TextMeshProUGUI body = Text("Body", card, UiBuildKit.LoadFontRole("SansRegular"), BodySize, "You must play the led suit if you have it.\nDrag the card up to play it.");
            body.color = UiBuildKit.Hex("#7A593F");
            body.lineSpacing = 2f;

            Button next = DialogBuilder.KitButton(
                "NextButton", card, new Vector2(NextWidth, ButtonHeight * 1.135f),
                "UI/Controls/UI_Button_Crimson_Box.png", "UI/Controls/UI_Button_Crimson_Box_Shadow.png", 1.7f, 0.82f);
            TextMeshProUGUI nextLabel = Label(next.transform, serif, NextLabelSize, "Next", UiBuildKit.Hex("#FEFCFC"));
            nextLabel.fontSharedMaterial = UiBuildKit.LoadShadowPreset(serif, "ButtonLabel", new Color(0.29f, 0.03f, 0.04f, 0.9f), new Vector2(0.25f, -0.35f), 0.2f);

            Button skip = DialogBuilder.KitButton(
                "SkipButton", card, new Vector2(SkipWidth, ButtonHeight),
                "UI/Buttons/UI_ButtonSecondary_Normal.png", "UI/Buttons/UI_Shadow_Secondary.png", 2.3f, 0.4f);
            TextMeshProUGUI skipLabel = Label(skip.transform, UiBuildKit.LoadFontRole("SansSemiBold"), SkipLabelSize, "Skip tutorial", UiBuildKit.Hex("#222424"));

            RectTransform pointer = Child("Pointer", safeArea);
            pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 0.5f);
            pointer.pivot = PointerPivot;
            pointer.sizeDelta = PointerSize;
            UiBuildKit.AddImage(pointer, UiBuildKit.LoadSpriteAt(PointerPath, 0f), false);
            var pointerGroup = pointer.gameObject.AddComponent<CanvasGroup>();
            pointerGroup.interactable = false;
            pointerGroup.blocksRaycasts = false;

            var view = root.AddComponent<TutorialView>();
            var so = new SerializedObject(view);
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_overlay").objectReferenceValue = safeArea;
            so.FindProperty("_dim").objectReferenceValue = dim.gameObject;
            so.FindProperty("_card").objectReferenceValue = card;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_body").objectReferenceValue = body;
            so.FindProperty("_nextButton").objectReferenceValue = next;
            so.FindProperty("_nextLabel").objectReferenceValue = nextLabel;
            so.FindProperty("_skipButton").objectReferenceValue = skip;
            so.FindProperty("_skipLabel").objectReferenceValue = skipLabel;
            so.FindProperty("_pointer").objectReferenceValue = pointer;
            so.FindProperty("_pointerGroup").objectReferenceValue = pointerGroup;
            so.FindProperty("_buttonHeight").floatValue = ButtonHeight * 1.135f;

            // Wide enough that the score's two lines never leave a word alone on a third.
            so.FindProperty("_centreWidth").floatValue = 860f;
            so.ApplyModifiedPropertiesWithoutUndo();

            int uiLayer = LayerMask.NameToLayer("UI");
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = uiLayer;
            }

            dim.gameObject.SetActive(false);
            card.gameObject.SetActive(false);
            pointer.gameObject.SetActive(false);
            return root;
        }

        /// <summary>A centred, wrapping text block; <see cref="TutorialView"/> places it.</summary>
        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, string sample)
        {
            RectTransform rect = Child(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(CardWidth - 68f, size * 1.3f);
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

        private static TextMeshProUGUI Label(Transform button, TMP_FontAsset font, float size, string sample, Color color)
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
