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
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The shop in the real Boot and Menu scenes, with the real wallet and cosmetics (the owner,
    /// 4 Oct 2026; mockups 15, 20 and 21; designer's spec shop-drawer-and-tables-prompts.md): the
    /// main menu's storefront opens it on Cards with the drawer open, the storefront disc closes and
    /// opens the drawer, the drawer switches categories and each keeps its choice and its row's
    /// position, the row browses by chevrons and by drag and stops at its ends, browsing hides when
    /// everything fits, a table is bought after a confirmation and then put to use, not enough coins
    /// offers the Earn Coins page or Not now, a deck is equipped, and Back leaves. Saves go to a
    /// temporary folder.
    /// </summary>
    public sealed class ShopFlowTests
    {
        private const int Cards = 0;
        private const int Backgrounds = 1;
        private const int Tables = 2;
        private const float MotionTimeout = 3f;

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
        public IEnumerator TheMainMenusStorefront_OpensTheShopOnCards_WithTheDrawerOpen_AndBackLeaves()
        {
            yield return OpenShop();

            Assert.AreEqual(Cards, View.Category);
            Assert.IsTrue(View.IsDrawerOpen, "the drawer is open when the shop opens");
            Assert.AreEqual(0f, View.Drawer.Closedness, 0.001f);
            Assert.AreEqual(Cards, View.Drawer.Active, "Cards is the crimson button");
            Assert.AreEqual(3, View.Carousel(Cards).Count, "the three decks");
            ShopItemTile classic = Tile(Cards, 0);
            Assert.IsTrue(classic.IsChosen && classic.RadioFilled, "mockup 15: Classic Crimson chosen, filled radio");
            Assert.AreEqual(Text("ui.shop.status.equipped"), classic.StatusLabel.text);
            Assert.AreEqual(Text("ui.shop.status.owned"), Tile(Cards, 1).StatusLabel.text);
            Assert.IsFalse(Tile(Cards, 1).RadioFilled);
            Assert.IsFalse(View.ShowsBuy);
            Assert.IsFalse(View.PrimaryInteractable, "Use Selected Style is off while the chosen deck is equipped");

            GameFixture.Press(Router, "BackButton");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, Router.Current, "Back leaves the shop");
        }

        [UnityTest]
        public IEnumerator TheStorefrontDisc_ClosesAndOpensTheDrawer_AtOnceWithReduceMotion()
        {
            yield return OpenShop();

            GameFixture.Press(Router, "ShopButton");
            Assert.IsFalse(View.IsDrawerOpen);
            Assert.IsTrue(View.Drawer.IsMoving, "it slides out");
            yield return null;
            Assert.IsFalse(GameFixture.CanPress(View, "CategoryButton1"), "a closing drawer takes no taps");
            yield return GameFixture.Until(() => !View.Drawer.IsMoving, "the drawer to close", MotionTimeout);
            Assert.AreEqual(1f, View.Drawer.Closedness, 0.001f, "closed: off the right edge");
            Assert.IsTrue(GameFixture.CanPress(View, "ShopButton"), "the storefront disc stays to open it again");

            GameFixture.Press(Router, "ShopButton");
            Assert.IsTrue(View.IsDrawerOpen);
            yield return GameFixture.Until(() => !View.Drawer.IsMoving, "the drawer to open", MotionTimeout);
            Assert.AreEqual(0f, View.Drawer.Closedness, 0.001f);
            yield return null;
            Assert.IsTrue(GameFixture.CanPress(View, "CategoryButton1"));

            _game.Settings.SetReduceMotion(true);
            GameFixture.Press(Router, "ShopButton");
            Assert.IsFalse(View.Drawer.IsMoving, "Reduce motion: no slide");
            Assert.AreEqual(1f, View.Drawer.Closedness, 0.001f, "closed at once");
            GameFixture.Press(Router, "ShopButton");
            Assert.AreEqual(0f, View.Drawer.Closedness, 0.001f, "open at once");
        }

        [UnityTest]
        public IEnumerator SwitchingCategories_KeepsEachOnesChoiceAndPosition()
        {
            yield return OpenShop();

            Press("Tile2");
            Assert.IsTrue(Tile(Cards, 2).IsChosen);

            Press("CategoryButton1");
            Assert.AreEqual(Backgrounds, View.Category);
            Assert.AreEqual(Backgrounds, View.Drawer.Active);
            Assert.AreEqual(6, View.Carousel(Backgrounds).Count, "six backgrounds");
            Assert.IsTrue(Tile(Backgrounds, 3).IsChosen, "the shop opens with each choice on its equipped item: Himalayan Vista, off screen");
            Assert.AreEqual(0f, View.Carousel(Backgrounds).Offset, "the first three in view");
            Press("Tile1");
            Assert.IsTrue(Tile(Backgrounds, 1).IsChosen);
            Assert.AreEqual(Text("ui.shop.status.selected"), Tile(Backgrounds, 1).StatusLabel.text, "mockup 20: owned, chosen, not equipped");
            Assert.IsTrue(Tile(Backgrounds, 1).RadioFilled);
            Assert.AreEqual(Text("ui.shop.status.equipped"), Tile(Backgrounds, 3).StatusLabel.text);
            Press("NextButton");
            yield return GameFixture.Until(() => !View.Carousel(Backgrounds).IsMoving, "the row to glide", MotionTimeout);
            Assert.AreEqual(3f, View.Carousel(Backgrounds).Offset, 0.001f);

            Press("CategoryButton2");
            Assert.AreEqual(Tables, View.Category);
            Press("CategoryButton0");
            Assert.AreEqual(Cards, View.Category);
            Assert.IsTrue(Tile(Cards, 2).IsChosen, "Cards kept its choice");
            Assert.IsFalse(Tile(Cards, 0).IsChosen);

            Press("CategoryButton1");
            Assert.IsTrue(Tile(Backgrounds, 1).IsChosen, "Backgrounds kept its choice");
            Assert.AreEqual(3f, View.Carousel(Backgrounds).Offset, 0.001f, "and its position");
        }

        [UnityTest]
        public IEnumerator TheChevrons_BrowseAPage_AndAreOffAtTheEnds_WhileATrackFollows()
        {
            yield return OpenShop();
            Press("CategoryButton1");
            ShopCarousel row = View.Carousel(Backgrounds);

            Assert.IsTrue(row.Browsable);
            Assert.IsFalse(GameFixture.CanPress(View, "PrevButton"), "off at the start");
            Assert.IsTrue(GameFixture.CanPress(View, "NextButton"));
            Assert.AreEqual(0f, row.ThumbPosition, 0.001f);

            Press("NextButton");
            yield return GameFixture.Until(() => !row.IsMoving, "the row to glide", MotionTimeout);
            Assert.AreEqual(3f, row.Offset, 0.001f, "a page: the last three");
            Assert.AreEqual(1f, row.ThumbPosition, 0.001f, "the track's thumb at its end");
            Assert.IsFalse(GameFixture.CanPress(View, "NextButton"), "off at the end");
            Assert.IsTrue(GameFixture.CanPress(View, "PrevButton"));

            _game.Settings.SetReduceMotion(true);
            Press("PrevButton");
            Assert.IsFalse(row.IsMoving, "Reduce motion: no glide");
            Assert.AreEqual(0f, row.Offset, 0.001f);
        }

        [UnityTest]
        public IEnumerator ADrag_BrowsesAndSettlesOnATile_ResistsPastTheStart_AndAQuickOneGoesOn()
        {
            yield return OpenShop();
            Press("CategoryButton1");
            ShopCarousel row = View.Carousel(Backgrounds);

            // A slow drag of a tile and a third: it follows the finger, then settles on the nearest tile.
            yield return Drag(row, 1.3f, pauseBeforeRelease: true);
            yield return GameFixture.Until(() => !row.IsMoving, "the row to settle", MotionTimeout);
            Assert.AreEqual(1f, row.Offset, 0.001f, "the nearest tile");

            // Back past the start: the row gives a little, and comes back.
            yield return Drag(row, -1.8f, pauseBeforeRelease: true, observe: offset => Assert.Greater(offset, -1.8f + 1f, "it resists past the start"));
            yield return GameFixture.Until(() => !row.IsMoving, "the row to settle", MotionTimeout);
            Assert.AreEqual(0f, row.Offset, 0.001f, "back at the start");

            // A quick release past half a tile: on to the next tile at least (a flick may carry it further).
            yield return Drag(row, 0.6f, pauseBeforeRelease: false);
            yield return GameFixture.Until(() => !row.IsMoving, "the row to settle", MotionTimeout);
            Assert.GreaterOrEqual(row.Offset, 1f - 0.001f, "it goes on");
            Assert.AreEqual(Mathf.Round(row.Offset), row.Offset, 0.001f, "and rests on a whole tile");
        }

        [UnityTest]
        public IEnumerator WhenEverythingFits_BrowsingIsHidden()
        {
            yield return OpenShop();

            Assert.IsFalse(View.Carousel(Cards).Browsable, "three decks fit");
            Assert.IsFalse(GameFixture.CanPress(View, "PrevButton"));
            Assert.IsFalse(GameFixture.CanPress(View, "NextButton"));
            Assert.IsFalse(Shows("Track") || Shows("SwipeHint"), "no track and no hint");

            Press("CategoryButton1");
            Assert.IsTrue(Shows("Track") && Shows("SwipeHint"), "six backgrounds: the track and the hint");
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
            Assert.AreEqual("350", Tile(Tables, 1).PriceLabel.text);
            Assert.AreEqual("250", Tile(Tables, 2).PriceLabel.text);
            Press("Tile1");
            Assert.IsTrue(View.ShowsBuy && View.PrimaryInteractable, "Buy for 350");
            Assert.AreEqual("350", View.BuyPrice);

            // Cancelled first: nothing changes.
            Press("PrimaryButton");
            Assert.IsTrue(_game.Dialogs.IsOpen, "a confirmation");
            Assert.IsTrue(DialogShows(Format("ui.shop.buy.title", Text("ui.shop.table.royal_crimson"))), "it names the item");
            Assert.IsTrue(DialogShows(Format("ui.shop.buy.body", "350", "500")), "and its price");
            _game.PressDialog(confirm: false);
            yield return null;
            Assert.IsFalse(shop.IsOwned(CosmeticKind.Table, CosmeticCatalog.Tables.RoyalCrimson));
            Assert.AreEqual(500, wallet.Coins);

            Press("PrimaryButton");
            _game.PressDialog(confirm: true);
            yield return null;
            Assert.IsTrue(shop.IsOwned(CosmeticKind.Table, CosmeticCatalog.Tables.RoyalCrimson), "bought");
            Assert.AreEqual(150, wallet.Coins, "for 350 coins");
            Assert.AreEqual(CosmeticCatalog.Tables.HimalayanIvory, shop.Equipped(CosmeticKind.Table), "buying does not equip");
            Assert.IsFalse(Tile(Tables, 1).ShowsPrice);
            Assert.AreEqual(Text("ui.shop.status.selected"), Tile(Tables, 1).StatusLabel.text);
            Assert.IsFalse(View.ShowsBuy, "the button turns to Use Selected Table");
            Assert.IsTrue(View.PrimaryInteractable);

            Press("PrimaryButton");
            yield return null;
            Assert.AreEqual(CosmeticCatalog.Tables.RoyalCrimson, shop.Equipped(CosmeticKind.Table), "Use equips it");
            Assert.AreEqual(Text("ui.shop.status.equipped"), Tile(Tables, 1).StatusLabel.text);
            Assert.AreEqual(Text("ui.shop.status.owned"), Tile(Tables, 0).StatusLabel.text);
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
            Assert.IsFalse(shop.IsOwned(CosmeticKind.Table, CosmeticCatalog.Tables.RoyalCrimson));

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
        public IEnumerator UseSelectedStyle_EquipsTheChosenDeck()
        {
            yield return OpenShop();
            var cosmetics = _game.Services.Resolve<ICosmeticsService>();

            Press("Tile1");
            Assert.AreEqual(Text("ui.shop.status.selected"), Tile(Cards, 1).StatusLabel.text);
            Assert.IsTrue(View.PrimaryInteractable, "Use Selected Style");
            Press("PrimaryButton");
            yield return null;

            Assert.AreEqual(CosmeticCatalog.Decks.HimalayanIvory, cosmetics.SelectedDeckId, "the deck for the next match");
            Assert.AreEqual(Text("ui.shop.status.equipped"), Tile(Cards, 1).StatusLabel.text);
            Assert.AreEqual(Text("ui.shop.status.owned"), Tile(Cards, 0).StatusLabel.text);
            Assert.IsFalse(Tile(Cards, 0).RadioFilled);
            Assert.IsFalse(View.PrimaryInteractable);
        }

        [UnityTest]
        public IEnumerator OpeningTheShopAgain_StartsAfresh()
        {
            yield return OpenShop();
            Press("CategoryButton1");
            Press("Tile2");
            Press("NextButton");
            yield return GameFixture.Until(() => !View.Carousel(Backgrounds).IsMoving, "the row to glide", MotionTimeout);
            GameFixture.Press(Router, "ShopButton");
            GameFixture.Press(Router, "BackButton");
            yield return null;

            GameFixture.Press(Router, "ShopButton");
            yield return GameFixture.Until(() => Router.Current == ScreenId.Shop, "the shop to open again");
            Assert.AreEqual(Cards, View.Category, "Cards again");
            Assert.IsTrue(View.IsDrawerOpen, "with the drawer open");
            Assert.AreEqual(0f, View.Carousel(Backgrounds).Offset, "every row at its start");
            Assert.IsTrue(Tile(Backgrounds, 3).IsChosen, "every choice on its equipped item");
        }

        private ScreenRouter Router => _game.MenuRouter;

        private ShopView View => Router.Get<ShopView>(ScreenId.Shop);

        private IEnumerator OpenShop(System.Action<ISaveStore> seed = null)
        {
            yield return _game.Boot(seed: seed);
            GameFixture.Press(Router, "ShopButton");
            yield return GameFixture.Until(() => Router.Current == ScreenId.Shop && !_game.Curtain.IsCovering, "the shop to open");
        }

        private void Press(string button) => GameFixture.PressIn(View, button);

        private ShopItemTile Tile(int category, int index)
        {
            ShopItemTile tile = View.Carousel(category).Tile(index);
            Assert.IsNotNull(tile, $"category {category} has a tile {index}");
            return tile;
        }

        private bool Shows(string node)
        {
            foreach (Transform child in View.Carousel(View.Category).GetComponentsInChildren<Transform>(false))
            {
                if (child.name == node)
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// A finger on the row's window, moving it by <paramref name="tiles"/> (positive: on to later
        /// tiles, so the finger moves left) in ten frames, then lifting, after a rest when asked.
        /// </summary>
        private static IEnumerator Drag(ShopCarousel row, float tiles, bool pauseBeforeRelease, System.Action<float> observe = null)
        {
            RectTransform window = row.Viewport;
            Canvas canvas = window.GetComponentInParent<Canvas>().rootCanvas;
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, window.TransformPoint(window.rect.center));
            float distance = tiles * row.Stride * canvas.scaleFactor;
            var data = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = start,
                pressPosition = start,
            };
            ExecuteEvents.Execute(row.gameObject, data, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(row.gameObject, data, ExecuteEvents.beginDragHandler);
            const int Steps = 10;
            for (int i = 1; i <= Steps; i++)
            {
                data.position = start - new Vector2(distance * i / Steps, 0f);
                ExecuteEvents.Execute(row.gameObject, data, ExecuteEvents.dragHandler);
                observe?.Invoke(row.Offset);
                yield return null;
            }

            if (pauseBeforeRelease)
            {
                yield return new WaitForSecondsRealtime(0.25f);
            }

            ExecuteEvents.Execute(row.gameObject, data, ExecuteEvents.endDragHandler);
        }
    }
}
