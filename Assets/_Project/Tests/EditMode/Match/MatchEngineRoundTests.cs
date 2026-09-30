using System.Collections.Generic;
using System.Linq;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Match
{
    public sealed class MatchEngineRoundTests
    {
        // South holds every spade, so South wins all 13 tricks whatever anyone plays.
        private static CardSet[] OneSuitEach => new[]
        {
            CardSet.OfSuit(Suit.Spades),
            CardSet.OfSuit(Suit.Hearts),
            CardSet.OfSuit(Suit.Diamonds),
            CardSet.OfSuit(Suit.Clubs),
        };

        [Test]
        public void Trick_FourthCard_WinnerCreditedAndLeadsTheNextTrick()
        {
            MatchEngine engine = OrdinaryEngine(RuleSet.NepaliStandard);
            CallAll(engine, 3);

            Play(engine, "A♣");
            Play(engine, "2♣");
            Play(engine, "Q♠");
            Play(engine, "2♦");
            List<MatchEvent> events = MatchEngineStartTests.Drain(engine);

            var completed = (TrickCompleted)events.Single(e => e is TrickCompleted);
            Assert.AreEqual(0, completed.TrickIndex);
            Assert.AreEqual(Seat.North, completed.Winner);
            Assert.AreEqual("South: A♣ 2♣ Q♠ 2♦", completed.Trick.ToString());
            var next = (PlayTurnStarted)events[events.IndexOf(completed) + 1];
            Assert.AreEqual(Seat.North, next.Seat);
            Assert.IsNull(next.LedSuit);
            PlayerView view = engine.ViewFor(Seat.East);
            Assert.AreEqual(1, view.TricksWonBy(Seat.North));
            Assert.AreEqual(1, view.TrickIndex);
            Assert.AreEqual(12, view.HandCountOf(Seat.West));
        }

        [Test]
        public void AutoPlayForcedMoves_On_SingleLegalCardsPlayThemselvesWithoutTurns()
        {
            MatchEngine engine = OrdinaryEngine(RuleSet.NepaliStandard.With(autoPlayForcedMoves: true));
            CallAll(engine, 3);
            TurnId southTurn = engine.CurrentTurn;

            Play(engine, "A♣");
            List<MatchEvent> events = MatchEngineStartTests.Drain(engine);

            // East can only follow with 2♣; North, void in clubs, can only trump with Q♠.
            CollectionAssert.AreEqual(
                new[]
                {
                    "CardPlayed seat=South card=A♣ auto=0 forced=0",
                    "CardPlayed seat=East card=2♣ auto=1 forced=1",
                    "CardPlayed seat=North card=Q♠ auto=1 forced=1",
                    "PlayTurnStarted seat=West turn=" + (southTurn.Value + 1) + " led=♣",
                },
                events.Take(4).Select(e => e.ToString()).ToArray());
            Assert.AreEqual(Seat.West, engine.CurrentSeat);
        }

        [Test]
        public void AutoPlayForcedMoves_Off_ASingleLegalCardStillGetsATurn()
        {
            MatchEngine engine = OrdinaryEngine(RuleSet.NepaliStandard);
            CallAll(engine, 3);

            Play(engine, "A♣");
            List<MatchEvent> events = MatchEngineStartTests.Drain(engine);

            Assert.AreEqual("PlayTurnStarted seat=East turn=6 led=♣", events[1].ToString());
            Assert.AreEqual(Seat.East, engine.CurrentSeat);
        }

        [TestCase(true, 48)]
        [TestCase(false, 52)]
        public void AutoPlayLastTrick_Switch_ThirteenthTrickPlaysItselfOnlyWhenOn(bool autoLast, int playTurnsPerRound)
        {
            RuleSet rules = RuleSet.NepaliStandard.With(autoPlayLastTrick: autoLast, rounds: 1);
            var driver = new MatchDriver(new MatchEngine(rules, new Pcg32Random(17UL), TestSetups.Humans(Seat.West)), randomPolicy: true);
            driver.Start();
            driver.PlayToEnd();

            Assert.AreEqual(playTurnsPerRound, driver.Events.OfType<PlayTurnStarted>().Count());
            CardPlayed[] plays = driver.Events.OfType<CardPlayed>().ToArray();
            Assert.AreEqual(52, plays.Length);
            Assert.AreEqual(autoLast ? 4 : 0, plays.Count(p => p.IsForced));
            Assert.AreEqual(autoLast, plays.Skip(48).All(p => p.IsForced && p.IsAuto));
        }

        [Test]
        public void Round_Case7CallEightWinAllThirteen_Scores85AndOthersLose()
        {
            RoundScored scored = PlayOneSuitEachRound(RuleSet.NepaliStandard.With(redealIfNoSpade: false));

            Assert.AreEqual(13, scored.Result[Seat.South].Won);
            Assert.AreEqual(85, scored.Result[Seat.South].DeltaTenths);
            Assert.AreEqual(-10, scored.Result[Seat.East].DeltaTenths);
            Assert.AreEqual(-10, scored.Result[Seat.North].DeltaTenths);
            Assert.AreEqual(-10, scored.Result[Seat.West].DeltaTenths);
        }

        [Test]
        public void Round_Case7WithBigCallBonus_Scores130()
        {
            RoundScored scored = PlayOneSuitEachRound(RuleSet.NepaliStandard.With(redealIfNoSpade: false, bigCallBonus: true));

            Assert.AreEqual(130, scored.Result[Seat.South].DeltaTenths);
            Assert.AreEqual(130, scored.Result[Seat.South].TotalTenths);
        }

        [Test]
        public void Round_RandomPlay_RoundScoredEqualsTheCalculatorAndTricksTotal13()
        {
            var driver = new MatchDriver(new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(23UL), TestSetups.Humans(Seat.West)), randomPolicy: true);
            driver.Start();
            driver.PlayToEnd();

            RoundScored[] rounds = driver.Events.OfType<RoundScored>().ToArray();
            Assert.AreEqual(5, rounds.Length);
            foreach (RoundScored scored in rounds)
            {
                int tricks = 0;
                for (int s = 0; s < 4; s++)
                {
                    SeatRoundResult line = scored.Result[(Seat)s];
                    tricks += line.Won;
                    Assert.AreEqual(ScoreCalculator.RoundScore(line.Call, line.Won, RuleSet.NepaliStandard), line.DeltaTenths);
                }

                Assert.AreEqual(13, tricks);
            }
        }

        [Test]
        public void RoundSummary_HumansReadyOneByOne_NextRoundAfterTheFourth()
        {
            MatchEngine engine = PlayFirstRound(TestSetups.Humans(Seat.West));
            MatchEngineStartTests.Drain(engine);

            Assert.AreEqual(Phase.RoundSummary, engine.Phase);
            Assert.AreEqual(TurnId.None, engine.CurrentTurn);
            foreach (Seat seat in new[] { Seat.North, Seat.South, Seat.West })
            {
                Assert.IsTrue(engine.Execute(new ReadyNext(seat)).IsAccepted);
                Assert.AreEqual(Phase.RoundSummary, engine.Phase);
            }

            Assert.AreEqual(RejectReason.AlreadyReady, engine.Execute(new ReadyNext(Seat.South)).Reason);
            Assert.AreEqual(RejectReason.InvalidSeat, engine.Execute(new ReadyNext((Seat)5)).Reason);
            Assert.IsTrue(engine.ViewFor(Seat.East).IsReady(Seat.North));
            Assert.IsFalse(engine.ViewFor(Seat.East).IsReady(Seat.East));

            Assert.IsTrue(engine.Execute(new ReadyNext(Seat.East)).IsAccepted);
            List<MatchEvent> events = MatchEngineStartTests.Drain(engine);

            CollectionAssert.AreEqual(
                new[] { "ReadyConfirmed seat=North", "ReadyConfirmed seat=South", "ReadyConfirmed seat=West", "ReadyConfirmed seat=East", "RoundStarted round=2 dealer=South" },
                events.Take(5).Select(e => e.ToString()).ToArray());
            Assert.AreEqual(Phase.Calling, engine.Phase);
            Assert.AreEqual(2, engine.Round);
            Assert.AreEqual(Seat.East, engine.CurrentSeat, "round 2: dealer South, so East calls first (GDD §2 table)");
        }

        [Test]
        public void RoundSummary_BotSeats_ReadyAutomaticallyRightAfterScoring()
        {
            MatchEngine engine = PlayFirstRound(TestSetups.With(false, true, false, true, Seat.West));
            List<MatchEvent> events = MatchEngineStartTests.Drain(engine);

            int scored = events.FindIndex(e => e is RoundScored);
            Assert.AreEqual("ReadyConfirmed seat=East", events[scored + 1].ToString());
            Assert.AreEqual("ReadyConfirmed seat=West", events[scored + 2].ToString());
            Assert.AreEqual(RejectReason.AlreadyReady, engine.Execute(new ReadyNext(Seat.East)).Reason);

            engine.Execute(new ReadyNext(Seat.South));
            engine.Execute(new ReadyNext(Seat.North));
            Assert.AreEqual(Phase.Calling, engine.Phase);
        }

        [Test]
        public void RoundSummary_AllBots_NextRoundDealtWithoutWaiting()
        {
            MatchEngine engine = PlayFirstRound(TestSetups.AllBots(Seat.West));

            Assert.AreEqual(Phase.Calling, engine.Phase);
            Assert.AreEqual(2, engine.Round);
        }

        [Test]
        public void RoundSummary_CountdownExpires_ReadyingEverySeatNotReadyAdvances()
        {
            MatchEngine engine = PlayFirstRound(TestSetups.With(false, false, true, false, Seat.West));
            engine.Execute(new ReadyNext(Seat.South));

            // What a driver does when the summary countdown ends (GDD §7 "or timer ends").
            for (int s = 0; s < 4; s++)
            {
                if (!engine.ViewFor((Seat)s).IsReady((Seat)s))
                {
                    Assert.IsTrue(engine.Execute(new ReadyNext((Seat)s)).IsAccepted);
                }
            }

            Assert.AreEqual(Phase.Calling, engine.Phase);
            Assert.AreEqual(2, engine.Round);
        }

        [Test]
        public void Match_LastRoundReadied_MatchEndedWithConsistentStandings()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(rounds: 2);
            var driver = new MatchDriver(new MatchEngine(rules, new Pcg32Random(29UL), TestSetups.Humans(Seat.West)), randomPolicy: true);
            driver.Start();
            driver.PlayToEnd();

            var ended = (MatchEnded)driver.Events.Last();
            Assert.AreEqual(Phase.MatchOver, driver.Engine.Phase);
            Assert.IsTrue(driver.Events[driver.Events.Count - 2] is ReadyConfirmed, "the match ends when the last summary is readied");
            Assert.AreEqual(2, driver.Events.OfType<RoundScored>().Count());
            PlayerView view = driver.Engine.ViewFor(Seat.South);
            CollectionAssert.AreEqual(ended.Standings.ToArray(), view.FinalStandings.ToArray());
            CollectionAssert.AreEqual(Standings.Compute(view.Scores).ToArray(), ended.Standings.ToArray());
            foreach (Standing standing in ended.Standings)
            {
                Assert.AreEqual(view.TotalOf(standing.Seat), standing.TotalTenths);
            }

            Assert.AreEqual(RejectReason.WrongPhase, driver.Engine.Execute(new ReadyNext(Seat.South)).Reason);
            Assert.AreEqual(RejectReason.WrongPhase, driver.Engine.Execute(new PlaceCall(Seat.South, 3, new TurnId(1))).Reason);
        }

        [Test]
        public void Match_OneRoundVariant_EndsAfterRoundOne()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(rounds: 1);
            var driver = new MatchDriver(new MatchEngine(rules, new Pcg32Random(31UL), TestSetups.AllBots(Seat.South)), randomPolicy: false);
            driver.Start();
            driver.PlayToEnd();

            Assert.AreEqual(1, driver.Events.OfType<RoundStarted>().Count());
            Assert.IsTrue(driver.Events.Last() is MatchEnded);
        }

        [Test]
        public void Close_MidRound_RejectsEveryCommandAndKeepsState()
        {
            MatchEngine engine = OrdinaryEngine(RuleSet.NepaliStandard);
            CallAll(engine, 3);
            Play(engine, "A♣");
            TurnId turn = engine.CurrentTurn;

            engine.Close();

            Assert.AreEqual(Phase.Closed, engine.Phase);
            Assert.AreEqual(TurnId.None, engine.CurrentTurn);
            Assert.AreEqual(RejectReason.WrongPhase, engine.Execute(new PlayCard(Seat.East, C("2♣"), turn)).Reason);
            Assert.AreEqual(RejectReason.WrongPhase, engine.Execute(new ReadyNext(Seat.East)).Reason);
            Assert.AreEqual(12, engine.ViewFor(Seat.South).Hand.Count);
            Assert.IsNull(engine.FindInvariantViolation());
        }

        [Test]
        public void Close_AfterTheMatchEnded_KeepsFinalStandingsThroughSnapshots()
        {
            var driver = new MatchDriver(new MatchEngine(RuleSet.NepaliStandard.With(rounds: 1), new Pcg32Random(43UL), TestSetups.Humans(Seat.West)), randomPolicy: true);
            driver.Start();
            driver.PlayToEnd();
            IReadOnlyList<Standing> standings = driver.Engine.ViewFor(Seat.South).FinalStandings;

            driver.Engine.Close();
            MatchEngine restored = MatchEngine.Restore(SnapshotComparer.Clone(driver.Engine.Snapshot()), driver.Engine.Rules, new Pcg32Random(1UL));

            Assert.AreEqual(4, standings.Count);
            Assert.IsNull(driver.Engine.FindInvariantViolation());
            CollectionAssert.AreEqual(standings.ToArray(), driver.Engine.ViewFor(Seat.South).FinalStandings.ToArray());
            Assert.AreEqual(driver.Engine.ViewFor(Seat.East), restored.ViewFor(Seat.East));
        }

        [Test]
        public void Close_InTheLastRoundSummary_NoStandingsThroughSnapshots()
        {
            MatchEngine engine = PlayFirstRound(TestSetups.Humans(Seat.West), RuleSet.NepaliStandard.With(rounds: 1));
            Assert.AreEqual(Phase.RoundSummary, engine.Phase);

            engine.Close();
            MatchEngine restored = MatchEngine.Restore(SnapshotComparer.Clone(engine.Snapshot()), engine.Rules, new Pcg32Random(1UL));

            Assert.IsNull(engine.FindInvariantViolation());
            Assert.AreEqual(0, engine.ViewFor(Seat.South).FinalStandings.Count);
            Assert.AreEqual(engine.ViewFor(Seat.North), restored.ViewFor(Seat.North));
        }

        private static MatchEngine OrdinaryEngine(RuleSet rules)
        {
            var engine = new MatchEngine(
                rules,
                RiggedDeal.Random(MatchEngineStartTests.OrdinaryHands, Seat.West, rules.Direction),
                TestSetups.Humans(Seat.West));
            engine.Start();
            MatchEngineStartTests.Drain(engine);
            return engine;
        }

        private static void CallAll(MatchEngine engine, int call)
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(engine.Execute(new PlaceCall(engine.CurrentSeat, call, engine.CurrentTurn)).IsAccepted);
            }

            MatchEngineStartTests.Drain(engine);
        }

        private static void Play(MatchEngine engine, string card)
        {
            CommandResult result = engine.Execute(new PlayCard(engine.CurrentSeat, C(card), engine.CurrentTurn));
            Assert.IsTrue(result.IsAccepted, card + " -> " + result);
            engine.AssertInvariants();
        }

        private static MatchEngine PlayFirstRound(MatchSetup setup, RuleSet rules = null)
        {
            var engine = new MatchEngine(rules ?? RuleSet.NepaliStandard, new Pcg32Random(37UL), setup);
            engine.Start();
            while (engine.Phase == Phase.Calling)
            {
                engine.Execute(new PlaceCall(engine.CurrentSeat, 2, engine.CurrentTurn));
            }

            while (engine.Phase == Phase.Playing)
            {
                Card card = engine.ViewFor(engine.CurrentSeat).LegalCards().Lowest();
                engine.Execute(new PlayCard(engine.CurrentSeat, card, engine.CurrentTurn));
                engine.AssertInvariants();
            }

            return engine;
        }

        private static RoundScored PlayOneSuitEachRound(RuleSet rules)
        {
            var engine = new MatchEngine(rules, RiggedDeal.Random(OneSuitEach, Seat.West, Direction.Anticlockwise), TestSetups.Humans(Seat.West));
            engine.Start();
            int[] calls = { 8, 1, 1, 1 };
            while (engine.Phase == Phase.Calling)
            {
                engine.Execute(new PlaceCall(engine.CurrentSeat, calls[(int)engine.CurrentSeat], engine.CurrentTurn));
            }

            while (engine.Phase == Phase.Playing)
            {
                Card card = engine.ViewFor(engine.CurrentSeat).LegalCards().Highest();
                engine.Execute(new PlayCard(engine.CurrentSeat, card, engine.CurrentTurn));
                engine.AssertInvariants();
            }

            return MatchEngineStartTests.Drain(engine).OfType<RoundScored>().Single();
        }
    }
}
