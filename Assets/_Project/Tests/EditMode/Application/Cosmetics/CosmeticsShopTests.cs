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
    /// The shop (the owner, 4, 5 and 6 Oct 2026; mockup 15; the themes, ADR 064): the decks and the
    /// themes in shop order with their prices, names and defaults; a theme's background and cloth,
    /// owned and equipped only with it; buying with coins, which never equips; equipping what is
    /// owned; the equipped deck being the selected deck; the cosmetics file, migrated from versions 1
    /// and 2, where a background or a cloth bought on its own owns its whole theme and the equipped
    /// background decides the equipped theme; the backgrounds that left the shop falling back to the
    /// default; the tables on disk becoming the cloths that took their places (ADR 058); and the
    /// Himalayan Vista on disk becoming Durbar Square (the owner, 30 Oct 2026).
    /// </summary>
    public sealed class CosmeticsShopTests
    {
        private static readonly string[] Decks = { "classic-crimson", "himalayan-ivory", "kathmandu-gold" };

        private static readonly string[] Themes =
        {
            "durbar-square", "himalayan-sunrise", "laligurans-forest", "golden-terraces", "tihar-lights",
            "prayer-flag-pass", "mithila-garden", "phewa-moonlight", "pokhara-lakeside", "monastery-garden",
        };

        // The placeholders of Figma's "V1 Rebuild · Shop" and, for the last two, the prices they had as backgrounds.
        private static readonly int[] ThemePrices = { 0, 500, 500, 750, 1000, 750, 1000, 1000, 500, 750 };

        private static readonly string[] Backgrounds =
        {
            "durbar-square", "himalayan-sunrise", "laligurans-forest", "golden-terraces", "tihar-lights",
            "prayer-flag-pass", "mithila-garden", "phewa-moonlight", "pokhara-lakeside", "monastery-garden",
        };

        private static readonly string[] Cloths =
        {
            "green-dhaka", "himalayan-sunrise", "laligurans-forest", "golden-terraces", "tihar-lights",
            "prayer-flag-pass", "mithila-garden", "phewa-moonlight", "indigo-dhaka", "dusk-himalaya",
        };

        private static readonly string[] PaidThemes = Themes.Skip(1).ToArray();

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
            AssertCatalog(CosmeticKind.Theme, Themes, ThemePrices);
        }

        [Test]
        public void ThePrices_AreTheCatalogsOwn_ForTheOwnerToChange()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    0, CosmeticCatalog.Themes.HimalayanSunrisePrice, CosmeticCatalog.Themes.LaliguransForestPrice,
                    CosmeticCatalog.Themes.GoldenTerracesPrice, CosmeticCatalog.Themes.TiharLightsPrice,
                    CosmeticCatalog.Themes.PrayerFlagPassPrice, CosmeticCatalog.Themes.MithilaGardenPrice,
                    CosmeticCatalog.Themes.PhewaMoonlightPrice, CosmeticCatalog.Themes.PokharaLakesidePrice,
                    CosmeticCatalog.Themes.MonasteryGardenPrice,
                },
                _shop.Catalog(CosmeticKind.Theme).Select(i => i.Price));
        }

        [Test]
        public void EveryTheme_IsABackground_AndTheClothThatMatchesIt()
        {
            IReadOnlyList<CosmeticItem> themes = _shop.Catalog(CosmeticKind.Theme);

            CollectionAssert.AreEqual(Backgrounds, themes.Select(t => t.BackgroundId));
            CollectionAssert.AreEqual(Cloths, themes.Select(t => t.TableId));
            Assert.AreEqual("green-dhaka", themes[0].TableId, "Durbar Square on the green dhaka cloth");
            Assert.AreEqual("indigo-dhaka", themes[8].TableId, "Pokhara Lakeside on the indigo dhaka");
            Assert.AreEqual("dusk-himalaya", themes[9].TableId, "Monastery Garden on the Himalaya at dusk");
        }

        [Test]
        public void TheBackgroundsAndCloths_AreTheThemesParts_InTheirOrder_NamedAndPricedAsTheirTheme()
        {
            AssertCatalog(CosmeticKind.Background, Backgrounds, ThemePrices);
            AssertCatalog(CosmeticKind.Table, Cloths, ThemePrices);
            IReadOnlyList<CosmeticItem> themes = _shop.Catalog(CosmeticKind.Theme);
            for (int i = 0; i < themes.Count; i++)
            {
                Assert.AreEqual(themes[i].NameKey, _shop.Catalog(CosmeticKind.Background)[i].NameKey);
                Assert.AreEqual(themes[i].NameKey, _shop.Catalog(CosmeticKind.Table)[i].NameKey);
                Assert.AreSame(themes[i], CosmeticCatalog.ThemeOf(CosmeticKind.Background, Backgrounds[i]));
                Assert.AreSame(themes[i], CosmeticCatalog.ThemeOf(CosmeticKind.Table, Cloths[i]));
            }

            Assert.IsNull(CosmeticCatalog.ThemeOf(CosmeticKind.Theme, "tihar-lights"), "only a part has a theme");
            Assert.IsNull(CosmeticCatalog.ThemeOf(CosmeticKind.Table, "royal-crimson"));
            Assert.IsNull(CosmeticCatalog.ThemeOf(CosmeticKind.Background, null));
        }

        [Test]
        public void NameKeys_ReuseTheDeckNames_AndFollowUiShopThemeId()
        {
            CollectionAssert.AreEqual(
                new[] { "ui.deck.classic_crimson", "ui.deck.himalayan_ivory", "ui.deck.kathmandu_gold" },
                _shop.Catalog(CosmeticKind.Deck).Select(i => i.NameKey));
            CollectionAssert.AreEqual(
                Themes.Select(id => "ui.shop.theme." + id.Replace('-', '_')),
                _shop.Catalog(CosmeticKind.Theme).Select(i => i.NameKey));
        }

        [Test]
        public void Names_AreFigmasNames_InEnglish()
        {
            var strings = new TableStrings();

            CollectionAssert.AreEqual(
                new[] { "Classic Crimson", "Himalayan Ivory", "Kathmandu Gold" },
                _shop.Catalog(CosmeticKind.Deck).Select(i => strings.Get(i.NameKey)));
            CollectionAssert.AreEqual(
                new[]
                {
                    "Durbar Square", "Himalayan Sunrise", "Laligurans Forest", "Golden Terraces", "Tihar Lights",
                    "Prayer Flag Pass", "Mithila Garden", "Phewa Moonlight", "Pokhara Lakeside", "Monastery Garden",
                },
                _shop.Catalog(CosmeticKind.Theme).Select(i => strings.Get(i.NameKey)));
        }

        [Test]
        public void ANewPlayer_OwnsTheFirstDeck_AndDurbarSquare_AndNothingElse()
        {
            AssertOwned(_shop, CosmeticKind.Deck, Decks, true, false, false);
            AssertOwned(_shop, CosmeticKind.Theme, Themes, Themes.Select(id => id == "durbar-square").ToArray());
            AssertOwned(_shop, CosmeticKind.Background, Backgrounds, Backgrounds.Select(id => id == "durbar-square").ToArray());
            AssertOwned(_shop, CosmeticKind.Table, Cloths, Cloths.Select(id => id == "green-dhaka").ToArray());
        }

        [TestCaseSource(nameof(PaidThemes))]
        public void EveryPaidTheme_MustBeBought_BeforeItCanBeEquipped_AndThenBringsItsBackgroundAndCloth(string id)
        {
            CosmeticItem theme = CosmeticCatalog.Find(CosmeticKind.Theme, id);
            Assert.Throws<InvalidOperationException>(() => _shop.Equip(CosmeticKind.Theme, id));

            if (theme.Price > 500)
            {
                _wallet.Add(theme.Price - 500);
            }

            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Theme, id));
            _shop.Equip(CosmeticKind.Theme, id);

            Assert.AreEqual(0, _wallet.Coins, "the theme's price, once");
            Assert.AreEqual(id, _shop.Equipped(CosmeticKind.Theme));
            Assert.AreEqual(theme.BackgroundId, _shop.Equipped(CosmeticKind.Background));
            Assert.AreEqual(theme.TableId, _shop.Equipped(CosmeticKind.Table));
            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Background, theme.BackgroundId));
            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Table, theme.TableId));
        }

        [Test]
        public void ANewPlayer_HasTodaysLook_ClassicCrimson_DurbarSquare_GreenDhaka()
        {
            Assert.AreEqual("classic-crimson", _shop.Equipped(CosmeticKind.Deck));
            Assert.AreEqual("durbar-square", _shop.Equipped(CosmeticKind.Theme));
            Assert.AreEqual("durbar-square", _shop.Equipped(CosmeticKind.Background));
            Assert.AreEqual("green-dhaka", _shop.Equipped(CosmeticKind.Table));
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void ABackgroundOrAClothAlone_IsNeitherSoldNorEquipped()
        {
            _wallet.Add(1000);

            Assert.AreEqual(PurchaseResult.UnknownItem, _shop.Buy(CosmeticKind.Background, "pokhara-lakeside"));
            Assert.AreEqual(PurchaseResult.UnknownItem, _shop.Buy(CosmeticKind.Table, "tihar-lights"));
            Assert.AreEqual(PurchaseResult.UnknownItem, _shop.Buy(CosmeticKind.Background, "durbar-square"));
            Assert.Throws<InvalidOperationException>(() => _shop.Equip(CosmeticKind.Background, "durbar-square"));
            Assert.Throws<InvalidOperationException>(() => _shop.Equip(CosmeticKind.Table, "green-dhaka"));

            Assert.AreEqual(1500, _wallet.Coins);
            Assert.AreEqual(0, _store.SavesOf(CosmeticsService.SaveKey));
        }

        [Test]
        public void ABackgroundAndItsCloth_AreOwned_WithTheirTheme()
        {
            _wallet.Add(500);
            Assert.IsFalse(_shop.IsOwned(CosmeticKind.Background, "tihar-lights"));
            Assert.IsFalse(_shop.IsOwned(CosmeticKind.Table, "tihar-lights"));

            _shop.Buy(CosmeticKind.Theme, "tihar-lights");

            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Background, "tihar-lights"));
            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Table, "tihar-lights"));
            Assert.IsFalse(_shop.IsOwned(CosmeticKind.Table, "phewa-moonlight"));
        }

        [TestCase(CosmeticKind.Deck, "indigo-dhaka")]
        [TestCase(CosmeticKind.Theme, "himalayan-ivory")]
        [TestCase(CosmeticKind.Theme, "green-dhaka")]
        [TestCase(CosmeticKind.Theme, "Durbar-Square")]
        [TestCase(CosmeticKind.Theme, "")]
        [TestCase(CosmeticKind.Theme, null)]
        [TestCase(CosmeticKind.Background, "green-dhaka")]
        [TestCase((CosmeticKind)7, "durbar-square")]
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
        public void Buy_SpendsTheCoins_AndOwnsTheTheme_WithoutEquippingIt()
        {
            int changed = 0;
            _shop.Changed += () => changed++;

            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Theme, "himalayan-sunrise"));

            Assert.AreEqual(0, _wallet.Coins, "the 500 starting coins buy the 500 theme");
            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Theme, "himalayan-sunrise"));
            Assert.AreEqual("durbar-square", _shop.Equipped(CosmeticKind.Theme), "buying never equips");
            Assert.AreEqual("green-dhaka", _shop.Equipped(CosmeticKind.Table));
            Assert.AreEqual(1, changed);
            Assert.IsTrue(new CosmeticsService(_store, new Wallet(_store)).IsOwned(CosmeticKind.Theme, "himalayan-sunrise"), "the purchase is saved");
            CollectionAssert.AreEqual(new[] { "himalayan-sunrise" }, _store.Peek<CosmeticsData>(CosmeticsService.SaveKey).boughtThemeIds);
        }

        [Test]
        public void Buy_SomethingOwned_IsAlreadyOwned_AndCostsNothing()
        {
            int changed = 0;
            _shop.Changed += () => changed++;

            Assert.AreEqual(PurchaseResult.AlreadyOwned, _shop.Buy(CosmeticKind.Theme, "durbar-square"));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, _shop.Buy(CosmeticKind.Deck, "classic-crimson"));
            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, changed);

            _wallet.Add(500);
            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Theme, "golden-terraces"));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, _shop.Buy(CosmeticKind.Theme, "golden-terraces"));
            Assert.AreEqual(250, _wallet.Coins, "paid once");
        }

        [Test]
        public void Buy_WithoutEnoughCoins_TakesNothing_AndOwnsNothing()
        {
            _wallet.Add(499);
            int changed = 0;
            _shop.Changed += () => changed++;

            Assert.AreEqual(PurchaseResult.NotEnoughCoins, _shop.Buy(CosmeticKind.Theme, "tihar-lights"));

            Assert.AreEqual(999, _wallet.Coins);
            Assert.IsFalse(_shop.IsOwned(CosmeticKind.Theme, "tihar-lights"));
            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SavesOf(CosmeticsService.SaveKey));

            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Theme, "golden-terraces"));
            Assert.AreEqual(249, _wallet.Coins);
        }

        [Test]
        public void AWalletListenerThatThrows_CannotLoseAPurchase()
        {
            _wallet.Changed += () => throw new InvalidOperationException("a broken view");

            Assert.Throws<InvalidOperationException>(() => _shop.Buy(CosmeticKind.Theme, "laligurans-forest"), "the listener's exception reaches the caller");

            Assert.AreEqual(0, new Wallet(_store).Coins);
            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Theme, "laligurans-forest"), "the coins were taken, so the theme is the player's");
            Assert.IsTrue(new CosmeticsService(_store, new Wallet(_store)).IsOwned(CosmeticKind.Theme, "laligurans-forest"), "and that is saved");
        }

        [Test]
        public void AWalletThatRefusesThePayment_LeavesNothingBought()
        {
            var shop = new CosmeticsService(_store, new RefusingWallet());

            Assert.AreEqual(PurchaseResult.NotEnoughCoins, shop.Buy(CosmeticKind.Theme, "tihar-lights"));

            Assert.IsFalse(shop.IsOwned(CosmeticKind.Theme, "tihar-lights"));
            Assert.IsFalse(new CosmeticsService(_store).IsOwned(CosmeticKind.Theme, "tihar-lights"), "nothing bought is left in the file");
        }

        [Test]
        public void Buy_TheWholeBalance_IsEnough()
        {
            _wallet.Add(500);

            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Theme, "mithila-garden"));
            Assert.AreEqual(0, _wallet.Coins);
        }

        [TestCase(CosmeticKind.Theme, "dashain-gold")]
        [TestCase(CosmeticKind.Theme, "Tihar-Lights")]
        [TestCase(CosmeticKind.Theme, "")]
        [TestCase(CosmeticKind.Theme, null)]
        [TestCase(CosmeticKind.Theme, "indigo-dhaka")]
        [TestCase(CosmeticKind.Deck, "tihar-lights")]
        [TestCase(CosmeticKind.Background, "tihar-lights")]
        [TestCase(CosmeticKind.Table, "indigo-dhaka")]
        [TestCase((CosmeticKind)7, "tihar-lights")]
        public void Buy_SomethingNotForSale_IsUnknown(CosmeticKind kind, string id)
        {
            Assert.AreEqual(PurchaseResult.UnknownItem, _shop.Buy(kind, id));

            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void Equip_ATheme_SavesAndRaisesChanged_AndChangesTheBackgroundAndTheCloth()
        {
            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Theme, "pokhara-lakeside"));
            int saves = _store.SavesOf(CosmeticsService.SaveKey);
            int changed = 0;
            _shop.Changed += () => changed++;

            _shop.Equip(CosmeticKind.Theme, "pokhara-lakeside");

            Assert.AreEqual("pokhara-lakeside", _shop.Equipped(CosmeticKind.Background));
            Assert.AreEqual("indigo-dhaka", _shop.Equipped(CosmeticKind.Table));
            Assert.AreEqual(1, changed, "one change for both");
            Assert.AreEqual(saves + 1, _store.SavesOf(CosmeticsService.SaveKey));
            CosmeticsData saved = _store.Peek<CosmeticsData>(CosmeticsService.SaveKey);
            Assert.AreEqual("pokhara-lakeside", saved.equippedThemeId);
            Assert.AreEqual("pokhara-lakeside", saved.equippedBackgroundId);
            Assert.AreEqual("indigo-dhaka", saved.equippedTableId);
        }

        [Test]
        public void Equip_WhatIsEquipped_DoesNothing()
        {
            int changed = 0;
            _shop.Changed += () => changed++;

            _shop.Equip(CosmeticKind.Theme, "durbar-square");
            _shop.Equip(CosmeticKind.Deck, "classic-crimson");

            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void Equip_SomethingNotOwned_Throws_UntilItIsBought()
        {
            Assert.Throws<InvalidOperationException>(() => _shop.Equip(CosmeticKind.Theme, "prayer-flag-pass"));
            Assert.AreEqual("durbar-square", _shop.Equipped(CosmeticKind.Theme));

            _wallet.Add(250);
            _shop.Buy(CosmeticKind.Theme, "prayer-flag-pass");
            _shop.Equip(CosmeticKind.Theme, "prayer-flag-pass");

            Assert.AreEqual("prayer-flag-pass", _shop.Equipped(CosmeticKind.Theme));
        }

        [TestCase(CosmeticKind.Theme, "dashain-gold")]
        [TestCase(CosmeticKind.Theme, null)]
        [TestCase(CosmeticKind.Theme, "")]
        [TestCase(CosmeticKind.Theme, "indigo-dhaka")]
        [TestCase(CosmeticKind.Theme, "himalayan-vista")]
        [TestCase(CosmeticKind.Deck, "durbar-square")]
        [TestCase(CosmeticKind.Background, "himalayan-vista")]
        [TestCase(CosmeticKind.Table, "royal-crimson")]
        public void Equip_SomethingNotInTheCatalog_Throws(CosmeticKind kind, string id)
        {
            Assert.Throws<ArgumentException>(() => _shop.Equip(kind, id));

            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void TheCatalogs_CannotBeChanged()
        {
            var themes = (IList<CosmeticItem>)_shop.Catalog(CosmeticKind.Theme);

            Assert.Throws<NotSupportedException>(() => themes.Add(new CosmeticItem(CosmeticKind.Theme, "free-theme", "ui.shop.theme.free_theme", 0)));
            Assert.Throws<NotSupportedException>(() => ((IList<CosmeticItem>)_shop.Catalog(CosmeticKind.Table)).RemoveAt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _shop.Catalog((CosmeticKind)7));
            Assert.Throws<ArgumentOutOfRangeException>(() => _shop.Equipped((CosmeticKind)7));
        }

        [Test]
        public void EverythingSurvivesARestart()
        {
            _wallet.Add(2100);
            _shop.Buy(CosmeticKind.Theme, "phewa-moonlight");
            _shop.Buy(CosmeticKind.Theme, "golden-terraces");
            _shop.Buy(CosmeticKind.Deck, "kathmandu-gold");
            _shop.Equip(CosmeticKind.Theme, "phewa-moonlight");
            _shop.SelectDeck("kathmandu-gold");

            var wallet = new Wallet(_store);
            var again = new CosmeticsService(_store, wallet);

            Assert.AreEqual("phewa-moonlight", again.Equipped(CosmeticKind.Theme));
            Assert.AreEqual("phewa-moonlight", again.Equipped(CosmeticKind.Background));
            Assert.AreEqual("phewa-moonlight", again.Equipped(CosmeticKind.Table));
            Assert.AreEqual("kathmandu-gold", again.Equipped(CosmeticKind.Deck));
            Assert.IsTrue(again.IsOwned(CosmeticKind.Theme, "golden-terraces"));
            Assert.IsTrue(again.IsOwned(CosmeticKind.Deck, "kathmandu-gold"));
            Assert.IsFalse(again.IsOwned(CosmeticKind.Theme, "tihar-lights"));
            Assert.IsFalse(again.IsOwned(CosmeticKind.Deck, "himalayan-ivory"));
            Assert.AreEqual(100, wallet.Coins, "2600 less 1000, 750 and 750");
            Assert.AreEqual(CosmeticsData.CurrentVersion, _store.Peek<CosmeticsData>(CosmeticsService.SaveKey).schemaVersion);
        }

        [Test]
        public void AVersionOneFile_IsMigrated_KeepingTheDeck_WithEverythingElseAtItsDefault()
        {
            Assert.AreEqual(3, CosmeticsData.CurrentVersion);
            // A version 1 file as JsonUtility reads it (the fields it lacks keep their initializers)...
            var old = new CosmeticsData { schemaVersion = 1, selectedDeckId = "classic-crimson" };
            // ...and, to show nothing is taken from it but the deck, one that names a cloth it never knew.
            old.equippedTableId = "indigo-dhaka";
            old.boughtTableIds.Add("indigo-dhaka");
            _store.Put(CosmeticsService.SaveKey, old);

            var migrated = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("classic-crimson", migrated.SelectedDeckId);
            Assert.AreEqual("durbar-square", migrated.Equipped(CosmeticKind.Theme));
            Assert.AreEqual("green-dhaka", migrated.Equipped(CosmeticKind.Table));
            Assert.IsFalse(migrated.IsOwned(CosmeticKind.Theme, "pokhara-lakeside"));

            Assert.AreEqual(PurchaseResult.Bought, migrated.Buy(CosmeticKind.Theme, "pokhara-lakeside"));
            migrated.Equip(CosmeticKind.Theme, "pokhara-lakeside");

            CosmeticsData saved = _store.Peek<CosmeticsData>(CosmeticsService.SaveKey);
            Assert.AreEqual(3, saved.schemaVersion);
            Assert.AreEqual("classic-crimson", saved.selectedDeckId);
            Assert.AreEqual("pokhara-lakeside", saved.equippedThemeId);
            CollectionAssert.IsEmpty(saved.boughtTableIds);
        }

        /// <summary>
        /// Every deck was free until 6 Oct 2026. A file (of any version) that has a deck, a background
        /// or a theme equipped that now costs coins, and was never bought, goes back to the default:
        /// what a player owns is the free first item and what they paid for (the owner, 6 Oct 2026).
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void AnEquippedDeckOrThemeThatCostsCoins_AndWasNeverBought_FallsBackToTheDefault(int version)
        {
            _store.Put(CosmeticsService.SaveKey, new CosmeticsData
            {
                schemaVersion = version,
                selectedDeckId = "kathmandu-gold",
                equippedThemeId = "monastery-garden",
                equippedBackgroundId = "monastery-garden",
                equippedTableId = "dusk-himalaya",
            });

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("classic-crimson", cosmetics.SelectedDeckId);
            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Theme));
            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Background));
            Assert.AreEqual("green-dhaka", cosmetics.Equipped(CosmeticKind.Table));
            Assert.IsFalse(cosmetics.IsOwned(CosmeticKind.Deck, "kathmandu-gold"));
            Assert.AreEqual(500, _wallet.Coins);
        }

        [Test]
        public void UnknownOrUnownedIdsOnDisk_FallBackToTheDefaults()
        {
            _store.Put(CosmeticsService.SaveKey, new CosmeticsData
            {
                selectedDeckId = "retired-seasonal",
                equippedThemeId = "diwali-lights",
                equippedBackgroundId = "tihar-lights",
                equippedTableId = "indigo-dhaka",
            });

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("classic-crimson", cosmetics.Equipped(CosmeticKind.Deck));
            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Theme));
            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Background), "the theme's background, whatever the file says");
            Assert.AreEqual("green-dhaka", cosmetics.Equipped(CosmeticKind.Table), "and its cloth");
        }

        [Test]
        public void AThemeFile_KeepsItsTheme_AndTakesItsBackgroundAndClothFromIt()
        {
            var data = new CosmeticsData { equippedThemeId = "tihar-lights", equippedBackgroundId = "durbar-square", equippedTableId = "dusk-himalaya" };
            data.boughtThemeIds.Add("tihar-lights");
            _store.Put(CosmeticsService.SaveKey, data);

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("tihar-lights", cosmetics.Equipped(CosmeticKind.Theme));
            Assert.AreEqual("tihar-lights", cosmetics.Equipped(CosmeticKind.Background));
            Assert.AreEqual("tihar-lights", cosmetics.Equipped(CosmeticKind.Table));
            Assert.AreEqual(0, _store.SaveCount, "nothing is written until something changes");
        }

        // ------------------------------------------------------------------ carried over (ADR 064)

        [TestCase("pokhara-lakeside", "pokhara-lakeside", "indigo-dhaka")]
        [TestCase("monastery-garden", "monastery-garden", "dusk-himalaya")]
        public void ABackgroundBoughtBeforeTheThemes_OwnsItsWholeTheme(string background, string theme, string cloth)
        {
            var data = new CosmeticsData { schemaVersion = 2 };
            data.boughtBackgroundIds.Add(background);
            _store.Put(CosmeticsService.SaveKey, data);

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Theme, theme));
            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Table, cloth), "its cloth comes with it");
            Assert.AreEqual(PurchaseResult.AlreadyOwned, cosmetics.Buy(CosmeticKind.Theme, theme), "nobody pays twice");
            cosmetics.Equip(CosmeticKind.Theme, theme);
            Assert.AreEqual(cloth, cosmetics.Equipped(CosmeticKind.Table));
            Assert.AreEqual(500, _wallet.Coins);
            AssertOwned(cosmetics, CosmeticKind.Theme, Themes, Themes.Select(id => id == "durbar-square" || id == theme).ToArray());
        }

        [TestCase("indigo-dhaka", "pokhara-lakeside")]
        [TestCase("dusk-himalaya", "monastery-garden")]
        [TestCase("royal-crimson", "pokhara-lakeside")]
        [TestCase("heritage-walnut", "monastery-garden")]
        public void AClothOrTableBoughtBeforeTheThemes_OwnsItsWholeTheme(string cloth, string theme)
        {
            var data = new CosmeticsData { schemaVersion = 2 };
            data.boughtTableIds.Add(cloth);
            _store.Put(CosmeticsService.SaveKey, data);

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Theme, theme));
            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Background, theme), "its background comes with it");
            Assert.AreEqual(PurchaseResult.AlreadyOwned, cosmetics.Buy(CosmeticKind.Theme, theme), "nobody pays twice");
            Assert.AreEqual(500, _wallet.Coins);
        }

        [Test]
        public void TheFreeTableOnDisk_OwnsNothingMore()
        {
            var data = new CosmeticsData { schemaVersion = 2, equippedTableId = "himalayan-ivory" };
            _store.Put(CosmeticsService.SaveKey, data);

            var cosmetics = new CosmeticsService(_store, _wallet);

            AssertOwned(cosmetics, CosmeticKind.Theme, Themes, Themes.Select(id => id == "durbar-square").ToArray());
            Assert.AreEqual("green-dhaka", cosmetics.Equipped(CosmeticKind.Table));
        }

        /// <summary>
        /// Before the themes a player could show one background with another cloth. The equipped
        /// background decides their theme (the owner, 31 Oct 2026), so the cloth may change to the
        /// theme's; both themes stay theirs.
        /// </summary>
        [Test]
        public void TheEquippedBackground_DecidesTheTheme_AndItsClothComesWithIt()
        {
            var data = new CosmeticsData { schemaVersion = 2, equippedBackgroundId = "pokhara-lakeside", equippedTableId = "dusk-himalaya" };
            data.boughtBackgroundIds.Add("pokhara-lakeside");
            data.boughtTableIds.Add("heritage-walnut");
            _store.Put(CosmeticsService.SaveKey, data);

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("pokhara-lakeside", cosmetics.Equipped(CosmeticKind.Theme));
            Assert.AreEqual("pokhara-lakeside", cosmetics.Equipped(CosmeticKind.Background));
            Assert.AreEqual("indigo-dhaka", cosmetics.Equipped(CosmeticKind.Table), "the theme's cloth");
            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Theme, "monastery-garden"), "the theme of the cloth bought is theirs too");

            cosmetics.Equip(CosmeticKind.Deck, "classic-crimson");
            cosmetics.Equip(CosmeticKind.Theme, "monastery-garden");

            CosmeticsData saved = _store.Peek<CosmeticsData>(CosmeticsService.SaveKey);
            Assert.AreEqual(3, saved.schemaVersion);
            Assert.AreEqual("monastery-garden", saved.equippedThemeId);
            Assert.AreEqual("dusk-himalaya", saved.equippedTableId);
            CollectionAssert.AreEqual(new[] { "pokhara-lakeside" }, saved.boughtBackgroundIds, "what was bought is kept as it was");
            CollectionAssert.AreEqual(new[] { "dusk-himalaya" }, saved.boughtTableIds, "the table as the cloth in its place");
            CollectionAssert.IsEmpty(saved.boughtThemeIds, "nothing was bought now");
        }

        [Test]
        public void TheFreeBackgroundOnDisk_DecidesDurbarSquare_EvenOverABoughtCloth()
        {
            var data = new CosmeticsData { schemaVersion = 2, equippedBackgroundId = "durbar-square", equippedTableId = "indigo-dhaka" };
            data.boughtTableIds.Add("indigo-dhaka");
            _store.Put(CosmeticsService.SaveKey, data);

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Theme));
            Assert.AreEqual("green-dhaka", cosmetics.Equipped(CosmeticKind.Table));
            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Theme, "pokhara-lakeside"), "the indigo cloth's theme is theirs to equip");
        }

        // ------------------------------------------------------------------ renamed and retired ids

        [TestCase("himalayan-ivory", "green-dhaka")]
        [TestCase("royal-crimson", "indigo-dhaka")]
        [TestCase("heritage-walnut", "dusk-himalaya")]
        public void ATableOnDisk_IsReadAsTheClothInItsPlace(string table, string cloth)
        {
            // The game has no table any more; the shop sells cloths where it sold tables (ADR 058).
            var data = new CosmeticsData { schemaVersion = 2, equippedTableId = table };
            if (table != "himalayan-ivory")
            {
                data.boughtTableIds.Add(table);
            }

            _store.Put(CosmeticsService.SaveKey, data);
            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Table, cloth));
            Assert.IsFalse(cosmetics.IsOwned(CosmeticKind.Table, table), "the table's id is gone from the shop");
            Assert.AreEqual(500, _wallet.Coins);
        }

        [Test]
        public void TheTablesBoughtOnDisk_AreKeptAsCloths_WhenTheFileIsNextSaved()
        {
            var data = new CosmeticsData { schemaVersion = 2, equippedTableId = "royal-crimson" };
            data.boughtTableIds.Add("royal-crimson");
            data.boughtTableIds.Add("heritage-walnut");
            _store.Put(CosmeticsService.SaveKey, data);
            var cosmetics = new CosmeticsService(_store, _wallet);

            cosmetics.Equip(CosmeticKind.Theme, "monastery-garden");

            CosmeticsData saved = _store.Peek<CosmeticsData>(CosmeticsService.SaveKey);
            CollectionAssert.AreEqual(new[] { "indigo-dhaka", "dusk-himalaya" }, saved.boughtTableIds);
            Assert.AreEqual("dusk-himalaya", saved.equippedTableId);
        }

        /// <summary>
        /// Durbar Square took the Himalayan Vista's place as the free background (the owner, 30 Oct
        /// 2026: "use durbar square as mainmenu bg image"). Every player had the Vista equipped unless
        /// they chose another: a file that names it has the Durbar Square theme, with the rest of the
        /// file as it was, and the next save writes Durbar Square.
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
            }

            _store.Put(CosmeticsService.SaveKey, data);
            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Theme), "equipped");
            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Background));
            Assert.IsFalse(cosmetics.IsOwned(CosmeticKind.Background, "himalayan-vista"), "the Vista's id is gone from the shop");
            CollectionAssert.DoesNotContain(cosmetics.Catalog(CosmeticKind.Theme).Select(i => i.Id), "himalayan-vista");
            Assert.Throws<ArgumentException>(() => cosmetics.Equip(CosmeticKind.Theme, "himalayan-vista"));
            Assert.AreEqual(500, _wallet.Coins);
            if (version > 1)
            {
                Assert.AreEqual("kathmandu-gold", cosmetics.Equipped(CosmeticKind.Deck), "the rest of the file is kept");
                Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Theme, "pokhara-lakeside"), "what was bought stays bought");
            }

            _wallet.Add(250);
            Assert.AreEqual(PurchaseResult.Bought, cosmetics.Buy(CosmeticKind.Theme, "golden-terraces"), "a purchase writes the file");

            CosmeticsData saved = _store.Peek<CosmeticsData>(CosmeticsService.SaveKey);
            Assert.AreEqual("durbar-square", saved.equippedBackgroundId);
            Assert.AreEqual("durbar-square", saved.equippedThemeId);
            Assert.AreEqual(CosmeticsData.CurrentVersion, saved.schemaVersion);
            Assert.AreEqual("durbar-square", new CosmeticsService(_store, _wallet).Equipped(CosmeticKind.Theme), "after a restart too");
        }

        [Test]
        public void AVistaInTheBoughtList_IsKeptAsDurbarSquare_WhenTheFileIsNextSaved()
        {
            var data = new CosmeticsData { schemaVersion = 2, equippedBackgroundId = "pokhara-lakeside" };
            data.boughtBackgroundIds.Add("himalayan-vista");
            data.boughtBackgroundIds.Add("pokhara-lakeside");
            _store.Put(CosmeticsService.SaveKey, data);
            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("pokhara-lakeside", cosmetics.Equipped(CosmeticKind.Theme));
            cosmetics.Equip(CosmeticKind.Theme, "durbar-square");

            CosmeticsData saved = _store.Peek<CosmeticsData>(CosmeticsService.SaveKey);
            CollectionAssert.AreEqual(new[] { "durbar-square", "pokhara-lakeside" }, saved.boughtBackgroundIds);
            Assert.AreEqual("durbar-square", saved.equippedBackgroundId);
        }

        /// <summary>
        /// The owner (5 Oct 2026) kept three backgrounds. A file that still has one of the three that
        /// left the shop equipped, or listed as bought, gets the default theme (Durbar Square) like any
        /// other id the build doesn't know: the old one is neither owned, sold nor equippable, and the
        /// next save writes the default (the unknown purchase stays in the file, as every one does).
        /// </summary>
        [TestCase("kathmandu-courtyard")]
        [TestCase("village-terrace")]
        [TestCase("vintage-home")]
        public void ABackgroundThatLeftTheShop_OnDisk_FallsBackToDurbarSquare(string removed)
        {
            var data = new CosmeticsData { schemaVersion = 2, selectedDeckId = "kathmandu-gold", equippedBackgroundId = removed };
            data.boughtDeckIds.Add("kathmandu-gold");
            data.boughtBackgroundIds.Add(removed);
            _store.Put(CosmeticsService.SaveKey, data);

            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Theme), "equipped");
            Assert.IsFalse(cosmetics.IsOwned(CosmeticKind.Background, removed), "bought");
            CollectionAssert.DoesNotContain(cosmetics.Catalog(CosmeticKind.Background).Select(i => i.Id), removed);
            Assert.AreEqual(PurchaseResult.UnknownItem, cosmetics.Buy(CosmeticKind.Theme, removed));
            Assert.Throws<ArgumentException>(() => cosmetics.Equip(CosmeticKind.Theme, removed));
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
            data.boughtThemeIds.Add("festival-gold");
            data.boughtThemeIds.Add("prayer-flag-pass");
            _store.Put(CosmeticsService.SaveKey, data);
            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.IsTrue(cosmetics.IsOwned(CosmeticKind.Theme, "prayer-flag-pass"));

            cosmetics.Equip(CosmeticKind.Theme, "prayer-flag-pass");

            CollectionAssert.AreEqual(new[] { "festival-gold", "prayer-flag-pass" }, _store.Peek<CosmeticsData>(CosmeticsService.SaveKey).boughtThemeIds);
        }

        [Test]
        public void FileFromANewerVersion_SellsNothing_AndIsNeverOverwritten()
        {
            _store.Put(CosmeticsService.SaveKey, new CosmeticsData { schemaVersion = CosmeticsData.CurrentVersion + 1, equippedThemeId = "monastery-garden" });
            var cosmetics = new CosmeticsService(_store, _wallet);

            Assert.IsTrue(cosmetics.IsReadOnly);
            Assert.AreEqual(PurchaseResult.UnknownItem, cosmetics.Buy(CosmeticKind.Theme, "himalayan-sunrise"), "this build couldn't keep the purchase");
            Assert.AreEqual(PurchaseResult.UnknownItem, cosmetics.Buy(CosmeticKind.Deck, "himalayan-ivory"));
            Assert.AreEqual(500, _wallet.Coins);

            // The session starts at the defaults, the only items owned without buying (6 Oct 2026).
            Assert.AreEqual("durbar-square", cosmetics.Equipped(CosmeticKind.Theme));
            Assert.Throws<InvalidOperationException>(() => cosmetics.Equip(CosmeticKind.Theme, "monastery-garden"));
            cosmetics.Equip(CosmeticKind.Theme, "durbar-square");

            Assert.AreEqual(0, _store.SaveCount);
            Assert.AreEqual("monastery-garden", _store.Peek<CosmeticsData>(CosmeticsService.SaveKey).equippedThemeId);
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
            Assert.AreEqual(PurchaseResult.AlreadyOwned, decksOnly.Buy(CosmeticKind.Theme, "durbar-square"));
            Assert.Throws<InvalidOperationException>(() => decksOnly.Buy(CosmeticKind.Theme, "tihar-lights"));
            Assert.Throws<InvalidOperationException>(() => decksOnly.Buy(CosmeticKind.Deck, "kathmandu-gold"));
            Assert.IsFalse(decksOnly.IsOwned(CosmeticKind.Theme, "tihar-lights"));
            Assert.Throws<InvalidOperationException>(() => decksOnly.SelectDeck("kathmandu-gold"));
        }

        private static void AssertOwned(ICosmeticsShop shop, CosmeticKind kind, string[] ids, params bool[] owned)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.AreEqual(owned[i], shop.IsOwned(kind, ids[i]), kind + " " + ids[i]);
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
