using System;
using System.IO;
using System.Linq;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Table;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor
{
    /// <summary>
    /// Everything the table needs to draw cards (GDD §13, §23):
    /// the import settings of the three decks (sprites with mip maps, since a fan card is a fifth
    /// of its texture's size); the <see cref="CardSpriteProvider"/> asset indexing 52 faces and a
    /// back per deck; the two effect sprites the game draws itself (GDD §13 "glow, dimming and
    /// shadows are drawn by the game, never baked"): the card silhouette for the subdued overlay
    /// and the gold glow of a legal card, both derived from the cards' own outline; and the Card
    /// prefab the pool instantiates 52 times. Re-running rebuilds everything; a PNG is only
    /// rewritten when its pixels change.
    /// </summary>
    public static class TableCardBuilder
    {
        public const string CardsFolder = "Assets/_Project/Art/Cards";
        public const string FacesFolder = CardsFolder + "/Faces";
        public const string LibraryPath = "Assets/_Project/Config/Cards/CardSprites.asset";
        public const string CardPrefabPath = "Assets/_Project/Prefabs/Table/Card.prefab";
        public const string GameplayScreenPrefabPath = "Assets/_Project/Prefabs/Table/GameplayScreen.prefab";
        public const string SilhouettePath = "Assets/_Project/Art/Screens/Table/FX_CardSilhouette.png";
        public const string GlowPath = "Assets/_Project/Art/Screens/Table/FX_CardGlow.png";
        public const string ShadowPath = "Assets/_Project/Art/UI/Common/UI_Panel_Card_Shadow.png";

        /// <summary>A hand card on the 1080p canvas (GDD §12: 180 wide; the height follows the art's 228 × 358).</summary>
        public const float CardWidth = 180f;

        public const float CardHeight = CardWidth * 358f / 228f;

        // Glow, in card-texture pixels: a solid rim at the edge, then a halo that fades out.
        // Measured on mockup 08's A♥: a 2.5 px rim and about 7 px of halo at hand size (× 228 / 156.75).
        private const int GlowMargin = 18;
        private const float GlowRim = 3.6f;
        private const float GlowHalo = 11f;
        private const float GlowHaloOpacity = 0.62f;
        private const string GlowRimColour = "#FFB20C";
        private const string GlowHaloColour = "#FF9A10";

        // The subdued overlay, fitted to mockup 08's 8♥ for blending in linear light (GDD §13: "a
        // warm-dark overlay, never transparency").
        private const string DimColour = "#BF9A8B";
        private const float DimOpacity = 0.20f;

        private const float ShadowSpread = 9f;
        private const float ShadowDrop = 3f;
        private const float ShadowOpacity = 0.55f;
        private const float ShadowSliceScale = 2.6f;

        private static readonly (string id, string folder)[] Decks =
        {
            ("classic-crimson", "ClassicCrimson"),
            ("himalayan-ivory", "HimalayanIvory"),
            ("kathmandu-gold", "KathmanduGold"),
        };

        private static readonly string[] SuitNames = { "Clubs", "Diamonds", "Hearts", "Spades" };

        [MenuItem("Callbreak/Build/Table Card Assets")]
        public static void BuildMenu()
        {
            Debug.Log(BuildAll());
        }

        /// <summary>
        /// ADR 053 in one go, for a batch run (-executeMethod): the card assets with the shared faces
        /// and the deck bases, then the bases added under the main menu's fan and the shop's decks.
        /// </summary>
        public static void BuildDeckBases()
        {
            Debug.Log(BuildAll());
            Debug.Log("[MainMenuBuilder] " + MainMenuBuilder.AddFanBases());
            Debug.Log("[ShopCustomizer] " + Screens.ShopCustomizer.AddDeckBases());
        }

        /// <summary>Builds the sprite library, the effect sprites and the Card prefab; returns a report.</summary>
        public static string BuildAll()
        {
            CardSpriteProvider library = BuildLibrary(out string libraryReport);
            BuildEffects();
            BuildCardPrefab(library);
            BuildGameplayScreen();
            AssetDatabase.SaveAssets();
            return $"[TableCardBuilder] {libraryReport}; effects {SilhouettePath}, {GlowPath}; prefabs {CardPrefabPath}, {GameplayScreenPrefabPath}";
        }

        /// <summary>The library asset, built on first use.</summary>
        public static CardSpriteProvider EnsureLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<CardSpriteProvider>(LibraryPath);
            return library != null && library.DeckCount == Decks.Length ? library : BuildLibrary(out _);
        }

        /// <summary>The Card prefab's view, built on first use.</summary>
        public static CardView EnsureCardPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (prefab == null || prefab.GetComponent<CardView>() == null)
            {
                BuildEffects();
                BuildCardPrefab(EnsureLibrary());
                AssetDatabase.SaveAssets();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            }

            return prefab.GetComponent<CardView>();
        }

        /// <summary>
        /// One set of 52 faces for every deck (<see cref="FacesFolder"/>, see-through around the
        /// marks) and, per deck, the base drawn under them and the back (ADR 053).
        /// </summary>
        public static CardSpriteProvider BuildLibrary(out string report)
        {
            var decks = new CardSpriteProvider.DeckSprites[Decks.Length];
            int missing = 0;
            var faces = new Sprite[Card.Count];
            for (int id = 0; id < Card.Count; id++)
            {
                Card card = Card.FromId(id);
                faces[id] = LoadCardSprite($"{FacesFolder}/Card_{SuitNames[(int)card.Suit]}_{TurnPrompt.RankLabel(card.Rank)}.png");
                if (faces[id] == null)
                {
                    missing++;
                }
            }

            for (int d = 0; d < Decks.Length; d++)
            {
                string folder = $"{CardsFolder}/{Decks[d].folder}";
                Sprite back = LoadCardSprite($"{folder}/Back_{Decks[d].folder}.png");
                Sprite cardBase = LoadCardSprite($"{folder}/Base_{Decks[d].folder}.png");
                missing += (back == null ? 1 : 0) + (cardBase == null ? 1 : 0);
                decks[d] = new CardSpriteProvider.DeckSprites(Decks[d].id, back, cardBase, faces);
            }

            var library = AssetDatabase.LoadAssetAtPath<CardSpriteProvider>(LibraryPath);
            if (library == null)
            {
                UiBuildKit.EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
                library = ScriptableObject.CreateInstance<CardSpriteProvider>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.SetDecks(decks, Decks[0].id);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            report = $"{LibraryPath}: {Decks.Length} decks, {Card.Count + Decks.Length * 2 - missing} sprites, {missing} missing";
            return library;
        }

        /// <summary>
        /// Card textures are sprites shown far below their own size (a fan card is 46 px wide from a
        /// 228 px texture), so they keep mip maps; everything else follows the UI recipe.
        /// </summary>
        private static Sprite LoadCardSprite(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[TableCardBuilder] Missing card art: {path}");
                return null;
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = (TextureImporter)AssetImporter.GetAtPath(path);
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !importer.mipmapEnabled
                || !importer.alphaIsTransparency
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.filterMode != FilterMode.Trilinear
                || !Mathf.Approximately(importer.spritePixelsPerUnit, 100f)
                || settings.spriteMeshType != SpriteMeshType.FullRect;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Draws the silhouette and the glow from the outline of a card back.</summary>
        public static void BuildEffects()
        {
            string source = $"{CardsFolder}/{Decks[0].folder}/Back_{Decks[0].folder}.png";
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(source)))
                {
                    throw new InvalidOperationException($"[TableCardBuilder] Could not read {source}");
                }

                int w = texture.width, h = texture.height;
                Color32[] card = texture.GetPixels32();
                WriteSprite(SilhouettePath, RenderSilhouette(card, w, h));
                WriteSprite(GlowPath, RenderGlow(card, w, h));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static byte[] RenderSilhouette(Color32[] card, int w, int h)
        {
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, card[i].a);
            }

            return Encode(pixels, w, h);
        }

        private static byte[] RenderGlow(Color32[] card, int w, int h)
        {
            int gw = w + 2 * GlowMargin, gh = h + 2 * GlowMargin;
            int reach = GlowMargin;

            // Distance to the card, outside it: first along each row, then combined down the columns.
            var rowDistance = new float[gw * gh];
            for (int y = 0; y < gh; y++)
            {
                int cy = y - GlowMargin;
                for (int x = 0; x < gw; x++)
                {
                    float best = float.MaxValue;
                    if (cy >= 0 && cy < h)
                    {
                        for (int dx = -reach; dx <= reach; dx++)
                        {
                            int cx = x - GlowMargin + dx;
                            if (cx < 0 || cx >= w)
                            {
                                continue;
                            }

                            // Coverage moves the edge by up to half a pixel, which keeps the rim smooth.
                            float a = card[cy * w + cx].a / 255f;
                            if (a <= 0.02f)
                            {
                                continue;
                            }

                            best = Mathf.Min(best, Mathf.Abs(dx) + (1f - a));
                        }
                    }

                    rowDistance[y * gw + x] = best;
                }
            }

            Color rim = UiBuildKit.Hex(GlowRimColour), halo = UiBuildKit.Hex(GlowHaloColour);
            var pixels = new Color32[gw * gh];
            for (int y = 0; y < gh; y++)
            {
                for (int x = 0; x < gw; x++)
                {
                    float best = float.MaxValue;
                    for (int dy = -reach; dy <= reach; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= gh)
                        {
                            continue;
                        }

                        float row = rowDistance[yy * gw + x];
                        if (row >= float.MaxValue)
                        {
                            continue;
                        }

                        best = Mathf.Min(best, Mathf.Sqrt(row * row + dy * dy));
                    }

                    float rimAlpha = Mathf.Clamp01(GlowRim + 0.5f - best);
                    float t = Mathf.Clamp01(1f - (best - GlowRim) / GlowHalo);
                    float haloAlpha = best >= float.MaxValue ? 0f : GlowHaloOpacity * t * t;
                    float alpha = Mathf.Max(rimAlpha, haloAlpha);
                    Color c = Color.Lerp(halo, rim, rimAlpha);
                    c.a = alpha;
                    pixels[y * gw + x] = c;
                }
            }

            return Encode(pixels, gw, gh);
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

        private static void WriteSprite(string path, byte[] png)
        {
            UiBuildKit.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        /// <summary>
        /// Card.prefab: shadow, glow, the deck's base, the face and the subdued overlay under one
        /// root the size of a hand card. The face shows a back until a card is assigned; the base
        /// shows only under a face (ADR 053).
        /// </summary>
        public static void BuildCardPrefab(CardSpriteProvider library)
        {
            Sprite silhouette = UiBuildKit.LoadSpriteAt(SilhouettePath, 100f);
            Sprite glow = UiBuildKit.LoadSpriteAt(GlowPath, 100f);
            Sprite shadow = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowPath);
            Sprite back = library != null ? library.Back(library.DefaultDeckId) : null;

            var root = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(CardWidth, CardHeight);
                var group = root.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;

                Image shadowImage = Layer(rect, "Shadow", shadow, new Vector2(-ShadowSpread, -ShadowSpread - ShadowDrop), new Vector2(ShadowSpread, ShadowSpread - ShadowDrop));
                shadowImage.color = new Color(1f, 1f, 1f, ShadowOpacity);
                shadowImage.type = Image.Type.Sliced;
                shadowImage.pixelsPerUnitMultiplier = ShadowSliceScale;

                float margin = GlowMargin * CardWidth / 228f;
                Image glowImage = Layer(rect, "Glow", glow, new Vector2(-margin, -margin), new Vector2(margin, margin));
                glowImage.enabled = false;

                Image cardBase = Layer(rect, "Base", null, Vector2.zero, Vector2.zero);
                cardBase.enabled = false;

                Image face = Layer(rect, "Face", back, Vector2.zero, Vector2.zero);

                Image dim = Layer(rect, "Dim", silhouette, Vector2.zero, Vector2.zero);
                Color dimColour = UiBuildKit.Hex(DimColour);
                dimColour.a = DimOpacity;
                dim.color = dimColour;
                dim.enabled = false;

                var perspectives = new[]
                {
                    shadowImage.gameObject.AddComponent<CardPerspective>(),
                    glowImage.gameObject.AddComponent<CardPerspective>(),
                    cardBase.gameObject.AddComponent<CardPerspective>(),
                    face.gameObject.AddComponent<CardPerspective>(),
                    dim.gameObject.AddComponent<CardPerspective>(),
                };

                var view = root.AddComponent<CardView>();
                var so = new SerializedObject(view);
                so.FindProperty("_rect").objectReferenceValue = rect;
                so.FindProperty("_group").objectReferenceValue = group;
                so.FindProperty("_shadow").objectReferenceValue = shadowImage;
                so.FindProperty("_glow").objectReferenceValue = glowImage;
                so.FindProperty("_base").objectReferenceValue = cardBase;
                so.FindProperty("_face").objectReferenceValue = face;
                so.FindProperty("_dim").objectReferenceValue = dim;
                SerializedProperty list = so.FindProperty("_perspectives");
                list.arraySize = perspectives.Length;
                for (int i = 0; i < perspectives.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = perspectives[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                int uiLayer = LayerMask.NameToLayer("UI");
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = uiLayer;
                }

                UiBuildKit.EnsureFolder(Path.GetDirectoryName(CardPrefabPath).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// GameplayScreen.prefab: the empty, see-through screen at the bottom of the in-match screen
        /// stack (its id is Gameplay). The Table scene's router gets it as its first screen.
        /// </summary>
        public static void BuildGameplayScreen()
        {
            var root = new GameObject("GameplayScreen", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var screen = root.AddComponent<GameplayScreen>();
                var so = new SerializedObject(screen);
                so.FindProperty("_screenId").intValue = (int)Callbreak.Presentation.Framework.ScreenId.Gameplay;
                so.ApplyModifiedPropertiesWithoutUndo();
                root.layer = LayerMask.NameToLayer("UI");
                UiBuildKit.EnsureFolder(Path.GetDirectoryName(GameplayScreenPrefabPath).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, GameplayScreenPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Image Layer(RectTransform parent, string name, Sprite sprite, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }
    }
}
