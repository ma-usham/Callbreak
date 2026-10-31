using System.Collections.Generic;
using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Table;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// The shop's panel, Themes row and theme preview (Figma "V1 Rebuild · Shop" 278:2, ADR 064), in
    /// canvas units:
    /// <list type="bullet">
    /// <item>a cream panel from right of Back to just short of the drawer, as wide as the screen lets
    /// it be, holding the tab's title and subtitle, a divider and the wallet (its "+" opens Earn
    /// Coins); words on it read whatever the background;</item>
    /// <item>Themes: every theme's tile in a row that scrolls sideways (<see cref="ThemeShelf"/>,
    /// <see cref="ThemeTile"/>), a thin bar under it, and a hint;</item>
    /// <item>Cards: mockup 15's three decks and its primary button, moved into the panel;</item>
    /// <item>the theme preview over everything (<see cref="ThemePreview"/>), hidden: a dim, a panel
    /// with the theme's name, its two halves side by side (each a small 1920 × 1080 screen: the real
    /// background under a picture of the menu, the real cloth and its border under a picture of the
    /// table), the description, the button, the arrows, the cross and the wallet.</item>
    /// </list>
    /// </summary>
    public static partial class ShopCustomizer
    {
        public const string MenuOverlayDayPath = FinalThemesFolder + "/Preview_Menu_Day.png";
        public const string MenuOverlayNightPath = FinalThemesFolder + "/Preview_Menu_Night.png";
        public const string TableOverlayPath = FinalThemesFolder + "/Preview_Table.png";

        // The panel: from right of Back to the drawer, less a gap; as tall as Figma draws it.
        private const float PanelLeft = 180f;
        private const float PanelGap = 40f;
        private const float PanelHeight = 1010f;

        // Measured from the panel's top.
        private const float TitleY = 98f;
        private const float SubtitleY = 170f;
        private const float DividerY = 208f;
        private const float ShelfTop = 236f;
        private const float ShelfHeight = 600f;
        private const float ShelfInset = 30f;
        private const float ScrollBarY = 870f;
        private const float ScrollBarWidth = 520f;
        private const float HintY = 920f;
        private const float DecksY = 495f;
        private const float PrimaryY = 865f;

        // A theme's tile and its row.
        private const float TileWidth = 360f;
        private const float TileHeight = 560f;
        private const float TileStep = 386f;
        private const float TileLead = 24f;
        private const float TileTop = 14f;

        // The wallet: the main menu's, in the panel's top right corner.
        private static readonly Vector2 WalletSize = new Vector2(270f, 80f);
        private const float WalletInset = 44f;

        // The preview: its panel and its two small screens.
        private static readonly Vector2 PreviewSize = new Vector2(1660f, 980f);
        private static readonly Vector2 ScreenBox = new Vector2(632f, 356f);
        private const float ScreenGap = 96f;
        private const float ScreenTop = 236f;
        private const float BackdropWidth = 2560f;
        private const float BackdropHeight = 1440f;

        private static readonly Color TitleCrimson = UiBuildKit.Hex("#810911");
        private static readonly Color SubtitleBrown = UiBuildKit.Hex("#6E4E35");
        private static readonly Color Ink = UiBuildKit.Hex("#1C1E1F");
        private static readonly Color SoftInk = UiBuildKit.Hex("#724E33");
        private static readonly Color StatusInk = UiBuildKit.Hex("#624229");
        private static readonly Color PriceInk = UiBuildKit.Hex("#572E12");
        private static readonly Color ButtonWords = UiBuildKit.Hex("#FDFBFB");
        private static readonly Color TagCrimson = UiBuildKit.Hex("#9A0815");

        private static void BuildThemes(ScreenBuildContext ctx)
        {
            RectTransform oldPanel = ctx.Nodes["Panel"];
            RectTransform panel = BuildPanel(ctx);

            // The tabs' titles: Themes (new) and Cards (mockup 15's), in the panel.
            RectTransform cardsTitle = ctx.Nodes["Title0"];
            RectTransform cardsSubtitle = ctx.Nodes["Subtitle0"];
            RectTransform themesTitle = CopyLabel(cardsTitle, panel, "Title0", "Themes", "ui.shop.themes.title");
            RectTransform themesSubtitle = CopyLabel(cardsSubtitle, panel, "Subtitle0", "A MENU BACKGROUND AND ITS CLOTH, TOGETHER", "ui.shop.themes.subtitle");
            cardsTitle.name = "Title1";
            cardsSubtitle.name = "Subtitle1";
            foreach ((RectTransform label, float y) in new[] { (themesTitle, TitleY), (cardsTitle, TitleY), (themesSubtitle, SubtitleY), (cardsSubtitle, SubtitleY) })
            {
                PinTop(label, panel, y);
            }

            WidenToPanel(themesSubtitle);
            cardsTitle.gameObject.SetActive(false);
            cardsSubtitle.gameObject.SetActive(false);
            ctx.Nodes["Title0"] = themesTitle;
            ctx.Nodes["Title1"] = cardsTitle;
            ctx.Nodes["Subtitle0"] = themesSubtitle;
            ctx.Nodes["Subtitle1"] = cardsSubtitle;
            PinTop(ctx.Nodes["TitleDivider"], panel, DividerY);

            ctx.Bound["_walletBalance"] = BuildWallet(panel, new Vector2(1f, 1f), new Vector2(-WalletInset - WalletSize.x / 2f, -WalletInset - WalletSize.y / 2f), out Button earn);
            ctx.Bound["_earnCoinsButton"] = earn;

            // The pages.
            RectTransform themesPage = Stretch("ThemesPage", panel);
            ctx.Nodes["ThemesPage"] = themesPage;
            ctx.Bound["_shelf"] = BuildShelf(themesPage);
            TextMeshProUGUI hint = NewLabel("Hint", themesPage, "Tap a theme to see it on the menu and in a game.", "SansSemiBold", 28f, SoftInk, TextAlignmentOptions.Center, "ui.shop.themes.hint");
            PinTop(hint.rectTransform, themesPage, HintY, new Vector2(1200f, 40f));

            RectTransform cardsPage = Stretch("CardsPage", panel);
            ctx.Nodes["CardsPage"] = cardsPage;
            RectTransform decks = ctx.Nodes["Decks"];
            decks.SetParent(cardsPage, false);
            decks.anchorMin = decks.anchorMax = decks.pivot = new Vector2(0.5f, 0.5f);
            decks.anchoredPosition = new Vector2(0f, PanelHeight / 2f - DecksY);
            RectTransform primary = ctx.Nodes["PrimaryButton"];
            primary.SetParent(cardsPage, false);
            primary.anchorMin = primary.anchorMax = new Vector2(0.5f, 0.5f);
            primary.anchoredPosition = new Vector2(0f, PanelHeight / 2f - PrimaryY);
            cardsPage.gameObject.SetActive(false);

            Object.DestroyImmediate(oldPanel.gameObject);
            ctx.Nodes.Remove("Panel");

            ctx.Bound["_preview"] = BuildPreview(ctx);
            ctx.Bound["_library"] = TableBackdropBuilder.EnsureLibrary();
        }

        /// <summary>The cream panel (the dialogs' plate), stretched from right of Back to short of the drawer.</summary>
        private static RectTransform BuildPanel(ScreenBuildContext ctx)
        {
            var go = new GameObject("ShopPanel", typeof(RectTransform));
            var panel = (RectTransform)go.transform;
            panel.SetParent(ctx.SafeArea, false);
            panel.SetSiblingIndex(0);
            panel.anchorMin = new Vector2(0f, 0.5f);
            panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            float right = DrawerWidth * UiBuildKit.ScaleX + PanelGap;
            panel.offsetMin = new Vector2(PanelLeft, -PanelHeight / 2f);
            panel.offsetMax = new Vector2(-right, PanelHeight / 2f);

            Image shadow = UiBuildKit.AddImage(Stretch("Shadow", panel, 18f, -6f), Load($"{UiBuildKit.ArtRoot}/UI/Common/UI_Panel_Large_Shadow.png"), false);
            shadow.color = new Color(1f, 1f, 1f, 0.6f);
            Backing(panel);
            Image face = UiBuildKit.AddImage(Stretch("Face", panel), Load($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Modal_Menu.png"), true);
            face.type = Image.Type.Sliced;
            return panel;
        }

        // ------------------------------------------------------------------ the Themes row

        /// <summary>The row: a viewport that clips, a content as wide as every tile, the tiles, and the bar under it.</summary>
        private static ThemeShelf BuildShelf(RectTransform page)
        {
            var viewportGo = new GameObject("Shelf", typeof(RectTransform));
            var viewport = (RectTransform)viewportGo.transform;
            viewport.SetParent(page, false);
            viewport.anchorMin = new Vector2(0f, 1f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.sizeDelta = new Vector2(-2f * ShelfInset, ShelfHeight);
            viewport.anchoredPosition = new Vector2(0f, -ShelfTop);
            Image catcher = viewportGo.AddComponent<Image>();
            catcher.color = new Color(1f, 1f, 1f, 0f);
            viewportGo.AddComponent<RectMask2D>();

            IReadOnlyList<CosmeticItem> themes = CosmeticCatalog.Of(CosmeticKind.Theme);
            var contentGo = new GameObject("Content", typeof(RectTransform));
            var content = (RectTransform)contentGo.transform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(2f * TileLead + themes.Count * TileStep - (TileStep - TileWidth), 0f);
            content.anchoredPosition = Vector2.zero;

            var tiles = new Object[themes.Count];
            for (int i = 0; i < themes.Count; i++)
            {
                tiles[i] = BuildThemeTile(content, themes[i], new Vector2(TileLead + i * TileStep, -TileTop));
            }

            var scroll = viewportGo.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 30f;
            scroll.horizontalScrollbar = BuildScrollBar(page);
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            var shelf = viewportGo.AddComponent<ThemeShelf>();
            var so = new SerializedObject(shelf);
            so.FindProperty("_scroll").objectReferenceValue = scroll;
            SetArray(so.FindProperty("_tiles"), tiles);
            so.ApplyModifiedPropertiesWithoutUndo();
            return shelf;
        }

        /// <summary>The thin bar under the row: the slider's track, its fill as the handle.</summary>
        private static Scrollbar BuildScrollBar(RectTransform page)
        {
            RectTransform bar = Pinned("ScrollBar", page, new Vector2(0.5f, 1f), new Vector2(0f, -ScrollBarY), new Vector2(ScrollBarWidth, 10f));
            Image track = UiBuildKit.AddImage(bar, Load($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Slider_Track.png"), true);
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = 4f;
            RectTransform area = Stretch("SlidingArea", bar);
            RectTransform handle = Stretch("Handle", area);
            Image handleImage = UiBuildKit.AddImage(handle, Load($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Slider_Fill.png"), true);
            handleImage.type = Image.Type.Sliced;
            handleImage.pixelsPerUnitMultiplier = 4f;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            return scrollbar;
        }

        /// <summary>
        /// One theme's tile, its top-left corner at <paramref name="at"/> in the row: the kit's light
        /// card and its crimson outline (as the decks'), the picture, the NEW tag, the name, "Background
        /// + cloth", and the status and price lines, each a centred row.
        /// </summary>
        private static ThemeTile BuildThemeTile(RectTransform content, CosmeticItem theme, Vector2 at)
        {
            RectTransform root = Pinned("Tile_" + TableBackdropBuilder.ArtName(theme.Id), content, new Vector2(0f, 1f), at, new Vector2(TileWidth, TileHeight));
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = at;

            Image shadow = UiBuildKit.AddImage(Pinned("Shadow", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(TileWidth + 28f, TileHeight + 28f)), Load($"{UiBuildKit.ArtRoot}/UI/Common/UI_Panel_Card_Shadow.png"), false);
            shadow.type = Image.Type.Sliced;
            Image face = UiBuildKit.AddImage(Stretch("Face", root), Load($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Card_Light.png"), true);
            face.pixelsPerUnitMultiplier = 1.157f;
            Image outline = UiBuildKit.AddImage(Stretch("Outline", root, -3f, -3f), Load($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Outline_Thin_Selected.png"), false);
            outline.pixelsPerUnitMultiplier = 1.27f;
            outline.gameObject.SetActive(false);

            RectTransform pictureRect = Pinned("Picture", root, new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(328f, 296f));
            pictureRect.pivot = new Vector2(0f, 1f);
            pictureRect.anchoredPosition = new Vector2(16f, -16f);
            Image picture = UiBuildKit.AddImage(pictureRect, Load(PictureArt(CosmeticKind.Theme, theme.Id)), false);
            picture.enabled = picture.sprite != null;

            RectTransform tag = Pinned("NewTag", root, new Vector2(0f, 1f), new Vector2(30f + 46f, -30f - 18f), new Vector2(92f, 36f));
            Image tagFace = UiBuildKit.AddImage(tag, Load($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Card_Light.png"), false);
            tagFace.type = Image.Type.Sliced;
            tagFace.pixelsPerUnitMultiplier = 1.6f;
            tagFace.color = TagCrimson;
            TextMeshProUGUI tagWords = NewLabel("Words", tag, "NEW", "SansBold", 22f, UiBuildKit.Hex("#FFF6E8"), TextAlignmentOptions.Center, "ui.shop.themes.new");
            tagWords.characterSpacing = 10f;
            Fill(tagWords.rectTransform);
            tag.gameObject.SetActive(false);

            TextMeshProUGUI name = NewLabel("Name", root, theme.Id, "SerifBold", 34f, Ink, TextAlignmentOptions.Center);
            PinTop(name.rectTransform, root, 352f, new Vector2(328f, 46f));
            TextMeshProUGUI contents = NewLabel("Contents", root, "Background + cloth", "SansSemiBold", 24f, SoftInk, TextAlignmentOptions.Center, "ui.shop.themes.contents");
            PinTop(contents.rectTransform, root, 392f, new Vector2(328f, 32f));

            // The status line: a radio and its words, centred on the tile.
            RectTransform statusRow = Pinned("StatusRow", root, new Vector2(0.5f, 1f), new Vector2(0f, -495f), new Vector2(TileWidth, 56f));
            RectTransform radio = Pinned("Radio", statusRow, new Vector2(0.5f, 0.5f), new Vector2(-70f, 0f), new Vector2(44f, 44f));
            Image radioOn = UiBuildKit.AddImage(Stretch("RadioOn", radio), Load($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Radio_On.png"), false);
            Image radioOff = UiBuildKit.AddImage(Stretch("RadioOff", radio), Load($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Radio_Off.png"), false);
            radioOff.gameObject.SetActive(false);
            TextMeshProUGUI status = NewLabel("Status", statusRow, "Equipped", "SansMedium", 30f, StatusInk, TextAlignmentOptions.MidlineLeft);
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 0.5f);
            statusRect.pivot = new Vector2(0f, 0.5f);
            statusRect.sizeDelta = new Vector2(220f, 44f);
            statusRect.anchoredPosition = new Vector2(-36f, 0f);
            ShopRow statusLayout = Row(statusRow, radio, statusRect);

            // The price line: a coin and the price.
            RectTransform priceRow = Pinned("PriceRow", root, new Vector2(0.5f, 1f), new Vector2(0f, -495f), new Vector2(TileWidth, 56f));
            RectTransform coin = Pinned("Coin", priceRow, new Vector2(0.5f, 0.5f), new Vector2(-50f, 0f), new Vector2(50f, 50f));
            UiBuildKit.AddImage(coin, Load($"{UiBuildKit.ArtRoot}/UI/Icons/Icon_Coin.png"), false).preserveAspect = true;
            TextMeshProUGUI price = NewLabel("Price", priceRow, "1,000", "SansSemiBold", 36f, PriceInk, TextAlignmentOptions.MidlineLeft);
            RectTransform priceRect = price.rectTransform;
            priceRect.anchorMin = priceRect.anchorMax = new Vector2(0.5f, 0.5f);
            priceRect.pivot = new Vector2(0f, 0.5f);
            priceRect.sizeDelta = new Vector2(200f, 48f);
            priceRect.anchoredPosition = new Vector2(-13f, 0f);
            ShopRow priceLayout = Row(priceRow, coin, priceRect);
            priceRow.gameObject.SetActive(false);

            Button button = AddButton(root.gameObject, face);
            var tile = root.gameObject.AddComponent<ThemeTile>();
            var so = new SerializedObject(tile);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_picture").objectReferenceValue = picture;
            so.FindProperty("_newTag").objectReferenceValue = tag.gameObject;
            so.FindProperty("_name").objectReferenceValue = name;
            so.FindProperty("_outline").objectReferenceValue = outline.gameObject;
            so.FindProperty("_statusRow").objectReferenceValue = statusLayout;
            so.FindProperty("_radioOn").objectReferenceValue = radioOn.gameObject;
            so.FindProperty("_radioOff").objectReferenceValue = radioOff.gameObject;
            so.FindProperty("_status").objectReferenceValue = status;
            so.FindProperty("_priceRow").objectReferenceValue = priceLayout;
            so.FindProperty("_price").objectReferenceValue = price;
            so.ApplyModifiedPropertiesWithoutUndo();
            return tile;
        }

        // ------------------------------------------------------------------ the preview

        /// <summary>The theme preview over the whole screen, hidden until a theme is tapped.</summary>
        private static ThemePreview BuildPreview(ScreenBuildContext ctx)
        {
            RectTransform root = Stretch("ThemePreview", ctx.Root);
            root.SetAsLastSibling();
            Image dim = UiBuildKit.AddImage(Stretch("Dim", root, 400f), null, true);
            dim.color = new Color(0.08f, 0.04f, 0.02f, 0.62f);

            RectTransform panel = Pinned("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, PreviewSize);
            Backing(panel);
            Image face = UiBuildKit.AddImage(Stretch("Face", panel), Load($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Modal_Menu.png"), true);
            face.type = Image.Type.Sliced;

            TextMeshProUGUI walletBalance = BuildWallet(panel, new Vector2(0f, 1f), new Vector2(WalletInset + WalletSize.x / 2f, -WalletInset - WalletSize.y / 2f), out Button earn);
            Button close = RoundButton("CloseButton", panel, new Vector2(1f, 1f), new Vector2(-44f - 49f, -40f - 49f), $"{UiBuildKit.ArtRoot}/UI/Common/Icon_Close.png", 40f, false);

            TextMeshProUGUI title = NewLabel("Title", panel, "Tihar Lights", "SerifBold", 72f, TitleCrimson, TextAlignmentOptions.Center);
            PinTop(title.rectTransform, panel, 84f, new Vector2(1000f, 96f));
            TextMeshProUGUI subtitle = NewLabel("Subtitle", panel, "ONE PURCHASE, BOTH LOOKS", "SansSemiBold", 26f, SubtitleBrown, TextAlignmentOptions.Center, "ui.shop.preview.subtitle");
            subtitle.characterSpacing = 26f;
            PinTop(subtitle.rectTransform, panel, 150f, new Vector2(1000f, 36f));
            Image divider = UiBuildKit.AddImage(Pinned("Divider", panel, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(404f, 26f)), Load($"{UiBuildKit.ArtRoot}/UI/Decor/Deco_TitleDivider.png"), false);
            divider.preserveAspect = true;

            float half = ScreenGap / 2f + ScreenBox.x / 2f;
            float screenCentreY = -(ScreenTop + ScreenBox.y / 2f);
            RectTransform menuScreen = SmallScreen("MenuPreview", panel, new Vector2(-half, screenCentreY), out Image background, out Image menuOverlay, out Image unusedFrame, false);
            RectTransform playScreen = SmallScreen("PlayPreview", panel, new Vector2(half, screenCentreY), out Image cloth, out Image tableOverlay, out Image clothFrame, true);
            Object.DestroyImmediate(unusedFrame.gameObject);
            menuOverlay.sprite = Load(MenuOverlayDayPath);
            menuOverlay.enabled = menuOverlay.sprite != null;
            tableOverlay.sprite = Load(TableOverlayPath);
            tableOverlay.enabled = tableOverlay.sprite != null;
            TableBackdropLibrary library = TableBackdropBuilder.EnsureLibrary();
            background.sprite = library.BackgroundPicture(CosmeticCatalog.Backgrounds.Default, out _);
            cloth.sprite = library.TablePicture(CosmeticCatalog.Tables.Default, out _);
            clothFrame.sprite = library.TableFrame(CosmeticCatalog.Tables.Default);

            // The "+" between the halves.
            RectTransform plus = Pinned("Plus", panel, new Vector2(0.5f, 1f), new Vector2(0f, screenCentreY), new Vector2(76f, 76f));
            UiBuildKit.AddImage(plus, Load($"{UiBuildKit.ArtRoot}/UI/Buttons/UI_ButtonDisc_Normal.png"), false).preserveAspect = true;
            TextMeshProUGUI plusSign = NewLabel("Sign", plus, "+", "SansBold", 56f, UiBuildKit.Hex("#B07A2A"), TextAlignmentOptions.Center);
            Fill(plusSign.rectTransform);

            float captionY = ScreenTop + ScreenBox.y + 44f;
            Caption("MenuCaption", panel, new Vector2(-half, -captionY), $"{UiBuildKit.ArtRoot}/UI/Icons/Icon_Mountains_Brown.png", "Menu background", "ui.shop.preview.menu");
            Caption("ClothCaption", panel, new Vector2(half, -captionY), $"{UiBuildKit.ArtRoot}/UI/Icons/Icon_TableOval_Brown.png", "Cloth for your games", "ui.shop.preview.cloth");

            TextMeshProUGUI description = NewLabel("Description", panel, "", "SansSemiBold", 28f, SoftInk, TextAlignmentOptions.Top);
            description.textWrappingMode = TextWrappingModes.Normal;
            PinTop(description.rectTransform, panel, captionY + 82f, new Vector2(1240f, 84f));

            // The button: Buy for (coin) N, Equip theme, or Equipped.
            var buttonAt = new Vector2(0f, -PreviewSize.y + 156f - 55f);
            Button buy = CrimsonButton("BuyButton", panel, buttonAt, out CanvasGroup buyContent, out RectTransform buyWords);
            TextMeshProUGUI buyFor = NewLabel("BuyFor", buyWords, "Buy for", "SerifSemiBold", 44f, ButtonWords, TextAlignmentOptions.MidlineLeft, "ui.shop.buy_for");
            SizeLeft(buyFor.rectTransform, new Vector2(-150f, 0f), new Vector2(200f, 60f));
            RectTransform buyCoin = Pinned("Coin", buyWords, new Vector2(0.5f, 0.5f), new Vector2(40f, 0f), new Vector2(56f, 56f));
            UiBuildKit.AddImage(buyCoin, Load($"{UiBuildKit.ArtRoot}/UI/Icons/Icon_Coin.png"), false).preserveAspect = true;
            TextMeshProUGUI buyPrice = NewLabel("Price", buyWords, "1,000", "SerifSemiBold", 44f, ButtonWords, TextAlignmentOptions.MidlineLeft);
            SizeLeft(buyPrice.rectTransform, new Vector2(80f, 0f), new Vector2(200f, 60f));
            ShopRow buyRow = Row(buyWords, buyFor.rectTransform, buyCoin, buyPrice.rectTransform);

            Button equip = CrimsonButton("EquipButton", panel, buttonAt, out CanvasGroup equipContent, out RectTransform equipWords);
            TextMeshProUGUI equipLabel = NewLabel("Words", equipWords, "Equip theme", "SerifSemiBold", 44f, ButtonWords, TextAlignmentOptions.Center, "ui.shop.preview.equip");
            Fill(equipLabel.rectTransform);
            equip.gameObject.SetActive(false);

            RectTransform equipped = Pinned("Equipped", panel, new Vector2(0.5f, 1f), buttonAt, new Vector2(560f, 100f));
            Image equippedFace = UiBuildKit.AddImage(equipped, Load($"{UiBuildKit.ArtRoot}/UI/Buttons/UI_ButtonSecondary_Normal.png"), false);
            equippedFace.type = Image.Type.Sliced;
            equippedFace.pixelsPerUnitMultiplier = 1.17f;
            RectTransform equippedRow = Stretch("Row", equipped);
            RectTransform equippedRadio = Pinned("Radio", equippedRow, new Vector2(0.5f, 0.5f), new Vector2(-80f, 0f), new Vector2(44f, 44f));
            UiBuildKit.AddImage(equippedRadio, Load($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Radio_On.png"), false);
            TextMeshProUGUI equippedWords = NewLabel("Words", equippedRow, "Equipped", "SansMedium", 34f, StatusInk, TextAlignmentOptions.MidlineLeft, "ui.shop.status.equipped");
            SizeLeft(equippedWords.rectTransform, new Vector2(-46f, 0f), new Vector2(240f, 50f));
            Row(equippedRow, equippedRadio, equippedWords.rectTransform).Refresh();
            equipped.gameObject.SetActive(false);

            float arrowY = screenCentreY;
            Button previous = RoundButton("PreviousButton", panel, new Vector2(0f, 1f), new Vector2(30f + 49f, arrowY), $"{UiBuildKit.ArtRoot}/UI/Icons/Icon_Chevron.png", 34f, true);
            Button next = RoundButton("NextButton", panel, new Vector2(1f, 1f), new Vector2(-30f - 49f, arrowY), $"{UiBuildKit.ArtRoot}/UI/Icons/Icon_Chevron.png", 34f, false);

            var preview = root.gameObject.AddComponent<ThemePreview>();
            var so = new SerializedObject(preview);
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_description").objectReferenceValue = description;
            so.FindProperty("_menuBackground").objectReferenceValue = background;
            so.FindProperty("_menuOverlay").objectReferenceValue = menuOverlay;
            so.FindProperty("_menuOverlayDay").objectReferenceValue = Load(MenuOverlayDayPath);
            so.FindProperty("_menuOverlayNight").objectReferenceValue = Load(MenuOverlayNightPath);
            so.FindProperty("_cloth").objectReferenceValue = cloth;
            so.FindProperty("_clothFrame").objectReferenceValue = clothFrame;
            so.FindProperty("_closeButton").objectReferenceValue = close;
            so.FindProperty("_previousButton").objectReferenceValue = previous;
            so.FindProperty("_nextButton").objectReferenceValue = next;
            so.FindProperty("_buyButton").objectReferenceValue = buy;
            so.FindProperty("_buyContent").objectReferenceValue = buyContent;
            so.FindProperty("_buyRow").objectReferenceValue = buyRow;
            so.FindProperty("_buyPrice").objectReferenceValue = buyPrice;
            so.FindProperty("_equipButton").objectReferenceValue = equip;
            so.FindProperty("_equipContent").objectReferenceValue = equipContent;
            so.FindProperty("_equipped").objectReferenceValue = equipped.gameObject;
            so.FindProperty("_walletBalance").objectReferenceValue = walletBalance;
            so.FindProperty("_earnCoinsButton").objectReferenceValue = earn;
            so.ApplyModifiedPropertiesWithoutUndo();
            _ = menuScreen;
            _ = playScreen;
            root.gameObject.SetActive(false);
            return preview;
        }

        /// <summary>
        /// One half of the preview: a rounded frame holding a 1920 × 1080 screen scaled to fit, with the
        /// backdrop (2560 × 1440, as the screens show it) under a picture of the menu or the table; the
        /// table's half also has the cloth's border, tiled round its edges as the table tiles it.
        /// </summary>
        private static RectTransform SmallScreen(string name, RectTransform panel, Vector2 centre, out Image backdrop, out Image overlay, out Image frame, bool withFrame)
        {
            RectTransform box = Pinned(name, panel, new Vector2(0.5f, 1f), centre, ScreenBox);
            Image border = UiBuildKit.AddImage(Stretch("Border", box, -6f, -6f), Load($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Card_Light.png"), false);
            border.type = Image.Type.Sliced;
            border.pixelsPerUnitMultiplier = 1.4f;
            RectTransform mask = Stretch("Mask", box);
            Image maskImage = UiBuildKit.AddImage(mask, Load($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Card_Light.png"), false);
            maskImage.type = Image.Type.Sliced;
            maskImage.pixelsPerUnitMultiplier = 1.6f;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            RectTransform screen = Pinned("Screen", mask, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(UiBuildKit.CanvasWidth, UiBuildKit.CanvasHeight));
            float scale = ScreenBox.x / UiBuildKit.CanvasWidth;
            screen.localScale = new Vector3(scale, scale, 1f);
            backdrop = UiBuildKit.AddImage(Pinned("Backdrop", screen, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BackdropWidth, BackdropHeight)), null, false);
            frame = UiBuildKit.AddImage(Stretch("ClothFrame", screen), null, false);
            frame.type = Image.Type.Tiled;
            frame.fillCenter = false;
            frame.enabled = withFrame;
            overlay = UiBuildKit.AddImage(Stretch("Overlay", screen), null, false);
            return box;
        }

        /// <summary>A preview caption: an icon and its words, centred under its half.</summary>
        private static void Caption(string name, RectTransform panel, Vector2 centre, string iconPath, string words, string key)
        {
            RectTransform row = Pinned(name, panel, new Vector2(0.5f, 1f), centre, new Vector2(ScreenBox.x, 50f));
            RectTransform icon = Pinned("Icon", row, new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), new Vector2(64f, 36f));
            UiBuildKit.AddImage(icon, Load(iconPath), false).preserveAspect = true;
            TextMeshProUGUI label = NewLabel("Words", row, words, "SansSemiBold", 32f, Ink, TextAlignmentOptions.MidlineLeft, key);
            SizeLeft(label.rectTransform, new Vector2(-104f, 0f), new Vector2(400f, 46f));
            Row(row, icon, label.rectTransform).Refresh();
        }

        /// <summary>A crimson button as the decks' primary one (its shadow, its dhaka ends), its words in a group that dims with it.</summary>
        private static Button CrimsonButton(string name, RectTransform panel, Vector2 centre, out CanvasGroup content, out RectTransform words)
        {
            var size = new Vector2(620f, 110f);
            RectTransform root = Pinned(name, panel, new Vector2(0.5f, 1f), centre, size);
            Image shadow = UiBuildKit.AddImage(Pinned("Shadow", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -7f), size + new Vector2(24f, 24f)), Load($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Button_Crimson_Shadow.png"), false);
            shadow.type = Image.Type.Sliced;
            shadow.color = new Color(1f, 1f, 1f, 0.25f);
            Image face = UiBuildKit.AddImage(Stretch("Face", root), Load($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Button_Crimson.png"), true);
            face.type = Image.Type.Sliced;
            words = Stretch("Content", root);
            content = words.gameObject.AddComponent<CanvasGroup>();
            content.interactable = false;
            content.blocksRaycasts = false;
            return AddButton(root.gameObject, face);
        }

        /// <summary>A round button (Back's disc) with an icon; a mirrored icon for the arrow that points left.</summary>
        private static Button RoundButton(string name, RectTransform parent, Vector2 anchor, Vector2 centre, string iconPath, float iconHeight, bool mirrored)
        {
            RectTransform root = Pinned(name, parent, anchor, centre, new Vector2(98f, 98f));
            Image shadow = UiBuildKit.AddImage(Pinned("Shadow", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(118f, 118f)), Load($"{UiBuildKit.ArtRoot}/UI/Buttons/UI_Shadow_Circle.png"), false);
            shadow.color = new Color(1f, 1f, 1f, 0.2f);
            Image face = UiBuildKit.AddImage(Stretch("Face", root), Load($"{UiBuildKit.ArtRoot}/UI/Buttons/UI_ButtonCircle_Normal.png"), true);
            Sprite icon = Load(iconPath);
            float aspect = icon != null && icon.rect.height > 0f ? icon.rect.width / icon.rect.height : 1f;
            RectTransform iconRect = Pinned("Icon", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconHeight * aspect, iconHeight));
            UiBuildKit.AddImage(iconRect, icon, false).preserveAspect = true;
            if (mirrored)
            {
                iconRect.localScale = new Vector3(-1f, 1f, 1f);
            }

            return AddButton(root.gameObject, face);
        }

        /// <summary>
        /// The wallet, as the main menu draws it (the secondary plate, the coin, the balance, the "+"),
        /// centred at <paramref name="centre"/> from <paramref name="anchor"/>. Returns its balance.
        /// </summary>
        private static TextMeshProUGUI BuildWallet(RectTransform parent, Vector2 anchor, Vector2 centre, out Button earnCoins)
        {
            RectTransform wallet = Pinned("Wallet", parent, anchor, centre, WalletSize);
            Sprite pill = Load($"{UiBuildKit.ArtRoot}/UI/Buttons/UI_ButtonSecondary_Normal.png");
            float scale = WalletSize.y / UiBuildKit.NativeSize(pill).y;
            Image shadow = UiBuildKit.AddImage(Pinned("Shadow", wallet, new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), WalletSize + new Vector2(20f, 20f)), Load($"{UiBuildKit.ArtRoot}/UI/Buttons/UI_Shadow_Secondary.png"), false);
            shadow.color = new Color(1f, 1f, 1f, 0.4f);
            shadow.pixelsPerUnitMultiplier = 1f / scale;
            UiBuildKit.AddImage(Stretch("Pill", wallet), pill, false).pixelsPerUnitMultiplier = 1f / scale;
            RectTransform coin = Pinned("Coin", wallet, new Vector2(0f, 0.5f), new Vector2(26f + 30f, 0f), new Vector2(60f, 60f));
            UiBuildKit.AddImage(coin, Load($"{UiBuildKit.ArtRoot}/UI/Icons/Icon_Coin.png"), false).preserveAspect = true;
            TextMeshProUGUI balance = NewLabel("Balance", wallet, "250", "SansBold", 36f, UiBuildKit.Hex("#181C22"), TextAlignmentOptions.Center);
            balance.enableAutoSizing = true;
            balance.fontSizeMax = 36f;
            balance.fontSizeMin = 22f;
            balance.overflowMode = TextOverflowModes.Ellipsis;
            Pinned(balance.rectTransform, wallet, new Vector2(0.5f, 0.5f), new Vector2(8f, 0f), new Vector2(100f, 50f));
            RectTransform plus = Pinned("EarnCoinsButton", wallet, new Vector2(1f, 0.5f), new Vector2(-14f - 28f, 0f), new Vector2(56f, 56f));
            Image plusFace = UiBuildKit.AddImage(Stretch("Face", plus), Load($"{UiBuildKit.ArtRoot}/UI/Buttons/UI_ButtonPlus_Normal.png"), true);
            plusFace.raycastPadding = -Vector4.one * 15f;
            earnCoins = AddButton(plus.gameObject, plusFace);
            return balance;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A solid cream plate under a panel's face, so nothing behind shows through its plate.</summary>
        private static void Backing(RectTransform panel)
        {
            Image backing = UiBuildKit.AddImage(Stretch("Backing", panel, -10f), Load($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Card_Light.png"), false);
            backing.type = Image.Type.Sliced;
            backing.pixelsPerUnitMultiplier = 0.5f;
            backing.color = UiBuildKit.Hex("#FBF5EC");
        }

        /// <summary>A copy of a mockup label (its font, size and look) with other words and key.</summary>
        private static RectTransform CopyLabel(RectTransform source, RectTransform parent, string name, string words, string key)
        {
            RectTransform copy = Object.Instantiate(source, parent, false);
            copy.name = name;
            var label = copy.GetComponent<TMP_Text>();
            label.text = words;
            var localized = copy.GetComponent<LocalizedText>();
            if (localized != null)
            {
                var so = new SerializedObject(localized);
                so.FindProperty("_key").stringValue = key;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                AddKey(copy.gameObject, key);
            }

            return copy;
        }

        /// <summary>Lets a label run as wide as the panel (a longer subtitle stays on one line, centred).</summary>
        private static void WidenToPanel(RectTransform label)
        {
            label.sizeDelta = new Vector2(1400f, label.sizeDelta.y);
            var text = label.GetComponent<TMP_Text>();
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>Puts a node under <paramref name="parent"/>, centred across it, its centre <paramref name="y"/> below its top.</summary>
        private static void PinTop(RectTransform rect, RectTransform parent, float y, Vector2? size = null)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            if (size.HasValue)
            {
                rect.sizeDelta = size.Value;
            }

            rect.anchoredPosition = new Vector2(0f, -y);
        }

        private static RectTransform Pinned(string name, Transform parent, Vector2 anchor, Vector2 centre, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            return Pinned((RectTransform)go.transform, parent, anchor, centre, size);
        }

        private static RectTransform Pinned(RectTransform rect, Transform parent, Vector2 anchor, Vector2 centre, Vector2 size)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = centre;
            return rect;
        }

        /// <summary>A child that fills its parent, grown by <paramref name="grow"/> each side and moved by <paramref name="drop"/>.</summary>
        private static RectTransform Stretch(string name, Transform parent, float grow = 0f, float drop = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(-grow, -grow + drop);
            rect.offsetMax = new Vector2(grow, grow + drop);
            return rect;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>Sizes a label whose left edge sits at <paramref name="left"/> from its parent's centre.</summary>
        private static void SizeLeft(RectTransform rect, Vector2 left, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = left;
        }

        /// <summary>A label in a typography role (GDD §12), its key when it has one.</summary>
        private static TextMeshProUGUI NewLabel(string name, Transform parent, string words, string role, float size, Color colour, TextAlignmentOptions align, string key = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = UiBuildKit.LoadFontRole(role);
            label.fontSize = size;
            label.color = colour;
            label.text = words;
            label.alignment = align;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            AddKey(go, key);
            return label;
        }
    }
}
