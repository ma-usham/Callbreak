using System;
using System.IO;
using System.Linq;
using Callbreak.Presentation.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor
{
    /// <summary>
    /// Builds the toast overlay (GDD §23 toast layer, sorting order 60): its own overlay canvas
    /// scaled like the screens (1920 × 1080, Expand), a pill anchored 40 px below the top of the
    /// safe area with a soft shadow, and a <see cref="ToastView"/>. The pill is V2's toast (Figma
    /// "V2 · Screens", Messages &amp; Errors): #17140F at 90% with a 1-unit #DDB565 rim at 50%,
    /// 74 high, round-ended, the words ivory Inter SemiBold 28; its shadow is V2's (y 8, blur 24,
    /// #17140F at 30%). The UI is blended in linear light, so V2's 90% is 97.5% here
    /// (Tools/ArtPipeline/V2/pieces.py, linear_alpha). Both sprites are drawn here as 9-sliced @2x
    /// PNGs (PPU 200), so re-running rebuilds everything; a PNG is only rewritten when its pixels
    /// change.
    /// </summary>
    public static class ToastBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Overlays/Toast.prefab";
        public const string FaceSpritePath = "Assets/_Project/Art/UI/Common/UI_Toast.png";
        public const string ShadowSpritePath = "Assets/_Project/Art/UI/Common/UI_Toast_Shadow.png";

        // Sprite pixels are @2x: two per 1080p canvas pixel.
        private const float PixelsPerUnit = 200f;
        private const int Radius = 74;
        private const int Outline = 2;
        private const int StretchWidth = 8;
        private const int StretchHeight = 4;
        private const float ShadowSigma = 24f;
        private const int ShadowMargin = 72;
        private const float ShadowOpacity = 0.3f;
        private const float FaceOpacity = 0.975f;
        private const float RimOpacity = 0.5f;

        // Canvas pixels.
        private const float ShadowDrop = 8f;
        private const float TopMargin = 40f;
        private const float FontSize = 28f;
        private const float PaddingX = 32f;
        private const float PaddingY = 18f;
        private const float MinHeight = 74f;
        private const float MaxTextWidth = 1100f;
        private const int SortingOrder = 60;

        private const string Ink = "#17140F";
        private const string Gold = "#DDB565";
        private const string Ivory = "#FFFCF7";

        [MenuItem("Callbreak/Build/Toast")]
        public static void Build()
        {
            Sprite face = EnsureSprite(FaceSpritePath, RenderFace(), Radius);
            Sprite shadow = EnsureSprite(ShadowSpritePath, RenderShadow(), ShadowMargin + Radius);

            GameObject root = BuildHierarchy(face, shadow);
            try
            {
                UiBuildKit.EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ToastBuilder] Built {PrefabPath}");
        }

        private static GameObject BuildHierarchy(Sprite face, Sprite shadow)
        {
            var root = new GameObject("Toast", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiBuildKit.CanvasWidth, UiBuildKit.CanvasHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;

            RectTransform safeArea = Child("SafeArea", root.transform);
            Stretch(safeArea, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            RectTransform pill = Child("Pill", safeArea);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0.5f, 1f);
            pill.anchoredPosition = new Vector2(0f, -TopMargin);
            pill.sizeDelta = new Vector2(480f, MinHeight);
            var group = pill.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            // The shadow sprite reaches ShadowMargin sprite pixels (half that on the canvas) beyond the pill.
            float reach = ShadowMargin / 2f;
            RectTransform shadowRect = Child("Shadow", pill);
            Stretch(shadowRect, new Vector2(-reach, -reach - ShadowDrop), new Vector2(reach, reach - ShadowDrop));
            UiBuildKit.AddImage(shadowRect, shadow, false);

            RectTransform faceRect = Child("Face", pill);
            Stretch(faceRect, Vector2.zero, Vector2.zero);
            UiBuildKit.AddImage(faceRect, face, false);

            RectTransform labelRect = Child("Label", pill);
            Stretch(labelRect, new Vector2(PaddingX, PaddingY), new Vector2(-PaddingX, -PaddingY));
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = UiBuildKit.LoadFontRole("SansSemiBold");
            label.fontSize = FontSize;
            label.color = UiBuildKit.Hex(Ivory);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.text = "A hand had no spades — redealing.";

            var view = root.AddComponent<ToastView>();
            var so = new SerializedObject(view);
            so.FindProperty("_pill").objectReferenceValue = pill;
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_padding").vector2Value = new Vector2(PaddingX, PaddingY);
            so.FindProperty("_minHeight").floatValue = MinHeight;
            so.FindProperty("_maxTextWidth").floatValue = MaxTextWidth;
            so.ApplyModifiedPropertiesWithoutUndo();

            pill.gameObject.SetActive(false);
            int uiLayer = LayerMask.NameToLayer("UI");
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = uiLayer;
            }

            // The sorting override only matters when the toast sits under a scene's canvas (a root
            // canvas ignores it, and its setter doesn't stick there), so it is written directly.
            // In that case ToastView also stretches the root over its parent: an overlay canvas's
            // RectTransform is engine-driven and is saved at zero scale.
            var canvasSettings = new SerializedObject(canvas);
            canvasSettings.FindProperty("m_OverrideSorting").boolValue = true;
            canvasSettings.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>
        /// The pill face: a dark rounded rectangle (radius = half the pill height) with a 1-unit gold
        /// rim, the rim laid on the fill as Figma lays it.
        /// </summary>
        private static byte[] RenderFace()
        {
            int width = 2 * Radius + StretchWidth;
            int height = 2 * Radius + StretchHeight;
            Color fill = UiBuildKit.Hex(Ink);
            Color rim = Color.Lerp(fill, UiBuildKit.Hex(Gold), RimOpacity);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f, y + 0.5f, width, height, Radius);
                    float coverage = Mathf.Clamp01(0.5f - d);
                    float inner = Mathf.Clamp01(0.5f - (d + Outline));
                    Color32 c = Color.Lerp(rim, fill, inner);
                    c.a = (byte)Mathf.RoundToInt(coverage * FaceOpacity * 255f);
                    pixels[y * width + x] = c;
                }
            }

            return Encode(pixels, width, height);
        }

        /// <summary>The soft shadow: the same shape blurred by a Gaussian (sigma = blur / 2), faded out over the margin.</summary>
        private static byte[] RenderShadow()
        {
            int shapeWidth = 2 * Radius + StretchWidth;
            int shapeHeight = 2 * Radius + StretchHeight;
            int width = shapeWidth + 2 * ShadowMargin;
            int height = shapeHeight + 2 * ShadowMargin;
            Color32 ink = UiBuildKit.Hex(Ink);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f - ShadowMargin, y + 0.5f - ShadowMargin, shapeWidth, shapeHeight, Radius);
                    float alpha = ShadowOpacity * 0.5f * Erfc(d / (ShadowSigma * 1.41421356f));
                    Color32 c = ink;
                    c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
                    pixels[y * width + x] = c;
                }
            }

            return Encode(pixels, width, height);
        }

        /// <summary>Signed distance from a point to a w × h rounded rectangle at the origin (negative inside).</summary>
        private static float RoundedRectDistance(float px, float py, float w, float h, float r)
        {
            float qx = Mathf.Abs(px - w / 2f) - (w / 2f - r);
            float qy = Mathf.Abs(py - h / 2f) - (h / 2f - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>Complementary error function (Abramowitz and Stegun 7.1.26, error below 1.5e-7).</summary>
        private static float Erfc(float x)
        {
            float z = Mathf.Abs(x);
            float t = 1f / (1f + 0.3275911f * z);
            float poly = t * (0.254829592f + t * (-0.284496736f + t * (1.421413741f + t * (-1.453152027f + t * 1.061405429f))));
            float erfcPositive = poly * Mathf.Exp(-z * z);
            return x >= 0f ? erfcPositive : 2f - erfcPositive;
        }

        private static byte[] Encode(Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Writes the PNG when it differs from what is on disk, then imports it as a 9-sliced sprite.</summary>
        private static Sprite EnsureSprite(string path, byte[] png, int border)
        {
            UiBuildKit.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            Sprite sprite = UiBuildKit.LoadSpriteAt(path, PixelsPerUnit, new Vector4(border, border, border, border));
            if (sprite == null)
            {
                throw new InvalidOperationException($"[ToastBuilder] Could not load {path} as a sprite.");
            }

            return sprite;
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
