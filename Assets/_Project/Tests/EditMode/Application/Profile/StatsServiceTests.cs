using System;
using Callbreak.Application.Profile;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §10 and §15: stats change exactly once per match id; wins, win rate and best score.</summary>
    public sealed class StatsServiceTests
    {
        private InMemorySaveStore _store;
        private StatsService _stats;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemorySaveStore();
            _stats = new StatsService(_store);
        }

        [Test]
        public void NoFile_IsAllZero()
        {
            PlayerStats current = _stats.Current;

            Assert.AreEqual(0, current.gamesPlayed);
            Assert.AreEqual(0, current.wins);
            Assert.IsFalse(current.hasBest);
            Assert.AreEqual(0f, current.WinRate);
        }

        [Test]
        public void FirstPlace_CountsAGameAWinAndTheBest()
        {
            int changed = 0;
            _stats.Changed += () => changed++;

            _stats.RecordMatch("m1", 1, 213, false);

            PlayerStats current = _stats.Current;
            Assert.AreEqual(1, current.gamesPlayed);
            Assert.AreEqual(1, current.wins);
            Assert.IsTrue(current.hasBest);
            Assert.AreEqual(213, current.bestTotalTenths);
            Assert.AreEqual(1f, current.WinRate);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(1, _store.SavesOf(StatsService.SaveKey));
        }

        [Test]
        public void SharedFirstPlace_IsAWin()
        {
            // Tied players share the place (GDD §4): both are recorded with rank 1.
            _stats.RecordMatch("m1", 1, 161, false);
            _stats.RecordMatch("m2", 2, 120, false);

            Assert.AreEqual(2, _stats.Current.gamesPlayed);
            Assert.AreEqual(1, _stats.Current.wins);
            Assert.AreEqual(0.5f, _stats.Current.WinRate);
        }

        [Test]
        public void SameMatchId_IsRecordedOnce()
        {
            int changed = 0;
            _stats.Changed += () => changed++;

            _stats.RecordMatch("m1", 1, 213, false);
            _stats.RecordMatch("m1", 1, 213, false);
            _stats.RecordMatch("m1", 3, 500, false);

            Assert.AreEqual(1, _stats.Current.gamesPlayed);
            Assert.AreEqual(213, _stats.Current.bestTotalTenths);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(1, _store.SaveCount);
        }

        [Test]
        public void Best_IsTheHighestFinalTotal()
        {
            _stats.RecordMatch("m1", 2, 150, false);
            _stats.RecordMatch("m2", 3, 120, false);
            Assert.AreEqual(150, _stats.Current.bestTotalTenths);

            _stats.RecordMatch("m3", 1, 213, false);
            Assert.AreEqual(213, _stats.Current.bestTotalTenths);
        }

        [Test]
        public void Best_CanBeNegative_WhenThatIsAllThereIs()
        {
            _stats.RecordMatch("m1", 4, -30, false);

            Assert.IsTrue(_stats.Current.hasBest);
            Assert.AreEqual(-30, _stats.Current.bestTotalTenths);
        }

        [Test]
        public void AbandonedMatch_CountsAsPlayedAndLost_AndCannotSetABest()
        {
            _stats.RecordMatch("left", 1, 400, true);

            PlayerStats current = _stats.Current;
            Assert.AreEqual(1, current.gamesPlayed);
            Assert.AreEqual(0, current.wins);
            Assert.IsFalse(current.hasBest);

            _stats.RecordMatch("m2", 2, 100, false);
            _stats.RecordMatch("left2", 1, 999, true);
            Assert.AreEqual(100, _stats.Current.bestTotalTenths);
        }

        [Test]
        public void RemembersTheLastFiftyMatchIds()
        {
            for (int i = 0; i <= StatsService.RememberedMatches; i++)
            {
                _stats.RecordMatch("m" + i, 2, 10, false);
            }

            Assert.AreEqual(51, _stats.Current.gamesPlayed);

            _stats.RecordMatch("m" + StatsService.RememberedMatches, 2, 10, false);
            Assert.AreEqual(51, _stats.Current.gamesPlayed, "a recent id is still remembered");

            _stats.RecordMatch("m0", 2, 10, false);
            Assert.AreEqual(52, _stats.Current.gamesPlayed, "the oldest id was forgotten");
            Assert.AreEqual(StatsService.RememberedMatches, _store.Peek<StatsData>(StatsService.SaveKey).recentMatchIds.Count);
        }

        [Test]
        public void StatsAndRecordedIds_SurviveARestart()
        {
            _stats.RecordMatch("m1", 1, 213, false);
            _stats.RecordMatch("m2", 3, 90, false);

            var reloaded = new StatsService(_store);
            reloaded.RecordMatch("m2", 3, 90, false);

            Assert.AreEqual(2, reloaded.Current.gamesPlayed);
            Assert.AreEqual(1, reloaded.Current.wins);
            Assert.AreEqual(213, reloaded.Current.bestTotalTenths);
        }

        [Test]
        public void Current_IsASnapshot()
        {
            _stats.RecordMatch("m1", 1, 213, false);

            _stats.Current.wins = 99;

            Assert.AreEqual(1, _stats.Current.wins);
        }

        [Test]
        public void RecordMatch_RejectsAMissingIdOrARankBelowOne()
        {
            Assert.Throws<ArgumentException>(() => _stats.RecordMatch(null, 1, 10, false));
            Assert.Throws<ArgumentException>(() => _stats.RecordMatch("", 1, 10, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => _stats.RecordMatch("m1", 0, 10, false));
            Assert.AreEqual(0, _stats.Current.gamesPlayed);
        }

        [Test]
        public void FileFromANewerVersion_IsIgnored_AndNeverOverwritten()
        {
            _store.Put(StatsService.SaveKey, new StatsData { schemaVersion = StatsData.CurrentVersion + 1, gamesPlayed = 40, wins = 12 });
            var stats = new StatsService(_store);

            Assert.IsTrue(stats.IsReadOnly);
            Assert.AreEqual(0, stats.Current.gamesPlayed);

            stats.RecordMatch("m1", 1, 100, false);

            Assert.AreEqual(1, stats.Current.gamesPlayed);
            Assert.AreEqual(0, _store.SaveCount);
            Assert.AreEqual(40, _store.Peek<StatsData>(StatsService.SaveKey).gamesPlayed);
        }

        [Test]
        public void InconsistentCountersOnDisk_AreRepaired()
        {
            _store.Put(StatsService.SaveKey, new StatsData { gamesPlayed = 3, wins = 7, recentMatchIds = null });

            var stats = new StatsService(_store);
            stats.RecordMatch("m1", 2, 10, false);

            Assert.AreEqual(4, stats.Current.gamesPlayed);
            Assert.AreEqual(3, stats.Current.wins);
        }
    }
}
