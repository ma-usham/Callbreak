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
    /// the import settings of the cards (sprites with mip maps, since a fan card is a fifth
    /// of its texture's size, filtered anisotropically, since a thrown card lies flat); the
    /// <see cref="CardSpriteProvider"/> asset indexing each deck's 52 faces and its back (since ADR
    /// 068 every deck has its own faces, each a whole card, so no deck has a base); the
    /// premultiplied material a shared face over a base would draw with (ADR 054); the two effect
    /// sprites the game draws itself (GDD §13 "glow, dimming and shadows are drawn by the game, never
    /// baked"): the card silhouette for the subdued overlay and the gold glow of a legal card, both
    /// derived from the cards' own outline; and the Card prefab the pool instantiates 52 times.
    /// Re-running rebuilds everything; a PNG is only rewritten when its pixels change.
    /// </summary>
    public static class TableCardBuilder
    {
        public const string CardsFolder = "Assets/_Project/Art/Cards";
        public const string FaceMaterialPath = CardsFolder + "/CardFace.mat";
        public const string FaceShaderName = "Callbreak/UI/Premultiplied";
        public const string LibraryPath = "Assets/_Project/Config/Cards/CardSprites.asset";
        public const string CardPrefabPath = "Assets/_Project/Prefabs/Table/Card.prefab";
        public const string GameplayScreenPrefabPath = "Assets/_Project/Prefabs/Table/GameplayScreen.prefab";
        public const string SilhouettePath = "Assets/_Project/Art/Screens/Table/FX_CardSilhouette.png";
        public const string GlowPath = "Assets/_Project/Art/Screens/Table/FX_CardGlow.png";
        public const string ShadowPath = "Assets/_Project/Art/UI/Common/UI_Panel_Card_Shadow.png";

        /// <summary>
        /// A hand card on the 1080p canvas: as tall as GDD §12's card (180 wide at the old art's
        /// 228 × 358), in the shape of Figma's 512 × 712 decks since ADR 068.
        /// </summary>
        public const float CardHeight = 180f * 358f / 228f;

        public const float CardWidth = CardHeight * 512f / 712f;

        // Glow, in card-texture pixels: a solid rim at the edge, then a halo that fades out.
        // Measured on mockup 08's A♥: a 2.5 px rim and about 7 px of halo at hand size (× 228 / 156.75),
        // on a back 358 px tall; GlowTexels scales them to the back's own height.
        private const int GlowMargin = 18;
        private const float GlowTexelHeight = 358f;
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

        // The main menu's fan: its middle aces' centres stand this far apart (canvas units; the spade
        // and the diamond of MainMenuBuilder's fan, 168.5 mockup px).
        private const float FanCardStride = 193.4f;

        // A card thrown on the table lies flat, drawn at 64% of its height (CardView): without
        // anisotropic filtering the GPU picks its mip level from that squash and the face blurs.
        // Phones use the Mobile quality level, which filters anisotropically only the textures that
        // ask for it (the Editor's PC level forces it on, so there the cards looked clear).
        private const int CardAnisoLevel = 8;

        // The decks of CosmeticCatalog.Decks, the default first, and the folder of each one's art
        // (Tools/ArtPipeline/NewUI/cut_decks.py).
        private static readonly (string id, string folder)[] Decks =
        {
            ("classic", "Classic"),
            ("mithila-ink", "MithilaInk"),
            ("kathmandu-royal", "KathmanduRoyal"),
        };

        private static readonly string[] SuitNames = { "Clubs", "Diamonds", "Hearts", "Spades" };

        [MenuItem("Callbreak/Build/Table Card Assets")]
        public static void BuildMenu()
        {
            Debug.Log(BuildAll());
        }

        /// <summary>
        /// ADR 068 in one go, for a batch run (-executeMethod): the card assets with every deck's own
        /// faces, then the main menu's fan given the default deck's aces at the new shape.
        /// </summary>
        public static void BuildDeckFaces()
        {
            Debug.Log(BuildAll());
            Debug.Log("[TableCardBuilder] " + UseDeckFanFaces());
        }

        /// <summary>
        /// The material of the shared faces (ADR 054): they are premultiplied in linear light, and
        /// only premultiplied blending keeps their mip maps and filtering true. Created on first use.
        /// </summary>
        public static Material EnsureFaceMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FaceMaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(FaceShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"The shader {FaceShaderName} is missing (Art/Shaders/UI_Premultiplied.shader).");
            }

            material = new Material(shader) { name = "CardFace" };
            AssetDatabase.CreateAsset(material, FaceMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
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
        /// Per deck (ADR 068), its 52 faces, each a whole card (Art/Cards/&lt;Deck&gt;/Card_&lt;Suit&gt;_&lt;Rank&gt;.png),
        /// and its back; no deck has a base.
        /// </summary>
        public static CardSpriteProvider BuildLibrary(out string report)
        {
            var decks = new CardSpriteProvider.DeckSprites[Decks.Length];
            int missing = 0;
            for (int d = 0; d < Decks.Length; d++)
            {
                string folder = $"{CardsFolder}/{Decks[d].folder}";
                var faces = new Sprite[Card.Count];
                for (int id = 0; id < Card.Count; id++)
                {
                    Card card = Card.FromId(id);
                    faces[id] = LoadCardSprite($"{folder}/Card_{SuitNames[(int)card.Suit]}_{TurnPrompt.RankLabel(card.Rank)}.png", false);
                    missing += faces[id] == null ? 1 : 0;
                }

                Sprite back = LoadCardSprite($"{folder}/Back_{Decks[d].folder}.png", false);
                missing += back == null ? 1 : 0;
                decks[d] = new CardSpriteProvider.DeckSprites(Decks[d].id, back, null, faces);
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
            report = $"{LibraryPath}: {Decks.Length} decks, {Decks.Length * (Card.Count + 1) - missing} sprites, {missing} missing";
            return library;
        }

        /// <summary>
        /// Card textures are sprites shown far below their own size (a fan card is 33 px wide from a
        /// 278 px texture), so they keep mip maps, and lying on the table, so they are filtered
        /// anisotropically (<see cref="CardAnisoLevel"/>); everything else follows the UI recipe.
        /// </summary>
        /// <param name="premultiplied">
        /// A shared face over a base (ADR 054): its colour is premultiplied, so Unity must not bleed
        /// colour into its see-through pixels (Alpha Is Transparency off); whole cards and backs have
        /// it on.
        /// </param>
        private static Sprite LoadCardSprite(string path, bool premultiplied)
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
                || importer.alphaIsTransparency == premultiplied
                || !importer.sRGBTexture
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.filterMode != FilterMode.Trilinear
                || importer.anisoLevel != CardAnisoLevel
                || !Mathf.Approximately(importer.spritePixelsPerUnit, 100f)
                || settings.spriteMeshType != SpriteMeshType.FullRect;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = !premultiplied;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = CardAnisoLevel;
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
                WriteSprite(GlowPath, RenderGlow(card, w, h, GlowTexels(h)));
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

        /// <summary>The glow's sizes in the texels of a back <paramref name="height"/> px tall (they are measured on one 358 tall).</summary>
        private static float GlowTexels(int height) => height / GlowTexelHeight;

        private static int GlowMarginOf(int height) => Mathf.RoundToInt(GlowMargin * GlowTexels(height));

        private static byte[] RenderGlow(Color32[] card, int w, int h, float texels)
        {
            int margin = GlowMarginOf(h);
            int gw = w + 2 * margin, gh = h + 2 * margin;
            int reach = margin;
            float rimWidth = GlowRim * texels, haloWidth = GlowHalo * texels;

            // Distance to the card, outside it: first along each row, then combined down the columns.
            var rowDistance = new float[gw * gh];
            for (int y = 0; y < gh; y++)
            {
                int cy = y - margin;
                for (int x = 0; x < gw; x++)
                {
                    float best = float.MaxValue;
                    if (cy >= 0 && cy < h)
                    {
                        for (int dx = -reach; dx <= reach; dx++)
                        {
                            int cx = x - margin + dx;
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

                    float rimAlpha = Mathf.Clamp01(rimWidth + 0.5f - best);
                    float t = Mathf.Clamp01(1f - (best - rimWidth) / haloWidth);
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

                // The glow reaches its margin past the back it was drawn from (BuildEffects), in that back's texels.
                int backHeight = back != null ? (int)back.rect.height : (int)GlowTexelHeight;
                float margin = GlowMarginOf(backHeight) * CardHeight / backHeight;
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
                so.FindProperty("_faceMaterial").objectReferenceValue = EnsureFaceMaterial();
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
        /// Gives the four aces of the saved main menu's fan the default deck's own faces at its shape
        /// (ADR 068): each card keeps its height, its top edge and its tilt and takes the deck's width;
        /// its face is a whole card, drawn with the default UI material, with no base under it.
        /// Everything else in the prefab stays as it is. The presenter puts in the equipped deck's aces
        /// when the menu opens.
        /// </summary>
        public static string UseDeckFanFaces()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MainMenuBuilder.PrefabPath);
            try
            {
                CardSpriteProvider cards = EnsureLibrary();
                var serialized = new SerializedObject(root.GetComponent<Callbreak.Presentation.Screens.MainMenuView>());
                SerializedProperty faces = serialized.FindProperty("_fanFaces");
                SerializedProperty bases = serialized.FindProperty("_fanBases");
                for (int i = 0; i < faces.arraySize; i++)
                {
                    var face = faces.GetArrayElementAtIndex(i).objectReferenceValue as Image;
                    if (face == null)
                    {
                        return $"FAILED: the fan has no face {i}";
                    }

                    // The fan's faces are indexed by suit; each hangs from its top edge, so a card that
                    // keeps its height and its centre keeps that edge. The fan spreads from its foot as
                    // much as a card widened, so each ace still shows its corner index beside the next.
                    Sprite ace = cards.Face(cards.DefaultDeckId, new Card((Suit)i, Rank.Ace));
                    face.sprite = ace;
                    face.material = null;
                    var card = (RectTransform)face.transform.parent;
                    float width = card.sizeDelta.y * ace.rect.width / ace.rect.height;
                    if (!Mathf.Approximately(width, card.sizeDelta.x))
                    {
                        card.anchoredPosition = new Vector2(card.anchoredPosition.x * (1f + ((width - card.sizeDelta.x) / FanCardStride)), card.anchoredPosition.y);
                        card.sizeDelta = new Vector2(width, card.sizeDelta.y);
                    }

                    // Cleared through its serialized property: a sprite whose file is gone reads as null,
                    // so setting the Image's sprite to null would keep the dangling reference.
                    var under = i < bases.arraySize ? bases.GetArrayElementAtIndex(i).objectReferenceValue as Image : null;
                    if (under != null)
                    {
                        var image = new SerializedObject(under);
                        image.FindProperty("m_Sprite").objectReferenceValue = null;
                        image.FindProperty("m_Enabled").boolValue = false;
                        image.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, MainMenuBuilder.PrefabPath, out bool saved);
                return saved ? $"saved {MainMenuBuilder.PrefabPath}: the fan shows {cards.DefaultDeckId}'s aces at its shape" : $"FAILED to save {MainMenuBuilder.PrefabPath}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
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
