using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Builds a screen prefab from its layout.json (see Tools/ArtTool/README.md): the background
    /// plate, the design groups, every sprite, button and label at its mockup position, then the
    /// screen's view with its serialized fields bound by name. A screen that needs widgets beyond
    /// that has a <c>Callbreak.Editor.Screens.&lt;Name&gt;Customizer</c> with
    /// <c>public static void Customize(ScreenBuildContext ctx)</c>, called before binding.
    /// Re-running overwrites the prefab, so every change belongs in the layout or the customizer.
    /// </summary>
    public static class ScreenLayoutBuilder
    {
        public const string PrefabFolder = "Assets/_Project/Prefabs/UI/Screens";

        public static string PrefabPath(string screenName) => $"{PrefabFolder}/{screenName}.prefab";

        /// <summary>Builds and saves the prefab; returns its path.</summary>
        public static string Build(string screenName)
        {
            ScreenLayout layout = ScreenLayout.Load(screenName);
            var ctx = new ScreenBuildContext(layout);

            // TextMeshPro only lays text out under a Canvas, and the labels are fitted by
            // measuring their layout, so the screen is built inside a temporary one.
            var canvasObject = new GameObject("LayoutBuildCanvas", typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            ctx.BuildParent = canvasObject.transform;
            string path = PrefabPath(screenName);
            try
            {
                BuildHierarchy(ctx);

                Type customizer = FindType($"Callbreak.Editor.Screens.{screenName}Customizer");
                customizer?.GetMethod("Customize", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { ctx });

                AttachView(ctx);
                UiBuildKit.EnsureFolder(PrefabFolder);
                PrefabUtility.SaveAsPrefabAsset(ctx.Root.gameObject, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }

            AssetDatabase.SaveAssets();
            foreach (string w in ctx.Warnings)
            {
                Debug.LogWarning($"[ScreenLayoutBuilder] {screenName}: {w}");
            }

            Debug.Log($"[ScreenLayoutBuilder] Built {path} ({ctx.Nodes.Count} nodes, {ctx.Warnings.Count} warnings)");
            return path;
        }

        private static void BuildHierarchy(ScreenBuildContext ctx)
        {
            var rootObject = new GameObject(ctx.Layout.name, typeof(RectTransform));
            ctx.Root = (RectTransform)rootObject.transform;
            if (ctx.BuildParent != null)
            {
                ctx.Root.SetParent(ctx.BuildParent, false);
            }

            Stretch(ctx.Root);

            BuildBackground(ctx);
            if (Enum.TryParse(ctx.Layout.name, out ScreenId _))
            {
                AddTouchBlocker(ctx.Root);
            }

            // Everything else sits under Content, which is what screen transitions slide.
            var content = new GameObject("Content", typeof(RectTransform));
            ctx.Content = (RectTransform)content.transform;
            ctx.Content.SetParent(ctx.Root, false);
            Stretch(ctx.Content);

            // Centre content scales with the design area; corner groups follow the safe area.
            ctx.Groups["Centre"] = UiBuildKit.CreateDesignGroup("Centre", ctx.Content, UiBuildKit.Attach.Centre);
            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            var safeRect = (RectTransform)safe.transform;
            safeRect.SetParent(ctx.Content, false);
            Stretch(safeRect);
            ctx.SafeArea = safeRect;
            foreach (string g in ctx.Layout.nodes.Where(n => string.IsNullOrEmpty(n.parent)).Select(n => GroupOf(n)).Distinct())
            {
                if (!ctx.Groups.ContainsKey(g))
                {
                    ctx.Groups[g] = UiBuildKit.CreateDesignGroup(g, g == "Full" ? ctx.Content : safeRect, ParseAttach(g));
                }
            }

            foreach (LayoutNode node in ctx.Layout.nodes)
            {
                BuildNode(ctx, node);
            }
        }

        /// <summary>
        /// A page the router shows takes every touch on it. Over a running match the results, the
        /// scorecard and the settings cover the table, and a tap between their buttons must not
        /// reach the hand, the call chips or the HUD underneath (the table and the call panel are
        /// not router pages and get no blocker). The image is fully transparent, so it is culled
        /// from drawing and only answers raycasts.
        /// </summary>
        private static void AddTouchBlocker(RectTransform root)
        {
            var blocker = new GameObject("TouchBlocker", typeof(RectTransform));
            var rect = (RectTransform)blocker.transform;
            rect.SetParent(root, false);
            Stretch(rect);
            var image = blocker.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
        }

        private static string GroupOf(LayoutNode n) => string.IsNullOrEmpty(n.group) ? "Centre" : n.group;

        private static UiBuildKit.Attach ParseAttach(string group) => group switch
        {
            "HeaderLeft" => UiBuildKit.Attach.TopLeft,
            "HeaderRight" => UiBuildKit.Attach.TopRight,
            "BottomLeft" => UiBuildKit.Attach.BottomLeft,
            "BottomRight" => UiBuildKit.Attach.BottomRight,
            "Top" => UiBuildKit.Attach.Top,
            "Bottom" => UiBuildKit.Attach.Bottom,
            _ => UiBuildKit.Attach.Centre,
        };

        private static void BuildBackground(ScreenBuildContext ctx)
        {
            ScreenLayout l = ctx.Layout;
            if (string.IsNullOrEmpty(l.background))
            {
                return;
            }

            Sprite plate = UiBuildKit.LoadSpriteAt(l.background, l.backgroundPpu);
            float[] r = l.backgroundRect != null && l.backgroundRect.Length == 4 ? l.backgroundRect : new[] { 0f, 0f, l.mockW, l.mockH };
            RectTransform bg = UiBuildKit.CreateAt(
                "Background", ctx.Root, new Vector2(r[0] + r[2] / 2f, r[1] + r[3] / 2f), UiBuildKit.ScreenCentre, UiBuildKit.FromMock(r[2], r[3]));
            UiBuildKit.AddImage(bg, plate, false);
            ctx.Background = bg;
        }

        private static void BuildNode(ScreenBuildContext ctx, LayoutNode node)
        {
            Transform parent;
            Vector2 parentCentre;
            if (!string.IsNullOrEmpty(node.parent) && ctx.Nodes.TryGetValue(node.parent, out RectTransform p))
            {
                parent = p;
                parentCentre = ctx.Layout.nodes.First(n => n.id == node.parent).Centre;
            }
            else
            {
                parent = ctx.Groups[GroupOf(node)];
                parentCentre = UiBuildKit.ScreenCentre;
            }

            if (node.IsLabel)
            {
                BuildLabel(ctx, node, parent, parentCentre);
                return;
            }

            Vector2 centre = node.rect != null ? node.Centre : parentCentre;
            Vector2 size = node.rect != null ? UiBuildKit.FromMock(node.rect[2], node.rect[3]) : Vector2.zero;
            RectTransform root = UiBuildKit.CreateAt(node.id, parent, centre, parentCentre, size);
            ctx.Nodes[node.id] = root;

            if (!string.IsNullOrEmpty(node.shadowSprite))
            {
                Sprite shadow = UiBuildKit.LoadSpriteAt(node.shadowSprite, node.shadowPpu);
                PlaceSprite(root, "Shadow", shadow, node.shadowRect, centre, 0f, false);
            }

            Image face = null;
            if (!string.IsNullOrEmpty(node.sprite))
            {
                bool isButton = node.kind == "button";
                Sprite sprite = LoadNodeSprite(node);
                float[] faceRect = node.spriteRect ?? node.rect;
                float multiplier = SliceMultiplier(node, sprite, faceRect);
                if (!string.IsNullOrEmpty(node.shadowUse))
                {
                    PlaceSharedShadow(ctx, node, root, centre, sprite, faceRect, multiplier);
                }

                face = PlaceSprite(root, "Face", sprite, faceRect, centre, node.spritePpu, isButton || node.raycast);
                if (face != null)
                {
                    face.pixelsPerUnitMultiplier = multiplier;
                    if (node.opacity > 0f && node.opacity < 1f)
                    {
                        face.color = new Color(1f, 1f, 1f, node.opacity);
                    }
                }

                if (sprite == null)
                {
                    ctx.Warnings.Add($"{node.id}: sprite {node.sprite} missing");
                }
            }

            if (node.kind == "button")
            {
                var button = root.gameObject.AddComponent<Button>();
                if (face == null)
                {
                    // A button without art still needs something to hit.
                    face = root.gameObject.AddComponent<Image>();
                    face.color = new Color(1f, 1f, 1f, 0f);
                }

                button.targetGraphic = face;
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
                so.FindProperty("_cue").intValue = (int)CueFor(node.id);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (node.hidden)
            {
                root.gameObject.SetActive(false);
            }
        }

        /// <summary>GDD §14: a button that leads back (back, close, cancel) has its own cue; every other button taps.</summary>
        public static SoundCue CueFor(string buttonId)
        {
            return buttonId == "BackButton" || buttonId == "CloseButton" || buttonId == "CancelButton" ? SoundCue.UiBack : SoundCue.UiTap;
        }

        /// <summary>
        /// A shared sprite that is 9-sliced only horizontally (a pill button) is scaled as a whole to
        /// the element's height, so its rounded ends and woven caps keep their proportions; sliced
        /// panels keep their corners at native size. "sliceScale" in the layout overrides this.
        /// </summary>
        private static float SliceMultiplier(LayoutNode node, Sprite sprite, float[] rect)
        {
            if (node.sliceScale > 0f)
            {
                return node.sliceScale;
            }

            if (sprite == null || sprite.border == Vector4.zero || rect == null || rect.Length != 4 || node.spritePpu > 0)
            {
                return 1f;
            }

            bool horizontalOnly = sprite.border.y == 0f && sprite.border.w == 0f;
            if (!horizontalOnly)
            {
                return 1f;
            }

            float nativeHeight = UiBuildKit.NativeSize(sprite).y;
            float height = UiBuildKit.FromMock(0f, rect[3]).y;
            return height > 0.01f ? nativeHeight / height : 1f;
        }

        /// <summary>
        /// A shared shadow sprite behind a shared face ("shadowUse"): its spread is either given in
        /// mockup pixels ("shadowSpread", for 9-sliced shadows) or taken from how much larger the
        /// shadow sprite is than the face sprite, scaled like the face; "shadowDrop" moves it down.
        /// </summary>
        private static void PlaceSharedShadow(ScreenBuildContext ctx, LayoutNode node, RectTransform root, Vector2 centre, Sprite face, float[] faceRect, float multiplier)
        {
            Sprite shadow = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{node.shadowUse}", 0f);
            if (shadow == null || faceRect == null)
            {
                ctx.Warnings.Add($"{node.id}: shadow {node.shadowUse} missing");
                return;
            }

            float spreadX, spreadY;
            if (node.shadowSpread > 0f)
            {
                spreadX = spreadY = node.shadowSpread;
            }
            else
            {
                Vector2 s = UiBuildKit.NativeSize(shadow), f = face != null ? UiBuildKit.NativeSize(face) : s;
                float scale = face != null && f.y > 0.01f ? UiBuildKit.FromMock(0f, faceRect[3]).y / f.y : 1f;
                spreadX = (s.x - f.x) / 2f * scale / UiBuildKit.ScaleX;
                spreadY = (s.y - f.y) / 2f * scale / UiBuildKit.ScaleY;
            }

            var rect = new[] { faceRect[0] - spreadX, faceRect[1] - spreadY + node.shadowDrop, faceRect[2] + 2 * spreadX, faceRect[3] + 2 * spreadY };
            Image image = PlaceSprite(root, "Shadow", shadow, rect, centre, 0f, false);
            if (image != null)
            {
                image.pixelsPerUnitMultiplier = node.shadowSpread > 0f ? 1f : multiplier;
                image.color = new Color(1f, 1f, 1f, node.shadowOpacity > 0f ? node.shadowOpacity : 1f);
            }
        }

        private static Sprite LoadNodeSprite(LayoutNode node)
        {
            Vector4 border = Vector4.zero;
            if (node.slice != null && node.slice.Length == 4)
            {
                float k = node.spritePpu > 0 ? node.spritePpu / (100f * 1672f / 1920f) : 1f;
                border = new Vector4(node.slice[0] * k, node.slice[3] * k, node.slice[2] * k, node.slice[1] * k);
            }

            return UiBuildKit.LoadSpriteAt(node.sprite, node.spritePpu, border);
        }

        /// <summary>
        /// Places a sprite over the mockup rectangle it was cut from. With a pixels-per-unit the
        /// sprite keeps its native size (it was cut at that scale); without one it stretches to
        /// the rectangle.
        /// </summary>
        private static Image PlaceSprite(RectTransform parent, string name, Sprite sprite, float[] rect, Vector2 parentCentre, float ppu, bool raycast)
        {
            if (rect == null || rect.Length != 4)
            {
                return null;
            }

            var centre = new Vector2(rect[0] + rect[2] / 2f, rect[1] + rect[3] / 2f);
            Vector2 size = ppu > 0 && sprite != null && sprite.border == Vector4.zero ? UiBuildKit.NativeSize(sprite) : UiBuildKit.FromMock(rect[2], rect[3]);
            RectTransform r = UiBuildKit.CreateAt(name, parent, centre, parentCentre, size);
            return UiBuildKit.AddImage(r, sprite, raycast);
        }

        private static void BuildLabel(ScreenBuildContext ctx, LayoutNode node, Transform parent, Vector2 parentCentre)
        {
            if (node.inkBox == null || node.inkBox.Length != 4)
            {
                ctx.Warnings.Add($"{node.id}: no ink box measured; label skipped");
                return;
            }

            TMP_FontAsset font = UiBuildKit.LoadFontRole(node.font);
            Color top = UiBuildKit.Hex(string.IsNullOrEmpty(node.colorTop) ? node.color : node.colorTop);
            Color bottom = UiBuildKit.Hex(string.IsNullOrEmpty(node.colorBottom) ? node.color : node.colorBottom);
            TextMeshProUGUI label = UiBuildKit.FitLabel(node.id, parent, parentCentre, node.text ?? "", font, node.inkBox,
                string.IsNullOrEmpty(node.align) ? "centre" : node.align, node.tracked, top, bottom);
            if (node.style == "buttonLabel")
            {
                label.fontSharedMaterial = UiBuildKit.LoadShadowPreset(font, "ButtonLabel", new Color(0.29f, 0.03f, 0.04f, 0.9f), new Vector2(0.25f, -0.35f), 0.2f);
            }

            if (!string.IsNullOrEmpty(node.key))
            {
                var localized = label.gameObject.AddComponent<LocalizedText>();
                var so = new SerializedObject(localized);
                so.FindProperty("_key").stringValue = node.key;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            ctx.Nodes[node.id] = (RectTransform)label.transform;
            if (node.hidden)
            {
                label.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Adds the screen's view (Callbreak.Presentation.Screens.&lt;Name&gt;View) and fills its
        /// serialized fields: field <c>_createButton</c> takes node <c>CreateButton</c> (or a node
        /// whose "bind" says CreateButton); array fields take <c>Name0</c>, <c>Name1</c>...
        /// </summary>
        private static void AttachView(ScreenBuildContext ctx)
        {
            // Screens first; the in-match table and its overlays live in their own folders (GDD §20).
            Type viewType = FindType($"Callbreak.Presentation.Screens.{ctx.Layout.name}View")
                ?? FindType($"Callbreak.Presentation.Table.{ctx.Layout.name}View")
                ?? FindType($"Callbreak.Presentation.Overlays.{ctx.Layout.name}View");
            if (viewType == null)
            {
                ctx.Warnings.Add($"no view type Callbreak.Presentation.Screens.{ctx.Layout.name}View; prefab has no view");
                return;
            }

            Component view = ctx.Root.gameObject.AddComponent(viewType);
            var so = new SerializedObject(view);
            SerializedProperty contentProp = so.FindProperty("_content");
            if (contentProp != null)
            {
                contentProp.objectReferenceValue = ctx.Content;
            }

            SerializedProperty id = so.FindProperty("_screenId");
            if (id != null && Enum.TryParse(ctx.Layout.name, out ScreenId screenId))
            {
                id.intValue = (int)screenId;
            }

            var bound = new HashSet<string>();
            foreach (FieldInfo field in AllSerializedFields(viewType))
            {
                if (field.DeclaringType == typeof(UIScreen))
                {
                    continue;
                }

                SerializedProperty prop = so.FindProperty(field.Name);
                if (prop == null)
                {
                    continue;
                }

                string nodeName = char.ToUpperInvariant(field.Name.TrimStart('_')[0]) + field.Name.TrimStart('_').Substring(1);
                if (field.FieldType.IsArray)
                {
                    Type element = field.FieldType.GetElementType();
                    var found = new List<UnityEngine.Object>();
                    for (int i = 0; ; i++)
                    {
                        UnityEngine.Object o = Resolve(ctx, $"{nodeName}{i}", element);
                        if (o == null)
                        {
                            break;
                        }

                        found.Add(o);
                    }

                    if (found.Count == 0)
                    {
                        if (!ctx.Bound.ContainsKey(field.Name))
                        {
                            ctx.Warnings.Add($"field {field.Name} ({field.FieldType.Name}): no nodes {nodeName}0..");
                        }

                        continue;
                    }

                    prop.arraySize = found.Count;
                    for (int i = 0; i < found.Count; i++)
                    {
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
                    }

                    bound.Add(field.Name);
                    continue;
                }

                if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                UnityEngine.Object target = ctx.Bound.TryGetValue(field.Name, out UnityEngine.Object explicitTarget)
                    ? explicitTarget
                    : Resolve(ctx, nodeName, field.FieldType);
                if (target == null)
                {
                    ctx.Warnings.Add($"field {field.Name} ({field.FieldType.Name}): no node {nodeName}");
                    continue;
                }

                prop.objectReferenceValue = target;
                bound.Add(field.Name);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UnityEngine.Object Resolve(ScreenBuildContext ctx, string nodeName, Type type)
        {
            if (ctx.Bound.TryGetValue("_" + char.ToLowerInvariant(nodeName[0]) + nodeName.Substring(1), out UnityEngine.Object pre))
            {
                return pre;
            }

            RectTransform rect = null;
            if (!ctx.Nodes.TryGetValue(nodeName, out rect))
            {
                LayoutNode byBind = ctx.Layout.nodes.FirstOrDefault(n => n.bind == nodeName);
                if (byBind == null || !ctx.Nodes.TryGetValue(byBind.id, out rect))
                {
                    return null;
                }
            }

            if (type == typeof(GameObject))
            {
                return rect.gameObject;
            }

            if (type == typeof(RectTransform) || type == typeof(Transform))
            {
                return rect;
            }

            if (type == typeof(Image))
            {
                Transform face = rect.Find("Face");
                if (face != null)
                {
                    return face.GetComponent<Image>();
                }
            }

            Component c = rect.GetComponent(type);
            if (c == null && typeof(Component).IsAssignableFrom(type))
            {
                c = rect.GetComponentInChildren(type, true);
            }

            return c;
        }

        private static IEnumerable<FieldInfo> AllSerializedFields(Type t)
        {
            for (Type cur = t; cur != null && cur != typeof(MonoBehaviour); cur = cur.BaseType)
            {
                foreach (FieldInfo f in cur.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (f.IsPublic || f.GetCustomAttribute<SerializeField>() != null)
                    {
                        yield return f;
                    }
                }
            }
        }

        public static Type FindType(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = a.GetType(fullName, false);
                if (t != null)
                {
                    return t;
                }
            }

            return null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>What a screen customizer gets: the layout, the built nodes by id, and a place to bind extra fields.</summary>
    public sealed class ScreenBuildContext
    {
        public ScreenBuildContext(ScreenLayout layout)
        {
            Layout = layout;
        }

        public ScreenLayout Layout { get; }

        public RectTransform Root { get; set; }

        /// <summary>The temporary canvas the screen is built under (text layout needs one).</summary>
        public Transform BuildParent { get; set; }

        public RectTransform Background { get; set; }

        /// <summary>Parent of everything except the background.</summary>
        public RectTransform Content { get; set; }

        public RectTransform SafeArea { get; set; }

        public Dictionary<string, RectTransform> Groups { get; } = new Dictionary<string, RectTransform>();

        public Dictionary<string, RectTransform> Nodes { get; } = new Dictionary<string, RectTransform>();

        /// <summary>View field name (with underscore) → object, for fields a customizer fills itself.</summary>
        public Dictionary<string, UnityEngine.Object> Bound { get; } = new Dictionary<string, UnityEngine.Object>();

        public List<string> Warnings { get; } = new List<string>();

        public LayoutNode Node(string id) => Layout.nodes.First(n => n.id == id);

        /// <summary>A node's centre in mockup pixels, for placing new children with UiBuildKit.CreateAt.</summary>
        public Vector2 CentreOf(string id) => Node(id).Centre;
    }
}
