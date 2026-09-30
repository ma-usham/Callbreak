using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Match
{
    public sealed class SnapshotTests
    {
        private static IEnumerable<TestCaseData> Variants()
        {
            yield return new TestCaseData(RuleSet.NepaliStandard, 101UL, false).SetName("Restore_EveryCommandOfAMatch_StandardHumans");
            yield return new TestCaseData(RuleSet.NepaliStandard.With(direction: Direction.Clockwise, autoPlayForcedMoves: true), 102UL, true)
                .SetName("Restore_EveryCommandOfAMatch_ClockwiseForcedMovesMixedBots");
            yield return new TestCaseData(RuleSet.NepaliStandard.With(redealIfNoHonour: true, bigCallBonus: true, maxCall: 13, rounds: 3), 103UL, false)
                .SetName("Restore_EveryCommandOfAMatch_HouseRulesThreeRounds");
        }

        [TestCaseSource(nameof(Variants))]
        public void Restore_BeforeEveryCommand_SameViewsAndIdenticalFuture(RuleSet rules, ulong seed, bool someBots)
        {
            var rng = new Pcg32Random(seed);
            MatchSetup setup = someBots ? TestSetups.With(false, true, false, true) : TestSetups.Humans();
            var engine = new MatchEngine(rules, rng, setup);

            // Lobby, before Start.
            RestoreThenStepBoth(engine, rules, rng, e =>
            {
                e.Start();
                return CommandResult.Accepted;
            });
            int steps = 0;
            while (engine.Phase != Phase.MatchOver)
            {
                RecordedCommand next = NextCommand(engine);
                RestoreThenStepBoth(engine, rules, rng, e => next.ExecuteOn(e));
                steps++;
            }

            // MatchOver restores too, with its standings; both reject further commands alike.
            RestoreThenStepBoth(engine, rules, rng, e => e.Execute(new ReadyNext(Seat.South)));
            Assert.GreaterOrEqual(steps, 16 * rules.Rounds, "each round needs at least 4 call turns and 12 lead turns");
        }

        [Test]
        public void SnapshotTypes_Reflected_OnlyPublicFieldsOfSerializableTypes()
        {
            var allowed = new HashSet<Type>
            {
                typeof(int), typeof(bool), typeof(string), typeof(int[]),
                typeof(SeatSnapshot[]), typeof(TrickSnapshot), typeof(TrickSnapshot[]), typeof(RoundSnapshot[]),
            };

            foreach (Type type in new[] { typeof(MatchSnapshot), typeof(SeatSnapshot), typeof(TrickSnapshot), typeof(RoundSnapshot) })
            {
                Assert.IsTrue(type.IsSealed && type.IsClass, type.Name + " is a sealed class");
                Assert.IsTrue(type.GetCustomAttributes(typeof(SerializableAttribute), false).Length == 1, type.Name + " is [Serializable]");
                Assert.AreEqual(0, type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length, type.Name + " has no properties");
                Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes), type.Name + " has a parameterless constructor");
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.IsTrue(fields.All(f => f.IsPublic && !f.IsInitOnly), type.Name + " fields are public and writable");
                foreach (FieldInfo field in fields)
                {
                    Assert.IsTrue(allowed.Contains(field.FieldType), type.Name + "." + field.Name + " has type " + field.FieldType.Name);
                }
            }
        }

        [Test]
        public void Snapshot_Fields_CarrySchemaVersionAndRuleSetId()
        {
            var engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(3UL), TestSetups.Humans());
            engine.Start();

            MatchSnapshot snapshot = engine.Snapshot();

            Assert.AreEqual(MatchSnapshot.CurrentSchemaVersion, snapshot.schemaVersion);
            Assert.AreEqual(1, snapshot.schemaVersion);
            Assert.AreEqual("nepali-standard", snapshot.ruleSetId);
            Assert.AreEqual("test-match", snapshot.matchId);
            Assert.AreEqual(4, snapshot.seats.Length);
            Assert.AreEqual(13, snapshot.seats[0].hand.Length);
        }

#if !UNITY_5_3_OR_NEWER
        [Test]
        public void Snapshot_JsonRoundTripMidTrick_RestoresEqualViews()
        {
            var driver = new MatchDriver(new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(7UL), TestSetups.Humans()), randomPolicy: true);
            driver.Start();
            for (int i = 0; i < 30; i++)
            {
                driver.Step();
            }

            var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            string json = System.Text.Json.JsonSerializer.Serialize(driver.Engine.Snapshot(), options);
            MatchSnapshot parsed = System.Text.Json.JsonSerializer.Deserialize<MatchSnapshot>(json, options);
            MatchEngine restored = MatchEngine.Restore(parsed, RuleSet.NepaliStandard, new Pcg32Random(1UL));

            StringAssert.Contains("\"schemaVersion\":1", json);
            for (int s = 0; s < 4; s++)
            {
                Assert.AreEqual(driver.Engine.ViewFor((Seat)s), restored.ViewFor((Seat)s));
            }
        }
