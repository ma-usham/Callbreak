using System.Collections.Generic;
using System.Text;
using Callbreak.Application.Config;
using Callbreak.Bootstrap;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using PurrNet.Transports;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer.Unity;

namespace Callbreak.Editor
{
    /// <summary>
    /// Generates the three scenes of GDD §21 from code so they never drift from the scopes: Boot
    /// (ProjectLifetimeScope with the event system, toasts, curtain and audio host, all kept across
    /// scenes), Menu (MenuLifetimeScope and every menu screen prefab) and Table
    /// (TableLifetimeScope and the table prefabs), listed in the build settings in that order.
    /// Scenes are built additively and closed again, so the Editor keeps the scene it has open.
    /// </summary>
    public static class SceneSetup
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string BootPath = ScenesFolder + "/Boot.unity";
        public const string MenuPath = ScenesFolder + "/Menu.unity";
        public const string TablePath = ScenesFolder + "/Table.unity";

        private const string ScreensFolder = "Assets/_Project/Prefabs/UI/Screens";
        private const string TablePrefabsFolder = "Assets/_Project/Prefabs/Table";

        /// <summary>The Menu scene's screens (GDD §21: 00-06, 11, 12, 14 and 15), bottom to top.</summary>
        private static readonly string[] MenuScreens =
        {
            "MainMenu", "PlayWithFriends", "CreateRoom", "JoinRoom", "PrivateLobby", "Matchmaking",
            "OfflineSetup", "Settings", "HowToPlay", "Profile", "CardCollection",
        };

        /// <summary>
        /// The Table scene's screens over the table (GDD §21: 09, 10, 13, 16, plus Settings and
        /// the rules primer reached from it), bottom to top. The empty gameplay screen lies below.
        /// </summary>
        private static readonly string[] TableOverlays =
        {
            "MatchMenu", "RoundResults", "MatchResults", "Scorecard", "Settings", "HowToPlay",
        };

        // GDD §23 canvas layers.
        private const int ScreensLayer = 10;
        private const int OverlaysLayer = 40;
        private const int CurtainLayer = 70;

        [MenuItem("Callbreak/Build/Scenes")]
        private static void BuildFromMenu() => Debug.Log(BuildAll());

