using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Callbreak.Editor
{
    /// <summary>
    /// Renders a screen prefab to a PNG at a given screen size, laid out the way the game's
    /// canvas lays it out (1920 × 1080 reference, Expand), so a build can be compared with its
    /// mockup side by side at 16:9, 21:9 and 4:3 without entering Play mode.
    /// </summary>
    public static class ScreenCaptureTool
    {
        public const string OutputFolder = "Temp/Captures";

        [MenuItem("Callbreak/Capture/Main Menu (16:9, 21:9, 4:3)")]
        public static void CaptureMainMenu()
        {
            Capture(MainMenuBuilder.PrefabPath, 1920, 1080, $"{OutputFolder}/MainMenu_1920x1080.png");
            Capture(MainMenuBuilder.PrefabPath, 2520, 1080, $"{OutputFolder}/MainMenu_2520x1080.png");
            Capture(MainMenuBuilder.PrefabPath, 1920, 1440, $"{OutputFolder}/MainMenu_1920x1440.png");
            Debug.Log($"[ScreenCaptureTool] Captures written to {Path.GetFullPath(OutputFolder)}");
        }

        /// <summary>Captures a layout-built screen (Prefabs/UI/Screens/&lt;Name&gt;.prefab) at 16:9, 21:9 and 4:3.</summary>
        public static void CaptureScreen(string screenName)
        {
            string prefab = Screens.ScreenLayoutBuilder.PrefabPath(screenName);
            Capture(prefab, 1920, 1080, $"{OutputFolder}/{screenName}_1920x1080.png");
            Capture(prefab, 2520, 1080, $"{OutputFolder}/{screenName}_2520x1080.png");
            Capture(prefab, 1920, 1440, $"{OutputFolder}/{screenName}_1920x1440.png");
        }

        /// <summary>
        /// Captures the dialog showing these words at 16:9, laid out as the game lays it out, over
        /// the plain background (its fade does not run outside Play mode, so it is shown finished).
        /// </summary>
        public static string CaptureDialog(string title, string body, string confirm, string cancel, string outputPath)
        {
            return Capture(DialogBuilder.PrefabPath, 1920, 1080, outputPath, instance =>
            {
                // The dialog is a screen-overlay canvas of its own; here it covers the capture canvas.
                var rect = (RectTransform)instance.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localPosition = Vector3.zero;

                var view = instance.GetComponent<Presentation.Framework.DialogView>();
                view.ReduceMotion = true;
                view.Show(title, body, confirm, cancel);
                instance.GetComponent<CanvasGroup>().alpha = 1f;
            });
        }

        /// <param name="prepare">Puts a state of the caller's on the instance before it is rendered.</param>
        public static string Capture(string prefabPath, int width, int height, string outputPath, System.Action<GameObject> prepare = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[ScreenCaptureTool] No prefab at {prefabPath}");
                return null;
            }

            // Same maths as CanvasScaler in Expand mode: the design area always fits.
            float scale = Mathf.Min(width / UiBuildKit.CanvasWidth, height / UiBuildKit.CanvasHeight);
            var canvasSize = new Vector2(width / scale, height / scale);

            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D pixels = null;
            try
            {
                var cameraObject = new GameObject("CaptureCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = canvasSize.y / 2f;
                camera.aspect = (float)width / height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = UiBuildKit.Hex("#F7EFE5");
                camera.targetTexture = target;

                var canvasObject = new GameObject("CaptureCanvas", typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                var canvasRect = (RectTransform)canvasObject.transform;
                canvasRect.sizeDelta = canvasSize;
                canvasRect.position = Vector3.zero;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(canvasRect, false);

                // Optional design-time state. A screen whose content is built at runtime (the
                // table's cards, an overlay that sits on the table) has a class
                // Callbreak.Editor.Screens.<PrefabName>Preview with
                // public static void Prepare(GameObject instance), which puts the mockup's state on
                // the instance before it is rendered. Screens without one are captured as built.
                System.Type preview = Screens.ScreenLayoutBuilder.FindType($"Callbreak.Editor.Screens.{prefab.name}Preview");
                preview?.GetMethod("Prepare", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.Invoke(null, new object[] { instance });
                prepare?.Invoke(instance);

                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text text in instance.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.ForceMeshUpdate(true, true);
                }

                Canvas.ForceUpdateCanvases();

                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else
                {
                    camera.Render();
                }

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                RenderTexture.active = previous;

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
                return outputPath;
            }
            finally
            {
                if (pixels != null)
                {
                    Object.DestroyImmediate(pixels);
                }

                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
