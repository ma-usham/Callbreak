using System;
using Callbreak.Application.Economy;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The online match the player picks on Play Online (mockups 23 and 24): Quick Match until a stake
    /// of 50 or 100 is chosen. The stake the chips start on (the owner, 5 Oct 2026) is the last one the
    /// player chose, on a chip or by playing it; it is saved, survives a restart and a Quick Match, and
    /// starts on 50.
    /// </summary>
    public sealed class OnlineMatchChoiceTests
    {
        private InMemorySaveStore _saves;

        [SetUp]
        public void SetUp()
        {
            _saves = new InMemorySaveStore();
        }

        [Test]
        public void StartsOnQuickMatch()
        {
            Assert.AreEqual(StakeRules.None, new OnlineMatchChoice(_saves).Stake);
        }

        [Test]
        public void ANewPlayer_StartsTheChipsOnTheSmallestStake_AndNothingIsSaved()
        {
            var choice = new OnlineMatchChoice(_saves);

            Assert.AreEqual(50, choice.LastStake);
            Assert.AreEqual(StakeRules.Stakes[0], choice.LastStake);
            Assert.AreEqual(0, _saves.SaveCount, "loading writes nothing");
        }

        [TestCase(50)]
        [TestCase(100)]
        [TestCase(0)]
        public void Choose_QuickMatchOrAStake_IsKept(int stake)
        {
            var choice = new OnlineMatchChoice(_saves);
            choice.Choose(100);

            choice.Choose(stake);

            Assert.AreEqual(stake, choice.Stake);
        }

        [TestCase(25)]
        [TestCase(-50)]
        [TestCase(200)]
        public void Choose_AnyOtherAmount_Throws_AndKeepsTheChoice(int stake)
        {
            var choice = new OnlineMatchChoice(_saves);
            choice.Choose(100);

            Assert.Throws<ArgumentOutOfRangeException>(() => choice.Choose(stake));

            Assert.AreEqual(100, choice.Stake);
            Assert.AreEqual(100, choice.LastStake);
        }

        [Test]
        public void PlayingAStake_IsTheLastStake_AndIsSaved()
        {
            var choice = new OnlineMatchChoice(_saves);

            choice.Choose(100);

            Assert.AreEqual(100, choice.LastStake);
            Assert.AreEqual(1, _saves.SavesOf(OnlineMatchChoice.SaveKey));
            Assert.AreEqual(100, _saves.Peek<OnlineMatchChoiceData>(OnlineMatchChoice.SaveKey).lastStake);
        }

        [Test]
        public void QuickMatch_DoesNotOverwriteTheLastStake()
        {
            var choice = new OnlineMatchChoice(_saves);
            choice.Choose(100);
            int saves = _saves.SaveCount;

            choice.Choose(StakeRules.None);

            Assert.AreEqual(StakeRules.None, choice.Stake, "the next search is a Quick Match");
            Assert.AreEqual(100, choice.LastStake, "the chips keep 100");
            Assert.AreEqual(saves, _saves.SaveCount, "and nothing is written");
            Assert.AreEqual(100, new OnlineMatchChoice(_saves).LastStake, "after a restart too");
        }

        [Test]
        public void TheLastStake_RoundTripsThroughTheSaveFile()
        {
            var before = new OnlineMatchChoice(_saves);
            before.RememberStake(100);
            before.Choose(StakeRules.None);

            var after = new OnlineMatchChoice(_saves);

            Assert.AreEqual(100, after.LastStake, "the chips start on 100 after a restart");
            Assert.AreEqual(StakeRules.None, after.Stake, "the match itself is not saved: each launch starts on Quick Match");
            Assert.AreEqual(OnlineMatchChoiceData.CurrentVersion, _saves.Peek<OnlineMatchChoiceData>(OnlineMatchChoice.SaveKey).schemaVersion);
        }

        [Test]
        public void RememberStake_ChangesOnlyTheChips_NotTheMatchToLookFor()
        {
            var choice = new OnlineMatchChoice(_saves);
            choice.Choose(StakeRules.None);

            choice.RememberStake(100);

            Assert.AreEqual(100, choice.LastStake);
            Assert.AreEqual(StakeRules.None, choice.Stake, "a chip only shows the stake; Play chooses the match");
            Assert.AreEqual(100, _saves.Peek<OnlineMatchChoiceData>(OnlineMatchChoice.SaveKey).lastStake);

            choice.RememberStake(50);
            Assert.AreEqual(50, new OnlineMatchChoice(_saves).LastStake, "and back");
        }

        [Test]
        public void RememberingTheStakeAlreadyRemembered_WritesNothing()
        {
            var choice = new OnlineMatchChoice(_saves);
            choice.RememberStake(100);

            choice.RememberStake(100);
            choice.Choose(100);

            Assert.AreEqual(1, _saves.SavesOf(OnlineMatchChoice.SaveKey));
        }

        [TestCase(0)]
        [TestCase(25)]
        [TestCase(-50)]
        [TestCase(200)]
        public void RememberStake_AnyOtherAmount_Throws_AndKeepsTheLastStake(int stake)
        {
            var choice = new OnlineMatchChoice(_saves);
            choice.RememberStake(100);

            Assert.Throws<ArgumentOutOfRangeException>(() => choice.RememberStake(stake));

            Assert.AreEqual(100, choice.LastStake);
            Assert.AreEqual(100, new OnlineMatchChoice(_saves).LastStake);
        }

        [TestCase(0)]
        [TestCase(75)]
        [TestCase(-100)]
        [TestCase(500)]
        public void ASavedAmount_ThatIsNotAStake_StartsTheChipsOnTheSmallestStake(int saved)
        {
            _saves.Put(OnlineMatchChoice.SaveKey, new OnlineMatchChoiceData { lastStake = saved });

            Assert.AreEqual(50, new OnlineMatchChoice(_saves).LastStake);
        }

        [Test]
        public void AFileFromANewerVersion_IsIgnored_AndNeverOverwritten()
        {
            var newer = new OnlineMatchChoiceData { schemaVersion = OnlineMatchChoiceData.CurrentVersion + 1, lastStake = 100 };
            _saves.Put(OnlineMatchChoice.SaveKey, newer);
            var choice = new OnlineMatchChoice(_saves);

            Assert.IsTrue(choice.IsReadOnly);
            Assert.AreEqual(50, choice.LastStake, "a file this build can't read is not trusted");

            choice.Choose(100);
            choice.RememberStake(50);
            choice.RememberStake(100);

            Assert.AreEqual(100, choice.LastStake, "remembered for this session");
            Assert.AreEqual(0, _saves.SavesOf(OnlineMatchChoice.SaveKey), "the newer file is left alone");
            Assert.AreEqual(newer.schemaVersion, _saves.Peek<OnlineMatchChoiceData>(OnlineMatchChoice.SaveKey).schemaVersion);
        }

        [Test]
        public void NeedsASaveStore()
        {
            Assert.Throws<ArgumentNullException>(() => new OnlineMatchChoice(null));
        }
    }
}