        /// <summary>
        /// Rebuilds the three scenes. A scene the Editor has open with unsaved changes is left alone
        /// unless <paramref name="discardOpenChanges"/> is set (check what the changes are first).
        /// </summary>
        public static string BuildAll(bool discardOpenChanges = false)
        {
            EnsureSavedSceneOpen();
            var report = new StringBuilder();
            report.AppendLine(BuildBoot(discardOpenChanges));
            report.AppendLine(BuildMenu(discardOpenChanges));
            report.AppendLine(BuildTable(discardOpenChanges));
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(TablePath, true),
            };
            PlayFromBoot.Apply();
            report.Append("build settings: Boot, Menu, Table");
            return report.ToString();
        }

        /// <summary>
        /// The scenes are built next to the open one, which Unity refuses while the only open scene
        /// is an untitled one (a PlayMode test run leaves that behind). An untitled scene without
        /// changes makes way for the Boot scene; one with changes is somebody's work and stays.
        /// </summary>
        private static void EnsureSavedSceneOpen()
        {
            Scene active = SceneManager.GetActiveScene();
            if (SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path) && !active.isDirty && System.IO.File.Exists(BootPath))
            {
                EditorSceneManager.OpenScene(BootPath, OpenSceneMode.Single);
            }
        }

        public static string BuildBoot(bool discardOpenChanges = false)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            CreateCamera(scene);

            GameObject root = Create(scene, "ProjectLifetimeScope", null);
            // The one audio listener lives with the audio sources, so it survives every scene change.
            GameObject audio = Create(scene, "Audio", root.transform, typeof(AudioListener));

            GameObject events = Create(scene, "EventSystem", root.transform, typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var toastPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Overlays/Toast.prefab");
            var toastObject = (GameObject)PrefabUtility.InstantiatePrefab(toastPrefab, scene);
            toastObject.transform.SetParent(root.transform, false);

            var dialogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogBuilder.PrefabPath);
            var dialogObject = (GameObject)PrefabUtility.InstantiatePrefab(dialogPrefab, scene);
            dialogObject.transform.SetParent(root.transform, false);

            LoadingCurtain curtain = CreateCurtain(scene, root.transform);

            // The relay transport of private rooms (GDD §24 Phase 1); it needs no NetworkManager.
            GameObject network = Create(scene, "Network", root.transform, typeof(PurrTransport));

            var scope = root.AddComponent<ProjectLifetimeScope>();
            var so = new SerializedObject(scope);
            so.FindProperty("_roomTransport").objectReferenceValue = network.GetComponent<PurrTransport>();
            so.FindProperty("_localization").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<LocalizationConfig>("Assets/_Project/Config/Localization/LocalizationConfig.asset");
            so.FindProperty("_audioLibrary").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioCueLibrary>("Assets/_Project/Config/Audio/AudioCueLibrary.asset");
            so.FindProperty("_audioHost").objectReferenceValue = audio;
            so.FindProperty("_toast").objectReferenceValue = toastObject.GetComponentInChildren<ToastView>(true);
            so.FindProperty("_curtain").objectReferenceValue = curtain;
            so.FindProperty("_dialog").objectReferenceValue = dialogObject.GetComponentInChildren<DialogView>(true);
            so.FindProperty("_avatars").objectReferenceValue = EnsureAvatarLibrary();
            so.FindProperty("_cardSprites").objectReferenceValue = TableCardBuilder.EnsureLibrary();
            SetAutoInject(so, root);
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(scene, BootPath, discardOpenChanges);
        }

        public static string BuildMenu(bool discardOpenChanges = false)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            CreateCamera(scene);
            GameObject canvas = CreateCanvas(scene, "MenuCanvas", ScreensLayer);

            var screens = new List<UIScreen>();
            var missing = new List<string>();
            foreach (string name in MenuScreens)
            {
                UIScreen screen = InstantiateScreen(scene, $"{ScreensFolder}/{name}.prefab", canvas.transform);
                if (screen != null)
                {
                    screens.Add(screen);
                }
                else
                {
                    missing.Add(name);
                }
            }

            // The name question when the game starts (ADR 022): its own canvas, above the screens.
            var namePromptPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NamePromptBuilder.PrefabPath);
            GameObject namePrompt = namePromptPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(namePromptPrefab, scene) : null;
            if (namePrompt == null)
            {
                missing.Add("NamePrompt");
            }

            GameObject scopeObject = Create(scene, "MenuLifetimeScope", null);
            scopeObject.SetActive(false);
            var scope = scopeObject.AddComponent<MenuLifetimeScope>();
            scope.parentReference = ParentReference.Create<ProjectLifetimeScope>();
            var so = new SerializedObject(scope);
            if (namePrompt != null)
            {
                SetAutoInject(so, canvas, namePrompt);
                so.FindProperty("_namePrompt").objectReferenceValue = namePrompt.GetComponent<NamePromptView>();
            }
            else
            {
                SetAutoInject(so, canvas);
            }

            SerializedProperty list = so.FindProperty("_screens");
            list.arraySize = screens.Count;
            for (int i = 0; i < screens.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = screens[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            scopeObject.SetActive(true);

            string result = Save(scene, MenuPath, discardOpenChanges) + $" ({screens.Count} screens";
            return result + (missing.Count > 0 ? "; not built yet: " + string.Join(", ", missing) + ")" : ")");
        }

        public static string BuildTable(bool discardOpenChanges = false)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            CreateCamera(scene);

            // The table and its call panel stay on screen for the whole match.
            var missing = new List<string>();
            GameObject canvas = CreateCanvas(scene, "TableCanvas", ScreensLayer);
            UIScreen table = InstantiateTableScreen(scene, $"{ScreensFolder}/Table.prefab", canvas.transform, missing);
            UIScreen bidding = InstantiateTableScreen(scene, $"{ScreensFolder}/BiddingPanel.prefab", canvas.transform, missing);

            // Everything the router opens over them.
            GameObject overlays = CreateCanvas(scene, "OverlayCanvas", OverlaysLayer);
            var screens = new List<UIScreen>();
            UIScreen gameplay = InstantiateTableScreen(scene, TableCardBuilder.GameplayScreenPrefabPath, overlays.transform, missing);
            if (gameplay != null)
            {
                screens.Add(gameplay);
            }

            foreach (string name in TableOverlays)
            {
                UIScreen screen = InstantiateTableScreen(scene, $"{ScreensFolder}/{name}.prefab", overlays.transform, missing);
                if (screen != null)
                {
                    screens.Add(screen);
                }
            }

            GameObject scopeObject = Create(scene, "TableLifetimeScope", null);
            scopeObject.SetActive(false);
            var scope = scopeObject.AddComponent<TableLifetimeScope>();
            scope.parentReference = ParentReference.Create<ProjectLifetimeScope>();
            var so = new SerializedObject(scope);
            SetAutoInject(so, canvas, overlays);
            so.FindProperty("_table").objectReferenceValue = table;
            so.FindProperty("_bidding").objectReferenceValue = bidding;
            SerializedProperty list = so.FindProperty("_screens");
            list.arraySize = screens.Count;
            for (int i = 0; i < screens.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = screens[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            scopeObject.SetActive(true);

            string result = Save(scene, TablePath, discardOpenChanges) + $" (table, call panel, {screens.Count} screens";
            return result + (missing.Count > 0 ? "; not built yet: " + string.Join(", ", missing) + ")" : ")");
        }

        private static UIScreen InstantiateTableScreen(Scene scene, string path, Transform parent, List<string> missing)
        {
            UIScreen screen = InstantiateScreen(scene, path, parent);
            if (screen == null)
            {
                missing.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            }

            return screen;
        }

        /// <summary>
        /// The avatar presets: <c>Avatar_Default</c> is preset 0, then <c>Avatar_01</c>, <c>Avatar_02</c>...
        /// as far as they exist in the avatar folder.
        /// </summary>
        private static AvatarLibrary EnsureAvatarLibrary()
        {
            const string path = "Assets/_Project/Config/UI/AvatarLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<AvatarLibrary>(path);
            if (library == null)
            {
                UiBuildKit.EnsureFolder("Assets/_Project/Config/UI");
                library = ScriptableObject.CreateInstance<AvatarLibrary>();
                AssetDatabase.CreateAsset(library, path);
            }

            var sprites = new List<Sprite>();
            Sprite first = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiBuildKit.ArtRoot}/UI/Avatars/Avatar_Default.png");
            if (first != null)
            {
                sprites.Add(first);
            }

            for (int i = 1; i < 32; i++)
            {
                Sprite next = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiBuildKit.ArtRoot}/UI/Avatars/Avatar_{i:00}.png");
                if (next == null)
                {
                    break;
                }

                sprites.Add(next);
            }

            var so = new SerializedObject(library);
            SerializedProperty list = so.FindProperty("_avatars");
            list.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        /// <summary>VContainer injects every component under these objects (LocalizedText and friends).</summary>
        private static void SetAutoInject(SerializedObject scope, params GameObject[] targets)
        {
            SerializedProperty list = scope.FindProperty("autoInjectGameObjects");
            list.arraySize = targets.Length;
            for (int i = 0; i < targets.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            }
        }

        private static LoadingCurtain CreateCurtain(Scene scene, Transform parent)
        {
            GameObject curtain = Create(scene, "LoadingCurtain", parent, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = curtain.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CurtainLayer;
            ConfigureScaler(curtain.GetComponent<CanvasScaler>());

            // The shared menu background, framed exactly as the menu screens frame it, so the curtain
            // opening onto the main menu shows no jump.
            var background = (RectTransform)Create(scene, "Background", curtain.transform, typeof(RectTransform), typeof(Image)).transform;
            background.sizeDelta = UiBuildKit.FromMock(2230f, 1255f);
            Image plate = background.GetComponent<Image>();
            plate.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Backgrounds/BG_Menu_Wide.png");

            var logo = (RectTransform)Create(scene, "Logo", curtain.transform, typeof(RectTransform), typeof(Image)).transform;
            logo.sizeDelta = UiBuildKit.FromMock(834.75f, 180.25f);
            Image logoImage = logo.GetComponent<Image>();
            logoImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/Ornaments/Logo_Callbreak.png");
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;

            return curtain.AddComponent<LoadingCurtain>();
        }

        private static UIScreen InstantiateScreen(Scene scene, string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<UIScreen>() == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var rect = (RectTransform)instance.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return instance.GetComponent<UIScreen>();
        }

        private static GameObject CreateCanvas(Scene scene, string name, int sortingOrder)
        {
            GameObject go = Create(scene, name, null, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            ConfigureScaler(go.GetComponent<CanvasScaler>());
            return go;
        }

        /// <summary>GDD §12 FIG 12.1: Expand mode, so the 1920 x 1080 design area always fits.</summary>
        private static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiBuildKit.CanvasWidth, UiBuildKit.CanvasHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        /// <summary>A camera that only clears: every canvas is screen-space overlay.</summary>
        private static void CreateCamera(Scene scene)
        {
            GameObject go = Create(scene, "Main Camera", null, typeof(Camera), typeof(UniversalAdditionalCameraData));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiBuildKit.Hex("#F7EFE5");
            camera.cullingMask = 0;
            camera.orthographic = true;
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static GameObject Create(Scene scene, string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            else
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            return go;
        }

        /// <summary>
        /// Saves the freshly built scene over <paramref name="path"/> and closes it again. When the
        /// Editor has the old copy of that scene open, the new one takes its place (unless the open
        /// copy has unsaved changes, which are never thrown away).
        /// </summary>
        private static string Save(Scene scene, string path, bool discardOpenChanges)
        {
            Scene open = SceneManager.GetSceneByPath(path);
            bool replacesOpen = open.IsValid() && open.isLoaded && open != scene;
            if (replacesOpen)
            {
                if (open.isDirty && !discardOpenChanges)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    return $"skipped {path}: it is open in the Editor with unsaved changes";
                }

                SceneManager.SetActiveScene(scene);
                EditorSceneManager.CloseScene(open, true);
            }

            bool saved = EditorSceneManager.SaveScene(scene, path);
            if (!replacesOpen)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            return saved ? $"saved {path}" : $"FAILED to save {path}";
        }
    }

    /// <summary>
    /// Play mode always starts from the Boot scene (GDD §21: Boot builds the app scope that the
    /// Menu and Table scopes need). Toggle it under Callbreak/Play From Boot. A PlayMode test run
    /// is left alone: the Test Framework plays the scene it has just opened, and the tests load
    /// Boot themselves.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromBoot
    {
        private const string PrefKey = "Callbreak.PlayFromBoot";
        private const string MenuPath = "Callbreak/Play From Boot";
        private const string TestRunnerScenePrefix = "InitTestScene";

        static PlayFromBoot()
        {
            EditorApplication.delayCall += Apply;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void Apply()
        {
            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneSetup.BootPath);
            bool fromBoot = EditorPrefs.GetBool(PrefKey, true) && !IsTestRun();
            EditorSceneManager.playModeStartScene = fromBoot ? boot : null;
        }

        private static bool IsTestRun()
        {
            return SceneManager.GetActiveScene().name.StartsWith(TestRunnerScenePrefix, System.StringComparison.Ordinal);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.EnteredEditMode)
            {
                Apply();
            }
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(PrefKey, !EditorPrefs.GetBool(PrefKey, true));
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, true));
            return true;
        }
    }
}
