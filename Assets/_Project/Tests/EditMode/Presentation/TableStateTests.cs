using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using Callbreak.Presentation.Table;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// The table's own state, built one played event at a time, must say exactly what the engine's
    /// view for the local seat says once every event has been applied; and it must never learn
    /// another seat's cards.
    /// </summary>
    public sealed class TableStateTests
    {
        private static readonly RuleSet Rules = RuleSet.NepaliStandard;

        private static void AssertMatchesView(TableState state, PlayerView view, string when)
        {
            var fresh = new TableState(view.Viewer, view.Rules);
            fresh.Reset(view);
            Assert.AreEqual(fresh.Phase, state.Phase, when + ": phase");
            Assert.AreEqual(fresh.Round, state.Round, when + ": round");
            Assert.AreEqual(fresh.Hand, state.Hand, when + ": hand");
            Assert.AreEqual(fresh.HasTurn, state.HasTurn, when + ": turn open");
            Assert.AreEqual(fresh.Turn, state.Turn, when + ": turn id");
            if (fresh.HasTurn)
            {
                Assert.AreEqual(fresh.TurnSeat, state.TurnSeat, when + ": turn seat");
            }

            Assert.AreEqual(fresh.Legal, state.Legal, when + ": legal cards");
            Assert.AreEqual(fresh.IsLocalCallTurn, state.IsLocalCallTurn, when);
            Assert.AreEqual(fresh.IsLocalPlayTurn, state.IsLocalPlayTurn, when);
            Assert.AreEqual(fresh.TricksCompleted, state.TricksCompleted, when + ": tricks");
            Assert.AreEqual(fresh.Trick.Count, state.Trick.Count, when + ": trick size");
            for (int i = 0; i < fresh.Trick.Count; i++)
            {
                Assert.AreEqual(fresh.Trick[i], state.Trick[i], when + ": trick card " + i);
                Assert.AreEqual(fresh.Trick.SeatAt(i), state.Trick.SeatAt(i), when + ": trick seat " + i);
            }

            if (state.Phase != TablePhase.Idle)
            {
                Assert.AreEqual(fresh.Dealer, state.Dealer, when + ": dealer");
            }

            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                var seat = (Seat)s;
                Assert.AreEqual(fresh.CallOf(seat), state.CallOf(seat), when + ": call of " + seat);
                Assert.AreEqual(fresh.WonBy(seat), state.WonBy(seat), when + ": won by " + seat);
                Assert.AreEqual(fresh.HandCountOf(seat), state.HandCountOf(seat), when + ": cards of " + seat);
                Assert.AreEqual(fresh.TotalOf(seat), state.TotalOf(seat), when + ": total of " + seat);
                if (state.Phase == TablePhase.RoundEnded)
                {
                    Assert.AreEqual(fresh.IsReady(seat), state.IsReady(seat), when + ": ready " + seat);
                }
            }
        }

        [Test]
        public void AppliedEvents_AlwaysAgreeWithTheEnginesView([Values(1UL, 7UL, 2026UL)] ulong seed, [Values(Seat.South, Seat.West)] Seat local)
        {
            var engine = new MatchEngine(Rules, new Pcg32Random(seed), TestSetups.Humans());
            var driver = new MatchDriver(engine, randomPolicy: true, policySeed: seed);
            var state = new TableState(local, Rules);
            int applied = 0;

            void Catch(string when)
            {
                for (; applied < driver.Events.Count; applied++)
                {
                    MatchEvent e = driver.Events[applied];
                    if (e.Visibility.IsVisibleTo(local))
                    {
                        state.Apply(e);
                    }
                }

                AssertMatchesView(state, engine.ViewFor(local), when);
            }

            driver.Start();
            Catch("after the deal");
            int step = 0;
            while (driver.Step())
            {
                Catch("step " + (++step));
                Assert.Less(step, 5000, "the match did not finish");
            }

            Catch("after the match");
            Assert.AreEqual(TablePhase.MatchEnded, state.Phase);
            Assert.AreEqual(4, state.FinalStandings.Count);
            Assert.AreEqual(Rules.Rounds, state.Round);
        }

        [Test]
        public void TheFinishedTrick_StaysOnDisplay_UntilTheNextTurnOrCard()
        {
            var state = new TableState(Seat.South, Rules);
            state.Apply(new RoundStarted(1, Seat.West));
            state.Apply(new HandDealt(Seat.South, CardSet.Parse("A♠ K♠ 9♠ 6♠ 2♠ A♥ 8♥ 4♥ K♦ 5♦ 2♦ 7♣ 3♣")));
            var trick = new Trick(Seat.East, Rules.Direction);
            foreach ((Seat seat, string card) in new[] { (Seat.East, "9♥"), (Seat.North, "Q♥"), (Seat.West, "10♥"), (Seat.South, "A♥") })
            {
                state.Apply(new CardPlayed(seat, Card.Parse(card), false, false));
                trick = trick.With(Card.Parse(card));
            }

            Assert.AreEqual(12, state.Hand.Count);
            Assert.AreEqual(12, state.HandCountOf(Seat.North));
            Assert.IsTrue(state.IsTrickOnDisplay);

            state.Apply(new TrickCompleted(new CompletedTrick(0, trick)));
            Assert.IsTrue(state.IsTrickOnDisplay, "the four cards wait for the sweep");
            Assert.AreEqual(Seat.South, state.TrickWinner);
            Assert.AreEqual(1, state.WonBy(Seat.South));
            Assert.AreEqual(1, state.TricksCompleted);
            Assert.IsFalse(state.HasTurn);

            state.Apply(new PlayTurnStarted(Seat.South, TurnIdOf(9), null));
            Assert.IsFalse(state.IsTrickOnDisplay);
            Assert.AreEqual(0, state.Trick.Count);
            Assert.IsTrue(state.IsLocalPlayTurn);
            Assert.AreEqual(state.Hand, state.Legal, "leading: every card is legal");
            Assert.IsTrue(state.TryGetObligation(out PlayObligation obligation));
            Assert.AreEqual(ObligationKind.LeadAny, obligation.Kind);
        }

        [Test]
        public void ForcedPlays_StartANewTrickWithoutATurn()
        {
            // The last trick plays itself: four CardPlayed events and no turn in between.
            var state = new TableState(Seat.South, Rules);
            state.Apply(new RoundStarted(1, Seat.West));
            state.Apply(new HandDealt(Seat.South, CardSet.Parse("A♠ K♠ 9♠ 6♠ 2♠ A♥ 8♥ 4♥ K♦ 5♦ 2♦ 7♣ 3♣")));
            var first = new Trick(Seat.East, Rules.Direction).With(Card.Parse("9♥")).With(Card.Parse("Q♥")).With(Card.Parse("10♥")).With(Card.Parse("A♥"));
            foreach ((Seat seat, string card) in new[] { (Seat.East, "9♥"), (Seat.North, "Q♥"), (Seat.West, "10♥"), (Seat.South, "A♥") })
            {
                state.Apply(new CardPlayed(seat, Card.Parse(card), false, false));
            }

            state.Apply(new TrickCompleted(new CompletedTrick(0, first)));
            state.Apply(new CardPlayed(Seat.South, Card.Parse("3♣"), true, true));
            Assert.AreEqual(1, state.Trick.Count, "the swept trick is gone and the forced card leads a new one");
            Assert.AreEqual(Seat.South, state.Trick.Leader);
            Assert.AreEqual(11, state.Hand.Count);
            Assert.IsFalse(state.HasTurn);
        }

        [Test]
        public void OnlyTheLocalSeatsPrivateEvents_ChangeTheHand()
        {
            var state = new TableState(Seat.South, Rules);
            state.Apply(new RoundStarted(1, Seat.West));
            state.Apply(new HandDealt(Seat.North, CardSet.Parse("A♠ K♠ 9♠ 6♠ 2♠ A♥ 8♥ 4♥ K♦ 5♦ 2♦ 7♣ 3♣")));
            Assert.IsTrue(state.Hand.IsEmpty, "another seat's hand is never taken, even if it arrived");
            Assert.AreEqual(0, state.HandCountOf(Seat.North));

            state.Apply(new HandDealt(Seat.South, CardSet.Parse("Q♠ J♠ 10♠ 8♠ 7♠ K♥ 9♥ 5♥ A♦ 6♦ 3♦ 8♣ 4♣")));
            Assert.AreEqual(13, state.Hand.Count);
            Assert.AreEqual(13, state.HandCountOf(Seat.North), "others are known by count only");

            state.Apply(new PlayTurnStarted(Seat.North, TurnIdOf(5), null));
            state.Apply(new LegalHint(Seat.North, TurnIdOf(5), CardSet.Parse("A♠")));
            Assert.IsTrue(state.Legal.IsEmpty);
            Assert.IsFalse(state.IsLocalPlayTurn);
        }

        [Test]
        public void Calls_AndTheRoundScore_AreRemembered()
        {
            var state = new TableState(Seat.South, Rules);
            state.Apply(new RoundStarted(2, Seat.South));
            Assert.AreEqual(TablePhase.Dealing, state.Phase);
            Assert.AreEqual(Seat.South, state.Dealer);
            state.Apply(new CallTurnStarted(Seat.East, TurnIdOf(1)));
            Assert.AreEqual(TablePhase.Calling, state.Phase);
            Assert.IsTrue(state.HasTurn);
            Assert.AreEqual(Seat.East, state.TurnSeat);
            Assert.IsFalse(state.IsLocalCallTurn);
            state.Apply(new CallPlaced(Seat.East, 3, true));
            Assert.AreEqual(3, state.CallOf(Seat.East));
            Assert.IsTrue(state.IsAutoCall(Seat.East));
            Assert.IsFalse(state.HasCalled(Seat.South));
            Assert.IsFalse(state.AllCalled);
            state.Apply(new CallTurnStarted(Seat.South, TurnIdOf(2)));
            Assert.IsTrue(state.IsLocalCallTurn);

            var result = new RoundResult(2, new[]
            {
                new SeatRoundResult(Seat.South, 4, 5, 41, 82),
                new SeatRoundResult(Seat.East, 3, 3, 30, 60),
                new SeatRoundResult(Seat.North, 3, 2, -30, 0),
                new SeatRoundResult(Seat.West, 3, 3, 30, 30),
            });
            state.Apply(new RoundScored(result));
            Assert.AreEqual(TablePhase.RoundEnded, state.Phase);
            Assert.AreEqual(82, state.TotalOf(Seat.South));
            Assert.AreEqual(0, state.TotalOf(Seat.North));
            Assert.AreSame(result, state.LastRoundResult);
            state.Apply(new ReadyConfirmed(Seat.East));
            Assert.IsTrue(state.IsReady(Seat.East));
            Assert.IsFalse(state.IsReady(Seat.South));

            state.Apply(new RoundStarted(3, Seat.East));
            Assert.IsFalse(state.IsReady(Seat.East));
            Assert.AreEqual(CallRules.NotCalled, state.CallOf(Seat.East));
            Assert.AreEqual(82, state.TotalOf(Seat.South), "totals carry over");
        }

        private static TurnId TurnIdOf(int value) => new TurnId(value);
    }
}
