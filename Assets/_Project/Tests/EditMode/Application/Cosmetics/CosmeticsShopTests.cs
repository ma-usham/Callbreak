using System;
using System.Collections.Generic;
using System.Linq;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Tests.EditMode.Presentation;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The shop (the owner, 4, 5 and 6 Oct 2026; mockups 15, 20 and 21, the designer's
    /// shop-drawer-and-tables-prompts.md): the three catalogues of three items each, in shop order
    /// with their prices (the first free, the second 500 coins, the third 750), names and defaults;
    /// buying with coins, which never equips; equipping what is owned; the equipped deck being the
    /// selected deck; the cosmetics file, migrated from version 1; the three backgrounds that
    /// left the shop falling back to the default; the tables on disk becoming the cloths that took
    /// their places (ADR 058); and the Himalayan Vista on disk becoming Durbar Square, the free
    /// background in its place (the owner, 30 Oct 2026).
    /// </summary>
    public sealed class CosmeticsShopTests
    {
        private static readonly string[] Decks = { "classic-crimson", "himalayan-ivory", "kathmandu-gold" };
        private static readonly string[] Backgrounds = { "durbar-square", "pokhara-lakeside", "monastery-garden" };
        private static readonly string[] Tables = { "green-dhaka", "indigo-dhaka", "dusk-himalaya" };

        private InMemorySaveStore _store;
        private Wallet _wallet;
        private CosmeticsService _shop;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemorySaveStore();
            _wallet = new Wallet(_store);
            _shop = new CosmeticsService(_store, _wallet);
        }

        [Test]
        public void Catalogs_InShopOrder_WithTheirPrices()
        {
            AssertCatalog(CosmeticKind.Deck, Decks, new[] { 0, 500, 750 });
            AssertCatalog(CosmeticKind.Background, Backgrounds, new[] { 0, 500, 750 });
            AssertCatalog(CosmeticKind.Table, Tables, new[] { 0, 500, 750 });
        }

        [TestCase(CosmeticKind.Deck)]
        [TestCase(CosmeticKind.Background)]
        [TestCase(CosmeticKind.Table)]
        public void EveryKind_HasExactlyThreeItems_AsTheShopShowsThreeAndNeverScrolls(CosmeticKind kind)
        {
            Assert.AreEqual(3, _shop.Catalog(kind).Count);
        }

        [Test]
        public void NameKeys_ReuseTheDeckNames_AndFollowUiShopKindId()
        {
            CollectionAssert.AreEqual(
                new[] { "ui.deck.classic_crimson", "ui.deck.himalayan_ivory", "ui.deck.kathmandu_gold" },
                _shop.Catalog(CosmeticKind.Deck).Select(i => i.NameKey));
            CollectionAssert.AreEqual(
                new[] { "ui.shop.background.durbar_square", "ui.shop.background.pokhara_lakeside", "ui.shop.background.monastery_garden" },
                _shop.Catalog(CosmeticKind.Background).Select(i => i.NameKey));
            CollectionAssert.AreEqual(
                new[] { "ui.shop.table.green_dhaka", "ui.shop.table.indigo_dhaka", "ui.shop.table.dusk_himalaya" },
                _shop.Catalog(CosmeticKind.Table).Select(i => i.NameKey));
        }

        [Test]
        public void Names_AreTheMockupsNames_InEnglish()
        {
            var strings = new TableStrings();

            CollectionAssert.AreEqual(
                new[] { "Classic Crimson", "Himalayan Ivory", "Kathmandu Gold" },
                _shop.Catalog(CosmeticKind.Deck).Select(i => strings.Get(i.NameKey)));
            CollectionAssert.AreEqual(
                new[] { "Durbar Square", "Pokhara Lakeside", "Monastery Garden" },
                _shop.Catalog(CosmeticKind.Background).Select(i => strings.Get(i.NameKey)));
            CollectionAssert.AreEqual(
                new[] { "Green Dhaka", "Indigo Dhaka", "Dusk Himalaya" },
                _shop.Catalog(CosmeticKind.Table).Select(i => strings.Get(i.NameKey)));
        }

        [Test]
        public void ANewPlayer_OwnsTheFirstItemOfEachKind_AndNothingElse()
        {
            AssertOwned(CosmeticKind.Deck, Decks, true, false, false);
            AssertOwned(CosmeticKind.Background, Backgrounds, true, false, false);
            AssertOwned(CosmeticKind.Table, Tables, true, false, false);
        }

        [TestCase(CosmeticKind.Deck)]
        [TestCase(CosmeticKind.Background)]
        [TestCase(CosmeticKind.Table)]
        public void TheSecondAndThirdOfEveryKind_MustBeBought_BeforeTheyCanBeEquipped(CosmeticKind kind)
        {
            string second = _shop.Catalog(kind)[1].Id;
            string third = _shop.Catalog(kind)[2].Id;
            Assert.Throws<InvalidOperationException>(() => _shop.Equip(kind, second));
            Assert.Throws<InvalidOperationException>(() => _shop.Equip(kind, third));

            _wallet.Add(750);
            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(kind, second));
            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(kind, third));
            _shop.Equip(kind, third);

            Assert.AreEqual(third, _shop.Equipped(kind));
            Assert.AreEqual(0, _wallet.Coins, "500 for the second and 750 for the third");
        }

        [Test]
        public void ANewPlayer_HasTodaysLook_ClassicCrimson_DurbarSquare_GreenDhaka()
        {
            Assert.AreEqual("classic-crimson", _shop.Equipped(CosmeticKind.Deck));
            Assert.AreEqual("durbar-square", _shop.Equipped(CosmeticKind.Background));
            Assert.AreEqual("green-dhaka", _shop.Equipped(CosmeticKind.Table));
            Assert.AreEqual(0, _store.SaveCount);
        }

        [TestCase(CosmeticKind.Deck, "indigo-dhaka")]
        [TestCase(CosmeticKind.Background, "himalayan-ivory")]
        [TestCase(CosmeticKind.Table, "Indigo-Dhaka")]
        [TestCase(CosmeticKind.Table, "")]
        [TestCase(CosmeticKind.Table, null)]
        [TestCase((CosmeticKind)7, "indigo-dhaka")]
        public void IdsOfAnotherKindOrNone_AreNotOwned(CosmeticKind kind, string id)
        {
            Assert.IsFalse(_shop.IsOwned(kind, id));
        }

        [Test]
        public void TheEquippedDeck_IsTheSelectedDeck()
        {
            ICosmeticsService decks = _shop;
            ICosmeticsShop shop = _shop;
            _wallet.Add(750);
            shop.Buy(CosmeticKind.Deck, "kathmandu-gold");
            shop.Buy(CosmeticKind.Deck, "himalayan-ivory");

            shop.Equip(CosmeticKind.Deck, "kathmandu-gold");
            Assert.AreEqual("kathmandu-gold", decks.SelectedDeckId);

            decks.SelectDeck("himalayan-ivory");
            Assert.AreEqual("himalayan-ivory", shop.Equipped(CosmeticKind.Deck));
            CollectionAssert.AreEqual(Decks, decks.DeckIds);
        }

        [Test]
        public void Buy_SpendsTheCoins_AndOwnsTheItem_WithoutEquippingIt()
        {
            int changed = 0;
            _shop.Changed += () => changed++;

            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Table, "indigo-dhaka"));

            Assert.AreEqual(0, _wallet.Coins, "the 500 starting coins buy the 500 table");
            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Table, "indigo-dhaka"));
            Assert.AreEqual("green-dhaka", _shop.Equipped(CosmeticKind.Table), "buying never equips");
            Assert.AreEqual(1, changed);
            Assert.IsTrue(new CosmeticsService(_store, new Wallet(_store)).IsOwned(CosmeticKind.Table, "indigo-dhaka"), "the purchase is saved");
        }

        [Test]
        public void Buy_SomethingOwned_IsAlreadyOwned_AndCostsNothing()
        {
            int changed = 0;
            _shop.Changed += () => changed++;

            Assert.AreEqual(PurchaseResult.AlreadyOwned, _shop.Buy(CosmeticKind.Table, "green-dhaka"));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, _shop.Buy(CosmeticKind.Background, "durbar-square"));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, _shop.Buy(CosmeticKind.Deck, "classic-crimson"));
            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, changed);

            _wallet.Add(500);
            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Table, "dusk-himalaya"));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, _shop.Buy(CosmeticKind.Table, "dusk-himalaya"));
            Assert.AreEqual(250, _wallet.Coins, "paid once");
        }

        [Test]
        public void Buy_WithoutEnoughCoins_TakesNothing_AndOwnsNothing()
        {
            _wallet.Add(249);
            int changed = 0;
            _shop.Changed += () => changed++;

            Assert.AreEqual(PurchaseResult.NotEnoughCoins, _shop.Buy(CosmeticKind.Table, "dusk-himalaya"));

            Assert.AreEqual(749, _wallet.Coins);
            Assert.IsFalse(_shop.IsOwned(CosmeticKind.Table, "dusk-himalaya"));
            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SavesOf(CosmeticsService.SaveKey));

            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Table, "indigo-dhaka"));
            Assert.AreEqual(249, _wallet.Coins);
        }

        [Test]
        public void AWalletListenerThatThrows_CannotLoseAPurchase()
        {
            _wallet.Changed += () => throw new InvalidOperationException("a broken view");

            Assert.Throws<InvalidOperationException>(() => _shop.Buy(CosmeticKind.Table, "indigo-dhaka"), "the listener's exception reaches the caller");

            Assert.AreEqual(0, new Wallet(_store).Coins);
            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Table, "indigo-dhaka"), "the coins were taken, so the table is the player's");
            Assert.IsTrue(new CosmeticsService(_store, new Wallet(_store)).IsOwned(CosmeticKind.Table, "indigo-dhaka"), "and that is saved");
        }

        [Test]
        public void AWalletThatRefusesThePayment_LeavesNothingBought()
        {
            var shop = new CosmeticsService(_store, new RefusingWallet());

            Assert.AreEqual(PurchaseResult.NotEnoughCoins, shop.Buy(CosmeticKind.Table, "indigo-dhaka"));

            Assert.IsFalse(shop.IsOwned(CosmeticKind.Table, "indigo-dhaka"));
            Assert.IsFalse(new CosmeticsService(_store).IsOwned(CosmeticKind.Table, "indigo-dhaka"), "nothing bought is left in the file");
        }

        [Test]
        public void Buy_TheWholeBalance_IsEnough()
        {
            _wallet.Add(250);

            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Background, "monastery-garden"));
            Assert.AreEqual(0, _wallet.Coins);
        }

        [TestCase(CosmeticKind.Table, "dashain-gold")]
        [TestCase(CosmeticKind.Table, "Indigo-Dhaka")]
        [TestCase(CosmeticKind.Table, "")]
        [TestCase(CosmeticKind.Table, null)]
        [TestCase(CosmeticKind.Deck, "indigo-dhaka")]
        [TestCase(CosmeticKind.Background, "dusk-himalaya")]
        [TestCase((CosmeticKind)7, "indigo-dhaka")]
        public void Buy_SomethingNotInTheCatalog_IsUnknown(CosmeticKind kind, string id)
        {
            Assert.AreEqual(PurchaseResult.UnknownItem, _shop.Buy(kind, id));

            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void Equip_SomethingOwned_SavesAndRaisesChanged()
        {
            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Background, "pokhara-lakeside"));
            int saves = _store.SavesOf(CosmeticsService.SaveKey);
            int changed = 0;
            _shop.Changed += () => changed++;

            _shop.Equip(CosmeticKind.Background, "pokhara-lakeside");

            Assert.AreEqual("pokhara-lakeside", _shop.Equipped(CosmeticKind.Background));
            Assert.AreEqual(1, changed);
            Assert.AreEqual(saves + 1, _store.SavesOf(CosmeticsService.SaveKey));
        }

        [Test]
        public void Equip_WhatIsEquipped_DoesNothing()
        {
            int changed = 0;
            _shop.Changed += () => changed++;

            _shop.Equip(CosmeticKind.Table, "green-dhaka");
            _shop.Equip(CosmeticKind.Background, "durbar-square");
            _shop.Equip(CosmeticKind.Deck, "classic-crimson");

            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void Equip_SomethingNotOwned_Throws_UntilItIsBought()
        {
            Assert.Throws<InvalidOperationException>(() => _shop.Equip(CosmeticKind.Table, "indigo-dhaka"));
            Assert.AreEqual("green-dhaka", _shop.Equipped(CosmeticKind.Table));

            _shop.Buy(CosmeticKind.Table, "indigo-dhaka");
            _shop.Equip(CosmeticKind.Table, "indigo-dhaka");

            Assert.AreEqual("indigo-dhaka", _shop.Equipped(CosmeticKind.Table));
        }

        [TestCase(CosmeticKind.Table, "dashain-gold")]
        [TestCase(CosmeticKind.Table, null)]
        [TestCase(CosmeticKind.Background, "")]
        [TestCase(CosmeticKind.Background, "indigo-dhaka")]
        [TestCase(CosmeticKind.Deck, "durbar-square")]
        [TestCase(CosmeticKind.Background, "himalayan-vista")]
        public void Equip_SomethingNotInTheCatalog_Throws(CosmeticKind kind, string id)
        {
            Assert.Throws<ArgumentException>(() => _shop.Equip(kind, id));

            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void TheCatalogs_CannotBeChanged()
        {
            var tables = (IList<CosmeticItem>)_shop.Catalog(CosmeticKind.Table);

            Assert.Throws<NotSupportedException>(() => tables.Add(new CosmeticItem(CosmeticKind.Table, "free-table", "ui.shop.table.free_table", 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _shop.Catalog((CosmeticKind)7));
            Assert.Throws<ArgumentOutOfRangeException>(() => _shop.Equipped((CosmeticKind)7));
        }

        [Test]
        public void EverythingSurvivesARestart()
        {
            _wallet.Add(1600);
            _shop.Buy(CosmeticKind.Table, "indigo-dhaka");
            _shop.Buy(CosmeticKind.Background, "monastery-garden");
            _shop.Buy(CosmeticKind.Deck, "kathmandu-gold");
            _shop.Equip(CosmeticKind.Table, "indigo-dhaka");
            _shop.Equip(CosmeticKind.Background, "monastery-garden");
            _shop.SelectDeck("kathmandu-gold");

            var wallet = new Wallet(_store);
            var again = new CosmeticsService(_store, wallet);

            Assert.AreEqual("indigo-dhaka", again.Equipped(CosmeticKind.Table));
            Assert.AreEqual("monastery-garden", again.Equipped(CosmeticKind.Background));
            Assert.AreEqual("kathmandu-gold", again.Equipped(CosmeticKind.Deck));
            Assert.IsTrue(again.IsOwned(CosmeticKind.Table, "indigo-dhaka"));
            Assert.IsTrue(again.IsOwned(CosmeticKind.Background, "monastery-garden"));
            Assert.IsTrue(again.IsOwned(CosmeticKind.Deck, "kathmandu-gold"));
            Assert.IsFalse(again.IsOwned(CosmeticKind.Table, "dusk-himalaya"));
            Assert.IsFalse(again.IsOwned(CosmeticKind.Deck, "himalayan-ivory"));
            Assert.AreEqual(100, wallet.Coins, "2100 less 500, 750 and 750");
            Assert.AreEqual(CosmeticsData.CurrentVersion, _store.Peek<CosmeticsData>(CosmeticsService.SaveKey).schemaVersion);
        }

        [Test]
        public void TodaysFile_IsMigrated_KeepingTheDeck_WithEverythingElseAtItsDefault()
        {
            Assert.AreEqual(2, CosmeticsData.CurrentVersion);
            // A version 1 file as JsonUtility reads it (the fields it lacks keep their initializers)...
            var today = new CosmeticsData { schemaVersion = 1, selectedDeckId = "classic-crimson" };
            // ...and, to show nothing is taken from it but the deck, one that names a table it never knew.
            today.equippedTableId = "indigo-dhaka";
            today.boughtTableIds.Add("indigo-dhaka");
            _store.Put(CosmeticsService.SaveKey, today);

            var migrated = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("classic-crimson", migrated.SelectedDeckId);
            Assert.AreEqual("durbar-square", migrated.Equipped(CosmeticKind.Background));
            Assert.AreEqual("green-dhaka", migrated.Equipped(CosmeticKind.Table));
            Assert.IsFalse(migrated.IsOwned(CosmeticKind.Table, "indigo-dhaka"));

            Assert.AreEqual(PurchaseResult.Bought, migrated.Buy(CosmeticKind.Background, "pokhara-lakeside"));
            migrated.Equip(CosmeticKind.Background, "pokhara-lakeside");

            CosmeticsData saved = _store.Peek<CosmeticsData>(CosmeticsService.SaveKey);
            Assert.AreEqual(2, saved.schemaVersion);
            Assert.AreEqual("classic-crimson", saved.selectedDeckId);
            Assert.AreEqual("pokhara-lakeside", saved.equippedBackgroundId);
            CollectionAssert.IsEmpty(saved.boughtTableIds);
        }

        /// <summary>
        /// Every deck was free until 6 Oct 2026. A file (of either version) that has a deck or a
        /// background equipped that now costs coins, and was never bought, goes back to the default:
        /// what a player owns is the free first item and what they paid for (the owner, 6 Oct 2026).
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        public void AnEquippedDeckOrBackgroundThatNowCostsCoins_FallsBackToTheDefault(int version)
        {
            _store.Put(CosmeticsService.SaveKey, new CosmeticsData
            {
                schemaVersion = version,
                selectedDeckId = "kathmandu-gold",
                equippedBackgroundId = "monastery-garden",
            });

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("classic-crimson", cosmetics.SelectedDeckId);
            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Background));
            Assert.IsFalse(cosmetics.IsOwned(CosmeticKind.Deck, "kathmandu-gold"));
            Assert.AreEqual(500, _wallet.Coins);
        }

        [Test]
        public void UnknownOrUnownedIdsOnDisk_FallBackToTheDefaults()
        {
            _store.Put(CosmeticsService.SaveKey, new CosmeticsData
            {
                selectedDeckId = "retired-seasonal",
                equippedBackgroundId = "diwali-lights",
                equippedTableId = "indigo-dhaka",
            });

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("classic-crimson", cosmetics.Equipped(CosmeticKind.Deck));
            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Background));
            Assert.AreEqual("green-dhaka", cosmetics.Equipped(CosmeticKind.Table), "an equipped table that isn't owned");
        }

        [TestCase("himalayan-ivory", "green-dhaka")]
        [TestCase("royal-crimson", "indigo-dhaka")]
        [TestCase("heritage-walnut", "dusk-himalaya")]
        public void ATableOnDisk_BecomesTheClothInItsPlace_OwnedAndEquipped(string table, string cloth)
        {
            // The game has no table any more; the shop sells cloths where it sold tables (ADR 058).
            var data = new CosmeticsData { equippedTableId = table };
            if (table != "himalayan-ivory")
            {
                data.boughtTableIds.Add(table);
            }

            _store.Put(CosmeticsService.SaveKey, data);
            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual(cloth, cosmetics.Equipped(CosmeticKind.Table));
            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Table, cloth));
            Assert.IsFalse(cosmetics.IsOwned(CosmeticKind.Table, table), "the table's id is gone from the shop");
            Assert.AreEqual(PurchaseResult.AlreadyOwned, cosmetics.Buy(CosmeticKind.Table, cloth), "nobody pays twice");
            Assert.AreEqual(500, _wallet.Coins);
        }

        [Test]
        public void TheTablesBoughtOnDisk_AreKeptAsCloths_WhenTheFileIsNextSaved()
        {
            var data = new CosmeticsData { equippedTableId = "royal-crimson" };
            data.boughtTableIds.Add("royal-crimson");
            data.boughtTableIds.Add("heritage-walnut");
            _store.Put(CosmeticsService.SaveKey, data);
            var cosmetics = new CosmeticsService(_store, _wallet);

            cosmetics.Equip(CosmeticKind.Table, "dusk-himalaya");

            CosmeticsData saved = _store.Peek<CosmeticsData>(CosmeticsService.SaveKey);
            CollectionAssert.AreEqual(new[] { "indigo-dhaka", "dusk-himalaya" }, saved.boughtTableIds);
            Assert.AreEqual("dusk-himalaya", saved.equippedTableId);
        }

        /// <summary>
        /// Durbar Square took the Himalayan Vista's place as the free background (the owner, 30 Oct
        /// 2026: "use durbar square as mainmenu bg image"). Every player had the Vista equipped unless
        /// they chose another: a file that names it owns and equips Durbar Square instead, with the
        /// rest of the file as it was, and the next save writes Durbar Square.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        public void TheHimalayanVistaOnDisk_BecomesDurbarSquare_OwnedAndEquipped(int version)
        {
            var data = new CosmeticsData { schemaVersion = version, selectedDeckId = "classic-crimson", equippedBackgroundId = "himalayan-vista" };
            if (version > 1)
            {
                data.selectedDeckId = "kathmandu-gold";
                data.boughtDeckIds.Add("kathmandu-gold");
                data.boughtBackgroundIds.Add("pokhara-lakeside");
                data.equippedTableId = "indigo-dhaka";
                data.boughtTableIds.Add("indigo-dhaka");
            }

            _store.Put(CosmeticsService.SaveKey, data);
            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Background), "equipped");
            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Background, "durbar-square"));
            Assert.IsFalse(cosmetics.IsOwned(CosmeticKind.Background, "himalayan-vista"), "the Vista's id is gone from the shop");
            CollectionAssert.DoesNotContain(cosmetics.Catalog(CosmeticKind.Background).Select(i => i.Id), "himalayan-vista");
            Assert.Throws<ArgumentException>(() => cosmetics.Equip(CosmeticKind.Background, "himalayan-vista"));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, cosmetics.Buy(CosmeticKind.Background, "durbar-square"), "nobody pays for it");
            Assert.AreEqual(500, _wallet.Coins);
            if (version > 1)
            {
                Assert.AreEqual("kathmandu-gold", cosmetics.Equipped(CosmeticKind.Deck), "the rest of the file is kept");
                Assert.AreEqual("indigo-dhaka", cosmetics.Equipped(CosmeticKind.Table));
                Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Background, "pokhara-lakeside"), "what was bought stays bought");
            }

            _wallet.Add(250);
            Assert.AreEqual(PurchaseResult.Bought, cosmetics.Buy(CosmeticKind.Background, "monastery-garden"), "a purchase writes the file");

            CosmeticsData saved = _store.Peek<CosmeticsData>(CosmeticsService.SaveKey);
            Assert.AreEqual("durbar-square", saved.equippedBackgroundId);
            Assert.AreEqual(CosmeticsData.CurrentVersion, saved.schemaVersion);
            Assert.AreEqual("durbar-square", new CosmeticsService(_store, _wallet).Equipped(CosmeticKind.Background), "after a restart too");
        }

        [Test]
        public void AnotherBackgroundOnDisk_StaysEquipped_WhereTheVistaWasNot()
        {
            var data = new CosmeticsData { equippedBackgroundId = "monastery-garden" };
            data.boughtBackgroundIds.Add("monastery-garden");
            _store.Put(CosmeticsService.SaveKey, data);

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("monastery-garden", cosmetics.Equipped(CosmeticKind.Background));
            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Background, "durbar-square"), "the free one is owned too");
            cosmetics.Equip(CosmeticKind.Background, "durbar-square");
            Assert.AreEqual("durbar-square", _store.Peek<CosmeticsData>(CosmeticsService.SaveKey).equippedBackgroundId);
        }

        [Test]
        public void AVistaInTheBoughtList_IsKeptAsDurbarSquare_WhenTheFileIsNextSaved()
        {
            var data = new CosmeticsData { equippedBackgroundId = "pokhara-lakeside" };
            data.boughtBackgroundIds.Add("himalayan-vista");
            data.boughtBackgroundIds.Add("pokhara-lakeside");
            _store.Put(CosmeticsService.SaveKey, data);
            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("pokhara-lakeside", cosmetics.Equipped(CosmeticKind.Background));
            cosmetics.Equip(CosmeticKind.Background, "durbar-square");

            CosmeticsData saved = _store.Peek<CosmeticsData>(CosmeticsService.SaveKey);
            CollectionAssert.AreEqual(new[] { "durbar-square", "pokhara-lakeside" }, saved.boughtBackgroundIds);
            Assert.AreEqual("durbar-square", saved.equippedBackgroundId);
        }

        /// <summary>
        /// The owner (5 Oct 2026) kept three backgrounds. A file that still has one of the three that
        /// left the shop equipped, or listed as bought, gets the default (Durbar Square since 30 Oct 2026)
        /// like any other id the build doesn't know: the old one is neither owned, sold nor equippable,
        /// and the next save writes the default (the unknown purchase stays in the file, as every one does).
        /// </summary>
        [TestCase("kathmandu-courtyard")]
        [TestCase("village-terrace")]
        [TestCase("vintage-home")]
        public void ABackgroundThatLeftTheShop_OnDisk_FallsBackToDurbarSquare(string removed)
        {
            var data = new CosmeticsData { selectedDeckId = "kathmandu-gold", equippedBackgroundId = removed };
            data.boughtDeckIds.Add("kathmandu-gold");
            data.boughtBackgroundIds.Add(removed);
            _store.Put(CosmeticsService.SaveKey, data);

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Background), "equipped");
            Assert.IsFalse(cosmetics.IsOwned(CosmeticKind.Background, removed), "bought");
            CollectionAssert.DoesNotContain(cosmetics.Catalog(CosmeticKind.Background).Select(i => i.Id), removed);
            Assert.AreEqual(PurchaseResult.UnknownItem, cosmetics.Buy(CosmeticKind.Background, removed));
            Assert.Throws<ArgumentException>(() => cosmetics.Equip(CosmeticKind.Background, removed));
            Assert.AreEqual("kathmandu-gold", cosmetics.Equipped(CosmeticKind.Deck), "the rest of the file is kept");
            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, _store.SaveCount, "nothing is written until something changes");

            cosmetics.Equip(CosmeticKind.Deck, "classic-crimson");

            CosmeticsData saved = _store.Peek<CosmeticsData>(CosmeticsService.SaveKey);
            Assert.AreEqual("durbar-square", saved.equippedBackgroundId);
            CollectionAssert.AreEqual(new[] { removed }, saved.boughtBackgroundIds);
            Assert.AreEqual("durbar-square", new CosmeticsService(_store, _wallet).Equipped(CosmeticKind.Background), "after a restart too");
        }

        [Test]
        public void APurchaseThisBuildDoesNotKnow_StaysInTheFile()
        {
            var data = new CosmeticsData();
            data.boughtTableIds.Add("festival-gold");
            data.boughtTableIds.Add("dusk-himalaya");
            _store.Put(CosmeticsService.SaveKey, data);
            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Table, "dusk-himalaya"));

            cosmetics.Equip(CosmeticKind.Table, "dusk-himalaya");

            CollectionAssert.AreEqual(new[] { "festival-gold", "dusk-himalaya" }, _store.Peek<CosmeticsData>(CosmeticsService.SaveKey).boughtTableIds);
        }

        [Test]
        public void FileFromANewerVersion_SellsNothing_AndIsNeverOverwritten()
        {
            _store.Put(CosmeticsService.SaveKey, new CosmeticsData { schemaVersion = CosmeticsData.CurrentVersion + 1, equippedBackgroundId = "monastery-garden" });
            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.IsTrue(cosmetics.IsReadOnly);
            Assert.AreEqual(PurchaseResult.UnknownItem, cosmetics.Buy(CosmeticKind.Table, "indigo-dhaka"), "this build couldn't keep the purchase");
            Assert.AreEqual(PurchaseResult.UnknownItem, cosmetics.Buy(CosmeticKind.Background, "pokhara-lakeside"));
            Assert.AreEqual(500, _wallet.Coins);

            // The session starts at the defaults, the only items owned without buying (6 Oct 2026).
            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Background));
            Assert.Throws<InvalidOperationException>(() => cosmetics.Equip(CosmeticKind.Background, "pokhara-lakeside"));
            cosmetics.Equip(CosmeticKind.Background, "durbar-square");

            Assert.AreEqual(0, _store.SaveCount);
            Assert.AreEqual("monastery-garden", _store.Peek<CosmeticsData>(CosmeticsService.SaveKey).equippedBackgroundId);
        }

        [Test]
        public void WithoutAWallet_TheOwnedDecksStillWork_ButNothingCanBePaidFor()
        {
            var data = new CosmeticsData();
            data.boughtDeckIds.Add("himalayan-ivory");
            _store.Put(CosmeticsService.SaveKey, data);
            var decksOnly = new CosmeticsService(_store);

            decksOnly.SelectDeck("himalayan-ivory");

            Assert.AreEqual("himalayan-ivory", decksOnly.SelectedDeckId);
            Assert.AreEqual(PurchaseResult.AlreadyOwned, decksOnly.Buy(CosmeticKind.Table, "green-dhaka"));
            Assert.Throws<InvalidOperationException>(() => decksOnly.Buy(CosmeticKind.Table, "indigo-dhaka"));
            Assert.Throws<InvalidOperationException>(() => decksOnly.Buy(CosmeticKind.Deck, "kathmandu-gold"));
            Assert.IsFalse(decksOnly.IsOwned(CosmeticKind.Table, "indigo-dhaka"));
            Assert.Throws<InvalidOperationException>(() => decksOnly.SelectDeck("kathmandu-gold"));
        }

        private void AssertOwned(CosmeticKind kind, string[] ids, params bool[] owned)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.AreEqual(owned[i], _shop.IsOwned(kind, ids[i]), kind + " " + ids[i]);
            }
        }

        private void AssertCatalog(CosmeticKind kind, string[] ids, int[] prices)
        {
            IReadOnlyList<CosmeticItem> items = _shop.Catalog(kind);

            CollectionAssert.AreEqual(ids, items.Select(i => i.Id), kind + " order");
            CollectionAssert.AreEqual(prices, items.Select(i => i.Price), kind + " prices");
            Assert.IsTrue(items.All(i => i.Kind == kind), kind + " kinds");
        }

        /// <summary>A wallet that shows plenty of coins but turns every payment down, as one kept elsewhere might.</summary>
        private sealed class RefusingWallet : IWallet
        {
            public event Action Changed
            {
                add { }
                remove { }
            }

            public int Coins => 1000;

            public bool TrySpend(int amount) => false;

            public void Add(int amount)
            {
            }
        }
    }
}
