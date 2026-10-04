using System.Collections.Generic;
using System.IO;
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
    /// Screen 15, the Shop (mockups 15 Cards, 20 Backgrounds and 21 Tables). The builder lays out
    /// mockup 15 (ArtSource/Screens/Shop); this adds what the other two categories need from their
    /// own measured layouts (ShopBackgroundsLayout on 20, ShopTablesLayout on 21), each fitted on
    /// its own mockup:
    /// <list type="bullet">
    /// <item>a title, subtitle and Use label per category, and the "Buy for (coin) N" row;</item>
    /// <item>a carousel per category (<see cref="ShopCarousel"/>): its tile template is that
    /// category's measured tile 0 (picture, name, status line; the price line comes from mockup 21),
    /// its tiles stand at the measured spacing, its window holds exactly three of them, and the
    /// chevrons, track and hint are where that mockup draws them;</item>
    /// <item>the drawer (<see cref="ShopDrawer"/>): the panel flush with the right edge and as tall
    /// as the screen, sliding with its buttons, while the storefront disc stays; each category
    /// button has its crimson (active) and ivory (inactive) look, each as a mockup draws it;</item>
    /// <item>the background and table pictures (<see cref="ShopPictures"/>).</item>
    /// </list>
    /// The UI pieces are the kit's and the ShopUI batch's, named in the layouts (rects from
    /// ArtSource/Working/ShopUI/notes.md). The background thumbnails and the table previews are
    /// placeholders cut from the mockups until the generated ones are imported: once
    /// Art/Shop/Backgrounds/Thumb_&lt;Name&gt;.png or Art/Shop/Tables/Preview_&lt;Name&gt;.png exists,
    /// rebuilding the prefab uses it (<see cref="PictureArt"/>).
    /// </summary>
    public static class ShopCustomizer
    {
        public const string BackgroundsLayout = "ShopBackgroundsLayout";
        public const string TablesLayout = "ShopTablesLayout";

        public const string PlaceholderFolder = "Assets/_Project/Art/Screens/Shop";
        public const string FinalBackgroundsFolder = "Assets/_Project/Art/Shop/Backgrounds";
        public const string FinalTablesFolder = "Assets/_Project/Art/Shop/Tables";

        private const int Categories = 3;

        // Mockup pixels above and below the tiles inside a carousel's window: the selected outline and the tiles' shadows.
        private const float WindowMargin = 16f;

        // A background thumbnail's frame: its corner radius against the kit card's (22).
        private const float ThumbCornerScale = 2.2f;

        // Plain drawer beyond the right edge (canvas units), for a phone whose safe area leaves a strip there.
        private const float DrawerBleed = 220f;

        // The mockups' drawer: its left edge, and its plain ivory.
        private const float DrawerLeft = 1439.8f;
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
            BuildCarousels(ctx, layouts);
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

            TextMeshProUGUI use2 = FitOnMockupWording(Node(layouts[0], "UseLabel0"), content, centre, "UseLabel2", "Use Selected Table", "ui.shop.tables.use");
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

        // ------------------------------------------------------------------ carousels

        private static void BuildCarousels(ScreenBuildContext ctx, ScreenLayout[] layouts)
        {
            RectTransform panel = ctx.Nodes["Panel"];
            Vector2 panelCentre = ctx.CentreOf("Panel");
            LayoutNode panelNode = ctx.Node("Panel");

            // Mockup 15's browsing controls; the other categories get copies at their own mockup's places.
            RectTransform prev = ctx.Nodes["PrevButton"];
            RectTransform next = ctx.Nodes["NextButton"];
            ctx.Nodes["PrevIcon"].localScale = new Vector3(-1f, 1f, 1f);   // the kit's chevron points right

            for (int c = 0; c < Categories; c++)
            {
                ScreenLayout layout = layouts[c];
                RectTransform root = UiBuildKit.CreateAt("Carousel" + c, panel, panelCentre, panelCentre, UiBuildKit.FromMock(panelNode.rect[2], panelNode.rect[3]));
                ctx.Nodes["Carousel" + c] = root;
                var carousel = root.gameObject.AddComponent<ShopCarousel>();

                TileModel model = TileModel.Measure(layout);
                Vector2 windowCentre = new Vector2(model.FirstX + model.Stride, model.CentreY);
                var windowRect = new[] { windowCentre.x - 1.5f * model.Stride, model.Top - WindowMargin, 3f * model.Stride, model.Height + 2f * WindowMargin };
                RectTransform window = Box("Viewport", root, windowRect, panelCentre);
                Image hit = window.gameObject.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0f);
                hit.raycastTarget = true;
                window.gameObject.AddComponent<RectMask2D>();
                RectTransform content = UiBuildKit.CreateAt("Content", window, windowCentre, windowCentre, window.sizeDelta);

                ShopItemTile template = BuildTile(c, layouts, model, content, windowCentre);

                RectTransform prevCopy = c == 0 ? Move(prev, root) : Copy(prev, root, Centre(Node(layout, "PrevButton").rect), panelCentre);
                RectTransform nextCopy = c == 0 ? Move(next, root) : Copy(next, root, Centre(Node(layout, "NextButton").rect), panelCentre);
                RectTransform track = c == 0 ? Move(ctx.Nodes["Track"], root) : Copy(ctx.Nodes["Track"], root);
                RectTransform thumb = c == 0 ? Move(ctx.Nodes["Thumb"], root) : Copy(ctx.Nodes["Thumb"], root);
                RectTransform hint = c == 0 ? Move(ctx.Nodes["SwipeHint"], root) : Copy(ctx.Nodes["SwipeHint"], root);

                LayoutNode trackNode = ctx.Node("Track");
                LayoutNode thumbNode = ctx.Node("Thumb");
                var so = new SerializedObject(carousel);
                so.FindProperty("_viewport").objectReferenceValue = window;
                so.FindProperty("_content").objectReferenceValue = content;
                so.FindProperty("_template").objectReferenceValue = template;
                so.FindProperty("_firstTile").vector2Value = Position(new Vector2(model.FirstX, model.CentreY), windowCentre);
                so.FindProperty("_stride").floatValue = model.Stride * UiBuildKit.ScaleX;
                so.FindProperty("_previousButton").objectReferenceValue = prevCopy.GetComponent<Button>();
                so.FindProperty("_nextButton").objectReferenceValue = nextCopy.GetComponent<Button>();
                so.FindProperty("_previousGroup").objectReferenceValue = UiMotion.GroupOf(prevCopy.gameObject);
                so.FindProperty("_nextGroup").objectReferenceValue = UiMotion.GroupOf(nextCopy.gameObject);
                SetArray(so.FindProperty("_browsing"), prevCopy.gameObject, nextCopy.gameObject, track.gameObject, thumb.gameObject, hint.gameObject);
                so.FindProperty("_thumb").objectReferenceValue = thumb;
                so.FindProperty("_thumbStartX").floatValue = thumb.anchoredPosition.x;
                so.FindProperty("_thumbTravel").floatValue = (trackNode.rect[2] - thumbNode.rect[2]) * UiBuildKit.ScaleX;
                so.ApplyModifiedPropertiesWithoutUndo();

                root.gameObject.SetActive(c == 0);
            }

            // Mockup 15's own tiles were only measured.
            for (int i = 0; i < Categories; i++)
            {
                Object.DestroyImmediate(ctx.Nodes["Tile" + i].gameObject);
            }
        }

        /// <summary>
        /// A category's tile template, built from its mockup's tile 0 (the outline from the tile
        /// that has it, the price line from mockup 21's tile 1), at the place of the first tile.
        /// </summary>
        private static ShopItemTile BuildTile(int category, ScreenLayout[] layouts, TileModel model, RectTransform content, Vector2 windowCentre)
        {
            ScreenLayout layout = layouts[category];
            ScreenLayout tables = layouts[2];
            LayoutNode tile0 = Node(layout, "Tile0");
            var at = new Vector2(model.FirstX, model.CentreY);
            RectTransform root = UiBuildKit.CreateAt("TileTemplate", content, at, windowCentre, UiBuildKit.FromMock(model.Width, model.Height));

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
            switch (category)
            {
                case 0:
                    BuildDeckPicture(layout, root, at, out picture, out pictureFace);
                    break;
                case 1:
                    BuildThumbPicture(layout, model, root, at, out picture, out pictureFace);
                    break;
                default:
                    BuildTablePicture(layout, model, root, at, out picture, out pictureFace);
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
            so.FindProperty("_name").objectReferenceValue = name;
            so.FindProperty("_outline").objectReferenceValue = outlineImage.gameObject;
            so.FindProperty("_statusRow").objectReferenceValue = statusLayout;
            so.FindProperty("_radioOn").objectReferenceValue = radioOn.gameObject;
            so.FindProperty("_radioOff").objectReferenceValue = radioOff.gameObject;
            so.FindProperty("_status").objectReferenceValue = status;
            so.FindProperty("_priceRow").objectReferenceValue = priceLayout;
            so.FindProperty("_price").objectReferenceValue = price;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return tile;
        }

        /// <summary>
        /// A deck's back (mockup 15's tile 0), shown at the card's own proportions, with the kit
        /// card's soft shadow scaled down to a card (the mockup draws a faint one under each back).
        /// </summary>
        private static void BuildDeckPicture(ScreenLayout layout, RectTransform tile, Vector2 at, out RectTransform picture, out Image face)
        {
            LayoutNode node = Node(layout, "DeckPicture0");
            Vector2 source = Centre(Node(layout, "Tile0").rect);
            Vector2 centre = at + (Centre(node.rect) - source);
            picture = UiBuildKit.CreateAt("Picture", tile, centre, at, UiBuildKit.FromMock(node.rect[2], node.rect[3]));
            Sprite back = UiBuildKit.LoadSpriteAt(node.sprite, 0f);

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

        /// <summary>A background's thumbnail in a rounded frame that it fills (mockup 20's tiles, averaged).</summary>
        private static void BuildThumbPicture(ScreenLayout layout, TileModel model, RectTransform tile, Vector2 at, out RectTransform picture, out Image face)
        {
            float[] box = model.MeanBox(layout, "Thumb", useSpriteRect: false);
            Vector2 centre = at + new Vector2(box[0], box[1]);
            picture = UiBuildKit.CreateAt("Picture", tile, centre, at, UiBuildKit.FromMock(box[2], box[3]));
            RectTransform frame = UiBuildKit.CreateAt("Frame", picture, centre, centre, picture.sizeDelta);
            Image mask = UiBuildKit.AddImage(frame, Load($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Card.png"), false);
            mask.pixelsPerUnitMultiplier = ThumbCornerScale;
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            RectTransform faceRect = UiBuildKit.CreateAt("Face", frame, centre, centre, picture.sizeDelta);
            face = UiBuildKit.AddImage(faceRect, Load(PictureArt(CosmeticKind.Background, CosmeticCatalog.Backgrounds.KathmanduCourtyard)), false);
            var cover = faceRect.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = face.sprite != null ? face.sprite.rect.width / face.sprite.rect.height : box[2] / box[3];
        }

        /// <summary>A table's preview, inside the box mockup 21's tables take (averaged), at its own proportions.</summary>
        private static void BuildTablePicture(ScreenLayout layout, TileModel model, RectTransform tile, Vector2 at, out RectTransform picture, out Image face)
        {
            float[] box = model.MeanBox(layout, "TablePicture", useSpriteRect: true);
            Vector2 centre = at + new Vector2(box[0], box[1]);
            picture = UiBuildKit.CreateAt("Picture", tile, centre, at, UiBuildKit.FromMock(box[2], box[3]));
            RectTransform faceRect = UiBuildKit.CreateAt("Face", picture, centre, centre, picture.sizeDelta);
            face = UiBuildKit.AddImage(faceRect, Load(PictureArt(CosmeticKind.Table, CosmeticCatalog.Tables.HimalayanIvory)), false);
            face.preserveAspect = true;
        }

        // ------------------------------------------------------------------ drawer

        /// <summary>
        /// The drawer, rebuilt from the builder's nodes: a rail flush with the right edge and as tall
        /// as the screen holds the sliding panel (shadow, panel, a plain bleed beyond the edge, and
        /// the divider and category buttons centred on the screen like the rest of the design) and,
        /// on top and fixed, the storefront disc.
        /// </summary>
        private static void BuildDrawer(ScreenBuildContext ctx, ScreenLayout[] layouts)
        {
            RectTransform oldDrawer = ctx.Nodes["Drawer"];
            LayoutNode drawerNode = ctx.Node("Drawer");
            Vector2 oldCentre = Centre(drawerNode.rect);
            float width = UiBuildKit.MockWidth - DrawerLeft;
            var railCentre = new Vector2(DrawerLeft + width / 2f, UiBuildKit.ScreenCentre.y);

            var railObject = new GameObject("ShopDrawer", typeof(RectTransform));
            var rail = (RectTransform)railObject.transform;
            rail.SetParent(ctx.SafeArea, false);
            rail.anchorMin = new Vector2(1f, 0f);
            rail.anchorMax = new Vector2(1f, 1f);
            rail.pivot = new Vector2(1f, 0.5f);
            rail.sizeDelta = new Vector2(width * UiBuildKit.ScaleX, 0f);
            rail.anchoredPosition = Vector2.zero;

            var panelObject = new GameObject("DrawerPanel", typeof(RectTransform), typeof(CanvasGroup));
            var panel = (RectTransform)panelObject.transform;
            panel.SetParent(rail, false);
            Stretch(panel);

            // The panel and its shadow (the drawer's rect grown by "shadowSpread"), as tall as the
            // screen: both are sliced so only their plain middles stretch.
            float spread = drawerNode.shadowSpread;
            Image shadow = oldDrawer.Find("Shadow").GetComponent<Image>();
            shadow.type = Image.Type.Sliced;
            var shadowRect = shadow.rectTransform;
            shadowRect.SetParent(panel, false);
            shadowRect.anchorMin = new Vector2(0f, 0f);
            shadowRect.anchorMax = new Vector2(0f, 1f);
            shadowRect.pivot = new Vector2(0f, 0.5f);
            shadowRect.sizeDelta = new Vector2((drawerNode.rect[2] + 2f * spread) * UiBuildKit.ScaleX, 2f * spread * UiBuildKit.ScaleY);
            shadowRect.anchoredPosition = new Vector2((drawerNode.rect[0] - spread - DrawerLeft) * UiBuildKit.ScaleX, 0f);

            Image face = oldDrawer.Find("Face").GetComponent<Image>();
            face.type = Image.Type.Sliced;
            face.raycastTarget = true;   // the drawer takes the taps that miss its buttons
            RectTransform faceRect = face.rectTransform;
            faceRect.SetParent(panel, false);
            Stretch(faceRect);

            RectTransform bleed = new GameObject("Bleed", typeof(RectTransform)).GetComponent<RectTransform>();
            bleed.SetParent(panel, false);
            bleed.anchorMin = new Vector2(1f, 0f);
            bleed.anchorMax = new Vector2(1f, 1f);
            bleed.pivot = new Vector2(0f, 0.5f);
            bleed.sizeDelta = new Vector2(DrawerBleed, 0f);
            bleed.anchoredPosition = Vector2.zero;
            Image bleedImage = bleed.gameObject.AddComponent<Image>();
            bleedImage.color = DrawerIvory;
            bleedImage.raycastTarget = true;

            RectTransform items = new GameObject("Items", typeof(RectTransform)).GetComponent<RectTransform>();
            items.SetParent(panel, false);
            items.anchorMin = items.anchorMax = items.pivot = new Vector2(0.5f, 0.5f);
            items.sizeDelta = new Vector2(width * UiBuildKit.ScaleX, UiBuildKit.CanvasHeight);
            items.anchoredPosition = Vector2.zero;

            // The divider under the storefront: the title divider's diamond between two lines, the left one flipped.
            foreach (string part in new[] { "DrawerLineLeft", "DrawerLineRight", "DrawerDiamond" })
            {
                Reparent(ctx.Nodes[part], items, oldCentre, railCentre);
            }

            ctx.Nodes["DrawerLineLeft"].localScale = new Vector3(-1f, 1f, 1f);

            var buttons = new Button[Categories];
            var active = new GameObject[Categories];
            var inactive = new GameObject[Categories];
            for (int c = 0; c < Categories; c++)
            {
                BuildCategoryButton(ctx, layouts, c, items, oldCentre, railCentre, out buttons[c], out active[c], out inactive[c]);
            }

            // The storefront disc stays where it is, above the sliding panel.
            RectTransform shop = ctx.Nodes["ShopButton"];
            Reparent(shop, rail, oldCentre, railCentre);
            shop.SetAsLastSibling();

            Object.DestroyImmediate(oldDrawer.gameObject);

            var drawer = railObject.AddComponent<ShopDrawer>();
            var so = new SerializedObject(drawer);
            so.FindProperty("_toggleButton").objectReferenceValue = shop.GetComponent<Button>();
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_panelGroup").objectReferenceValue = panelObject.GetComponent<CanvasGroup>();
            so.FindProperty("_openX").floatValue = 0f;
            so.FindProperty("_closedX").floatValue = (width + spread + 2f) * UiBuildKit.ScaleX;
            SetArray(so.FindProperty("_categoryButton"), buttons);
            SetArray(so.FindProperty("_activeLook"), active);
            SetArray(so.FindProperty("_inactiveLook"), inactive);
            so.ApplyModifiedPropertiesWithoutUndo();
            drawer.SetActive(0);
            ctx.Bound["_drawer"] = drawer;
        }

        /// <summary>
        /// One category button: its active look (the crimson disc, the light icon and the crimson
        /// label, from the mockup where it is active) and its inactive look (the ivory disc, the dark
        /// icon and the brown label, from a mockup where it is not), and a hit area over both disc
        /// and label.
        /// </summary>
        private static void BuildCategoryButton(
            ScreenBuildContext ctx, ScreenLayout[] layouts, int c, RectTransform items, Vector2 oldCentre, Vector2 railCentre,
            out Button button, out GameObject activeLook, out GameObject inactiveLook)
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

            var hitRect = Union(activeDisc.rect, inactiveDisc.rect, InkRect(Node(activeIn, "CategoryLabel" + c)), InkRect(Node(inactiveIn, "CategoryLabel" + c)));
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

        // ------------------------------------------------------------------ pictures

        private static void BuildPictures(ScreenBuildContext ctx)
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
                        ctx.Warnings.Add($"no picture for {kind} {item.Id}");
                        continue;
                    }

                    keys.Add(ShopPictures.KeyOf(kind.ToString(), item.Id));
                    sprites.Add(sprite);
                }
            }

            var pictures = ctx.Root.gameObject.AddComponent<ShopPictures>();
            var so = new SerializedObject(pictures);
            SerializedProperty keyList = so.FindProperty("_keys");
            keyList.arraySize = keys.Count;
            for (int i = 0; i < keys.Count; i++)
            {
                keyList.GetArrayElementAtIndex(i).stringValue = keys[i];
            }

            SetArray(so.FindProperty("_sprites"), sprites.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_pictures"] = pictures;
        }

        /// <summary>
        /// The picture of a background or a table: the generated thumbnail or preview once imported
        /// (Art/Shop/Backgrounds/Thumb_&lt;Name&gt;.png, Art/Shop/Tables/Preview_&lt;Name&gt;.png), its
        /// placeholder until then.
        /// </summary>
        public static string PictureArt(CosmeticKind kind, string id)
        {
            string name = string.Concat(id.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
            string final = kind == CosmeticKind.Background ? $"{FinalBackgroundsFolder}/Thumb_{name}.png" : $"{FinalTablesFolder}/Preview_{name}.png";
            string placeholder = kind == CosmeticKind.Background ? $"{PlaceholderFolder}/Thumb_{name}_Placeholder.png" : $"{PlaceholderFolder}/Preview_{name}_Placeholder.png";
            return File.Exists(final) ? final : placeholder;
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

        /// <summary>Moves a node to a parent centred where its old parent was centred.</summary>
        private static RectTransform Move(RectTransform rect, Transform parent)
        {
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>A copy of a node under a parent centred where the original's parent is centred.</summary>
        private static RectTransform Copy(RectTransform original, Transform parent)
        {
            var copy = (RectTransform)Object.Instantiate(original.gameObject, parent, false).transform;
            copy.name = original.name;
            copy.anchoredPosition = original.anchoredPosition;
            return copy;
        }

        /// <summary>A copy of a node centred on another mockup point.</summary>
        private static RectTransform Copy(RectTransform original, Transform parent, Vector2 centre, Vector2 parentCentre)
        {
            RectTransform copy = Copy(original, parent);
            copy.anchoredPosition = Position(centre, parentCentre);
            return copy;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
            public float Top;
            public float Stride;
            public float FirstX;
            public float[] CentresX;

            public float CentreY => Top + Height / 2f;

            public static TileModel Measure(ScreenLayout layout)
            {
                float[][] tiles = Enumerable.Range(0, Categories).Select(i => layout.nodes.First(n => n.id == "Tile" + i).rect).ToArray();
                float[] centres = tiles.Select(r => r[0] + r[2] / 2f).ToArray();
                float stride = (centres[2] - centres[0]) / 2f;
                return new TileModel
                {
                    Width = tiles.Average(r => r[2]),
                    Height = tiles.Average(r => r[3]),
                    Top = tiles.Average(r => r[1]),
                    Stride = stride,
                    FirstX = Enumerable.Range(0, centres.Length).Average(i => centres[i] - i * stride),
                    CentresX = centres,
                };
            }

            /// <summary>
            /// The mean box of <paramref name="prefix"/>0..2 (rect, or the cut sprite's own rect)
            /// as an offset from its tile's centre and a size: [dx, dy, w, h].
            /// </summary>
            public float[] MeanBox(ScreenLayout layout, string prefix, bool useSpriteRect)
            {
                var boxes = new List<float[]>();
                for (int i = 0; i < Categories; i++)
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