#endif

        [Test]
        public void TryRestore_CorruptOrForeignSnapshots_RefusedWithAReason()
        {
            MatchSnapshot good = MidTrickSnapshot(out RuleSet rules);
            var corruptions = new List<KeyValuePair<string, Action<MatchSnapshot>>>
            {
                Corrupt("future schema", s => s.schemaVersion = 2),
                Corrupt("other rule set", s => s.ruleSetId = "house-rules"),
                Corrupt("transient phase", s => s.phase = (int)Phase.Dealing),
                Corrupt("phase out of range", s => s.phase = 42),
                Corrupt("seat out of range", s => s.currentSeat = 4),
                Corrupt("three seats", s => s.seats = s.seats.Take(3).ToArray()),
                Corrupt("card id out of range", s => s.seats[0].hand[0] = 60),
                Corrupt("card in two hands", s => s.seats[1].hand[0] = s.seats[0].hand[0]),
                Corrupt("card missing", s => s.seats[2].hand = s.seats[2].hand.Skip(1).ToArray()),
                Corrupt("call out of range", s => s.seats[3].call = 9),
                Corrupt("tricks won tampered", s => s.seats[0].tricksWon += 1),
                Corrupt("turn counter behind the open turn", s => s.turnCounter = s.currentTurn - 1),
                Corrupt("wrong seat to act", s => s.currentSeat = (s.currentSeat + 1) % 4),
                Corrupt("wrong dealer", s => s.dealer = (s.dealer + 1) % 4),
                Corrupt("short completed trick", s => s.completedTricks[0].cards = s.completedTricks[0].cards.Take(3).ToArray()),
                Corrupt("round delta tampered", s => s.rounds[0].deltas[0] += 1),
                Corrupt("null profile", s => s.seats[1] = null),
                Corrupt("missing match id", s => s.matchId = null),
                Corrupt("ended flag on an unfinished match", s => s.matchEnded = true),
                Corrupt("match over without the ended flag", s => s.phase = (int)Phase.MatchOver),
            };

            Assert.IsFalse(MatchEngine.TryRestore(null, rules, new Pcg32Random(1UL), out _, out string nullError));
            StringAssert.Contains("null", nullError);
            Assert.IsTrue(MatchEngine.TryRestore(SnapshotComparer.Clone(good), rules, new Pcg32Random(1UL), out _, out _), "fixture restores");
            foreach (KeyValuePair<string, Action<MatchSnapshot>> corruption in corruptions)
            {
                MatchSnapshot bad = SnapshotComparer.Clone(good);
                corruption.Value(bad);

                bool restored = MatchEngine.TryRestore(bad, rules, new Pcg32Random(1UL), out MatchEngine engine, out string error);

                Assert.IsFalse(restored, corruption.Key + " was accepted");
                Assert.IsNull(engine);
                Assert.IsFalse(string.IsNullOrEmpty(error), corruption.Key + " gave no reason");
                Assert.Throws<ArgumentException>(() => MatchEngine.Restore(bad, rules, new Pcg32Random(1UL)), corruption.Key);
            }
        }

        [Test]
        public void TryRestore_SameIdButDifferentOvertrickValue_RefusedBecauseScoresDisagree()
        {
            MatchSnapshot snapshot = MidTrickSnapshot(out RuleSet rules);
            Assert.IsTrue(HasOvertricks(snapshot), "fixture: every call was 1, so round 1 had overtricks");

            bool restored = MatchEngine.TryRestore(snapshot, rules.With(overtrickTenths: 2), new Pcg32Random(1UL), out _, out string error);

            Assert.IsFalse(restored);
            StringAssert.Contains("deltas", error);
        }

        [Test]
        public void TryRestore_MatchOverWithoutTheEndedFlag_Refused()
        {
            var driver = new MatchDriver(new MatchEngine(RuleSet.NepaliStandard.With(rounds: 1), new Pcg32Random(47UL), TestSetups.AllBots()), randomPolicy: true);
            driver.Start();
            driver.PlayToEnd();
            MatchSnapshot snapshot = driver.Engine.Snapshot();
            Assert.IsTrue(snapshot.matchEnded);

            snapshot.matchEnded = false;
            bool restored = MatchEngine.TryRestore(snapshot, RuleSet.NepaliStandard.With(rounds: 1), new Pcg32Random(1UL), out _, out string error);

            Assert.IsFalse(restored);
            StringAssert.Contains("without final standings", error);
        }

        [Test]
        public void Restore_NullRulesOrRandom_Throws()
        {
            MatchSnapshot snapshot = MidTrickSnapshot(out RuleSet rules);

            Assert.Throws<ArgumentNullException>(() => MatchEngine.Restore(snapshot, null, new Pcg32Random(1UL)));
            Assert.Throws<ArgumentNullException>(() => MatchEngine.Restore(snapshot, rules, null));
        }

        [Test]
        public void Restore_LobbyWithRandomFirstDealer_StartDrawsFromTheNewRandom()
        {
            var original = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(9UL), TestSetups.Humans(firstDealer: null));
            MatchEngine restored = MatchEngine.Restore(original.Snapshot(), RuleSet.NepaliStandard, new Pcg32Random(9UL));

            original.Start();
            restored.Start();

            CollectionAssert.AreEqual(Texts(original), Texts(restored));
        }

        /// <summary>
        /// Snapshots <paramref name="original"/> through a public-fields-only copy, restores it with a copy of
        /// the random state, checks the two engines look identical, then applies <paramref name="step"/> to
        /// both and checks they stay identical: same result, same events, same views.
        /// </summary>
        private static void RestoreThenStepBoth(MatchEngine original, RuleSet rules, Pcg32Random rng, Func<MatchEngine, CommandResult> step)
        {
            MatchEngineStartTests.Drain(original);
            MatchSnapshot snapshot = SnapshotComparer.Clone(original.Snapshot());
            MatchEngine twin = MatchEngine.Restore(snapshot, rules, Pcg32Random.FromState(rng.State, rng.Increment));

            Assert.AreEqual(original.Phase, twin.Phase);
            Assert.AreEqual(original.CurrentSeat, twin.CurrentSeat);
            Assert.AreEqual(original.CurrentTurn, twin.CurrentTurn);
            Assert.AreEqual(original.Round, twin.Round);
            Assert.AreEqual(0, MatchEngineStartTests.Drain(twin).Count, "a restored engine starts with an empty outbox");
            Assert.IsTrue(SnapshotComparer.AreEqual(snapshot, twin.Snapshot()), "the restored engine snapshots identically");
            AssertSameViews(original, twin);

            CommandResult originalResult = step(original);
            CommandResult twinResult = step(twin);

            Assert.AreEqual(originalResult, twinResult);
            CollectionAssert.AreEqual(Texts(original), Texts(twin));
            AssertSameViews(original, twin);
            original.AssertInvariants();
            twin.AssertInvariants();
        }

        private static void AssertSameViews(MatchEngine a, MatchEngine b)
        {
            for (int s = 0; s < 4; s++)
            {
                Assert.AreEqual(a.ViewFor((Seat)s), b.ViewFor((Seat)s), "view of " + (Seat)s);
            }
        }

        private static RecordedCommand NextCommand(MatchEngine engine)
        {
            switch (engine.Phase)
            {
                case Phase.Calling:
                    return RecordedCommand.Of(new PlaceCall(engine.CurrentSeat, 1 + (engine.CurrentTurn.Value % 8), engine.CurrentTurn));
                case Phase.Playing:
                {
                    PlayerView view = engine.ViewFor(engine.CurrentSeat);
                    Callbreak.Core.Cards.CardSet legal = view.LegalCards();
                    return RecordedCommand.Of(new PlayCard(view.Viewer, legal.ElementAt(engine.CurrentTurn.Value % legal.Count), view.CurrentTurn));
                }

                default:
                    for (int s = 0; s < 4; s++)
                    {
                        if (!engine.ViewFor((Seat)s).IsReady((Seat)s))
                        {
                            return RecordedCommand.Of(new ReadyNext((Seat)s));
                        }
                    }

                    throw new InvalidOperationException("Nothing to do in " + engine.Phase);
            }
        }

        private static MatchSnapshot MidTrickSnapshot(out RuleSet rules)
        {
            rules = RuleSet.NepaliStandard;
            var driver = new MatchDriver(new MatchEngine(rules, new Pcg32Random(55UL), TestSetups.Humans(Seat.West)), randomPolicy: false);
            driver.Start();

            // Finish round 1, then stop in round 2 with two tricks done and two cards on the table.
            while (true)
            {
                PlayerView view = driver.Engine.ViewFor(Seat.South);
                if (view.Round == 2 && view.Phase == Phase.Playing && view.CompletedTricks.Count >= 2 && view.CurrentTrick.Count == 2)
                {
                    return driver.Engine.Snapshot();
                }

                driver.Step();
            }
        }

        private static bool HasOvertricks(MatchSnapshot snapshot)
        {
            RoundSnapshot round = snapshot.rounds[0];
            for (int s = 0; s < 4; s++)
            {
                if (round.won[s] > round.calls[s])
                {
                    return true;
                }
            }

            return false;
        }

        private static KeyValuePair<string, Action<MatchSnapshot>> Corrupt(string name, Action<MatchSnapshot> change)
        {
            return new KeyValuePair<string, Action<MatchSnapshot>>(name, change);
        }

        private static List<string> Texts(MatchEngine engine)
        {
            return MatchEngineStartTests.Drain(engine).Select(e => e.ToString()).ToList();
        }
    }
}
