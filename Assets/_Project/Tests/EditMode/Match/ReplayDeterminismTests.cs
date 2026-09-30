using System.Collections.Generic;
using System.Linq;
using Callbreak.Core.Cards;
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
    /// <summary>
    /// GDD §30 "Determinism: seeded bot matches, the §22 invariants hold ... Replay(seed, commands) equals the
    /// recorded event stream". Plays hundreds of full seeded matches under several rule sets with "first legal"
    /// and "pseudo-random legal" policies, injects invalid commands, checks invariants after every command
    /// and replays each match from its command log.
    /// </summary>
    public sealed class ReplayDeterminismTests
    {
        private static readonly object[][] BatchTable =
        {
            new object[] { "standard", RuleSet.NepaliStandard, 80 },
            new object[] { "clockwise", RuleSet.NepaliStandard.With(direction: Direction.Clockwise), 50 },
            new object[] { "loose-follow", RuleSet.NepaliStandard.With(mustBeatLedSuit: false, mustOvertrump: false), 50 },
            new object[] { "strict-trump-forced", RuleSet.NepaliStandard.With(discardWhenCannotOvertrump: false, autoPlayForcedMoves: true), 50 },
            new object[] { "no-trump-rule-manual-last", RuleSet.NepaliStandard.With(mustTrumpWhenVoid: false, autoPlayLastTrick: false), 40 },
            new object[] { "house-rules", RuleSet.NepaliStandard.With(redealIfNoHonour: true, bigCallBonus: true, maxCall: 13, overtrickTenths: 5, rounds: 3), 40 },
        };

        private static IEnumerable<TestCaseData> Batches()
        {
            foreach (object[] row in BatchTable)
            {
                yield return new TestCaseData(row).SetName("SeededMatches_" + row[0] + "_InvariantsHoldAndReplayReproducesEvents");
            }
        }

        [Test]
        public void Batches_Together_PlayAtLeast300FullMatches()
        {
            Assert.GreaterOrEqual(BatchTable.Sum(row => (int)row[2]), 300);
        }

        [TestCaseSource(nameof(Batches))]
        public void SeededMatches_InvariantsHoldAndReplayReproducesEvents(string name, RuleSet rules, int matches)
        {
            int injected = 0;
            for (int m = 0; m < matches; m++)
            {
                ulong seed = SeedFor(name, m);
                MatchSetup setup = SetupFor(m);
                var driver = new MatchDriver(new MatchEngine(rules, new Pcg32Random(seed), setup), randomPolicy: m % 3 != 0, policySeed: seed + 1UL);
                var chaos = new Pcg32Random(seed, 7UL);

                driver.Start();
                do
                {
                    if (chaos.NextInt(4) == 0)
                    {
                        injected += InjectInvalidCommand(driver, chaos) ? 1 : 0;
                    }
                }
                while (driver.Step());

                string label = name + " match " + m + " seed " + seed;
                Assert.AreEqual(Phase.MatchOver, driver.Engine.Phase, label);
                Assert.IsTrue(driver.Events.Last() is MatchEnded, label);
                Assert.AreEqual(rules.Rounds, driver.Events.OfType<RoundScored>().Count(), label);
                Assert.AreEqual(rules.Rounds * 13, driver.Events.OfType<TrickCompleted>().Count(), label);

                List<MatchEvent> replay = MatchReplay.Run(rules, seed, setup, driver.Commands);
                CollectionAssert.AreEqual(Texts(driver.Events), Texts(replay), label);
            }

            Assert.Greater(injected, matches * 10, "invalid commands were exercised");
        }

        [Test]
        public void Replay_SameSeedSameCommands_IdenticalEvents()
        {
            var first = new MatchDriver(new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(71UL), TestSetups.Humans()), randomPolicy: true, policySeed: 3UL);
            var second = new MatchDriver(new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(71UL), TestSetups.Humans()), randomPolicy: true, policySeed: 3UL);

            first.Start();
            first.PlayToEnd();
            second.Start();
            second.PlayToEnd();

            CollectionAssert.AreEqual(Texts(first.Events), Texts(second.Events));
            CollectionAssert.AreEqual(first.Commands, second.Commands);
        }

        [Test]
        public void Replay_DifferentSeeds_DifferentDeals()
        {
            var a = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(1UL), TestSetups.Humans(Seat.West));
            var b = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(2UL), TestSetups.Humans(Seat.West));
            a.Start();
            b.Start();

            Assert.AreNotEqual(a.ViewFor(Seat.South).Hand, b.ViewFor(Seat.South).Hand);
        }

        [Test]
        public void Replay_TruncatedLog_ReproducesThePrefix()
        {
            var driver = new MatchDriver(new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(73UL), TestSetups.Humans()), randomPolicy: true);
            driver.Start();
            for (int i = 0; i < 40; i++)
            {
                driver.Step();
            }

            List<MatchEvent> replay = MatchReplay.Run(RuleSet.NepaliStandard, 73UL, TestSetups.Humans(), driver.Commands);

            CollectionAssert.AreEqual(Texts(driver.Events), Texts(replay));
        }

        private static bool InjectInvalidCommand(MatchDriver driver, Pcg32Random chaos)
        {
            MatchEngine engine = driver.Engine;
            RecordedCommand command;
            RejectReason expected;
            Seat current = engine.CurrentSeat;
            Seat other = current.Next(engine.Rules.Direction);
            TurnId stale = new TurnId(engine.CurrentTurn.Value + 1 + chaos.NextInt(3));
            switch (engine.Phase)
            {
                case Phase.Calling:
                    switch (chaos.NextInt(5))
                    {
                        case 0:
                            command = RecordedCommand.Of(new PlaceCall(other, 3, engine.CurrentTurn));
                            expected = RejectReason.NotYourTurn;
                            break;
                        case 1:
                            command = RecordedCommand.Of(new PlaceCall(current, 3, stale));
                            expected = RejectReason.StaleTurn;
                            break;
                        case 2:
                            command = RecordedCommand.Of(new PlaceCall(current, engine.Rules.MaxCall + 1, engine.CurrentTurn));
                            expected = RejectReason.InvalidCall;
                            break;
                        case 3:
                            command = RecordedCommand.Of(new PlayCard(current, Card.FromId(chaos.NextInt(52)), engine.CurrentTurn));
                            expected = RejectReason.WrongPhase;
                            break;
                        default:
                            command = RecordedCommand.Of(new PlaceCall((Seat)(4 + chaos.NextInt(10)), 3, engine.CurrentTurn));
                            expected = RejectReason.InvalidSeat;
                            break;
                    }

                    break;

                case Phase.Playing:
                {
                    CardSet hand = engine.ViewFor(current).Hand;
                    CardSet illegal = hand - engine.ViewFor(current).LegalCards();
                    CardSet notHeld = CardSet.FullDeck - hand;
                    switch (chaos.NextInt(5))
                    {
                        case 0:
                            // The seat is checked before the card, so any card will do.
                            command = RecordedCommand.Of(new PlayCard(other, Card.FromId(chaos.NextInt(52)), engine.CurrentTurn));
                            expected = RejectReason.NotYourTurn;
                            break;
                        case 1:
                            command = RecordedCommand.Of(new PlayCard(current, hand.Lowest(), stale));
                            expected = RejectReason.StaleTurn;
                            break;
                        case 2:
                            command = RecordedCommand.Of(new PlayCard(current, notHeld.ElementAt(chaos.NextInt(notHeld.Count)), engine.CurrentTurn));
                            expected = RejectReason.CardNotInHand;
                            break;
                        case 3 when !illegal.IsEmpty:
                            command = RecordedCommand.Of(new PlayCard(current, illegal.ElementAt(chaos.NextInt(illegal.Count)), engine.CurrentTurn));
                            expected = RejectReason.IllegalCard;
                            break;
                        default:
                            command = RecordedCommand.Of(new ReadyNext(current));
                            expected = RejectReason.WrongPhase;
                            break;
                    }

                    break;
                }

                case Phase.RoundSummary:
                {
                    PlayerView summary = engine.ViewFor(Seat.South);
                    int[] readySeats = Enumerable.Range(0, 4).Where(s => summary.IsReady((Seat)s)).ToArray();
                    if (readySeats.Length > 0 && chaos.NextInt(2) == 0)
                    {
                        command = RecordedCommand.Of(new ReadyNext((Seat)readySeats[chaos.NextInt(readySeats.Length)]));
                        expected = RejectReason.AlreadyReady;
                    }
                    else
                    {
                        command = RecordedCommand.Of(new PlaceCall(Seat.South, 3, new TurnId(1)));
                        expected = RejectReason.WrongPhase;
                    }

                    break;
                }

                default:
                    return false;
            }

            PlayerView[] before = Enumerable.Range(0, 4).Select(s => engine.ViewFor((Seat)s)).ToArray();
            int eventsBefore = driver.Events.Count;

            CommandResult result = driver.Submit(command);

            Assert.AreEqual(CommandResult.Rejected(expected), result, command.ToString());
            Assert.AreEqual(eventsBefore, driver.Events.Count, "a rejected command emits nothing");
            for (int s = 0; s < 4; s++)
            {
                Assert.AreEqual(before[s], engine.ViewFor((Seat)s));
            }

            return true;
        }

        private static MatchSetup SetupFor(int m)
        {
            Seat? dealer = m % 5 == 0 ? (Seat?)null : (Seat)(m % 4);
            switch (m % 4)
            {
                case 0:
                    return TestSetups.Humans(dealer);
                case 1:
                    return TestSetups.AllBots(dealer);
                case 2:
                    return TestSetups.With(false, true, false, true, dealer);
                default:
                    return TestSetups.With(true, false, true, true, dealer);
            }
        }

        private static ulong SeedFor(string name, int match)
        {
            // FNV-1a over the batch name: string.GetHashCode is randomized per process.
            ulong hash = 14695981039346656037UL;
            foreach (char c in name)
            {
                hash = (hash ^ c) * 1099511628211UL;
            }

            return hash ^ ((ulong)match * 0x9E3779B97F4A7C15UL);
        }

        private static List<string> Texts(IEnumerable<MatchEvent> events)
        {
            return events.Select(e => e.ToString()).ToList();
        }
    }
}
