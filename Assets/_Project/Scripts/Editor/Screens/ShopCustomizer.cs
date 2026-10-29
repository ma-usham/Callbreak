using System.Collections.Generic;
using System.Linq;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 15, the Shop (mockups 15 Cards, 20 Backgrounds and 21 Tables; the owner, 5 Oct 2026).
    /// The builder lays out mockup 15 (ArtSource/Screens/Shop); this adds what the other two
    /// categories need from their own measured layouts (ShopBackgroundsLayout on 20,
    /// ShopTablesLayout on 21), each fitted on its own mockup:
    /// <list type="bullet">
    /// <item>a title, subtitle and Use label per category, and the "Buy for (coin) N" row;</item>
    /// <item>three tiles per category (<see cref="ShopTiles"/>), each made from that category's
    /// measured tile 0 (picture, name, status line; the price line comes from mockup 21), at the
    /// measured spacing and in the middle of the panel. Nothing scrolls, so the mockups' chevrons,
    /// scroll track and "Swipe to browse" are not built;</item>
    /// <item>the drawer (<see cref="ShopDrawer"/>), always open and without the mockups'
    /// storefront disc and divider: the panel flush with the right edge and as tall as the screen,
    /// narrowed to <see cref="DrawerWidth"/>, its three category buttons at
    /// <see cref="CategoryScale"/> of the mockups' size and stacked in its middle; each button has
    /// its crimson (active) and ivory (inactive) look, each as a mockup draws it;</item>
    /// <item>the background and table pictures (<see cref="ShopPictures"/>).</item>
    /// </list>
    /// The UI pieces are the kit's and the ShopUI batch's, named in the layouts (rects from
    /// ArtSource/Working/ShopUI/notes.md). The pictures are the generated thumbnails and previews,
    /// Art/Shop/Backgrounds/Thumb_&lt;Name&gt;.png and Art/Shop/Tables/Preview_&lt;Name&gt;.png
    /// (<see cref="PictureArt"/>).
    /// </summary>
    public static class ShopCustomizer
    {
        public const string BackgroundsLayout = "ShopBackgroundsLayout";
        public const string TablesLayout = "ShopTablesLayout";

        public const string FinalBackgroundsFolder = "Assets/_Project/Art/Shop/Backgrounds";
        public const string FinalTablesFolder = "Assets/_Project/Art/Shop/Tables";

        private const int Categories = 3;

        // Every category shows exactly three items, side by side (the owner, 5 Oct 2026); the
        // mockups measure three tiles each.
        private const int TilesPerCategory = 3;

        // A background thumbnail's frame: its corner radius against the kit card's (22).
        private const float ThumbCornerScale = 2.2f;

        // Plain drawer beyond the panel art's right edge (canvas units), for a phone whose safe area
        // leaves a strip there.
        private const float DrawerBleed = 220f;

        // The drawer's width on the screen (mockup pixels), narrowed to suit its smaller buttons
        // (the owner, 5 Oct 2026); the mockups draw it 232.2 wide. Its panel art keeps its own width,
        // so the rounded left edge, the textile and the shadow look as drawn.
        private const float DrawerWidth = 160f;

        // The category buttons (disc, icon, label and the disc's shadow) at this share of the size
        // the mockups draw them (the ivory disc: 137.8 mockup pixels, 316 px at @2x).
        private const float CategoryScale = 0.68f;

        // From one category button's disc to the next, centre to centre (mockup pixels; the
        // mockups' full-size buttons are 207 apart).
        private const float CategoryPitch = 165f;

        private static readonly Color DrawerIvory = new Color32(0xF9, 0xF1, 0xE8, 0xFF);

        // A deck's soft shadow: the kit card's shadow at a card's corner radius (about 10 against the
        // tile's 22), a little out and down, faint.
        private const float DeckShadowSpread = 6f;
        private const float DeckShadowDrop = 3f;
        private const float DeckShadowSliceScale = 2.2f;
        private const float DeckShadowOpacity = 0.55f;

        // The drawer's round buttons cast the kit's round shadow, grown by this share of their
        // diameter each side and dropped 2 px, at half strength (ArtSource/Working/ShopUI/notes.md).
        private const float DiscShadowGrowth = 0.099f;
        private const float DiscShadowDrop = 2f;
        private const float DiscShadowOpacity = 0.5f;

        private static readonly string[] CategoryKeys = { "ui.shop.category.cards", "ui.shop.category.backgrounds", "ui.shop.category.tables" };

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenLayout cards = ctx.Layout;
            var layouts = new[] { cards, ScreenLayout.Load(BackgroundsLayout), ScreenLayout.Load(TablesLayout) };

            AddCategoryTitles(ctx, layouts);
            BuildPrimary(ctx, layouts);
            BuildTiles(ctx, layouts);
            BuildDrawer(ctx, layouts);
            BuildPictures(ctx);

            foreach (string gone in ctx.Nodes.Where(pair => pair.Value == null).Select(pair => pair.Key).ToList())
            {
                ctx.Nodes.Remove(gone);
            }
        }

        // ------------------------------------------------------------------ titles

        /// <summary>Title1/Subtitle1 (mockup 20) and Title2/Subtitle2 (mockup 21) next to Cards' own, hidden.</summary>
        private static void AddCategoryTitles(ScreenBuildContext ctx, ScreenLayout[] layouts)
        {
            Transform parent = ctx.Nodes["Title0"].parent;
            for (int c = 1; c < Categories; c++)
            {
                foreach (string id in new[] { "Title" + c, "Subtitle" + c })
                {
                    TextMeshProUGUI label = Label(Node(layouts[c], id), parent, UiBuildKit.ScreenCentre);
                    label.gameObject.SetActive(false);
                    ctx.Nodes[id] = label.rectTransform;
                }
            }
        }

        // ------------------------------------------------------------------ primary button

        /// <summary>
        /// The button's words in a group that dims with it: Use Selected Style (15), Background
        /// (20) and Table (fitted on 15's wording, as no mockup shows it), and "Buy for (coin) N" (21).
        /// </summary>
        private static void BuildPrimary(ScreenBuildContext ctx, ScreenLayout[] layouts)
        {
            RectTransform button = ctx.Nodes["PrimaryButton"];
            Vector2 centre = ctx.CentreOf("PrimaryButton");
            LayoutNode buttonNode = ctx.Node("PrimaryButton");
            RectTransform content = UiBuildKit.CreateAt("PrimaryContent", button, centre, centre, UiBuildKit.FromMock(buttonNode.rect[2], buttonNode.rect[3]));
            var group = content.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            ctx.Bound["_primaryContent"] = group;

            RectTransform use0 = ctx.Nodes["UseLabel0"];
            use0.SetParent(content, false);

            TextMeshProUGUI use1 = Label(Node(layouts[1], "UseLabel1"), content, centre);
            use1.gameObject.SetActive(false);
            ctx.Nodes["UseLabel1"] = use1.rectTransform;

            TextMeshProUGUI use2 = FitOnMockupWording(Node(layouts[0], "UseLabel0"), content, centre, "UseLabel2", "Use Selected Cloth", "ui.shop.tables.use");
            use2.gameObject.SetActive(false);
            ctx.Nodes["UseLabel2"] = use2.rectTransform;

            ScreenLayout tables = layouts[2];
            RectTransform row = UiBuildKit.CreateAt("BuyRow", content, centre, centre, UiBuildKit.FromMock(buttonNode.rect[2], buttonNode.rect[3]));
            TextMeshProUGUI buyFor = Label(Node(tables, "BuyLabel"), row, centre);
            LayoutNode buyCoin = Node(tables, "BuyCoin");
            Image coin = SpriteAt("BuyCoin", row, buyCoin.rect, centre, Load(buyCoin.sprite));
            coin.preserveAspect = true;
            TextMeshProUGUI price = Label(Node(tables, "BuyPrice"), row, centre);
            ShopRow layout = Row(row, buyFor.rectTransform, coin.rectTransform, price.rectTransform);
            row.gameObject.SetActive(false);
            ctx.Nodes["BuyRow"] = row;
            ctx.Nodes["BuyPrice"] = price.rectTransform;
            ctx.Bound["_buyRow"] = layout;
        }

        // ------------------------------------------------------------------ tiles

        /// <summary>
        /// Each category's three tiles (<see cref="ShopTiles"/>, node Tiles0..2), side by side at the
        /// spacing its mockup measures. The mockups put a scroll track and a hint under the tiles;
        /// without them the row sits in the middle of the panel, as much room above it as below.
        /// </summary>
        private static void BuildTiles(ScreenBuildContext ctx, ScreenLayout[] layouts)
        {
            RectTransform panel = ctx.Nodes["Panel"];
            Vector2 panelCentre = ctx.CentreOf("Panel");
            LayoutNode panelNode = ctx.Node("Panel");
            for (int c = 0; c < Categories; c++)
            {
                string id = "Tiles" + c;
                RectTransform root = UiBuildKit.CreateAt(id, panel, panelCentre, panelCentre, UiBuildKit.FromMock(panelNode.rect[2], panelNode.rect[3]));
                ctx.Nodes[id] = root;

                TileModel model = TileModel.Measure(layouts[c]);
                var tiles = new Object[TilesPerCategory];
                for (int i = 0; i < TilesPerCategory; i++)
                {
                    var at = new Vector2(model.FirstX + i * model.Stride, panelCentre.y);
                    tiles[i] = BuildTile(c, i, layouts, model, root, panelCentre, at);
                }

                var shopTiles = root.gameObject.AddComponent<ShopTiles>();
                var so = new SerializedObject(shopTiles);
                SetArray(so.FindProperty("_tiles"), tiles);
                so.ApplyModifiedPropertiesWithoutUndo();
                root.gameObject.SetActive(c == 0);
            }

            // Mockup 15's own tiles were only measured.
            for (int i = 0; i < TilesPerCategory; i++)
            {
                Object.DestroyImmediate(ctx.Nodes["Tile" + i].gameObject);
            }
        }

        /// <summary>
        /// Tile <paramref name="index"/> of a category, centred on <paramref name="at"/>, built from
        /// its mockup's tile 0 (the outline from the tile that has it, the price line from mockup
        /// 21's tile 1). It starts with the picture of the catalogue's item at its place.
        /// </summary>
        private static ShopItemTile BuildTile(int category, int index, ScreenLayout[] layouts, TileModel model, RectTransform parent, Vector2 parentCentre, Vector2 at)
        {
            ScreenLayout layout = layouts[category];
            ScreenLayout tables = layouts[2];
            LayoutNode tile0 = Node(layout, "Tile0");
            RectTransform root = UiBuildKit.CreateAt("Tile" + index, parent, at, parentCentre, UiBuildKit.FromMock(model.Width, model.Height));

            // Shadow, card and outline as the builder draws the tile node: its sprite and slice scale,
            // its shared shadow grown by its spread and dropped.
            Sprite cardShadow = Load($"{UiBuildKit.ArtRoot}/{tile0.shadowUse}");
            SpriteSized(root, "Shadow", at + new Vector2(0f, tile0.shadowDrop), at,
                new Vector2(model.Width + 2f * tile0.shadowSpread, model.Height + 2f * tile0.shadowSpread), cardShadow, false);
            Image face = SpriteSized(root, "Face", at, at, new Vector2(model.Width, model.Height), Load(tile0.sprite), true);
            face.pixelsPerUnitMultiplier = SliceScale(tile0);

            LayoutNode outlineNode = layout.nodes.First(n => n.id.StartsWith("Outline"));
            LayoutNode outlineTile = Node(layout, "Tile" + outlineNode.id.Substring("Outline".Length));
            Vector2 outlineOffset = Centre(outlineNode.rect) - Centre(outlineTile.rect);
            Image outlineImage = SpriteSized(root, "Outline", at + outlineOffset, at,
                new Vector2(model.Width + outlineNode.rect[2] - outlineTile.rect[2], model.Height + outlineNode.rect[3] - outlineTile.rect[3]), Load(outlineNode.sprite), false);
            outlineImage.pixelsPerUnitMultiplier = SliceScale(outlineNode);
            outlineImage.gameObject.SetActive(false);

            RectTransform picture;
            Image pictureFace;
            Image pictureBase = null;
            switch (category)
            {
                case 0:
                    BuildDeckPicture(layout, index, root, at, out picture, out pictureFace);
                    pictureBase = AddDeckBase(pictureFace);
                    break;
                case 1:
                    BuildThumbPicture(layout, model, root, at, FirstPicture(CosmeticKind.Background, index), out picture, out pictureFace);
                    break;
                default:
                    BuildTablePicture(layout, model, root, at, FirstPicture(CosmeticKind.Table, index), out picture, out pictureFace);
                    break;
            }

            // Everything measured on a tile is placed by its offset from that tile's own centre.
            Vector2 source = Centre(tile0.rect);
            TextMeshProUGUI name = Label(Node(layout, "Name0"), root, source, "Name");

            RectTransform statusRow = UiBuildKit.CreateAt("StatusRow", root, source, source, root.sizeDelta);
            LayoutNode radioNode = Node(layout, "RadioOn0");
            RectTransform radio = Box("Radio", statusRow, radioNode.rect, source);
            Image radioOn = SpriteAt("RadioOn", radio, radioNode.rect, Centre(radioNode.rect), Load($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Radio_On.png"));
            Image radioOff = SpriteAt("RadioOff", radio, radioNode.rect, Centre(radioNode.rect), Load($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Radio_Off.png"));
            radioOff.gameObject.SetActive(false);
            TextMeshProUGUI status = Label(Node(layout, "Status0"), statusRow, source, "Status");
            ShopRow statusLayout = Row(statusRow, radio, status.rectTransform);

            Vector2 priceSource = Centre(Node(tables, "Tile1").rect);
            RectTransform priceRow = UiBuildKit.CreateAt("PriceRow", root, priceSource, priceSource, root.sizeDelta);
            LayoutNode coinNode = Node(tables, "Coin1");
            Image coin = SpriteAt("Coin", priceRow, coinNode.rect, priceSource, Load(coinNode.sprite));
            coin.preserveAspect = true;
            TextMeshProUGUI price = Label(Node(tables, "Price1"), priceRow, priceSource, "Price");
            ShopRow priceLayout = Row(priceRow, coin.rectTransform, price.rectTransform);
            priceRow.gameObject.SetActive(false);

            Button button = AddButton(root.gameObject, face);
            var tile = root.gameObject.AddComponent<ShopItemTile>();
            var so = new SerializedObject(tile);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_picture").objectReferenceValue = picture;
            so.FindProperty("_face").objectReferenceValue = pictureFace;
            so.FindProperty("_base").objectReferenceValue = pictureBase;
            so.FindProperty("_faceMaterial").objectReferenceValue = pictureBase != null ? TableCardBuilder.EnsureFaceMaterial() : null;
            so.FindProperty("_name").objectReferenceValue = name;
            so.FindProperty("_outline").objectReferenceValue = outlineImage.gameObject;
            so.FindProperty("_statusRow").objectReferenceValue = statusLayout;
            so.FindProperty("_radioOn").objectReferenceValue = radioOn.gameObject;
            so.FindProperty("_radioOff").objectReferenceValue = radioOff.gameObject;
            so.FindProperty("_status").objectReferenceValue = status;
            so.FindProperty("_priceRow").objectReferenceValue = priceLayout;
            so.FindProperty("_price").objectReferenceValue = price;
            so.ApplyModifiedPropertiesWithoutUndo();
            return tile;
        }

        /// <summary>
        /// A deck's back (the geometry of mockup 15's tile 0, the back of the deck mockup 15 draws at
        /// this place), shown at the card's own proportions, with the kit card's soft shadow scaled
        /// down to a card (the mockup draws a faint one under each back).
        /// </summary>
        private static void BuildDeckPicture(ScreenLayout layout, int index, RectTransform tile, Vector2 at, out RectTransform picture, out Image face)
        {
            LayoutNode node = Node(layout, "DeckPicture0");
            Vector2 source = Centre(Node(layout, "Tile0").rect);
            Vector2 centre = at + (Centre(node.rect) - source);
            picture = UiBuildKit.CreateAt("Picture", tile, centre, at, UiBuildKit.FromMock(node.rect[2], node.rect[3]));
            Sprite back = UiBuildKit.LoadSpriteAt(Node(layout, "DeckPicture" + index).sprite, 0f);

            // The card as drawn inside its box: the back's own proportions at the box's height.
            float aspect = back != null && back.rect.height > 0f ? back.rect.width / back.rect.height : node.rect[2] / node.rect[3];
            var card = new Vector2(Mathf.Min(node.rect[2], node.rect[3] * aspect), Mathf.Min(node.rect[3], node.rect[2] / aspect));
            Image shadow = SpriteSized(picture, "Shadow", centre + new Vector2(0f, DeckShadowDrop), centre,
                card + new Vector2(2f * DeckShadowSpread, 2f * DeckShadowSpread), Load($"{UiBuildKit.ArtRoot}/UI/Common/UI_Panel_Card_Shadow.png"), false);
            shadow.pixelsPerUnitMultiplier = DeckShadowSliceScale;
            shadow.color = new Color(1f, 1f, 1f, DeckShadowOpacity);

            RectTransform faceRect = UiBuildKit.CreateAt("Face", picture, centre, centre, picture.sizeDelta);
            face = UiBuildKit.AddImage(faceRect, back, false);
            face.preserveAspect = true;
        }

        /// <summary>
        /// The deck's base under a deck picture's face, the same size, hidden: it shows only under the
        /// shared face while the deck peeks (ADR 053).
        /// </summary>
        private static Image AddDeckBase(Image face)
        {
            var rect = (RectTransform)face.transform;
            var go = new GameObject("Base", typeof(RectTransform));
            var baseRect = (RectTransform)go.transform;
            baseRect.SetParent(rect.parent, false);
            baseRect.SetSiblingIndex(rect.GetSiblingIndex());
            baseRect.anchorMin = rect.anchorMin;
            baseRect.anchorMax = rect.anchorMax;
            baseRect.pivot = rect.pivot;
            baseRect.anchoredPosition = rect.anchoredPosition;
            baseRect.sizeDelta = rect.sizeDelta;
            Image image = UiBuildKit.AddImage(baseRect, null, false);
            image.preserveAspect = true;
            image.enabled = false;
            go.layer = face.gameObject.layer;
            return image;
        }

        /// <summary>
        /// Adds the hidden deck base (<see cref="AddDeckBase"/>) to each deck tile of the saved shop
        /// prefab and binds it to the tile; everything else in the prefab stays as it is. Tiles that
        /// have their base are left alone.
        /// </summary>
        public static string AddDeckBases()
        {
            string path = ScreenLayoutBuilder.PrefabPath("Shop");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform decks = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Tiles0");
                if (decks == null)
                {
                    return "FAILED: the shop has no Tiles0 (the Cards category)";
                }

                int added = 0;
                foreach (ShopItemTile tile in decks.GetComponentsInChildren<ShopItemTile>(true))
                {
                    var so = new SerializedObject(tile);
                    if (so.FindProperty("_base").objectReferenceValue != null || tile.Face == null)
                    {
                        continue;
                    }

                    so.FindProperty("_base").objectReferenceValue = AddDeckBase(tile.Face);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    added++;
                }

                if (added == 0)
                {
                    return "the shop's deck tiles already have their bases";
                }

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                return saved ? $"saved {path} with {added} deck bases" : $"FAILED to save {path}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Binds the premultiplied material of the shared faces (ADR 054) to each deck tile of the
        /// saved shop prefab, for its face while it peeks; everything else stays as it is.
        /// </summary>
        public static string UseDeckFaceMaterial()
        {
            string path = ScreenLayoutBuilder.PrefabPath("Shop");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform decks = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Tiles0");
                if (decks == null)
                {
                    return "FAILED: the shop has no Tiles0 (the Cards category)";
                }

                Material material = TableCardBuilder.EnsureFaceMaterial();
                int bound = 0;
                foreach (ShopItemTile tile in decks.GetComponentsInChildren<ShopItemTile>(true))
                {
                    var so = new SerializedObject(tile);
                    SerializedProperty property = so.FindProperty("_faceMaterial");
                    if (tile.Base == null || property.objectReferenceValue == material)
                    {
                        continue;
                    }

                    property.objectReferenceValue = material;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    bound++;
                }

                if (bound == 0)
                {
                    return "the shop's deck tiles already have the face material";
                }

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                return saved ? $"saved {path} with the face material on {bound} deck tiles" : $"FAILED to save {path}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>A background's thumbnail in a rounded frame that it fills (mockup 20's tiles, averaged).</summary>
        private static void BuildThumbPicture(ScreenLayout layout, TileModel model, RectTransform tile, Vector2 at, Sprite thumbnail, out RectTransform picture, out Image face)
        {
            float[] box = model.MeanBox(layout, "Thumb", useSpriteRect: false);
            Vector2 centre = at + new Vector2(box[0], box[1]);
            picture = UiBuildKit.CreateAt("Picture", tile, centre, at, UiBuildKit.FromMock(box[2], box[3]));
            RectTransform frame = UiBuildKit.CreateAt("Frame", picture, centre, centre, picture.sizeDelta);
            Image mask = UiBuildKit.AddImage(frame, Load($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Card.png"), false);
            mask.pixelsPerUnitMultiplier = ThumbCornerScale;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            RectTransform faceRect = UiBuildKit.CreateAt("Face", frame, centre, centre, picture.sizeDelta);
            face = UiBuildKit.AddImage(faceRect, thumbnail, false);
            var cover = faceRect.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = thumbnail != null ? thumbnail.rect.width / thumbnail.rect.height : box[2] / box[3];
        }

        /// <summary>A table's preview, inside the box mockup 21's tables take (averaged), at its own proportions.</summary>
        private static void BuildTablePicture(ScreenLayout layout, TileModel model, RectTransform tile, Vector2 at, Sprite preview, out RectTransform picture, out Image face)
        {
            float[] box = model.MeanBox(layout, "TablePicture", useSpriteRect: true);
            Vector2 centre = at + new Vector2(box[0], box[1]);
            picture = UiBuildKit.CreateAt("Picture", tile, centre, at, UiBuildKit.FromMock(box[2], box[3]));
            RectTransform faceRect = UiBuildKit.CreateAt("Face", picture, centre, centre, picture.sizeDelta);
            face = UiBuildKit.AddImage(faceRect, preview, false);
            face.preserveAspect = true;
        }

        /// <summary>The picture of the catalogue's item at this place, for the prefab (the presenter sets every picture at run time).</summary>
        private static Sprite FirstPicture(CosmeticKind kind, int index)
        {
            IReadOnlyList<CosmeticItem> items = CosmeticCatalog.Of(kind);
            return index < items.Count ? Load(PictureArt(kind, items[index].Id)) : null;
        }

        // ------------------------------------------------------------------ drawer

        /// <summary>
        /// The drawer, rebuilt from the builder's nodes: a rail flush with the right edge and as tall
        /// as the screen, <see cref="DrawerWidth"/> wide. It holds the panel art at its own width
        /// from the rail's left edge (its right part lies beyond the screen's edge, and a plain bleed
        /// carries on past it), the panel's shadow to its left, and the three category buttons at
        /// <see cref="CategoryScale"/>, <see cref="CategoryPitch"/> apart, the three together centred
        /// on the screen's middle and each where the mockups' drawer has it across, scaled to the
        /// narrower one.
        /// </summary>
        private static void BuildDrawer(ScreenBuildContext ctx, ScreenLayout[] layouts)
        {
            RectTransform oldDrawer = ctx.Nodes["Drawer"];
            LayoutNode drawerNode = ctx.Node("Drawer");
            Vector2 oldCentre = Centre(drawerNode.rect);
            float artLeft = drawerNode.rect[0];
            float artWidth = drawerNode.rect[2];
            float left = UiBuildKit.MockWidth - DrawerWidth;
            var railCentre = new Vector2(left + DrawerWidth / 2f, UiBuildKit.ScreenCentre.y);

            var railObject = new GameObject("ShopDrawer", typeof(RectTransform));
            var rail = (RectTransform)railObject.transform;
            rail.SetParent(ctx.SafeArea, false);
            rail.anchorMin = new Vector2(1f, 0f);
            rail.anchorMax = new Vector2(1f, 1f);
            rail.pivot = new Vector2(1f, 0.5f);
            rail.sizeDelta = new Vector2(DrawerWidth * UiBuildKit.ScaleX, 0f);
            rail.anchoredPosition = Vector2.zero;

            // The shadow (the panel's rect grown by "shadowSpread") and the panel, as tall as the
            // screen: both are sliced so only their plain middles stretch.
            float spread = drawerNode.shadowSpread;
            Image shadow = oldDrawer.Find("Shadow").GetComponent<Image>();
            shadow.type = Image.Type.Sliced;
            PinLeft(shadow.rectTransform, rail, -spread, artWidth + 2f * spread, 2f * spread * UiBuildKit.ScaleY);

            Image face = oldDrawer.Find("Face").GetComponent<Image>();
            face.type = Image.Type.Sliced;
            face.raycastTarget = true;   // the drawer takes the taps that miss its buttons
            PinLeft(face.rectTransform, rail, 0f, artWidth, 0f);

            RectTransform bleed = new GameObject("Bleed", typeof(RectTransform)).GetComponent<RectTransform>();
            bleed.SetParent(rail, false);
            bleed.anchorMin = new Vector2(1f, 0f);
            bleed.anchorMax = new Vector2(1f, 1f);
            bleed.pivot = new Vector2(0f, 0.5f);
            bleed.sizeDelta = new Vector2(DrawerBleed, 0f);
            bleed.anchoredPosition = new Vector2((artWidth - DrawerWidth) * UiBuildKit.ScaleX, 0f);
            Image bleedImage = bleed.gameObject.AddComponent<Image>();
            bleedImage.color = DrawerIvory;
            bleedImage.raycastTarget = true;

            RectTransform items = new GameObject("Items", typeof(RectTransform)).GetComponent<RectTransform>();
            items.SetParent(rail, false);
            items.anchorMin = items.anchorMax = items.pivot = new Vector2(0.5f, 0.5f);
            items.sizeDelta = new Vector2(DrawerWidth * UiBuildKit.ScaleX, UiBuildKit.CanvasHeight);
            items.anchoredPosition = Vector2.zero;

            var buttons = new Button[Categories];
            var active = new GameObject[Categories];
            var inactive = new GameObject[Categories];
            var centres = new Vector2[Categories];
            var hits = new float[Categories][];
            for (int c = 0; c < Categories; c++)
            {
                centres[c] = ctx.CentreOf("CategoryButton" + c);
                BuildCategoryButton(ctx, layouts, c, items, oldCentre, railCentre, out buttons[c], out active[c], out inactive[c], out hits[c]);
            }

            // From the top of the first button (its disc) to the foot of the last (its label), each
            // measured from its disc's centre.
            float above = hits[0][1] - centres[0].y;
            float below = hits[Categories - 1][1] + hits[Categories - 1][3] - centres[Categories - 1].y;
            float firstY = UiBuildKit.ScreenCentre.y - (Categories - 1) * CategoryPitch / 2f - CategoryScale * (above + below) / 2f;
            for (int c = 0; c < Categories; c++)
            {
                RectTransform root = ctx.Nodes["CategoryButton" + c];
                float x = left + (centres[c].x - artLeft) * DrawerWidth / artWidth;
                root.anchoredPosition = Position(new Vector2(x, firstY + c * CategoryPitch), railCentre);
                root.localScale = new Vector3(CategoryScale, CategoryScale, 1f);
            }

            Object.DestroyImmediate(oldDrawer.gameObject);

            var drawer = railObject.AddComponent<ShopDrawer>();
            var so = new SerializedObject(drawer);
            SetArray(so.FindProperty("_categoryButton"), buttons);
            SetArray(so.FindProperty("_activeLook"), active);
            SetArray(so.FindProperty("_inactiveLook"), inactive);
            so.ApplyModifiedPropertiesWithoutUndo();
            drawer.SetActive(0);
            ctx.Bound["_drawer"] = drawer;
        }

        /// <summary>
        /// One category button, at the mockups' size (the drawer scales it): its active look (the
        /// crimson disc, the light icon and the crimson label, from the mockup where it is active)
        /// and its inactive look (the ivory disc, the dark icon and the brown label, from a mockup
        /// where it is not), and a hit area over both disc and label, <paramref name="hitRect"/>
        /// (mockup pixels).
        /// </summary>
        private static void BuildCategoryButton(
            ScreenBuildContext ctx, ScreenLayout[] layouts, int c, RectTransform items, Vector2 oldCentre, Vector2 railCentre,
            out Button button, out GameObject activeLook, out GameObject inactiveLook, out float[] hitRect)
        {
            // Where it is active: Cards on 15, Backgrounds on 20, Tables on 21. Inactive: Cards on 20, the others on 15.
            ScreenLayout activeIn = layouts[c];
            ScreenLayout inactiveIn = c == 0 ? layouts[1] : layouts[0];
            string id = "CategoryButton" + c;
            RectTransform root = ctx.Nodes[id];
            Vector2 centre = ctx.CentreOf(id);
            Reparent(root, items, oldCentre, railCentre);
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.GetChild(i).gameObject);
            }

            LayoutNode activeDisc = Node(activeIn, id);
            LayoutNode activeIcon = Node(activeIn, "CategoryIcon" + c);
            LayoutNode inactiveDisc = Node(inactiveIn, id);
            LayoutNode inactiveIcon = Node(inactiveIn, "CategoryIcon" + c);

            hitRect = Union(activeDisc.rect, inactiveDisc.rect, InkRect(Node(activeIn, "CategoryLabel" + c)), InkRect(Node(inactiveIn, "CategoryLabel" + c)));
            Image hit = SpriteAt("Hit", root, hitRect, centre, null);
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            inactiveLook = UiBuildKit.CreateAt("InactiveLook", root, centre, centre, Vector2.zero).gameObject;
            Disc(inactiveLook.transform, inactiveDisc, centre);
            SpriteAt("Icon", inactiveLook.transform, inactiveIcon.spriteRect, centre, Load(inactiveIcon.sprite));
            LookLabel(ctx, inactiveIn, layouts[0], c, inactiveLook.transform, centre, oldCentre);

            activeLook = UiBuildKit.CreateAt("ActiveLook", root, centre, centre, Vector2.zero).gameObject;
            Disc(activeLook.transform, activeDisc, centre);
            SpriteAt("Icon", activeLook.transform, activeIcon.spriteRect, centre, Load(activeIcon.sprite));
            LookLabel(ctx, activeIn, layouts[0], c, activeLook.transform, centre, oldCentre);

            button = root.GetComponent<Button>();
            button.targetGraphic = hit;
        }

        /// <summary>A look's label: mockup 15's own when 15 draws that state, otherwise fitted on the other mockup.</summary>
        private static void LookLabel(ScreenBuildContext ctx, ScreenLayout source, ScreenLayout cards, int c, Transform look, Vector2 centre, Vector2 oldCentre)
        {
            string id = "CategoryLabel" + c;
            if (source == cards)
            {
                RectTransform built = ctx.Nodes[id];
                Reparent(built, look, oldCentre, centre);
                return;
            }

            Label(Node(source, id), look, centre, id, null, CategoryKeys[c]);
        }

        /// <summary>
        /// Pins a sliced picture to the drawer's left edge, <paramref name="x"/> from it and
        /// <paramref name="width"/> wide (mockup pixels), as tall as the drawer and
        /// <paramref name="extraHeight"/> (canvas units) more.
        /// </summary>
        private static void PinLeft(RectTransform rect, Transform rail, float x, float width, float extraHeight)
        {
            rect.SetParent(rail, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width * UiBuildKit.ScaleX, extraHeight);
            rect.anchoredPosition = new Vector2(x * UiBuildKit.ScaleX, 0f);
        }

        // ------------------------------------------------------------------ pictures

        private static void BuildPictures(ScreenBuildContext ctx)
        {
            var pictures = ctx.Root.gameObject.AddComponent<ShopPictures>();
            FillPictures(pictures, ctx.Warnings);
            ctx.Bound["_pictures"] = pictures;
        }

        /// <summary>
        /// Gives the saved shop prefab's pictures the catalogue's ids as they are now (Durbar Square
        /// took the Himalayan Vista's place, 30 Oct 2026); everything else in the prefab stays as it is.
        /// </summary>
        public static string RefreshPictures()
        {
            string path = ScreenLayoutBuilder.PrefabPath("Shop");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pictures = root.GetComponent<ShopPictures>();
                if (pictures == null)
                {
                    return $"FAILED: {path} has no pictures";
                }

                var warnings = new List<string>();
                FillPictures(pictures, warnings);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                return (saved ? $"saved {path} with the catalogue's pictures" : $"FAILED to save {path}") + string.Concat(warnings.Select(w => "; " + w));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void FillPictures(ShopPictures pictures, List<string> warnings)
        {
            var keys = new List<string>();
            var sprites = new List<Object>();
            foreach (CosmeticKind kind in new[] { CosmeticKind.Background, CosmeticKind.Table })
            {
                foreach (CosmeticItem item in CosmeticCatalog.Of(kind))
                {
                    Sprite sprite = Load(PictureArt(kind, item.Id));
                    if (sprite == null)
                    {
                        warnings.Add($"no picture for {kind} {item.Id}");
                        continue;
                    }

                    keys.Add(ShopPictures.KeyOf(kind.ToString(), item.Id));
                    sprites.Add(sprite);
                }
            }

            var so = new SerializedObject(pictures);
            SerializedProperty keyList = so.FindProperty("_keys");
            keyList.arraySize = keys.Count;
            for (int i = 0; i < keys.Count; i++)
            {
                keyList.GetArrayElementAtIndex(i).stringValue = keys[i];
            }

            SetArray(so.FindProperty("_sprites"), sprites.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The picture of a background or a table: Art/Shop/Backgrounds/Thumb_&lt;Name&gt;.png or
        /// Art/Shop/Tables/Preview_&lt;Name&gt;.png, named after the id ("pokhara-lakeside" →
        /// Thumb_PokharaLakeside.png).
        /// </summary>
        public static string PictureArt(CosmeticKind kind, string id)
        {
            string name = string.Concat(id.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
            return kind == CosmeticKind.Background ? $"{FinalBackgroundsFolder}/Thumb_{name}.png" : $"{FinalTablesFolder}/Preview_{name}.png";
        }

        // ------------------------------------------------------------------ helpers

        private static Sprite Load(string path) => string.IsNullOrEmpty(path) ? null : UiBuildKit.LoadSpriteAt(path, 0f);

        /// <summary>A node's 9-slice scale ("sliceScale"), 1 when it has none.</summary>
        private static float SliceScale(LayoutNode node) => node.sliceScale > 0f ? node.sliceScale : 1f;

        /// <summary>A drawer disc as its node draws it, over the kit's round shadow.</summary>
        private static void Disc(Transform look, LayoutNode disc, Vector2 parentCentre)
        {
            float grow = disc.rect[2] * DiscShadowGrowth;
            var shadowRect = new[] { disc.rect[0] - grow, disc.rect[1] - grow + DiscShadowDrop, disc.rect[2] + 2f * grow, disc.rect[3] + 2f * grow };
            Image shadow = SpriteAt("Shadow", look, shadowRect, parentCentre, Load($"{UiBuildKit.ArtRoot}/UI/Buttons/UI_Shadow_Circle.png"));
            shadow.color = new Color(1f, 1f, 1f, DiscShadowOpacity);
            SpriteAt("Disc", look, disc.rect, parentCentre, Load(disc.sprite));
        }

        private static LayoutNode Node(ScreenLayout layout, string id)
        {
            LayoutNode node = layout.nodes.FirstOrDefault(n => n.id == id);
            if (node == null)
            {
                throw new KeyNotFoundException($"{layout.name}: no node {id}");
            }

            return node;
        }

        private static Vector2 Centre(float[] r) => new Vector2(r[0] + r[2] / 2f, r[1] + r[3] / 2f);

        /// <summary>A mockup point's anchored position under a parent centred on <paramref name="parentCentre"/>.</summary>
        private static Vector2 Position(Vector2 point, Vector2 parentCentre) =>
            new Vector2((point.x - parentCentre.x) * UiBuildKit.ScaleX, -(point.y - parentCentre.y) * UiBuildKit.ScaleY);

        private static RectTransform Box(string name, Transform parent, float[] rect, Vector2 parentCentre) =>
            UiBuildKit.CreateAt(name, parent, Centre(rect), parentCentre, UiBuildKit.FromMock(rect[2], rect[3]));

        private static Image SpriteAt(string name, Transform parent, float[] rect, Vector2 parentCentre, Sprite sprite)
        {
            return UiBuildKit.AddImage(Box(name, parent, rect, parentCentre), sprite, false);
        }

        /// <summary>A sprite centred on a mockup point with a size in mockup pixels.</summary>
        private static Image SpriteSized(RectTransform parent, string name, Vector2 centre, Vector2 parentCentre, Vector2 mockSize, Sprite sprite, bool raycast)
        {
            RectTransform rect = UiBuildKit.CreateAt(name, parent, centre, parentCentre, UiBuildKit.FromMock(mockSize.x, mockSize.y));
            return UiBuildKit.AddImage(rect, sprite, raycast);
        }

        /// <summary>A label as the builder makes it: fitted on the mockup's ink box, its key, the button-label shadow.</summary>
        private static TextMeshProUGUI Label(LayoutNode node, Transform parent, Vector2 parentCentre, string name = null, string text = null, string key = null)
        {
            TMP_FontAsset font = UiBuildKit.LoadFontRole(node.font);
            Color top = UiBuildKit.Hex(string.IsNullOrEmpty(node.colorTop) ? node.color : node.colorTop);
            Color bottom = UiBuildKit.Hex(string.IsNullOrEmpty(node.colorBottom) ? node.color : node.colorBottom);
            TextMeshProUGUI label = UiBuildKit.FitLabel(
                name ?? node.id, parent, parentCentre, text ?? node.text ?? string.Empty, font, node.inkBox, string.IsNullOrEmpty(node.align) ? "centre" : node.align, node.tracked, top, bottom);
            if (node.style == "buttonLabel")
            {
                label.fontSharedMaterial = UiBuildKit.LoadShadowPreset(font, "ButtonLabel", new Color(0.29f, 0.03f, 0.04f, 0.9f), new Vector2(0.25f, -0.35f), 0.2f);
            }

            AddKey(label.gameObject, key ?? node.key);
            return label;
        }

        /// <summary>
        /// A label for words no mockup shows: the size, width scale and baseline of the mockup's
        /// wording in its ink box, centred where that wording is centred.
        /// </summary>
        private static TextMeshProUGUI FitOnMockupWording(LayoutNode mockupNode, Transform parent, Vector2 parentCentre, string name, string text, string key)
        {
            TextMeshProUGUI label = Label(mockupNode, parent, parentCentre, name, null, key);
            var rect = label.rectTransform;
            bool measured = UiBuildKit.InkBounds(label, out Rect mockupInk, out _);
            float mockupCentreX = rect.anchoredPosition.x + mockupInk.center.x;
            label.text = text;
            if (measured && UiBuildKit.InkBounds(label, out Rect ink, out _))
            {
                rect.anchoredPosition += new Vector2(mockupCentreX - (rect.anchoredPosition.x + ink.center.x), 0f);
            }

            return label;
        }

        private static void AddKey(GameObject target, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var localized = target.AddComponent<LocalizedText>();
            var so = new SerializedObject(localized);
            so.FindProperty("_key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A button as the builder makes one: the tint, the press feedback and the tap cue.</summary>
        private static Button AddButton(GameObject target, Graphic face)
        {
            var button = target.AddComponent<Button>();
            button.targetGraphic = face;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var feedback = target.AddComponent<ButtonFeedback>();
            var so = new SerializedObject(feedback);
            so.FindProperty("_selectable").objectReferenceValue = button;
            so.FindProperty("_cue").intValue = (int)SoundCue.UiTap;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        /// <summary>A centred row of the given items (children of <paramref name="row"/>), its gaps and centre taken as they stand.</summary>
        private static ShopRow Row(RectTransform row, params RectTransform[] items)
        {
            var layout = row.gameObject.AddComponent<ShopRow>();
            var so = new SerializedObject(layout);
            SetArray(so.FindProperty("_items"), items);
            so.ApplyModifiedPropertiesWithoutUndo();
            layout.Capture();
            return layout;
        }

        private static void SetArray(SerializedProperty property, params Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        /// <summary>Moves a built node to a new parent whose centre is another mockup point; it stays where it is on screen.</summary>
        private static void Reparent(RectTransform rect, Transform parent, Vector2 oldParentCentre, Vector2 newParentCentre)
        {
            rect.SetParent(parent, false);
            rect.anchoredPosition += Position(oldParentCentre, newParentCentre);
        }

        private static float[] InkRect(LayoutNode label) =>
            new[] { label.inkBox[0], label.inkBox[1], label.inkBox[2] - label.inkBox[0], label.inkBox[3] - label.inkBox[1] };

        private static float[] Union(params float[][] rects)
        {
            float left = rects.Min(r => r[0]), top = rects.Min(r => r[1]);
            float right = rects.Max(r => r[0] + r[2]), bottom = rects.Max(r => r[1] + r[3]);
            return new[] { left - 4f, top - 4f, right - left + 8f, bottom - top + 8f };
        }

        /// <summary>
        /// A category's tiles as one model (mockup pixels): the mean size, the stride and the first
        /// tile's centre fitted through the three measured tiles (the mockups' own drift is a pixel
        /// or two).
        /// </summary>
        private sealed class TileModel
        {
            public float Width;
            public float Height;
            public float Stride;
            public float FirstX;

            public static TileModel Measure(ScreenLayout layout)
            {
                float[][] tiles = Enumerable.Range(0, TilesPerCategory).Select(i => layout.nodes.First(n => n.id == "Tile" + i).rect).ToArray();
                float[] centres = tiles.Select(r => r[0] + r[2] / 2f).ToArray();
                float stride = (centres[TilesPerCategory - 1] - centres[0]) / (TilesPerCategory - 1);
                return new TileModel
                {
                    Width = tiles.Average(r => r[2]),
                    Height = tiles.Average(r => r[3]),
                    Stride = stride,
                    FirstX = Enumerable.Range(0, centres.Length).Average(i => centres[i] - i * stride),
                };
            }

            /// <summary>
            /// The mean box of <paramref name="prefix"/>0..2 (rect, or the cut sprite's own rect)
            /// as an offset from its tile's centre and a size: [dx, dy, w, h].
            /// </summary>
            public float[] MeanBox(ScreenLayout layout, string prefix, bool useSpriteRect)
            {
                var boxes = new List<float[]>();
                for (int i = 0; i < TilesPerCategory; i++)
                {
                    LayoutNode node = layout.nodes.FirstOrDefault(n => n.id == prefix + i);
                    if (node == null)
                    {
                        continue;
                    }

                    float[] r = useSpriteRect && node.spriteRect != null && node.spriteRect.Length == 4 ? node.spriteRect : node.rect;
                    float[] tile = layout.nodes.First(n => n.id == "Tile" + i).rect;
                    boxes.Add(new[]
                    {
                        r[0] + r[2] / 2f - (tile[0] + tile[2] / 2f),
                        r[1] + r[3] / 2f - (tile[1] + tile[3] / 2f),
                        r[2],
                        r[3],
                    });
                }

                return Enumerable.Range(0, 4).Select(k => boxes.Average(b => b[k])).ToArray();
            }
        }
    }
}
