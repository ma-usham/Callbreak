using System;
using System.Collections.Generic;
using System.Linq;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Presentation.Screens;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// The shop page (mockups 15, 20 and 21; designer's spec shop-drawer-and-tables-prompts.md):
    /// each tile says Equipped or Selected with a filled radio, Owned with an empty one, or shows
    /// its price; the chosen tile is the one the primary button acts on, Use for an owned item
    /// (not while it is equipped) and Buy for one that is not; every category keeps its choice
    /// while the player switches, and a shop that opens afresh shows Cards with every choice back
    /// on the equipped item.
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
            _shop.Add(CosmeticKind.Deck, "himalayan-ivory", 0);
            _shop.Add(CosmeticKind.Deck, "kathmandu-gold", 0);
            foreach (string id in new[] { "himalayan-vista", "pokhara-lakeside", "monastery-garden" })
            {
                _shop.Add(CosmeticKind.Background, id, 0);
            }

            _shop.Add(CosmeticKind.Table, "himalayan-ivory", 0);
            _shop.Add(CosmeticKind.Table, "royal-crimson", 350);
            _shop.Add(CosmeticKind.Table, "heritage-walnut", 250);
            _shop.EquippedIds[CosmeticKind.Deck] = "classic-crimson";
            _shop.EquippedIds[CosmeticKind.Background] = "himalayan-vista";
            _shop.EquippedIds[CosmeticKind.Table] = "himalayan-ivory";
            _wallet = new FakeWallet { Coins = 500 };
            _state = new ShopState(_shop, _wallet);
        }

        [Test]
        public void TheShopOpensOnCards_WithEveryChoiceOnItsEquippedItem()
        {
            Assert.AreEqual(CosmeticKind.Deck, _state.Category);
            Assert.AreEqual(0, _state.ChosenIndex(CosmeticKind.Deck));
            Assert.AreEqual(0, _state.ChosenIndex(CosmeticKind.Background), "Himalayan Vista, the first");
            Assert.AreEqual(0, _state.ChosenIndex(CosmeticKind.Table));
        }

        [Test]
        public void Mockup15_TheEquippedDeckIsEquipped_TheOthersOwned()
        {
            AssertStatuses(CosmeticKind.Deck, ShopTileStatus.Equipped, ShopTileStatus.Owned, ShopTileStatus.Owned);
            Assert.AreEqual(ShopAction.Use, _state.ActionFor(CosmeticKind.Deck));
            Assert.IsFalse(_state.CanUse(CosmeticKind.Deck), "Use Selected Style is off while the chosen deck is the equipped one");
        }

        [Test]
        public void Mockup20_ChoosingAnOwnedBackground_SaysSelected_AndTheEquippedOneStaysEquipped()
        {
            AssertStatuses(CosmeticKind.Background, ShopTileStatus.Equipped, ShopTileStatus.Owned, ShopTileStatus.Owned);

            Assert.IsTrue(_state.Choose(CosmeticKind.Background, 1));

            AssertStatuses(CosmeticKind.Background, ShopTileStatus.Equipped, ShopTileStatus.Selected, ShopTileStatus.Owned);
            Assert.AreEqual(ShopAction.Use, _state.ActionFor(CosmeticKind.Background));
            Assert.IsTrue(_state.CanUse(CosmeticKind.Background), "Use Selected Background equips it");
        }

        [Test]
        public void Mockup21_ChoosingATableThatIsNotOwned_ShowsPrices_AndTheButtonBuys()
        {
            _state.Show(CosmeticKind.Table);
            Assert.IsTrue(_state.Choose(CosmeticKind.Table, 1));

            AssertStatuses(CosmeticKind.Table, ShopTileStatus.Equipped, ShopTileStatus.Price, ShopTileStatus.Price);
            Assert.AreEqual(ShopAction.Buy, _state.ActionFor(CosmeticKind.Table));
            Assert.AreEqual(350, _state.Chosen(CosmeticKind.Table).Price);
            Assert.IsFalse(_state.CanUse(CosmeticKind.Table));
            Assert.IsTrue(_state.CanAfford(CosmeticKind.Table), "500 coins buy a 350 table");
        }

        [Test]
        public void ABoughtTable_IsSelectedNotEquipped_AndTheButtonUsesIt()
        {
            _state.Choose(CosmeticKind.Table, 1);
            _shop.OwnedIds.Add((CosmeticKind.Table, "royal-crimson"));

            AssertStatuses(CosmeticKind.Table, ShopTileStatus.Equipped, ShopTileStatus.Selected, ShopTileStatus.Price);
            Assert.AreEqual(ShopAction.Use, _state.ActionFor(CosmeticKind.Table));
            Assert.IsTrue(_state.CanUse(CosmeticKind.Table), "Use Selected Table");
        }

        [Test]
        public void ChoosingTheEquippedItemAgain_SaysEquipped_AndUseIsOff()
        {
            _state.Choose(CosmeticKind.Background, 2);
            _state.Choose(CosmeticKind.Background, 0);

            Assert.AreEqual(ShopTileStatus.Equipped, _state.StatusOf(CosmeticKind.Background, 0));
            Assert.AreEqual(ShopTileStatus.Owned, _state.StatusOf(CosmeticKind.Background, 2));
            Assert.IsFalse(_state.CanUse(CosmeticKind.Background));
        }

        [Test]
        public void NotEnoughCoins_ForTheChosenItem()
        {
            _wallet.Coins = 349;
            _state.Choose(CosmeticKind.Table, 1);

            Assert.IsFalse(_state.CanAfford(CosmeticKind.Table));
            _state.Choose(CosmeticKind.Table, 2);
            Assert.IsTrue(_state.CanAfford(CosmeticKind.Table), "349 coins still buy the 250 table");
        }

        [Test]
        public void EveryCategoryKeepsItsChoice_WhileThePlayerSwitches()
        {
            _state.Choose(CosmeticKind.Deck, 2);
            _state.Show(CosmeticKind.Background);
            _state.Choose(CosmeticKind.Background, 1);
            _state.Show(CosmeticKind.Table);
            _state.Choose(CosmeticKind.Table, 2);

            _state.Show(CosmeticKind.Deck);
            Assert.AreEqual(CosmeticKind.Deck, _state.Category);
            Assert.AreEqual(2, _state.ChosenIndex(CosmeticKind.Deck));
            Assert.AreEqual(1, _state.ChosenIndex(CosmeticKind.Background));
            Assert.AreEqual(2, _state.ChosenIndex(CosmeticKind.Table));
        }

        [Test]
        public void OpeningAfresh_PutsCardsAndTheEquippedChoicesBack()
        {
            _state.Show(CosmeticKind.Table);
            _state.Choose(CosmeticKind.Table, 2);
            _state.Choose(CosmeticKind.Deck, 1);

            _state.Reset();

            Assert.AreEqual(CosmeticKind.Deck, _state.Category);
            Assert.AreEqual(0, _state.ChosenIndex(CosmeticKind.Deck));
            Assert.AreEqual(0, _state.ChosenIndex(CosmeticKind.Table));
        }

        [Test]
        public void AnItemThatIsNotThere_CannotBeChosen()
        {
            Assert.IsFalse(_state.Choose(CosmeticKind.Deck, 3));
            Assert.IsFalse(_state.Choose(CosmeticKind.Deck, -1));
            Assert.AreEqual(0, _state.ChosenIndex(CosmeticKind.Deck));
        }

        [Test]
        public void AChosenItemThatLeavesTheCatalogue_FallsBackToTheEquippedOne()
        {
            _state.Choose(CosmeticKind.Table, 2);
            _shop.Remove(CosmeticKind.Table, "heritage-walnut");

            Assert.AreEqual(0, _state.ChosenIndex(CosmeticKind.Table));
        }

        [TestCase(false, false, false, ShopTileStatus.Price)]
        [TestCase(false, false, true, ShopTileStatus.Price)]
        [TestCase(true, true, true, ShopTileStatus.Equipped)]
        [TestCase(true, true, false, ShopTileStatus.Equipped)]
        [TestCase(true, false, true, ShopTileStatus.Selected)]
        [TestCase(true, false, false, ShopTileStatus.Owned)]
        public void TheStatusRules(bool owned, bool equipped, bool selected, ShopTileStatus expected)
        {
            Assert.AreEqual(expected, ShopDisplay.Status(owned, equipped, selected));
        }

        [Test]
        public void OnlyEquippedAndSelected_FillTheRadio()
        {
            Assert.IsTrue(ShopDisplay.RadioFilled(ShopTileStatus.Equipped));
            Assert.IsTrue(ShopDisplay.RadioFilled(ShopTileStatus.Selected));
            Assert.IsFalse(ShopDisplay.RadioFilled(ShopTileStatus.Owned));
            Assert.IsFalse(ShopDisplay.RadioFilled(ShopTileStatus.Price));
        }

        [Test]
        public void StatusWords_AndPrices()
        {
            Assert.AreEqual("ui.shop.status.equipped", ShopDisplay.StatusKey(ShopTileStatus.Equipped));
            Assert.AreEqual("ui.shop.status.selected", ShopDisplay.StatusKey(ShopTileStatus.Selected));
            Assert.AreEqual("ui.shop.status.owned", ShopDisplay.StatusKey(ShopTileStatus.Owned));
            Assert.IsNull(ShopDisplay.StatusKey(ShopTileStatus.Price), "a price shows a coin and a number");
            Assert.AreEqual("350", ShopDisplay.Price(350));
            Assert.AreEqual("1250", ShopDisplay.Price(1250));
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
