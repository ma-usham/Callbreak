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

        // GDD §15 "Tracked for later: calls-met %, average call, win streak".

        [Test]
        public void NoFile_TheCallAndStreakFiguresAreZero()
        {
            PlayerStats current = _stats.Current;

            Assert.AreEqual(0, current.roundsPlayed);
            Assert.AreEqual(0, current.roundsMet);
            Assert.AreEqual(0, current.callsTotal);
            Assert.AreEqual(0f, current.CallsMetRate);
            Assert.AreEqual(0f, current.AverageCall);
            Assert.AreEqual(0, current.winStreak);
            Assert.AreEqual(0, current.bestWinStreak);
        }

        [Test]
        public void FinishedMatch_AddsItsRoundsItsMetCallsAndItsCalls()
        {
            _stats.RecordMatch("m1", 2, 120, false, roundsPlayed: 5, roundsMet: 3, callsTotal: 14);

            PlayerStats first = _stats.Current;
            Assert.AreEqual(5, first.roundsPlayed);
            Assert.AreEqual(3, first.roundsMet);
            Assert.AreEqual(14, first.callsTotal);
            Assert.AreEqual(0.6f, first.CallsMetRate, 0.0001f);
            Assert.AreEqual(2.8f, first.AverageCall, 0.0001f);

            _stats.RecordMatch("m2", 1, 161, false, roundsPlayed: 5, roundsMet: 5, callsTotal: 11);

            PlayerStats second = _stats.Current;
            Assert.AreEqual(10, second.roundsPlayed);
            Assert.AreEqual(8, second.roundsMet);
            Assert.AreEqual(25, second.callsTotal);
            Assert.AreEqual(0.8f, second.CallsMetRate, 0.0001f);
            Assert.AreEqual(2.5f, second.AverageCall, 0.0001f);
        }

        [Test]
        public void AbandonedMatch_AddsNoRounds()
        {
            _stats.RecordMatch("m1", 2, 120, false, roundsPlayed: 5, roundsMet: 3, callsTotal: 14);

            _stats.RecordMatch("left", 4, 20, true, roundsPlayed: 2, roundsMet: 1, callsTotal: 6);

            PlayerStats current = _stats.Current;
            Assert.AreEqual(2, current.gamesPlayed, "it still counts as played");
            Assert.AreEqual(5, current.roundsPlayed);
            Assert.AreEqual(3, current.roundsMet);
            Assert.AreEqual(14, current.callsTotal);
        }

        [Test]
        public void WinStreak_CountsWinsInARow_AndALossEndsIt()
        {
            _stats.RecordMatch("m1", 1, 150, false);
            _stats.RecordMatch("m2", 1, 140, false);
            Assert.AreEqual(2, _stats.Current.winStreak);
            Assert.AreEqual(2, _stats.Current.bestWinStreak);

            _stats.RecordMatch("m3", 2, 90, false);
            Assert.AreEqual(0, _stats.Current.winStreak, "second place ends the run");
            Assert.AreEqual(2, _stats.Current.bestWinStreak, "the best run is kept");

            _stats.RecordMatch("m4", 1, 130, false);
            Assert.AreEqual(1, _stats.Current.winStreak);
            Assert.AreEqual(2, _stats.Current.bestWinStreak);

            _stats.RecordMatch("m5", 1, 130, false);
            _stats.RecordMatch("m6", 1, 130, false);
            Assert.AreEqual(3, _stats.Current.winStreak);
            Assert.AreEqual(3, _stats.Current.bestWinStreak);
        }

        [Test]
        public void WinStreak_AbandonedMatchEndsIt_EvenFromFirstPlace()
        {
            _stats.RecordMatch("m1", 1, 150, false);
            _stats.RecordMatch("m2", 1, 140, false);

            _stats.RecordMatch("left", 1, 400, true);

            Assert.AreEqual(0, _stats.Current.winStreak);
            Assert.AreEqual(2, _stats.Current.bestWinStreak);
            Assert.AreEqual(2, _stats.Current.wins);
        }

        [Test]
        public void SameMatchId_CountsItsRoundsAndItsPlaceInTheStreakOnce()
        {
            _stats.RecordMatch("m1", 1, 213, false, roundsPlayed: 5, roundsMet: 4, callsTotal: 15);
            _stats.RecordMatch("m1", 1, 213, false, roundsPlayed: 5, roundsMet: 4, callsTotal: 15);
            _stats.RecordMatch("m1", 3, 50, false, roundsPlayed: 5, roundsMet: 0, callsTotal: 40);
            _stats.RecordMatch("m1", 1, 213, true);

            PlayerStats current = _stats.Current;
            Assert.AreEqual(5, current.roundsPlayed);
            Assert.AreEqual(4, current.roundsMet);
            Assert.AreEqual(15, current.callsTotal);
            Assert.AreEqual(1, current.winStreak, "a repeat neither extends the run nor ends it");
            Assert.AreEqual(1, current.bestWinStreak);
        }

        [Test]
        public void CallAndStreakFigures_SurviveARestart()
        {
            _stats.RecordMatch("m1", 1, 213, false, roundsPlayed: 5, roundsMet: 4, callsTotal: 15);
            _stats.RecordMatch("m2", 1, 190, false, roundsPlayed: 5, roundsMet: 5, callsTotal: 12);
            _stats.RecordMatch("m3", 3, 90, false, roundsPlayed: 5, roundsMet: 2, callsTotal: 18);
            _stats.RecordMatch("m4", 1, 170, false, roundsPlayed: 5, roundsMet: 3, callsTotal: 13);

            PlayerStats reloaded = new StatsService(_store).Current;

            Assert.AreEqual(20, reloaded.roundsPlayed);
            Assert.AreEqual(14, reloaded.roundsMet);
            Assert.AreEqual(58, reloaded.callsTotal);
            Assert.AreEqual(1, reloaded.winStreak);
            Assert.AreEqual(2, reloaded.bestWinStreak);
        }

        [Test]
        public void FileFromBeforeTheseFigures_KeepsItsCounters_StartsTheNewOnesAtZero_AndIsSavedAtTheCurrentVersion()
        {
            // Version 1 had no rounds, calls or streaks: nothing can fill them in afterwards.
            Assert.Greater(StatsData.CurrentVersion, 1, "adding fields bumped the version");
            var old = new StatsData { schemaVersion = 1, gamesPlayed = 40, wins = 12, hasBest = true, bestTotalTenths = 213 };
            old.recentMatchIds.Add("old-match");
            _store.Put(StatsService.SaveKey, old);
            var stats = new StatsService(_store);

            Assert.IsFalse(stats.IsReadOnly);
            Assert.AreEqual(40, stats.Current.gamesPlayed);
            Assert.AreEqual(12, stats.Current.wins);
            Assert.AreEqual(213, stats.Current.bestTotalTenths);
            Assert.AreEqual(0, stats.Current.roundsPlayed);
            Assert.AreEqual(0f, stats.Current.CallsMetRate);
            Assert.AreEqual(0, stats.Current.winStreak);
            Assert.AreEqual(0, stats.Current.bestWinStreak);

            stats.RecordMatch("old-match", 1, 100, false, roundsPlayed: 5, roundsMet: 5, callsTotal: 10);
            Assert.AreEqual(40, stats.Current.gamesPlayed, "the ids it remembered still count");

            stats.RecordMatch("m1", 1, 100, false, roundsPlayed: 5, roundsMet: 4, callsTotal: 16);

            StatsData saved = _store.Peek<StatsData>(StatsService.SaveKey);
            Assert.AreEqual(StatsData.CurrentVersion, saved.schemaVersion);
            Assert.AreEqual(41, saved.gamesPlayed);
            Assert.AreEqual(13, saved.wins);
            Assert.AreEqual(5, saved.roundsPlayed);
            Assert.AreEqual(4, saved.roundsMet);
            Assert.AreEqual(16, saved.callsTotal);
            Assert.AreEqual(1, saved.winStreak);
            Assert.AreEqual(1, saved.bestWinStreak);
        }

        [Test]
        public void RecordMatch_RejectsRoundFiguresNoMatchCanHave()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _stats.RecordMatch("m1", 1, 10, false, roundsPlayed: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _stats.RecordMatch("m1", 1, 10, false, roundsPlayed: 5, roundsMet: 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => _stats.RecordMatch("m1", 1, 10, false, roundsPlayed: 5, roundsMet: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _stats.RecordMatch("m1", 1, 10, false, roundsPlayed: 5, roundsMet: 2, callsTotal: -1));
            Assert.AreEqual(0, _stats.Current.gamesPlayed);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void InconsistentCallAndStreakFiguresOnDisk_AreRepaired()
        {
            _store.Put(StatsService.SaveKey, new StatsData
            {
                gamesPlayed = 3,
                wins = 2,
                roundsPlayed = 10,
                roundsMet = 14,
                callsTotal = -5,
                winStreak = 9,
                bestWinStreak = 1,
            });

            PlayerStats current = new StatsService(_store).Current;

            Assert.AreEqual(10, current.roundsMet, "no more rounds met than played");
            Assert.AreEqual(0, current.callsTotal);
            Assert.AreEqual(2, current.winStreak, "no longer than the wins there are");
            Assert.AreEqual(2, current.bestWinStreak, "never shorter than the current run");
        }

        [Test]
        public void Current_IsASnapshotOfTheNewFiguresToo()
        {
            _stats.RecordMatch("m1", 1, 213, false, roundsPlayed: 5, roundsMet: 4, callsTotal: 15);

            PlayerStats taken = _stats.Current;
            taken.roundsPlayed = 99;
            taken.winStreak = 99;

            Assert.AreEqual(5, _stats.Current.roundsPlayed);
            Assert.AreEqual(1, _stats.Current.winStreak);
        }
    }
}
