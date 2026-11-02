using System;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §13 and screen 15: the three Figma decks the game ships (ADR 068), the default, rejecting unknown ids, and (since 6 Oct 2026) decks not bought.</summary>
    public sealed class CosmeticsServiceTests
    {
        private InMemorySaveStore _store;
        private CosmeticsService _cosmetics;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemorySaveStore();
            _cosmetics = new CosmeticsService(_store);
        }

        [Test]
        public void ThreeDecks_ClassicSelectedByDefault()
        {
            CollectionAssert.AreEqual(new[] { "classic", "mithila-ink", "kathmandu-royal" }, _cosmetics.DeckIds);
            Assert.AreEqual("classic", _cosmetics.SelectedDeckId);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void SelectDeck_SavesAndRaisesChanged_AndSurvivesARestart()
        {
            var wallet = new Wallet(_store);
            wallet.Add(CosmeticCatalog.Decks.KathmanduRoyalPrice - WalletRules.StartingCoins);
            var cosmetics = new CosmeticsService(_store, wallet);
            Assert.AreEqual(PurchaseResult.Bought, cosmetics.Buy(CosmeticKind.Deck, "kathmandu-royal"), "the deck's price (ADR 077)");
            int changed = 0;
            cosmetics.Changed += () => changed++;

            cosmetics.SelectDeck("kathmandu-royal");

            Assert.AreEqual("kathmandu-royal", cosmetics.SelectedDeckId);
            Assert.AreEqual(1, changed);
            Assert.AreEqual("kathmandu-royal", new CosmeticsService(_store).SelectedDeckId);
        }

        [TestCase("mithila-ink")]
        [TestCase("kathmandu-royal")]
        public void SelectDeck_ADeckNotBought_IsRefused(string deckId)
        {
            Assert.Throws<InvalidOperationException>(() => _cosmetics.SelectDeck(deckId));
            Assert.AreEqual("classic", _cosmetics.SelectedDeckId);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void SelectDeck_TheSelectedOne_DoesNothing()
        {
            int changed = 0;
            _cosmetics.Changed += () => changed++;

            _cosmetics.SelectDeck("classic");

            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Classic")]
        [TestCase("classic-crimson")]
        [TestCase("vintage")]
        [TestCase("dashain-festival")]
        public void SelectDeck_UnknownId_IsRejected(string deckId)
        {
            Assert.Throws<ArgumentException>(() => _cosmetics.SelectDeck(deckId));
            Assert.AreEqual("classic", _cosmetics.SelectedDeckId);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void UnknownDeckOnDisk_FallsBackToClassic()
        {
            _store.Put(CosmeticsService.SaveKey, new CosmeticsData { selectedDeckId = "retired-seasonal" });

            Assert.AreEqual("classic", new CosmeticsService(_store).SelectedDeckId);
        }

        [Test]
        public void FileFromANewerVersion_IsIgnored_AndNeverOverwritten()
        {
            _store.Put(CosmeticsService.SaveKey, new CosmeticsData { schemaVersion = CosmeticsData.CurrentVersion + 1, selectedDeckId = "mithila-ink" });
            var cosmetics = new CosmeticsService(_store);

            Assert.IsTrue(cosmetics.IsReadOnly);
            Assert.AreEqual("classic", cosmetics.SelectedDeckId);

            Assert.Throws<InvalidOperationException>(() => cosmetics.SelectDeck("kathmandu-royal"), "nothing bought is read from a newer file");
            cosmetics.SelectDeck("classic");

            Assert.AreEqual(0, _store.SaveCount);
            Assert.AreEqual("mithila-ink", _store.Peek<CosmeticsData>(CosmeticsService.SaveKey).selectedDeckId);
        }
    }
}
