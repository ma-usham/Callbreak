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
    /// The shop in the real Boot and Menu scenes, with the real wallet and cosmetics (the owner, 4,
    /// 5 and 6 Oct 2026; mockups 15, 20 and 21): the main menu's storefront opens it on Cards; the
    /// drawer is always open, with its three category buttons stacked in its middle and no
    /// storefront disc of its own; every category shows exactly three tiles side by side and
    /// nothing scrolls; in each the second item costs 500 coins and the third 750; the drawer
    /// switches categories and each keeps its choice; the backgrounds are the Himalayan Vista,
    /// Pokhara Lakeside and Monastery Garden, and one that left the shop falls back to the Vista; a
    /// background and a deck that were bought are put to use; a table and a deck are bought after
    /// a confirmation and then put to use; not enough coins offers the Earn Coins page or Not now;
    /// and Back leaves. Saves go to a temporary folder.
    /// </summary>
    public sealed class ShopFlowTests
    {
        private const int Cards = 0;
        private const int Backgrounds = 1;
        private const int Tables = 2;
        private const int TilesShown = 3;

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
        public IEnumerator TheMainMenusStorefront_OpensTheShopOnCards_AndBackLeaves()
        {
            yield return OpenShop();

            Assert.AreEqual(Cards, View.Category);
            Assert.AreEqual(Cards, View.Drawer.Active, "Cards is the crimson button");
            Assert.AreEqual(3, View.Tiles(Cards).Count, "the three decks");
            ShopItemTile classic = Tile(Cards, 0);
            Assert.IsTrue(classic.IsChosen && classic.RadioFilled, "mockup 15: Classic Crimson chosen, filled radio");
            Assert.AreEqual(Text("ui.shop.status.equipped"), classic.StatusLabel.text);
            Assert.IsFalse(Tile(Cards, 1).ShowsStatus, "a deck for sale shows its price, no words");
            Assert.IsTrue(Tile(Cards, 1).ShowsPrice && Tile(Cards, 2).ShowsPrice, "the owner, 6 Oct 2026: the second and third decks are for sale");
            Assert.AreEqual("500", Tile(Cards, 1).PriceLabel.text);
            Assert.AreEqual("750", Tile(Cards, 2).PriceLabel.text);
            Assert.IsFalse(Tile(Cards, 1).RadioFilled);
            Assert.IsFalse(View.ShowsBuy);
            Assert.IsFalse(View.PrimaryInteractable, "Use Selected Style is off while the chosen deck is equipped");

            GameFixture.Press(Router, "BackButton");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, Router.Current, "Back leaves the shop");
        }

        [UnityTest]
        public IEnumerator TheDrawer_IsAlwaysOpen_WithItsThreeButtonsInItsMiddle_AndNoStorefrontDisc()
        {
            yield return OpenShop();
            yield return UntilSettled();

            Assert.IsFalse(Has("ShopButton") || Has("ShopIcon"), "no storefront disc in the shop's drawer");
            Assert.IsFalse(Has("DrawerLineLeft") || Has("DrawerLineRight") || Has("DrawerDiamond"), "and no divider under it");

            Rect screen = ScreenRect((RectTransform)View.transform);
            Rect drawer = ScreenRect((RectTransform)View.Drawer.transform);
            Assert.AreEqual(screen.xMax, drawer.xMax, 1f, "flush with the right edge");
            for (int shown = 0; shown < ShopView.CategoryCount; shown++)
            {
                Press("CategoryButton" + shown);
                Assert.AreEqual(shown, View.Category);
                Assert.AreEqual(shown, View.Drawer.Active);
                Rect previous = default;
                for (int c = 0; c < ShopView.CategoryCount; c++)
                {
                    Assert.IsTrue(GameFixture.CanPress(View, "CategoryButton" + c), $"button {c} can be tapped while category {shown} shows");
                    Rect button = ButtonRect(c);
                    Assert.IsTrue(button.xMin >= drawer.xMin && button.xMax <= drawer.xMax, $"button {c} lies inside the drawer: {button} in {drawer}");
                    Assert.IsTrue(button.yMin >= screen.yMin && button.yMax <= screen.yMax, $"button {c} lies on the screen: {button} in {screen}");
                    if (c > 0)
                    {
                        Assert.LessOrEqual(button.yMax, previous.yMin, $"button {c} lies below button {c - 1}");
                    }

                    previous = button;
                }

                float middle = (ButtonRect(0).yMax + ButtonRect(ShopView.CategoryCount - 1).yMin) / 2f;
                Assert.AreEqual(drawer.center.y, middle, drawer.height * 0.01f, "the three are centred in the drawer");
            }
        }

        [UnityTest]
        public IEnumerator EveryCategory_ShowsExactlyThreeTiles_SideBySideInThePanel_AndNothingScrolls()
        {
            yield return OpenShop();
            yield return UntilSettled();

            Assert.IsFalse(Has("PrevButton") || Has("NextButton"), "no chevrons");
            Assert.IsFalse(Has("Track") || Has("Thumb") || Has("SwipeHint"), "no scroll track and no \"Swipe to browse\"");
            Assert.IsEmpty(View.GetComponentsInChildren<IBeginDragHandler>(true), "nothing to drag");
            Assert.IsEmpty(View.GetComponentsInChildren<IScrollHandler>(true), "nothing to scroll");

            Rect panel = ScreenRect(Find("Panel"));
            for (int c = 0; c < ShopView.CategoryCount; c++)
            {
                Press("CategoryButton" + c);
                Assert.AreEqual(TilesShown, View.Tiles(c).Count, $"category {c} shows three");
                Rect previous = default;
                for (int i = 0; i < TilesShown; i++)
                {
                    ShopItemTile tile = Tile(c, i);
                    Assert.IsTrue(tile.isActiveAndEnabled, $"category {c}, tile {i} shows");
                    Rect rect = ScreenRect((RectTransform)tile.transform);
                    Assert.IsTrue(panel.Contains(rect.min) && panel.Contains(rect.max), $"category {c}, tile {i} lies in the panel: {rect} in {panel}");
                    if (i > 0)
                    {
                        Assert.Greater(rect.xMin, previous.xMax, $"category {c}, tile {i} stands right of tile {i - 1}");
                        Assert.AreEqual(previous.center.y, rect.center.y, 0.5f, "in one row");
                    }

                    previous = rect;
                }

                Assert.IsNull(View.Tiles(c).Tile(TilesShown), "and no fourth");
            }
        }

        [UnityTest]
        public IEnumerator TheBackgrounds_AreTheHimalayanVistaPokharaLakesideAndMonasteryGarden_WithTheirPictures()
        {
            yield return OpenShop();
            Press("CategoryButton1");

            string[] names = { "ui.shop.background.himalayan_vista", "ui.shop.background.pokhara_lakeside", "ui.shop.background.monastery_garden" };
            string[] pictures = { "Thumb_HimalayanVista", "Thumb_PokharaLakeside", "Thumb_MonasteryGarden" };
            for (int i = 0; i < TilesShown; i++)
            {
                Assert.AreEqual(Text(names[i]), Tile(Backgrounds, i).NameLabel.text);
                Assert.IsNotNull(Tile(Backgrounds, i).Face.sprite, names[i] + " has a picture");
                Assert.AreEqual(pictures[i], Tile(Backgrounds, i).Face.sprite.name);
            }

            Assert.IsTrue(Tile(Backgrounds, 0).IsChosen, "the shop opens with each choice on its equipped item");
            Assert.AreEqual(Text("ui.shop.status.equipped"), Tile(Backgrounds, 0).StatusLabel.text, "the Himalayan Vista is the default");
            string[] prices = { null, "500", "750" };
            for (int i = 1; i < TilesShown; i++)
            {
                Assert.IsFalse(Tile(Backgrounds, i).ShowsStatus, names[i] + ": no words");
                Assert.IsTrue(Tile(Backgrounds, i).ShowsPrice, names[i] + " is for sale (the owner, 6 Oct 2026)");
                Assert.AreEqual(prices[i], Tile(Backgrounds, i).PriceLabel.text);
            }
        }

        [UnityTest]
        public IEnumerator ABackgroundThatLeftTheShop_ShowsTheHimalayanVistaEquipped()
        {
            yield return OpenShop(saves => saves.Save(CosmeticsService.SaveKey, new CosmeticsData { equippedBackgroundId = "vintage-home" }));
            Assert.AreEqual(CosmeticCatalog.Backgrounds.HimalayanVista, _game.Services.Resolve<ICosmeticsShop>().Equipped(CosmeticKind.Background));

            Press("CategoryButton1");
            Assert.AreEqual(3, View.Tiles(Backgrounds).Count);
            Assert.IsTrue(Tile(Backgrounds, 0).IsChosen);
            Assert.AreEqual(Text("ui.shop.status.equipped"), Tile(Backgrounds, 0).StatusLabel.text);
            Assert.IsFalse(View.PrimaryInteractable, "Use Selected Background is off while the chosen one is equipped");
        }

        [UnityTest]
        public IEnumerator SwitchingCategories_KeepsEachOnesChoice()
        {
            yield return OpenShop();

            Press("Tile2");
            Assert.IsTrue(Tile(Cards, 2).IsChosen);

            Press("CategoryButton1");
            Assert.AreEqual(Backgrounds, View.Category);
            Assert.AreEqual(Backgrounds, View.Drawer.Active);
            Press("Tile1");
            Assert.IsTrue(Tile(Backgrounds, 1).IsChosen);
            Assert.IsFalse(Tile(Backgrounds, 1).ShowsStatus, "choosing only moves the outline: no \"Selected\" (the owner, 5 Oct 2026)");
            Assert.AreEqual(Text("ui.shop.status.equipped"), Tile(Backgrounds, 0).StatusLabel.text, "the one in use stays Equipped");
            Assert.IsTrue(Tile(Backgrounds, 0).ShowsStatus && Tile(Backgrounds, 0).RadioFilled);

            Press("CategoryButton2");
            Assert.AreEqual(Tables, View.Category);
            Press("CategoryButton0");
            Assert.AreEqual(Cards, View.Category);
            Assert.IsTrue(Tile(Cards, 2).IsChosen, "Cards kept its choice");
            Assert.IsFalse(Tile(Cards, 0).IsChosen);

            Press("CategoryButton1");
            Assert.IsTrue(Tile(Backgrounds, 1).IsChosen, "Backgrounds kept its choice");
            Assert.IsFalse(Tile(Backgrounds, 0).IsChosen);
        }

        [UnityTest]
        public IEnumerator UseSelectedBackground_EquipsTheChosenBackground()
        {
            yield return OpenShop(saves => saves.Save(CosmeticsService.SaveKey, Bought(CosmeticKind.Background, CosmeticCatalog.Backgrounds.MonasteryGarden)));
            var shop = _game.Services.Resolve<ICosmeticsShop>();

            Press("CategoryButton1");
            Press("Tile2");
            Assert.IsFalse(Tile(Backgrounds, 2).ShowsStatus, "chosen, not in use yet: no words");
            Assert.IsTrue(View.PrimaryInteractable, "Use Selected Background");
            Press("PrimaryButton");
            yield return null;

            Assert.AreEqual(CosmeticCatalog.Backgrounds.MonasteryGarden, shop.Equipped(CosmeticKind.Background), "the scenery for the next match");
            Assert.AreEqual(Text("ui.shop.status.equipped"), Tile(Backgrounds, 2).StatusLabel.text, "Equipped once it is used");
            Assert.IsTrue(Tile(Backgrounds, 2).ShowsStatus);
            Assert.IsFalse(Tile(Backgrounds, 0).ShowsStatus, "and no longer on the one before");
            Assert.IsFalse(View.PrimaryInteractable, "off while it is equipped");
        }

        [UnityTest]
        public IEnumerator BuyingATable_AfterTheConfirmation_OwnsIt_ThenUseEquipsIt()
        {
            yield return OpenShop();
            var shop = _game.Services.Resolve<ICosmeticsShop>();
            var wallet = _game.Services.Resolve<IWallet>();
            Assert.AreEqual(WalletRules.StartingCoins, wallet.Coins);

            Press("CategoryButton2");
            Assert.IsTrue(Tile(Tables, 1).ShowsPrice && Tile(Tables, 2).ShowsPrice, "mockup 21: the two tables for sale show their prices");
            Assert.AreEqual("500", Tile(Tables, 1).PriceLabel.text, "the owner, 6 Oct 2026");
            Assert.AreEqual("750", Tile(Tables, 2).PriceLabel.text);
            Press("Tile1");
            Assert.IsTrue(View.ShowsBuy && View.PrimaryInteractable, "Buy for 500");
            Assert.AreEqual("500", View.BuyPrice);

            // Cancelled first: nothing changes.
            Press("PrimaryButton");
            Assert.IsTrue(_game.Dialogs.IsOpen, "a confirmation");
            Assert.IsTrue(DialogShows(Format("ui.shop.buy.title", Text("ui.shop.table.royal_crimson"))), "it names the item");
            Assert.IsTrue(DialogShows(Format("ui.shop.buy.body", "500", "500")), "and its price");
            _game.PressDialog(confirm: false);
            yield return null;
            Assert.IsFalse(shop.IsOwned(CosmeticKind.Table, CosmeticCatalog.Tables.IndigoDhaka));
            Assert.AreEqual(500, wallet.Coins);

            Press("PrimaryButton");
            _game.PressDialog(confirm: true);
            yield return null;
            Assert.IsTrue(shop.IsOwned(CosmeticKind.Table, CosmeticCatalog.Tables.IndigoDhaka), "bought");
            Assert.AreEqual(0, wallet.Coins, "for 500 coins, the whole starting balance");
            Assert.AreEqual(CosmeticCatalog.Tables.GreenDhaka, shop.Equipped(CosmeticKind.Table), "buying does not equip");
            Assert.IsFalse(Tile(Tables, 1).ShowsPrice);
            Assert.IsFalse(Tile(Tables, 1).ShowsStatus, "bought and chosen: no words until it is used");
            Assert.IsFalse(View.ShowsBuy, "the button turns to Use Selected Table");
            Assert.IsTrue(View.PrimaryInteractable);

            Press("PrimaryButton");
            yield return null;
            Assert.AreEqual(CosmeticCatalog.Tables.IndigoDhaka, shop.Equipped(CosmeticKind.Table), "Use equips it");
            Assert.AreEqual(Text("ui.shop.status.equipped"), Tile(Tables, 1).StatusLabel.text);
            Assert.IsFalse(Tile(Tables, 0).ShowsStatus, "the table before shows nothing now");
            Assert.IsFalse(View.PrimaryInteractable, "off while it is equipped");
        }

        [UnityTest]
        public IEnumerator NotEnoughCoins_OffersEarnCoinsOrNotNow_AndTheShopWaitsForTheReturn()
        {
            yield return OpenShop(saves => new Wallet(saves).TrySpend(400));
            var shop = _game.Services.Resolve<ICosmeticsShop>();
            Assert.AreEqual(100, _game.Services.Resolve<IWallet>().Coins);

            Press("CategoryButton2");
            Press("Tile1");
            Press("PrimaryButton");
            Assert.IsTrue(_game.Dialogs.IsOpen);
            Assert.IsTrue(DialogShows(Text("ui.shop.not_enough.title")), "its own state, not the purchase question");
            _game.PressDialog(confirm: false);
            yield return null;
            Assert.IsFalse(_game.Dialogs.IsOpen, "Not now closes it");
            Assert.AreEqual(ScreenId.Shop, Router.Current);
            Assert.IsFalse(shop.IsOwned(CosmeticKind.Table, CosmeticCatalog.Tables.IndigoDhaka));

            Press("PrimaryButton");
            _game.PressDialog(confirm: true);
            yield return null;
            Assert.AreEqual(ScreenId.EarnCoins, Router.Current, "Earn Coins opens");

            Assert.IsTrue(Router.Back());
            yield return null;
            Assert.AreEqual(ScreenId.Shop, Router.Current);
            Assert.AreEqual(Tables, View.Category, "the shop is as it was left");
            Assert.IsTrue(Tile(Tables, 1).IsChosen, "Royal Crimson still chosen");
        }

        [UnityTest]
        public IEnumerator BuyingADeck_AfterTheConfirmation_OwnsIt_ThenUseEquipsIt()
        {
            yield return OpenShop(saves => new Wallet(saves).Add(250));
            var cosmetics = _game.Services.Resolve<ICosmeticsService>();
            var shop = _game.Services.Resolve<ICosmeticsShop>();
            var wallet = _game.Services.Resolve<IWallet>();
            Assert.AreEqual(750, wallet.Coins);

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
            Assert.IsFalse(Tile(Cards, 2).ShowsPrice);
            Assert.IsFalse(View.ShowsBuy, "the button turns to Use Selected Style");

            Press("PrimaryButton");
            yield return null;
            Assert.AreEqual(CosmeticCatalog.Decks.KathmanduGold, cosmetics.SelectedDeckId, "Use equips it");
            Assert.AreEqual(Text("ui.shop.status.equipped"), Tile(Cards, 2).StatusLabel.text);
        }

        [UnityTest]
        public IEnumerator UseSelectedStyle_EquipsTheChosenDeck()
        {
            yield return OpenShop(saves => saves.Save(CosmeticsService.SaveKey, Bought(CosmeticKind.Deck, CosmeticCatalog.Decks.HimalayanIvory)));
            var cosmetics = _game.Services.Resolve<ICosmeticsService>();

            Press("Tile1");
            Assert.IsFalse(Tile(Cards, 1).ShowsStatus, "chosen, not in use yet: no words");
            Assert.IsTrue(View.PrimaryInteractable, "Use Selected Style");
            Press("PrimaryButton");
            yield return null;

            Assert.AreEqual(CosmeticCatalog.Decks.HimalayanIvory, cosmetics.SelectedDeckId, "the deck for the next match");
            Assert.AreEqual(Text("ui.shop.status.equipped"), Tile(Cards, 1).StatusLabel.text);
            Assert.IsFalse(Tile(Cards, 0).ShowsStatus, "\"Equipped\" moved to the deck in use");
            Assert.IsFalse(Tile(Cards, 0).RadioFilled);
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

            Assert.AreEqual(Tables, View.Drawer.Active, "only the last one tapped is the crimson one");
        }

        [UnityTest]
        public IEnumerator OpeningTheShopAgain_StartsAfresh()
        {
            yield return OpenShop();
            Press("CategoryButton1");
            Press("Tile2");
            Assert.IsTrue(Tile(Backgrounds, 2).IsChosen);
            GameFixture.Press(Router, "BackButton");
            yield return null;

            GameFixture.Press(Router, "ShopButton");
            yield return GameFixture.Until(() => Router.Current == ScreenId.Shop, "the shop to open again");
            Assert.AreEqual(Cards, View.Category, "Cards again");
            Assert.IsTrue(Tile(Backgrounds, 0).IsChosen, "every choice on its equipped item");
            Assert.IsFalse(Tile(Backgrounds, 2).IsChosen);
        }

        /// <summary>A cosmetics file at the defaults with one item bought.</summary>
        private static CosmeticsData Bought(CosmeticKind kind, string id)
        {
            var data = new CosmeticsData();
            switch (kind)
            {
                case CosmeticKind.Deck:
                    data.boughtDeckIds.Add(id);
                    break;
                case CosmeticKind.Background:
                    data.boughtBackgroundIds.Add(id);
                    break;
                default:
                    data.boughtTableIds.Add(id);
                    break;
            }

            return data;
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

        private ShopItemTile Tile(int category, int index)
        {
            ShopItemTile tile = View.Tiles(category).Tile(index);
            Assert.IsNotNull(tile, $"category {category} has a tile {index}");
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

        /// <summary>The area a category button takes taps in: its disc and its label.</summary>
        private Rect ButtonRect(int category)
        {
            foreach (Button button in View.Drawer.GetComponentsInChildren<Button>(false))
            {
                if (button.name == "CategoryButton" + category)
                {
                    return ScreenRect(button.targetGraphic.rectTransform);
                }
            }

            Assert.Fail("no category button " + category);
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
    }
}
