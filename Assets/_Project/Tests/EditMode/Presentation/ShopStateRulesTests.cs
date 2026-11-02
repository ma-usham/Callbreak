using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Offers;
using Callbreak.Presentation.Screens;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// The shop page (designer's spec shop-drawer-and-tables-prompts.md; the themes, ADR 064): it opens
    /// on Themes; a theme's tile says Equipped with a filled radio, Owned with an empty one, or shows its
    /// price, red when the wallet can't pay it; tapping a theme opens its preview, whose button buys it,
    /// equips it, or says it is equipped; on Cards a deck's tile
    /// says Equipped or shows its price, and the chosen deck is the one the primary button acts on, Use
    /// for an owned deck (not while it is equipped) and Buy for one that is not; Cards keeps its choice
    /// while the player switches, and a shop that opens afresh shows Themes with no preview open and
    /// the chosen deck back on the equipped one.
    /// </summary>
    public sealed class ShopStateTests
    {
        private FakeShop _shop;
        private FakeWallet _wallet;
        private ShopState _state;

        [SetUp]
        public void SetUp()
        {
            _shop = new FakeShop();
            _shop.Add(CosmeticKind.Deck, "classic-crimson", 0);
            _shop.Add(CosmeticKind.Deck, "himalayan-ivory", 500);
            _shop.Add(CosmeticKind.Deck, "kathmandu-gold", 750);
            _shop.Add(CosmeticKind.Theme, "durbar-square", 0);
            _shop.Add(CosmeticKind.Theme, "himalayan-sunrise", 500);
            _shop.Add(CosmeticKind.Theme, "tihar-lights", 1000);
            _shop.Add(CosmeticKind.Theme, "pokhara-lakeside", 500);
            _shop.EquippedIds[CosmeticKind.Deck] = "classic-crimson";
            _shop.EquippedIds[CosmeticKind.Theme] = "durbar-square";
            _wallet = new FakeWallet { Coins = 600 };
            _state = new ShopState(_shop, _wallet);
        }

        [Test]
        public void TheShopOpensOnThemes_WithNoPreview_AndTheChosenDeckOnTheEquippedOne()
        {
            Assert.AreEqual(CosmeticKind.Theme, _state.Category);
            Assert.AreEqual(ShopState.NoPreview, _state.Previewing);
            Assert.IsNull(_state.PreviewTheme);
            Assert.AreEqual(0, _state.ChosenIndex(CosmeticKind.Deck));
            CollectionAssert.AreEqual(new[] { CosmeticKind.Theme, CosmeticKind.Deck }, ShopState.Kinds, "the drawer: Themes, then Cards");
        }

        [Test]
        public void ThemeTiles_SayEquippedOwnedOrTheirPrice()
        {
            _shop.OwnedIds.Add((CosmeticKind.Theme, "pokhara-lakeside"));

            // In the shop's order, the player's own first (ADR 074): Durbar Square, Pokhara Lakeside, Himalayan Sunrise, Tihar Lights.
            AssertStatuses(CosmeticKind.Theme, ShopTileStatus.Equipped, ShopTileStatus.Owned, ShopTileStatus.Price, ShopTileStatus.Price);
        }

        [Test]
        public void AThemesPrice_IsAffordable_OnlyWhenTheWalletHoldsIt()
        {
            Assert.IsTrue(_state.CanAffordTheme(1), "600 coins buy the 500 theme");
            Assert.IsTrue(_state.CanAffordTheme(2), "nor the other 500 one");
            Assert.IsFalse(_state.CanAffordTheme(3), "but not the 1,000 one: its price shows red");
            Assert.IsFalse(_state.CanAffordTheme(4));
            Assert.IsFalse(_state.CanAffordTheme(-1));
        }

        [Test]
        public void TappingATheme_OpensItsPreview_WhoseButtonBuysEquipsOrSaysEquipped()
        {
            Assert.IsTrue(_state.OpenPreview(3));
            Assert.AreEqual("tihar-lights", _state.PreviewTheme.Id);
            Assert.AreEqual(ThemeAction.Buy, _state.PreviewAction);

            _shop.OwnedIds.Add((CosmeticKind.Theme, "tihar-lights"));
            Assert.AreEqual(ThemeAction.Equip, _state.PreviewAction, "bought: Equip theme");

            _shop.Equip(CosmeticKind.Theme, "tihar-lights");
            Assert.AreEqual(ThemeAction.Equipped, _state.PreviewAction);

            _state.OpenPreview(0);
            Assert.AreEqual(ThemeAction.Equip, _state.PreviewAction, "Durbar Square, free and owned, can be put back on");
        }

        [Test]
        public void AThemeThatIsNotThere_CannotOpen()
        {
            Assert.IsFalse(_state.OpenPreview(4));
            Assert.IsFalse(_state.OpenPreview(-1));
            Assert.AreEqual(ShopState.NoPreview, _state.Previewing);
        }

        [Test]
        public void TheThemes_TheOneBoughtLastFirst_ThenFromTheCheapestToTheDearest()
        {
            CollectionAssert.AreEqual(
                new[] { "durbar-square", "himalayan-sunrise", "pokhara-lakeside", "tihar-lights" },
                _state.Themes.Select(t => t.Id),
                "nothing bought: by price, and in catalogue order where prices tie");

            _shop.LastBoughtIds[CosmeticKind.Theme] = "tihar-lights";
            CollectionAssert.AreEqual(
                new[] { "tihar-lights", "durbar-square", "himalayan-sunrise", "pokhara-lakeside" },
                _state.Themes.Select(t => t.Id));

            _shop.LastBoughtIds[CosmeticKind.Theme] = "pokhara-lakeside";
            CollectionAssert.AreEqual(
                new[] { "pokhara-lakeside", "durbar-square", "himalayan-sunrise", "tihar-lights" },
                _state.Themes.Select(t => t.Id),
                "only the last one bought comes first");

            _shop.LastBoughtIds[CosmeticKind.Theme] = "a-theme-gone-from-the-shop";
            Assert.AreEqual("durbar-square", _state.Themes[0].Id);
        }

        [Test]
        public void TheThemes_ThePlayersFirst_ThenAFestivalOffersTheme_ThenTheRestByPrice()
        {
            // The owner, 2 Nov 2026: "the free one should come after owned themes".
            var offers = new FakeOffers { ThemeId = "tihar-lights" };
            _state = new ShopState(_shop, _wallet, offers);
            _shop.OwnedIds.Add((CosmeticKind.Theme, "pokhara-lakeside"));
            CollectionAssert.AreEqual(
                new[] { "durbar-square", "pokhara-lakeside", "tihar-lights", "himalayan-sunrise" },
                _state.Themes.Select(t => t.Id),
                "owned, by price; the offer's theme; the rest, by price");

            _shop.LastBoughtIds[CosmeticKind.Theme] = "pokhara-lakeside";
            CollectionAssert.AreEqual(
                new[] { "pokhara-lakeside", "durbar-square", "tihar-lights", "himalayan-sunrise" },
                _state.Themes.Select(t => t.Id),
                "the one bought last before the other owned ones");
        }

        [Test]
        public void ThePreview_StaysOnItsTheme_WhenBuyingItMovesItFirst()
        {
            _state.OpenPreview(3);
            Assert.AreEqual("tihar-lights", _state.PreviewTheme.Id);

            _shop.OwnedIds.Add((CosmeticKind.Theme, "tihar-lights"));
            _shop.LastBoughtIds[CosmeticKind.Theme] = "tihar-lights";

            Assert.AreEqual(0, _state.Previewing);
            Assert.AreEqual("tihar-lights", _state.PreviewTheme.Id);
            Assert.AreEqual(ThemeAction.Equip, _state.PreviewAction);
        }

        [Test]
        public void ThePreview_Closes_WhenItsThemeLeavesTheShop()
        {
            _state.OpenPreview(1);
            _shop.Remove(CosmeticKind.Theme, "himalayan-sunrise");

            Assert.AreEqual(ShopState.NoPreview, _state.Previewing);
            Assert.IsNull(_state.PreviewTheme);
        }

        [Test]
        public void AFestivalTheme_SaysFestival_AndItsPreviewClaimsIt()
        {
            var offers = new FakeOffers { ThemeId = "tihar-lights" };
            _state = new ShopState(_shop, _wallet, offers);

            Assert.AreEqual(1, _state.FestivalIndex, "the shop opens scrolled to it, right after the player's own");
            Assert.IsTrue(_state.IsFestival(1));
            Assert.IsFalse(_state.IsFestival(2));
            Assert.IsFalse(_state.IsFestival(4));
            Assert.AreEqual(ShopTileStatus.Offer, _state.StatusOf(CosmeticKind.Theme, 1));
            Assert.AreEqual(ShopTileStatus.Price, _state.StatusOf(CosmeticKind.Theme, 2));

            _state.OpenPreview(1);
            Assert.AreEqual(ThemeAction.Claim, _state.PreviewAction, "Watch ad, free");

            // Claimed: owned, the offer gone, the button Equip theme in the same place.
            offers.ThemeId = null;
            _shop.OwnedIds.Add((CosmeticKind.Theme, "tihar-lights"));
            _shop.LastBoughtIds[CosmeticKind.Theme] = "tihar-lights";
            Assert.AreEqual(ThemeAction.Equip, _state.PreviewAction);
            Assert.AreEqual(-1, _state.FestivalIndex);
            Assert.AreEqual(ShopTileStatus.Owned, _state.StatusOf(CosmeticKind.Theme, 0));
        }

        [Test]
        public void OpenOffer_OpensTheFestivalThemesPreview_OrNothing()
        {
            var offers = new FakeOffers();
            _state = new ShopState(_shop, _wallet, offers);
            Assert.IsFalse(_state.OpenOffer(), "no offer");
            Assert.AreEqual(ShopState.NoPreview, _state.Previewing);

            offers.ThemeId = "tihar-lights";
            _state.Show(CosmeticKind.Deck);
            Assert.IsTrue(_state.OpenOffer());

            Assert.AreEqual(CosmeticKind.Theme, _state.Category);
            Assert.AreEqual("tihar-lights", _state.PreviewTheme.Id);
            Assert.AreEqual(ThemeAction.Claim, _state.PreviewAction);
        }

        [Test]
        public void WithoutOffers_NoThemeIsAFestivalOne()
        {
            Assert.AreEqual(-1, _state.FestivalIndex);
            Assert.IsFalse(_state.IsFestival(3));
            Assert.IsFalse(_state.OpenOffer());
        }

        [Test]
        public void ClosingThePreview_OrShowingCards_ClosesIt()
        {
            _state.OpenPreview(1);
            _state.ClosePreview();
            Assert.AreEqual(ShopState.NoPreview, _state.Previewing);

            _state.OpenPreview(1);
            _state.Show(CosmeticKind.Deck);
            Assert.AreEqual(ShopState.NoPreview, _state.Previewing);
            Assert.AreEqual(CosmeticKind.Deck, _state.Category);
        }

        [TestCase(CosmeticKind.Background)]
        [TestCase(CosmeticKind.Table)]
        public void APartIsNotATab(CosmeticKind kind)
        {
            _state.Show(kind);

            Assert.AreEqual(CosmeticKind.Theme, _state.Category);
        }

        [Test]
        public void TheEquippedDeckIsEquipped_TheOthersShowTheirPrices()
        {
            AssertStatuses(CosmeticKind.Deck, ShopTileStatus.Equipped, ShopTileStatus.Price, ShopTileStatus.Price);
            Assert.AreEqual(ShopAction.Use, _state.ActionFor(CosmeticKind.Deck));
            Assert.IsFalse(_state.CanUse(CosmeticKind.Deck), "Use Selected Style is off while the chosen deck is the equipped one");
        }

        [Test]
        public void ChoosingADeckThatIsNotOwned_TheButtonBuys()
        {
            _state.Show(CosmeticKind.Deck);
            Assert.IsTrue(_state.Choose(CosmeticKind.Deck, 1));

            Assert.AreEqual(ShopAction.Buy, _state.ActionFor(CosmeticKind.Deck));
            Assert.AreEqual(500, _state.Chosen(CosmeticKind.Deck).Price);
            Assert.IsFalse(_state.CanUse(CosmeticKind.Deck));
            Assert.IsTrue(_state.CanAfford(CosmeticKind.Deck));
        }

        [Test]
        public void ABoughtDeck_SaysNothingUntilItIsUsed_AndTheButtonUsesIt()
        {
            _state.Choose(CosmeticKind.Deck, 1);
            _shop.OwnedIds.Add((CosmeticKind.Deck, "himalayan-ivory"));

            AssertStatuses(CosmeticKind.Deck, ShopTileStatus.Equipped, ShopTileStatus.None, ShopTileStatus.Price);
            Assert.AreEqual(ShopAction.Use, _state.ActionFor(CosmeticKind.Deck));
            Assert.IsTrue(_state.CanUse(CosmeticKind.Deck), "Use Selected Style");
        }

        [Test]
        public void NotEnoughCoins_ForTheChosenDeck()
        {
            _state.Choose(CosmeticKind.Deck, 2);

            Assert.IsFalse(_state.CanAfford(CosmeticKind.Deck), "600 coins don't buy the 750 deck");
            _state.Choose(CosmeticKind.Deck, 1);
            Assert.IsTrue(_state.CanAfford(CosmeticKind.Deck));
        }

        [Test]
        public void CardsKeepsItsChoice_WhileThePlayerSwitches()
        {
            _state.Show(CosmeticKind.Deck);
            _state.Choose(CosmeticKind.Deck, 2);
            _state.Show(CosmeticKind.Theme);
            _state.OpenPreview(1);

            _state.Show(CosmeticKind.Deck);
            Assert.AreEqual(2, _state.ChosenIndex(CosmeticKind.Deck));
        }

        [Test]
        public void OpeningAfresh_PutsThemesAndTheEquippedDeckBack()
        {
            _state.Show(CosmeticKind.Deck);
            _state.Choose(CosmeticKind.Deck, 1);
            _state.OpenPreview(2);

            _state.Reset();

            Assert.AreEqual(CosmeticKind.Theme, _state.Category);
            Assert.AreEqual(ShopState.NoPreview, _state.Previewing);
            Assert.AreEqual(0, _state.ChosenIndex(CosmeticKind.Deck));
        }

        [Test]
        public void ADeckThatIsNotThere_CannotBeChosen()
        {
            Assert.IsFalse(_state.Choose(CosmeticKind.Deck, 3));
            Assert.IsFalse(_state.Choose(CosmeticKind.Deck, -1));
            Assert.IsFalse(_state.Choose(CosmeticKind.Theme, 1), "a theme opens its preview instead");
            Assert.AreEqual(0, _state.ChosenIndex(CosmeticKind.Deck));
        }

        [Test]
        public void AChosenDeckThatLeavesTheCatalogue_FallsBackToTheEquippedOne()
        {
            _state.Choose(CosmeticKind.Deck, 2);
            _shop.Remove(CosmeticKind.Deck, "kathmandu-gold");

            Assert.AreEqual(0, _state.ChosenIndex(CosmeticKind.Deck));
        }

        [TestCase(false, false, ShopTileStatus.Price)]
        [TestCase(true, true, ShopTileStatus.Equipped)]
        [TestCase(true, false, ShopTileStatus.None)]
        public void TheDeckStatusRules(bool owned, bool equipped, ShopTileStatus expected)
        {
            Assert.AreEqual(expected, ShopDisplay.Status(owned, equipped));
        }

        [TestCase(false, false, ShopTileStatus.Price)]
        [TestCase(true, true, ShopTileStatus.Equipped)]
        [TestCase(true, false, ShopTileStatus.Owned)]
        public void TheThemeStatusRules(bool owned, bool equipped, ShopTileStatus expected)
        {
            Assert.AreEqual(expected, ShopDisplay.ThemeStatus(owned, equipped));
        }

        [TestCase(false, false, ThemeAction.Buy)]
        [TestCase(true, false, ThemeAction.Equip)]
        [TestCase(true, true, ThemeAction.Equipped)]
        public void ThePreviewsButtonRules(bool owned, bool equipped, ThemeAction expected)
        {
            Assert.AreEqual(expected, ShopDisplay.ThemeActionFor(owned, equipped));
        }

        [Test]
        public void OnlyEquipped_FillsTheRadio()
        {
            Assert.IsTrue(ShopDisplay.RadioFilled(ShopTileStatus.Equipped));
            Assert.IsFalse(ShopDisplay.RadioFilled(ShopTileStatus.Owned));
            Assert.IsFalse(ShopDisplay.RadioFilled(ShopTileStatus.None));
            Assert.IsFalse(ShopDisplay.RadioFilled(ShopTileStatus.Price));
        }

        [Test]
        public void StatusWords_AndPrices()
        {
            Assert.AreEqual("ui.shop.status.equipped", ShopDisplay.StatusKey(ShopTileStatus.Equipped));
            Assert.AreEqual("ui.shop.status.owned", ShopDisplay.StatusKey(ShopTileStatus.Owned));
            Assert.IsNull(ShopDisplay.StatusKey(ShopTileStatus.None), "an owned deck not in use says nothing (the owner, 5 Oct 2026)");
            Assert.IsNull(ShopDisplay.StatusKey(ShopTileStatus.Price), "a price shows a coin and a number");
            Assert.AreEqual("750", ShopDisplay.Price(750));
            Assert.AreEqual("1,000", ShopDisplay.Price(1000), "grouped as the wallet groups coins");
        }

        private void AssertStatuses(CosmeticKind kind, params ShopTileStatus[] expected)
        {
            CollectionAssert.AreEqual(expected, Enumerable.Range(0, expected.Length).Select(i => _state.StatusOf(kind, i)).ToArray(), kind.ToString());
        }

        private sealed class FakeShop : ICosmeticsShop
        {
            private readonly Dictionary<CosmeticKind, List<CosmeticItem>> _items = new Dictionary<CosmeticKind, List<CosmeticItem>>();

            public event Action Changed
            {
                add { }
                remove { }
            }

            public Dictionary<CosmeticKind, string> EquippedIds { get; } = new Dictionary<CosmeticKind, string>();

            public HashSet<(CosmeticKind, string)> OwnedIds { get; } = new HashSet<(CosmeticKind, string)>();

            public void Add(CosmeticKind kind, string id, int price)
            {
                if (!_items.TryGetValue(kind, out List<CosmeticItem> list))
                {
                    _items[kind] = list = new List<CosmeticItem>();
                }

                list.Add(new CosmeticItem(kind, id, "ui.shop.test." + id.Replace('-', '_'), price));
                if (price == 0)
                {
                    OwnedIds.Add((kind, id));
                }
            }

            public void Remove(CosmeticKind kind, string id) => _items[kind].RemoveAll(item => item.Id == id);

            public IReadOnlyList<CosmeticItem> Catalog(CosmeticKind kind) => _items.TryGetValue(kind, out List<CosmeticItem> list) ? list : new List<CosmeticItem>();

            public bool IsOwned(CosmeticKind kind, string id) => OwnedIds.Contains((kind, id));

            public string Equipped(CosmeticKind kind) => EquippedIds.TryGetValue(kind, out string id) ? id : null;

            public void Equip(CosmeticKind kind, string id) => EquippedIds[kind] = id;

            public PurchaseResult Buy(CosmeticKind kind, string id) => throw new NotSupportedException("the page's state never buys");

            public PurchaseResult BuyAt(CosmeticKind kind, string id, int price) => throw new NotSupportedException("the page's state never buys");

            public Dictionary<CosmeticKind, string> LastBoughtIds { get; } = new Dictionary<CosmeticKind, string>();

            public string LastBought(CosmeticKind kind) => LastBoughtIds.TryGetValue(kind, out string id) ? id : null;
        }

        private sealed class FakeOffers : IFestivalOffers
        {
            public event Action Changed
            {
                add { }
                remove { }
            }

            /// <summary>The theme of the claimable offer; null for none.</summary>
            public string ThemeId { get; set; }

            public FestivalOffer Claimable => ThemeId == null
                ? null
                : new FestivalOffer("tihar-2026", ThemeId, FestivalOfferKind.WatchAd, 0, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, "ui.offer.tihar");

            public bool IsOffered(string themeId) => themeId != null && themeId == ThemeId;

            public bool TakeAutoOpen() => throw new NotSupportedException("the page's state never opens it");

            public void Refresh() => throw new NotSupportedException("the page's state never refreshes it");

            public Task<FestivalClaimResult> ClaimAsync(CancellationToken ct) => throw new NotSupportedException("the page's state never claims");
        }

        private sealed class FakeWallet : IWallet
        {
            public event Action Changed
            {
                add { }
                remove { }
            }

            public int Coins { get; set; }

            public bool TrySpend(int amount) => throw new NotSupportedException("the page's state never spends");

            public void Add(int amount) => throw new NotSupportedException();
        }
    }
}
