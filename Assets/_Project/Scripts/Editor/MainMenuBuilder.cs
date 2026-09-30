using System.IO;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Callbreak.Editor
{
    /// <summary>
    /// Builds the Main menu prefab and the Menu scene (GDD §11 screen 00). Every position below
    /// is a centre measured on 00-main-menu.png in mockup pixels; the parts were fitted to the
    /// mockup by re-composing it from the exported sprites (ArtSource/Prompts/MainMenu_Batch.md).
    /// Re-running overwrites the prefab, so layout changes belong here until the screen is signed
    /// off.
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Screens/MainMenu.prefab";
        public const string ScenePath = "Assets/_Project/Scenes/Menu.unity";

        private const string TextMeshProSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const float UiPixelsPerUnit = 200f;
        private const float BackgroundPixelsPerUnit = 100f;

        private static readonly Vector2 Screen = UiBuildKit.ScreenCentre;

        // The mockup's lettering is narrower than Inter and Source Serif 4 at their natural
        // width, so each label carries a horizontal scale and spacing measured against it.
        // Size, left edge or centre x, baseline y, horizontal scale, character spacing.
        private static readonly LabelSpec PlayOnlineText = new LabelSpec(72f, 703.3f, 657.6f, 0.886f, 0f);
        private static readonly LabelSpec PlayWithFriendsText = new LabelSpec(35.4f, 589.3f, 793.4f, 0.747f, -2f);
        private static readonly LabelSpec PlayOfflineText = new LabelSpec(35.4f, 971.7f, 793.4f, 0.842f, -2f);
        private static readonly LabelSpec TaglineText = new LabelSpec(21.6f, 829.6f, 883.5f, 1f, 26.2f);
        private static readonly LabelSpec GreetingText = new LabelSpec(27f, 206.6f, 82.1f, 1f, 0f);
        private static readonly LabelSpec PlayerNameText = new LabelSpec(39.6f, 205.4f, 117.5f, 0.966f, 0f);

        [MenuItem("Callbreak/Build/Main Menu")]
        public static void Build()
        {
            if (!File.Exists(TextMeshProSettingsPath))
            {
                // The fonts need TextMeshPro's shaders. The import finishes after this call
                // returns, so the build continues from the completion callback.
                AssetDatabase.importPackageCompleted += ContinueAfterTextMeshProImport;
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                return;
            }

            GameObject root = BuildHierarchy();
            UiBuildKit.EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // The scene only hosts an instance of the prefab, so it is created once and then left
            // alone: rebuilding it would overwrite a scene someone has open.
            if (!File.Exists(ScenePath))
            {
                BuildScene(prefab);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MainMenuBuilder] Built {PrefabPath}; scene at {ScenePath}");
        }

        private static void ContinueAfterTextMeshProImport(string packageName)
        {
            AssetDatabase.importPackageCompleted -= ContinueAfterTextMeshProImport;
            Build();
        }

        private static GameObject BuildHierarchy()
        {
            TMP_FontAsset serifBold = UiBuildKit.LoadFont("SourceSerif4-Bold");
            TMP_FontAsset sansSemiBold = UiBuildKit.LoadFont("Inter-SemiBold");
            TMP_FontAsset sansBold = UiBuildKit.LoadFont("Inter-Bold");

            Sprite circle = Ui("Buttons/UI_ButtonCircle_Normal.png");
            Sprite circleShadow = Ui("Buttons/UI_Shadow_Circle.png");
            Sprite secondary = Ui("Buttons/UI_ButtonSecondary_Normal.png", 117f);
            Sprite secondaryShadow = Ui("Buttons/UI_Shadow_Secondary.png", 150f);
            Sprite chevron = Ui("Icons/Icon_Chevron.png");

            var rootObject = new GameObject("MainMenu", typeof(RectTransform));
            var root = (RectTransform)rootObject.transform;
            Stretch(root);

            BuildBackground(root);

            RectTransform centre = UiBuildKit.CreateDesignGroup("Centre", root, UiBuildKit.Attach.Centre);
            RectTransform headerLeft = UiBuildKit.CreateDesignGroup("HeaderLeft", root, UiBuildKit.Attach.TopLeft);
            RectTransform headerRight = UiBuildKit.CreateDesignGroup("HeaderRight", root, UiBuildKit.Attach.TopRight);

            // Centre column, back to front
            Place("Logo", centre, new Vector2(834.75f, 180.25f), Screen, Ui("Ornaments/Logo_Callbreak.png"));

            var fanCentre = new Vector2(848f, 479.25f);
            PlaceShadow("CardFanShadow", centre, fanCentre, Screen, Ui("Ornaments/UI_Shadow_MenuFan.png"), 6f, 0.3f);
            Place("CardFan", centre, fanCentre, Screen, Ui("Ornaments/Deco_MenuFan.png"));

            var playOnlineCentre = new Vector2(835.75f, 636.25f);
            Sprite primary = Ui("Buttons/UI_ButtonPrimary_Normal.png", 202f);
            UnityEngine.UI.Button playOnline = CreateButton(
                "PlayOnlineButton", centre, playOnlineCentre, UiBuildKit.NativeSize(primary), primary,
                Ui("Buttons/UI_Shadow_Primary.png", 286f), 1f, 0f, 10f, 0.5f);
            Place("Icon", playOnline.transform, new Vector2(641.25f, 634.75f), playOnlineCentre, Ui("Icons/Icon_Spade_Gold.png"));
            TextMeshProUGUI playOnlineLabel = Label(
                "Label", playOnline.transform, playOnlineCentre, "PLAY ONLINE", serifBold, Color.white, PlayOnlineText, false);
            playOnlineLabel.enableVertexGradient = true;
            Color labelFoot = UiBuildKit.Hex("#E8D4B8");
            playOnlineLabel.colorGradient = new VertexGradient(Color.white, Color.white, labelFoot, labelFoot);
            playOnlineLabel.fontSharedMaterial = UiBuildKit.LoadShadowPreset(
                serifBold, "ButtonLabel", new Color(0.29f, 0.03f, 0.04f, 0.9f), new Vector2(0.25f, -0.35f), 0.2f);

            Color ink = UiBuildKit.Hex("#0B0C0E");
            Vector2 secondarySize = UiBuildKit.NativeSize(secondary);

            // Play Offline is 4 mockup pixels narrower than Play with Friends; the same sliced
            // sprite serves both.
            var friendsCentre = new Vector2(656.5f, 783.25f);
            UnityEngine.UI.Button playWithFriends = CreateButton(
                "PlayWithFriendsButton", centre, friendsCentre, secondarySize, secondary, secondaryShadow, 1f, 0f, 2f, 0.4f);
            Place("Icon", playWithFriends.transform, new Vector2(544f, 781.25f), friendsCentre, Ui("Icons/Icon_Friends.png"));
            Label("Label", playWithFriends.transform, friendsCentre, "Play with Friends", sansBold, ink, PlayWithFriendsText, false);
            Place("Chevron", playWithFriends.transform, new Vector2(796f, 783.5f), friendsCentre, chevron);

            var offlineCentre = new Vector2(1015.5f, 783.25f);
            float narrower = (335f - 331f) * UiBuildKit.ScaleX;
            UnityEngine.UI.Button playOffline = CreateButton(
                "PlayOfflineButton", centre, offlineCentre, secondarySize - new Vector2(narrower, 0f), secondary, secondaryShadow, 1f, -narrower, 2f, 0.4f);
            Place("Icon", playOffline.transform, new Vector2(924.5f, 782f), offlineCentre, Ui("Icons/Icon_Offline.png"));
            Label("Label", playOffline.transform, offlineCentre, "Play Offline", sansBold, ink, PlayOfflineText, false);
            Place("Chevron", playOffline.transform, new Vector2(1140.75f, 783.75f), offlineCentre, chevron);

            BuildFooter(centre, sansSemiBold);

            // Header, left: who you are. The mockup's ring is plain white, not the ivory button.
            var profileCentre = new Vector2(127.5f, 98f);
            Sprite ring = Ui("Avatars/UI_AvatarRing.png");
            UnityEngine.UI.Button profile = CreateButton(
                "ProfileButton", headerLeft, profileCentre, UiBuildKit.NativeSize(ring), ring, circleShadow, 104f / 92f, 0f, 6f, 0.15f);
            Place("Avatar", profile.transform, new Vector2(127.25f, 98f), profileCentre, Ui("Avatars/Avatar_Default.png"));
            Label("Greeting", headerLeft, Screen, "Namaste,", sansSemiBold, UiBuildKit.Hex("#9F7558"), GreetingText, false);
            TextMeshProUGUI playerName = Label(
                "PlayerName", headerLeft, Screen, "Player", sansBold, UiBuildKit.Hex("#151617"), PlayerNameText, false);

            // Header, right: settings and shop
            var settingsCentre = new Vector2(1402f, 97f);
            UnityEngine.UI.Button settings = CreateButton(
                "SettingsButton", headerRight, settingsCentre, UiBuildKit.NativeSize(circle), circle, circleShadow, 1f, 0f, 4f, 0.2f);
            Place("Icon", settings.transform, new Vector2(1401.1f, 96.85f), settingsCentre, Ui("Icons/Icon_Settings.png"));

            RectTransform divider = UiBuildKit.CreateAt(
                "Divider", headerRight, new Vector2(1474.5f, 98.4f), Screen, UiBuildKit.FromMock(2f, 65.75f));
            UiBuildKit.AddImage(divider, null, false).color = UiBuildKit.Hex("#86604A");

            var shopCentre = new Vector2(1547.5f, 97f);
            UnityEngine.UI.Button shop = CreateButton(
                "ShopButton", headerRight, shopCentre, UiBuildKit.NativeSize(circle), circle, circleShadow, 1f, 0f, 4f, 0.2f);
            Place("Icon", shop.transform, new Vector2(1548.75f, 96f), shopCentre, Ui("Icons/Icon_Shop.png"));

            var view = rootObject.AddComponent<MainMenuView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_screenId").intValue = (int)ScreenId.MainMenu;
            serialized.FindProperty("_profileButton").objectReferenceValue = profile;
            serialized.FindProperty("_settingsButton").objectReferenceValue = settings;
            serialized.FindProperty("_shopButton").objectReferenceValue = shop;
            serialized.FindProperty("_playOnlineButton").objectReferenceValue = playOnline;
            serialized.FindProperty("_playWithFriendsButton").objectReferenceValue = playWithFriends;
            serialized.FindProperty("_playOfflineButton").objectReferenceValue = playOffline;
            serialized.FindProperty("_playerNameText").objectReferenceValue = playerName;
            serialized.FindProperty("_playOnlineGroup").objectReferenceValue = playOnline.gameObject.AddComponent<CanvasGroup>();
            serialized.FindProperty("_playWithFriendsGroup").objectReferenceValue = playWithFriends.gameObject.AddComponent<CanvasGroup>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return rootObject;
        }

        private static void BuildBackground(RectTransform root)
        {
            var go = new GameObject("Background", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            Stretch(rect);
            UiBuildKit.AddImage(rect, UiBuildKit.LoadSprite("Backgrounds/BG_Menu_Clean.png", BackgroundPixelsPerUnit), false);

            // Backgrounds bleed to the screen edge on every aspect ratio (GDD §12).
            var fitter = go.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = UiBuildKit.CanvasWidth / UiBuildKit.CanvasHeight;
        }

        private static void BuildFooter(RectTransform centre, TMP_FontAsset font)
        {
            Color line = UiBuildKit.Hex("#A4856A");
            RectTransform footer = UiBuildKit.CreateAt("Footer", centre, Screen, Screen, Vector2.zero);

            RectTransform left = UiBuildKit.CreateAt("LineLeft", footer, new Vector2(553.25f, 876f), Screen, UiBuildKit.FromMock(58.5f, 1.9f));
            UiBuildKit.AddImage(left, null, false).color = line;

            Label("Tagline", footer, Screen, "A GAME THAT BRINGS US CLOSER", font, UiBuildKit.Hex("#9D795D"), TaglineText, true);

            RectTransform right = UiBuildKit.CreateAt("LineRight", footer, new Vector2(1112.5f, 876f), Screen, UiBuildKit.FromMock(63f, 1.9f));
            UiBuildKit.AddImage(right, null, false).color = line;
        }

        /// <summary>
        /// A button is a root with the Button component and, back to front, its shadow, its face
        /// and whatever the caller adds. The shadow sprite is padded, so it keeps its own size:
        /// <paramref name="shadowScale"/> reuses the shadow of one shape under a larger face of the
        /// same shape, and <paramref name="shadowWidthDelta"/> follows a sliced face made wider or
        /// narrower.
        /// </summary>
        private static UnityEngine.UI.Button CreateButton(
            string name,
            Transform parent,
            Vector2 centre,
            Vector2 size,
            Sprite face,
            Sprite shadow,
            float shadowScale,
            float shadowWidthDelta,
            float shadowDrop,
            float shadowOpacity)
        {
            RectTransform root = UiBuildKit.CreateAt(name, parent, centre, Screen, size);

            Vector2 shadowSize = UiBuildKit.NativeSize(shadow) * shadowScale;
            shadowSize.x += shadowWidthDelta;
            RectTransform shadowRect = UiBuildKit.CreateAt("Shadow", root, centre + new Vector2(0f, shadowDrop), centre, shadowSize);
            UiBuildKit.AddImage(shadowRect, shadow, false).color = new Color(1f, 1f, 1f, shadowOpacity);

            RectTransform faceRect = UiBuildKit.CreateAt("Face", root, centre, centre, size);
            UnityEngine.UI.Image faceImage = UiBuildKit.AddImage(faceRect, face, true);

            var button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = faceImage;

            // GDD §12: pressed is 8% darker; disabled is muted at half opacity.
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        /// <summary>Places a sprite pixel for pixel, centred on a mockup point.</summary>
        private static void Place(string name, Transform parent, Vector2 centre, Vector2 parentCentre, Sprite sprite)
        {
            RectTransform rect = UiBuildKit.CreateAt(name, parent, centre, parentCentre, UiBuildKit.NativeSize(sprite));
            UiBuildKit.AddImage(rect, sprite, false);
        }

        private static void PlaceShadow(string name, Transform parent, Vector2 ownerCentre, Vector2 parentCentre, Sprite sprite, float drop, float opacity)
        {
            RectTransform rect = UiBuildKit.CreateAt(
                name, parent, ownerCentre + new Vector2(0f, drop), parentCentre, UiBuildKit.NativeSize(sprite));
            UiBuildKit.AddImage(rect, sprite, false).color = new Color(1f, 1f, 1f, opacity);
        }

        private static TextMeshProUGUI Label(
            string name, Transform parent, Vector2 parentCentre, string text, TMP_FontAsset font, Color color, LabelSpec spec, bool centred)
        {
            TextMeshProUGUI label = UiBuildKit.CreateLabel(
                name, parent, parentCentre, text, font, spec.Size, color, spec.X, spec.Baseline, centred);
            label.characterHorizontalScale = spec.HorizontalScale;
            label.characterSpacing = spec.Spacing;
            return label;
        }

        private readonly struct LabelSpec
        {
            public readonly float Size;
            public readonly float X;
            public readonly float Baseline;
            public readonly float HorizontalScale;
            public readonly float Spacing;

            public LabelSpec(float size, float x, float baseline, float horizontalScale, float spacing)
            {
                Size = size;
                X = x;
                Baseline = baseline;
                HorizontalScale = horizontalScale;
                Spacing = spacing;
            }
        }

        private static Sprite Ui(string path, float sideBorder = 0f)
        {
            return UiBuildKit.LoadSprite($"UI/{path}", UiPixelsPerUnit, new Vector4(sideBorder, 0f, sideBorder, 0f));
        }

        private static void BuildScene(GameObject prefab)
        {
            UiBuildKit.EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiBuildKit.Hex("#F7EFE5");
            camera.cullingMask = 0;
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            var canvasObject = new GameObject(
                "MenuCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiBuildKit.CanvasWidth, UiBuildKit.CanvasHeight);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(canvasObject.transform, false);

            var eventSystem = new GameObject(
                "EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
