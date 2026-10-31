using System.Collections;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The shop in the real Boot and Menu scenes, with the real wallet and cosmetics (Figma "V1
    /// Rebuild · Shop", ADR 064; the decks as since the owner's 4, 5 and 6 Oct 2026): the main menu's
    /// storefront opens it on Themes; the drawer is always open and flush with the screen's right
    /// edge, Themes and Cards stacked in its middle with No Ads under them while the store sells it;
    /// the themes lie in a row that scrolls sideways, each saying Equipped, Owned or its price; a
    /// theme tapped opens its preview, its menu background and its cloth side by side, whose arrows
    /// go round the themes; a theme is bought after a confirmation and its preview then equips it,
    /// background and cloth together, and the shop itself changes to it; a background bought before
    /// the themes owns its theme; not enough coins offers the Earn Coins page or Not now, and the shop
    /// waits for the return; the wallet's "+" opens Earn Coins; on Cards a deck is bought and put to
    /// use as before; and Back leaves. Saves go to a temporary folder.
    /// </summary>
    public sealed class ShopFlowTests
    {
        private const int Themes = ShopView.Themes;
        private const int Cards = ShopView.Cards;
        private const int ThemeCount = 10;
        private const int Sunrise = 1;
        private const int Tihar = 4;
        private const int Pokhara = 8;

        private GameFixture _game;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator TheMainMenusStorefront_OpensTheShopOnThemes_AndBackLeaves()
        {
            yield return OpenShop();

            Assert.AreEqual(Themes, View.Category);
            Assert.AreEqual(Themes, View.Drawer.Active, "Themes is the crimson button");
            Assert.AreEqual(ThemeCount, View.Shelf.Count, "every theme has its tile");
            Assert.IsFalse(View.Preview.IsOpen);
            ThemeTile durbar = Theme(0);
            Assert.IsTrue(durbar.ShowsOutline && durbar.RadioFilled, "Durbar Square, the free one, is equipped");
            Assert.AreEqual(Text("ui.shop.status.equipped"), durbar.StatusText.text);
            Assert.AreEqual(Text("ui.shop.theme.durbar_square"), durbar.Name.text);
            Assert.IsFalse(durbar.ShowsNew);
            Assert.IsTrue(Theme(Sunrise).ShowsPrice && Theme(Sunrise).ShowsNew, "a new theme for sale: its price and the NEW tag");
            Assert.AreEqual("500", Theme(Sunrise).Price.text);
            Assert.AreEqual("1,000", Theme(Tihar).Price.text);
            Assert.AreNotEqual(Theme(Sunrise).Price.color, Theme(Tihar).Price.color, "the 500 starting coins can't pay 1,000: that price is red");
            Assert.IsFalse(Theme(Pokhara).ShowsNew, "an older theme has no tag");
            Assert.AreEqual(View.ThemePicture(CosmeticCatalog.Themes.TiharLights), Theme(Tihar).Picture.sprite, "each tile shows its theme's picture");
            Assert.AreEqual("500", View.Coins, "the wallet");

            GameFixture.Press(Router, "BackButton");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, Router.Current, "Back leaves the shop");
        }

        [UnityTest]
        public IEnumerator TheDrawer_ReachesTheRightEdge_WithThemesAndCardsInItsMiddle_AndNoStorefrontDisc()
        {
            yield return OpenShop();
            yield return UntilSettled();

            Assert.IsFalse(Has("ShopButton") || Has("ShopIcon"), "no storefront disc in the shop's drawer");
            Assert.IsFalse(Has("DrawerLineLeft") || Has("DrawerLineRight") || Has("DrawerDiamond"), "and no divider under it");

            Rect screen = ScreenRect((RectTransform)View.transform);
            Rect drawer = ScreenRect((RectTransform)View.Drawer.transform);
            Assert.AreEqual(ScreenRect(Find("SafeArea")).xMax, drawer.xMax, 1f, "its buttons' rail on the safe area's right edge");
            Assert.GreaterOrEqual(ScreenRect((RectTransform)View.Drawer.transform.Find("Face")).xMax, screen.xMax - 1f, "its panel on to the screen's right edge");
            Assert.AreEqual(screen.height, drawer.height, 1f, "as tall as the screen");
            for (int shown = 0; shown < ShopView.CategoryCount; shown++)
            {
                Press("CategoryButton" + shown);
                Assert.AreEqual(shown, View.Category);
                Assert.AreEqual(shown, View.Drawer.Active);
                Rect previous = default;
                for (int c = 0; c < ShopView.CategoryCount; c++)
                {
                    Assert.IsTrue(GameFixture.CanPress(View, "CategoryButton" + c), $"button {c} can be tapped while tab {shown} shows");
                    Rect button = ButtonRect("CategoryButton" + c);
                    Assert.IsTrue(button.xMin >= drawer.xMin && button.xMax <= drawer.xMax, $"button {c} lies inside the drawer: {button} in {drawer}");
                    Assert.IsTrue(button.yMin >= screen.yMin && button.yMax <= screen.yMax, $"button {c} lies on the screen: {button} in {screen}");
                    if (c > 0)
                    {
                        Assert.LessOrEqual(button.yMax, previous.yMin, $"button {c} lies below button {c - 1}");
                    }

                    previous = button;
                }
            }

            RectTransform noAds = Find("NoAdsButton");
            Assert.AreEqual(View.Drawer.NoAdsButton.transform, noAds, "No Ads, the store's purchase, under the tabs");
            Rect noAdsRect = ScreenRect(View.Drawer.NoAdsButton.targetGraphic.rectTransform);
            Assert.LessOrEqual(noAdsRect.yMax, ButtonRect("CategoryButton1").yMin, "No Ads lies below Cards");
            float middle = (ButtonRect("CategoryButton0").yMax + noAdsRect.yMin) / 2f;
            Assert.AreEqual(drawer.center.y, middle, drawer.height * 0.01f, "the three are centred in the drawer");
        }

        /// <summary>
        /// A phone with a notch on the right (here a safe area 216 canvas units short of the right edge,
        /// the drawer's xMax of 1704 the run of 31 Oct 2026 found): the buttons keep clear of it, and the
        /// drawer's panel still runs on to the screen's edge, with no strip beside it (ADR 064).
        /// </summary>
        [UnityTest]
        public IEnumerator WithANotchOnTheRight_TheDrawersPanelStillReachesTheEdge_ItsButtonsClearOfTheNotch()
        {
            yield return OpenShop();
            yield return UntilSettled();
            RectTransform safe = Find("SafeArea");
            safe.GetComponent<SafeAreaFitter>().enabled = false;
            float inset = 216f / ((RectTransform)View.transform).rect.width;
            safe.anchorMin = new Vector2(0f, 0f);
            safe.anchorMax = new Vector2(1f - inset, 1f);
            yield return null;
            yield return null;

            Rect screen = ScreenRect((RectTransform)View.transform);
            Rect safeRect = ScreenRect(safe);
            Rect drawer = ScreenRect((RectTransform)View.Drawer.transform);
            Assert.Less(safeRect.xMax, screen.xMax - 10f, "the notch is there");
            Assert.AreEqual(safeRect.xMax, drawer.xMax, 1f, "the buttons' rail ends where the notch begins");
            Assert.GreaterOrEqual(ScreenRect((RectTransform)View.Drawer.transform.Find("Face")).xMax, screen.xMax - 1f, "the panel runs on under the notch to the edge");
            for (int c = 0; c < ShopView.CategoryCount; c++)
            {
                Assert.LessOrEqual(ButtonRect("CategoryButton" + c).xMax, safeRect.xMax + 0.5f, $"button {c} keeps clear of the notch");
            }
        }

        [UnityTest]
        public IEnumerator TheThemes_LieInARowThatScrollsSideways_TheNextTilePeekingIn()
        {
            yield return OpenShop();
            yield return UntilSettled();
            ScrollRect scroll = View.Shelf.Scroll;

            Assert.IsTrue(scroll.horizontal && !scroll.vertical, "the row scrolls sideways");
            Assert.Greater(scroll.content.rect.width, scroll.viewport.rect.width, "more themes than the panel shows");
            Rect shown = ScreenRect(scroll.viewport);
            int whole = 0, partly = 0;
            for (int i = 0; i < ThemeCount; i++)
            {
                Rect tile = ScreenRect((RectTransform)Theme(i).transform);
                if (tile.xMin >= shown.xMin - 0.5f && tile.xMax <= shown.xMax + 0.5f)
                {
                    whole++;
                }
                else if (tile.xMin < shown.xMax && tile.xMax > shown.xMin)
                {
                    partly++;
                }
            }

            Assert.GreaterOrEqual(whole, 3, "three whole tiles at least");
            Assert.AreEqual(1, partly, "and the next one peeks in at the edge");
            Assert.IsNotNull(scroll.horizontalScrollbar, "a bar under the row shows where it is");

            scroll.horizontalNormalizedPosition = 1f;
            yield return null;
            Rect last = ScreenRect((RectTransform)Theme(ThemeCount - 1).transform);
            Assert.LessOrEqual(last.xMax, shown.xMax + 0.5f, "the last theme can be scrolled into view");
            Assert.AreEqual(1f, scroll.horizontalScrollbar.value, 0.01f);
        }

        [UnityTest]
        public IEnumerator TappingATheme_OpensItsPreview_ItsBackgroundAndItsClothSideBySide()
        {
            yield return OpenShop();
            TableBackdropLibraryCheck library = new TableBackdropLibraryCheck(View);

            PressTheme(Tihar);

            Assert.IsTrue(View.Preview.IsOpen);
            Assert.AreEqual(Text("ui.shop.theme.tihar_lights"), View.Preview.Title);
            Assert.AreEqual(Text("ui.shop.theme.tihar_lights.about"), View.Preview.Description);
            Assert.AreEqual(library.Background(CosmeticCatalog.Themes.TiharLights), View.Preview.MenuBackground, "the menu half shows its background");
            Assert.AreEqual(library.Cloth(CosmeticCatalog.Themes.TiharLights), View.Preview.Cloth, "the game half shows its cloth");
            Assert.AreEqual(library.Frame(CosmeticCatalog.Themes.TiharLights), View.Preview.ClothFrame, "with its border");
            Assert.AreEqual(ThemeAction.Buy, View.Preview.Action);
            Assert.AreEqual("1,000", View.Preview.BuyPrice);

            Press("NextButton");
            Assert.AreEqual(Text("ui.shop.theme.prayer_flag_pass"), View.Preview.Title, "the arrows go to the next theme");
            Press("PreviousButton");
            Press("PreviousButton");
            Assert.AreEqual(Text("ui.shop.theme.golden_terraces"), View.Preview.Title, "and back");

            PressTheme(0);
            Assert.AreEqual(ThemeAction.Equipped, View.Preview.Action, "Durbar Square is on: Equipped, off");
            Press("PreviousButton");
            Assert.AreEqual(Text("ui.shop.theme.monastery_garden"), View.Preview.Title, "round from the first to the last");
            Assert.IsNull(View.Preview.ClothFrame, "Dusk Himalaya has no border");

            Press("CloseButton");
            Assert.IsFalse(View.Preview.IsOpen, "the cross closes it");
            Assert.AreEqual(ScreenId.Shop, Router.Current);
        }

        [UnityTest]
        public IEnumerator BuyingATheme_AfterTheConfirmation_OwnsIt_ThenItsPreviewEquipsBackgroundAndClothTogether()
        {
            yield return OpenShop();
            var shop = _game.Services.Resolve<ICosmeticsShop>();
            var wallet = _game.Services.Resolve<IWallet>();
            Assert.AreEqual(WalletRules.StartingCoins, wallet.Coins);

            PressTheme(Sunrise);
            Assert.AreEqual(ThemeAction.Buy, View.Preview.Action);
            Assert.AreEqual("500", View.Preview.BuyPrice);

            // Cancelled first: nothing changes.
            Press("BuyButton");
            Assert.IsTrue(_game.Dialogs.IsOpen, "a confirmation");
            Assert.IsTrue(DialogShows(Format("ui.shop.buy.title", Text("ui.shop.theme.himalayan_sunrise"))), "it names the theme");
            Assert.IsTrue(DialogShows(Format("ui.shop.theme.buy.body", "500", "500")), "both its halves and its price");
            _game.PressDialog(confirm: false);
            yield return null;
            Assert.IsFalse(shop.IsOwned(CosmeticKind.Theme, CosmeticCatalog.Themes.HimalayanSunrise));
            Assert.AreEqual(500, wallet.Coins);

            Press("BuyButton");
            _game.PressDialog(confirm: true);
            yield return null;
            Assert.IsTrue(shop.IsOwned(CosmeticKind.Theme, CosmeticCatalog.Themes.HimalayanSunrise), "bought");
            Assert.AreEqual(0, wallet.Coins, "for 500 coins, the whole starting balance");
            Assert.AreEqual(CosmeticCatalog.Themes.DurbarSquare, shop.Equipped(CosmeticKind.Theme), "buying does not equip");
            Assert.AreEqual(ThemeAction.Equip, View.Preview.Action, "the button turns to Equip theme");
            Assert.IsTrue(ToastShows(Format("ui.shop.theme.bought", Text("ui.shop.theme.himalayan_sunrise"))), "a toast says it is the player's");
            Assert.AreEqual(Text("ui.shop.status.owned"), Theme(Sunrise).StatusText.text);
            Assert.IsFalse(Theme(Sunrise).RadioFilled);

            Press("EquipButton");
            yield return null;
            Assert.AreEqual(CosmeticCatalog.Themes.HimalayanSunrise, shop.Equipped(CosmeticKind.Theme), "Equip theme equips it");
            Assert.AreEqual(CosmeticCatalog.Themes.HimalayanSunrise, shop.Equipped(CosmeticKind.Background), "its background");
            Assert.AreEqual(CosmeticCatalog.Themes.HimalayanSunrise, shop.Equipped(CosmeticKind.Table), "and its cloth");
            Assert.AreEqual(ThemeAction.Equipped, View.Preview.Action);
            Assert.IsTrue(Theme(Sunrise).RadioFilled && Theme(Sunrise).ShowsOutline, "its tile says Equipped");
            Assert.AreEqual(Text("ui.shop.status.owned"), Theme(0).StatusText.text, "Durbar Square is owned, not on");
            EquippedBackground shown = View.GetComponentInChildren<EquippedBackground>(true);
            Assert.AreEqual(new TableBackdropLibraryCheck(View).Background(CosmeticCatalog.Themes.HimalayanSunrise), shown.Shown, "the shop itself shows the new background");
        }

        [UnityTest]
        public IEnumerator NotEnoughCoins_OffersEarnCoinsOrNotNow_AndTheShopWaitsForTheReturn()
        {
            yield return OpenShop();
            var shop = _game.Services.Resolve<ICosmeticsShop>();

            PressTheme(Tihar);
            Press("BuyButton");
            Assert.IsTrue(_game.Dialogs.IsOpen);
            Assert.IsTrue(DialogShows(Text("ui.shop.not_enough.title")), "its own state, not the purchase question");
            _game.PressDialog(confirm: false);
            yield return null;
            Assert.IsFalse(_game.Dialogs.IsOpen, "Not now closes it");
            Assert.AreEqual(ScreenId.Shop, Router.Current);
            Assert.IsFalse(shop.IsOwned(CosmeticKind.Theme, CosmeticCatalog.Themes.TiharLights));

            Press("BuyButton");
            _game.PressDialog(confirm: true);
            yield return null;
            Assert.AreEqual(ScreenId.EarnCoins, Router.Current, "Earn Coins opens");

            Assert.IsTrue(Router.Back());
            yield return null;
            Assert.AreEqual(ScreenId.Shop, Router.Current);
            Assert.IsTrue(View.Preview.IsOpen, "the shop is as it was left");
            Assert.AreEqual(Text("ui.shop.theme.tihar_lights"), View.Preview.Title);
        }

        [UnityTest]
        public IEnumerator TheWalletsPlus_OpensEarnCoins()
        {
            yield return OpenShop();

            Press("EarnCoinsButton");
            yield return null;

            Assert.AreEqual(ScreenId.EarnCoins, Router.Current);
        }

        [UnityTest]
        public IEnumerator ABackgroundBoughtBeforeTheThemes_ShowsItsThemeOwned()
        {
            yield return OpenShop(saves =>
            {
                var data = new CosmeticsData { schemaVersion = 2 };
                data.boughtBackgroundIds.Add(CosmeticCatalog.Backgrounds.PokharaLakeside);
                saves.Save(CosmeticsService.SaveKey, data);
            });

            Assert.IsTrue(Theme(Pokhara).ShowsStatus && !Theme(Pokhara).ShowsPrice, "Pokhara Lakeside is theirs");
            Assert.AreEqual(Text("ui.shop.status.owned"), Theme(Pokhara).StatusText.text);
            PressTheme(Pokhara);
            Assert.AreEqual(ThemeAction.Equip, View.Preview.Action, "its preview equips it, nothing to pay");
        }

        [UnityTest]
        public IEnumerator BuyingADeck_AfterTheConfirmation_OwnsIt_ThenUseEquipsIt()
        {
            yield return OpenShop(saves => new Wallet(saves).Add(250));
            var cosmetics = _game.Services.Resolve<ICosmeticsService>();
            var shop = _game.Services.Resolve<ICosmeticsShop>();
            var wallet = _game.Services.Resolve<IWallet>();
            Assert.AreEqual(750, wallet.Coins);

            Press("CategoryButton1");
            Assert.AreEqual(Cards, View.Category);
            Assert.AreEqual(3, View.Decks.Count, "the three decks");
            Assert.IsTrue(Deck(0).IsChosen && Deck(0).RadioFilled, "mockup 15: Classic Crimson chosen, filled radio");
            Assert.AreEqual("500", Deck(1).PriceLabel.text);
            Press("Tile2");
            Assert.IsTrue(View.ShowsBuy && View.PrimaryInteractable, "Buy for 750");
            Assert.AreEqual("750", View.BuyPrice);
            Press("PrimaryButton");
            Assert.IsTrue(DialogShows(Format("ui.shop.buy.title", Text("ui.deck.kathmandu_gold"))), "it names the deck");
            _game.PressDialog(confirm: true);
            yield return null;

            Assert.IsTrue(shop.IsOwned(CosmeticKind.Deck, CosmeticCatalog.Decks.KathmanduGold), "bought");
            Assert.AreEqual(0, wallet.Coins, "for 750 coins");
            Assert.AreEqual(CosmeticCatalog.Decks.ClassicCrimson, cosmetics.SelectedDeckId, "buying does not equip");
            Assert.IsFalse(Deck(2).ShowsPrice);
            Assert.IsFalse(View.ShowsBuy, "the button turns to Use Selected Style");

            Press("PrimaryButton");
            yield return null;
            Assert.AreEqual(CosmeticCatalog.Decks.KathmanduGold, cosmetics.SelectedDeckId, "Use equips it");
            Assert.AreEqual(Text("ui.shop.status.equipped"), Deck(2).StatusLabel.text);
        }

        [UnityTest]
        public IEnumerator UseSelectedStyle_EquipsTheChosenDeck()
        {
            yield return OpenShop(saves =>
            {
                var data = new CosmeticsData();
                data.boughtDeckIds.Add(CosmeticCatalog.Decks.HimalayanIvory);
                saves.Save(CosmeticsService.SaveKey, data);
            });
            var cosmetics = _game.Services.Resolve<ICosmeticsService>();

            Press("CategoryButton1");
            Press("Tile1");
            Assert.IsFalse(Deck(1).ShowsStatus, "chosen, not in use yet: no words");
            Assert.IsTrue(View.PrimaryInteractable, "Use Selected Style");
            Press("PrimaryButton");
            yield return null;

            Assert.AreEqual(CosmeticCatalog.Decks.HimalayanIvory, cosmetics.SelectedDeckId, "the deck for the next match");
            Assert.AreEqual(Text("ui.shop.status.equipped"), Deck(1).StatusLabel.text);
            Assert.IsFalse(Deck(0).ShowsStatus, "\"Equipped\" moved to the deck in use");
            Assert.IsFalse(View.PrimaryInteractable);
        }

        [UnityTest]
        public IEnumerator TappingTheDrawerButtons_LeavesEachAtItsOwnSize()
        {
            yield return OpenShop();
            yield return UntilSettled();
            var buttons = new Button[ShopView.CategoryCount];
            var sizes = new Vector3[ShopView.CategoryCount];
            for (int c = 0; c < ShopView.CategoryCount; c++)
            {
                buttons[c] = Find("CategoryButton" + c).GetComponent<Button>();
                sizes[c] = buttons[c].transform.localScale;
                Assert.Less(sizes[c].x, 1f, $"button {c} is built smaller than full size");
            }

            // Tapped one after another, twice round, the way a player tries them (the owner, 5 Oct 2026).
            for (int round = 0; round < 2; round++)
            {
                for (int c = 0; c < ShopView.CategoryCount; c++)
                {
                    yield return Tap(buttons[c].gameObject);
                    Assert.AreEqual(c, View.Category);
                }
            }

            yield return new WaitForSecondsRealtime(0.4f);
            for (int c = 0; c < ShopView.CategoryCount; c++)
            {
                Assert.AreEqual(sizes[c].x, buttons[c].transform.localScale.x, 0.001f, $"button {c} is back at its own size, not full size");
                Assert.AreEqual(sizes[c].y, buttons[c].transform.localScale.y, 0.001f);
            }

            Assert.AreEqual(Cards, View.Drawer.Active, "only the last one tapped is the crimson one");
        }

        [UnityTest]
        public IEnumerator OpeningTheShopAgain_StartsAfresh()
        {
            yield return OpenShop();
            PressTheme(Tihar);
            GameFixture.Press(Router, "BackButton");
            yield return null;

            GameFixture.Press(Router, "ShopButton");
            yield return GameFixture.Until(() => Router.Current == ScreenId.Shop, "the shop to open again");
            Assert.AreEqual(Themes, View.Category, "Themes again");
            Assert.IsFalse(View.Preview.IsOpen, "no preview open");
        }

        private ScreenRouter Router => _game.MenuRouter;

        private ShopView View => Router.Get<ShopView>(ScreenId.Shop);

        private IEnumerator OpenShop(System.Action<ISaveStore> seed = null)
        {
            yield return _game.Boot(seed: seed);
            GameFixture.Press(Router, "ShopButton");
            yield return GameFixture.Until(() => Router.Current == ScreenId.Shop && !_game.Curtain.IsCovering, "the shop to open");
        }

        /// <summary>Waits for the shop to finish sliding in, so that everything lies where it rests.</summary>
        private IEnumerator UntilSettled()
        {
            yield return GameFixture.Until(
                () => Mathf.Approximately(View.SlideTarget.anchoredPosition.x, 0f) && Mathf.Approximately(View.SlideTarget.localScale.x, 1f),
                "the shop to finish sliding in");
        }

        private void Press(string button) => GameFixture.PressIn(View, button);

        private void PressTheme(int index) => Theme(index).GetComponent<Button>().onClick.Invoke();

        /// <summary>A finger on the button, lifted a moment later: the press effect runs as on a phone.</summary>
        private static IEnumerator Tap(GameObject button)
        {
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button, data, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(0.05f);
            ExecuteEvents.Execute(button, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button, data, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSecondsRealtime(0.05f);
        }

        private ThemeTile Theme(int index)
        {
            ThemeTile tile = View.Shelf.Tile(index);
            Assert.IsNotNull(tile, $"the row has a tile {index}");
            return tile;
        }

        private ShopItemTile Deck(int index)
        {
            ShopItemTile tile = View.Decks.Tile(index);
            Assert.IsNotNull(tile, $"the decks have a tile {index}");
            return tile;
        }

        /// <summary>Anything of that name in the shop, shown or not.</summary>
        private bool Has(string node) => FindOrNull(node) != null;

        private RectTransform Find(string node)
        {
            RectTransform found = FindOrNull(node);
            Assert.IsNotNull(found, "the shop has a " + node);
            return found;
        }

        private RectTransform FindOrNull(string node)
        {
            foreach (RectTransform child in View.GetComponentsInChildren<RectTransform>(true))
            {
                if (child.name == node)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>The area a drawer button takes taps in: its disc and its label.</summary>
        private Rect ButtonRect(string name)
        {
            foreach (Button button in View.Drawer.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name)
                {
                    return ScreenRect(button.targetGraphic.rectTransform);
                }
            }

            Assert.Fail("no drawer button " + name);
            return default;
        }

        /// <summary>Where a rect is on the screen (the menu canvas draws over the screen).</summary>
        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private string Text(string key) => _game.Services.Resolve<ILocalization>().Get(key);

        private string Format(string key, params object[] args) => _game.Services.Resolve<ILocalization>().Format(key, args);

        private bool DialogShows(string text)
        {
            DialogView dialog = _game.Project.GetComponentInChildren<DialogView>(true);
            foreach (TMP_Text label in dialog.GetComponentsInChildren<TMP_Text>(false))
            {
                if (label.text == text)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ToastShows(string text)
        {
            foreach (ToastView toast in _game.Project.GetComponentsInChildren<ToastView>(true))
            {
                foreach (TMP_Text label in toast.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.text == text)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The pictures the shop's library gives a theme's background, cloth and border.</summary>
        private sealed class TableBackdropLibraryCheck
        {
            private readonly ShopView _view;

            public TableBackdropLibraryCheck(ShopView view) => _view = view;

            public Sprite Background(string id) => _view.Library.BackgroundPicture(id, out _);

            public Sprite Cloth(string id) => _view.Library.TablePicture(id, out _);

            public Sprite Frame(string id) => _view.Library.TableFrame(id);
        }
    }
}
