using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 17, the loading curtain (GDD §23 curtain layer): the screen built from its layout
    /// becomes the <see cref="LoadingCurtain"/> the Boot scene keeps for the whole session. Its root
    /// gets a canvas of its own above every other layer, a group to fade, and a transparent blocker
    /// that takes every touch while it covers. The ring turns on unscaled time (a paused time scale
    /// does not stop it) and the curtain stops it for Reduce motion; the spade inside it is a separate,
    /// still node. The tagline's two fine lines are plain colour, as on the main menu.
    /// </summary>
    public static class SceneLoadingCustomizer
    {
        /// <summary>TextMeshPro's channels (TexCoord1, Normal, Tangent); set here so the text never has to change the canvas.</summary>
        private const AdditionalCanvasShaderChannels TextChannels =
            AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

        public static void Customize(ScreenBuildContext ctx)
        {
            foreach (string id in new[] { "TaglineLineLeft", "TaglineLineRight" })
            {
                Image line = ctx.Nodes[id].gameObject.AddComponent<Image>();
                line.color = UiBuildKit.Hex(ctx.Node(id).color);
                line.raycastTarget = false;
            }

            var ring = ctx.Nodes["Ring"].gameObject.AddComponent<Spinner>();
            var ringSettings = new SerializedObject(ring);
            ringSettings.FindProperty("_unscaledTime").boolValue = true;
            ringSettings.ApplyModifiedPropertiesWithoutUndo();

            AddTouchBlocker(ctx);

            // The root is built under a temporary canvas, and a nested canvas ignores the render mode
            // and sorting order it is given (it stays World Space, order 0), so they are written into
            // the serialized canvas: in the Boot scene the curtain is a canvas of its own.
            GameObject root = ctx.Root.gameObject;
            var canvas = root.AddComponent<Canvas>();
            var canvasSettings = new SerializedObject(canvas);
            canvasSettings.FindProperty("m_RenderMode").intValue = (int)RenderMode.ScreenSpaceOverlay;
            canvasSettings.FindProperty("m_SortingOrder").intValue = SceneSetup.CurtainLayer;
            canvasSettings.FindProperty("m_AdditionalShaderChannelsFlag").intValue = (int)TextChannels;
            canvasSettings.ApplyModifiedPropertiesWithoutUndo();
            SceneSetup.ConfigureScaler(root.AddComponent<CanvasScaler>());
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<CanvasGroup>();

            // The builder adds the curtain as the screen's view and binds _ring to the Ring node.
            ctx.ViewType = typeof(LoadingCurtain);
        }

        /// <summary>
        /// A fully transparent image over the whole screen, just above the background: it is culled
        /// from drawing and only answers raycasts, so nothing under the curtain can be tapped.
        /// </summary>
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
    }
}
