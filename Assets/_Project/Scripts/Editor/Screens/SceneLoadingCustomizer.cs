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
    /// still node. The tagline's two fine lines are plain colour, as on the main menu. Over all of it
    /// lies the curtain's black look (ADR 042), hidden until the curtain shows it.
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
            BuildDark(ctx);

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
        /// The black look (ADR 042), the root's last child so it lies over everything else: black to
        /// the screen's edges and, in the middle of the screen however it is turned, the loading
        /// emblem at the loading screen's size: the same ring, turning on unscaled time, round the
        /// gold spade of the menu's Play buttons (the loading screen's black spade would not show on
        /// black). It takes every touch. Hidden until <see cref="LoadingCurtain.CoverDarkAsync"/>.
        /// </summary>
        private static void BuildDark(ScreenBuildContext ctx)
        {
            RectTransform dark = Child("Dark", ctx.Root);
            dark.anchorMin = Vector2.zero;
            dark.anchorMax = Vector2.one;
            dark.offsetMin = Vector2.zero;
            dark.offsetMax = Vector2.zero;
            Image black = dark.gameObject.AddComponent<Image>();
            black.color = Color.black;
            black.raycastTarget = true;

            LayoutNode ringNode = ctx.Node("Ring"), spadeNode = ctx.Node("Spade");
            RectTransform ring = Centred("Ring", dark, UiBuildKit.FromMock(ringNode.rect[2], ringNode.rect[3]), Vector2.zero);
            UiBuildKit.AddImage(ring, UiBuildKit.LoadSpriteAt(ringNode.sprite, 0f), false);
            var spinner = ring.gameObject.AddComponent<Spinner>();
            var spinnerSettings = new SerializedObject(spinner);
            spinnerSettings.FindProperty("_unscaledTime").boolValue = true;
            spinnerSettings.ApplyModifiedPropertiesWithoutUndo();

            // Where the spade sits in the ring on the loading screen (the mockup's y runs down).
            Vector2 offset = UiBuildKit.FromMock(spadeNode.Centre.x - ringNode.Centre.x, ringNode.Centre.y - spadeNode.Centre.y);
            RectTransform spade = Centred("Spade", dark, UiBuildKit.FromMock(spadeNode.rect[2], spadeNode.rect[3]), offset);
            Image gold = UiBuildKit.AddImage(spade, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Icons/Icon_Spade_Gold.png", 0f), false);
            gold.preserveAspect = true;

            dark.gameObject.SetActive(false);
            ctx.Bound["_dark"] = dark.gameObject;
            ctx.Bound["_darkRing"] = spinner;
        }

        private static RectTransform Centred(string name, Transform parent, Vector2 size, Vector2 offset)
        {
            RectTransform rect = Child(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            return rect;
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
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
