using System;
using Callbreak.Application.Cosmetics;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §13 and screen 15: the three v1 decks, the default, and rejecting unknown ids.</summary>
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
        public void ThreeDecks_ClassicCrimsonSelectedByDefault()
        {
            CollectionAssert.AreEqual(new[] { "classic-crimson", "himalayan-ivory", "kathmandu-gold" }, _cosmetics.DeckIds);
            Assert.AreEqual("classic-crimson", _cosmetics.SelectedDeckId);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void SelectDeck_SavesAndRaisesChanged_AndSurvivesARestart()
        {
            int changed = 0;
            _cosmetics.Changed += () => changed++;

            _cosmetics.SelectDeck("kathmandu-gold");

            Assert.AreEqual("kathmandu-gold", _cosmetics.SelectedDeckId);
            Assert.AreEqual(1, changed);
            Assert.AreEqual("kathmandu-gold", new CosmeticsService(_store).SelectedDeckId);
        }

        [Test]
        public void SelectDeck_TheSelectedOne_DoesNothing()
        {
            int changed = 0;
            _cosmetics.Changed += () => changed++;

            _cosmetics.SelectDeck("classic-crimson");

            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Classic-Crimson")]
        [TestCase("dashain-festival")]
        public void SelectDeck_UnknownId_IsRejected(string deckId)
        {
            Assert.Throws<ArgumentException>(() => _cosmetics.SelectDeck(deckId));
            Assert.AreEqual("classic-crimson", _cosmetics.SelectedDeckId);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void UnknownDeckOnDisk_FallsBackToClassicCrimson()
        {
            _store.Put(CosmeticsService.SaveKey, new CosmeticsData { selectedDeckId = "retired-seasonal" });

            Assert.AreEqual("classic-crimson", new CosmeticsService(_store).SelectedDeckId);
        }

        [Test]
        public void FileFromANewerVersion_IsIgnored_AndNeverOverwritten()
        {
            _store.Put(CosmeticsService.SaveKey, new CosmeticsData { schemaVersion = CosmeticsData.CurrentVersion + 1, selectedDeckId = "himalayan-ivory" });
            var cosmetics = new CosmeticsService(_store);

            Assert.IsTrue(cosmetics.IsReadOnly);
            Assert.AreEqual("classic-crimson", cosmetics.SelectedDeckId);

            cosmetics.SelectDeck("kathmandu-gold");

            Assert.AreEqual(0, _store.SaveCount);
            Assert.AreEqual("himalayan-ivory", _store.Peek<CosmeticsData>(CosmeticsService.SaveKey).selectedDeckId);
        }
    }
}
