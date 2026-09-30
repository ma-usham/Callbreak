using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Sessions
{
    /// <summary>
    /// GDD §8 "Resume your match?" and §22 "a snapshot that fails to load is discarded": Resume is
    /// offered only for a save the game can really continue.
    /// </summary>
    public sealed class OfflineMatchStoreTests
    {
        private InMemorySaveStore _saves;
        private OfflineMatchStore _store;

        [SetUp]
        public void SetUp()
        {
            _saves = new InMemorySaveStore();
            _store = new OfflineMatchStore(_saves);
        }

        [Test]
        public void HasSave_NoFile_IsFalse()
        {
            Assert.IsFalse(_store.HasSave);
            Assert.IsFalse(_store.TryLoad(out OfflineMatchSave save));
            Assert.IsNull(save);
        }

        [Test]
        public void HasSave_UnfinishedMatch_IsTrueAndTryLoadGivesItBack()
        {
            OfflineMatchSave saved = UnfinishedMatch();

            _store.Save(saved);

            Assert.IsTrue(_store.HasSave);
            Assert.IsTrue(_store.TryLoad(out OfflineMatchSave loaded));
            Assert.AreEqual(saved.match.matchId, loaded.match.matchId);
            Assert.AreEqual(saved.rngState, loaded.rngState);
            Assert.AreEqual(saved.difficulty, loaded.difficulty);
        }

        [Test]
        public void HasSave_FileFromAnotherSchema_IsFalse()
        {
            OfflineMatchSave save = UnfinishedMatch();
            save.schemaVersion = 2;
            _saves.Put(OfflineMatchStore.Key, save);

            AssertNothingToResume("a save from another schema");
        }

        [Test]
        public void HasSave_FileWithoutAMatch_IsFalse()
        {
            _saves.Put(OfflineMatchStore.Key, new OfflineMatchSave());
            AssertNothingToResume("no match at all");

            // What a JSON store makes of a file that has no match in it: an empty one.
            _saves.Put(OfflineMatchStore.Key, new OfflineMatchSave { match = new MatchSnapshot(), rngIncrement = 1UL });
            AssertNothingToResume("an empty match");
        }

        [Test]
        public void HasSave_MatchTheEngineRefuses_IsFalse()
        {
            OfflineMatchSave otherRules = UnfinishedMatch();
            otherRules.match.ruleSetId = "house-rules";
            _saves.Put(OfflineMatchStore.Key, otherRules);
            AssertNothingToResume("a match under rules this build does not play");

            OfflineMatchSave newerSnapshot = UnfinishedMatch();
            newerSnapshot.match.schemaVersion = MatchSnapshot.CurrentSchemaVersion + 1;
            _saves.Put(OfflineMatchStore.Key, newerSnapshot);
            AssertNothingToResume("a snapshot from another snapshot schema");

            OfflineMatchSave missingCards = UnfinishedMatch();
            missingCards.match.seats[(int)Seat.East].hand = new int[0];
            _saves.Put(OfflineMatchStore.Key, missingCards);
            AssertNothingToResume("an impossible match");
        }

        [Test]
        public void HasSave_GeneratorStateThatCannotBeRebuilt_IsFalseAndDoesNotThrow()
        {
            OfflineMatchSave save = UnfinishedMatch();
            save.rngIncrement = 0UL;
            _saves.Put(OfflineMatchStore.Key, save);

            AssertNothingToResume("a PCG increment is always odd");
        }

        [Test]
        public void HasSave_MatchWithNothingLeftToPlay_IsFalse()
        {
            var rng = new Pcg32Random(11UL);
            var engine = new MatchEngine(RuleSet.NepaliStandard, rng, TestSetups.With(false, true, true, true));
            _saves.Put(OfflineMatchStore.Key, SaveOf(engine, rng));
            AssertNothingToResume("a match that was never dealt");

            var driver = new MatchDriver(engine, randomPolicy: false);
            driver.Start();
            driver.PlayToEnd();
            _saves.Put(OfflineMatchStore.Key, SaveOf(engine, rng));
            AssertNothingToResume("a finished match");
        }

        [Test]
        public void Clear_RemovesAFileThatCannotBeResumedToo()
        {
            _saves.Put(OfflineMatchStore.Key, new OfflineMatchSave());

            _store.Clear();

            Assert.IsFalse(_saves.Exists(OfflineMatchStore.Key));
        }

        private void AssertNothingToResume(string why)
        {
            Assert.IsFalse(_store.HasSave, why);
            Assert.IsFalse(_store.TryLoad(out OfflineMatchSave save), why);
            Assert.IsNull(save, why);
        }

        /// <summary>A match a few turns into its first round, as an offline session saves it.</summary>
        private static OfflineMatchSave UnfinishedMatch()
        {
            var rng = new Pcg32Random(7UL);
            var engine = new MatchEngine(RuleSet.NepaliStandard, rng, TestSetups.With(false, true, true, true));
            var driver = new MatchDriver(engine, randomPolicy: false);
            driver.Start();
            for (int i = 0; i < 9; i++)
            {
                driver.Step();
            }

            Assert.AreEqual(Phase.Playing, engine.Phase);
            return SaveOf(engine, rng);
        }

        private static OfflineMatchSave SaveOf(MatchEngine engine, Pcg32Random rng)
        {
            return new OfflineMatchSave
            {
                match = engine.Snapshot(),
                difficulty = (int)BotDifficulty.Hard,
                seed = 7UL,
                rngState = rng.State,
                rngIncrement = rng.Increment,
            };
        }
    }
}
