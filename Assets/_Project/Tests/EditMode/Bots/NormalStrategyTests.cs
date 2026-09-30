using System;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Bots
{
    /// <summary>
    /// GDD §9 "Normal play heuristics", one rigged view per rule. Seats play South, East, North, West
    /// (anticlockwise); every seat has called 3 unless a test says otherwise.
    /// </summary>
    public sealed class NormalStrategyTests
    {
        private const string SampleHand = "A♠ K♠ J♠ 9♠ 4♠ A♥ J♥ 8♥ K♦ 10♦ 5♦ Q♣ 7♣";

        [Test]
        public void Level_Normal()
        {
            Assert.AreEqual(BotLevel.Normal, new NormalStrategy().Level);
        }

        [Test]
        public void ChooseCall_GddSampleHand_TheEstimatorsFour()
        {
            PlayerView view = BotViews.For(Seat.South, SampleHand).Calls(-1, -1, -1, -1).Calling().Build();

            Assert.AreEqual(4, new NormalStrategy().ChooseCall(view, new Pcg32Random(1UL)));
        }

        [Test]
        public void ChooseCallAndCard_AnyView_UseNoRandomness()
        {
            var rng = new SequenceRandom(new int[0]);
            var normal = new NormalStrategy();

            normal.ChooseCall(BotViews.For(Seat.South, SampleHand).Calls(-1, -1, -1, -1).Calling().Build(), rng);
            normal.ChooseCard(BotViews.For(Seat.South, SampleHand).Build(), rng);

            Assert.AreEqual(0, rng.Draws);
        }

        // ---- Rule 1: needs tricks ------------------------------------------------------------------

        [Test]
        public void ChooseCard_NeedsTricksLastToPlay_WinsWithTheCheapestWinningCard()
        {
            PlayerView view = BotViews.For(Seat.South, "A♥ Q♥ 3♥ K♠ 8♠ 4♠ K♦ 9♦ 6♦ 2♦ J♣ 8♣ 5♣")
                .Current(Seat.East, "9♥ J♥ 5♥")
                .Build();

            Assert.AreEqual(C("Q♥"), Play(view));
        }

        [Test]
        public void ChooseCard_NeedsTricksNotLastHoldingTheTopRemainingCard_PlaysIt()
        {
            PlayerView view = BotViews.For(Seat.South, "A♥ 10♥ 4♥ K♠ 8♠ 4♠ K♦ 9♦ 6♦ 2♦ J♣ 8♣ 5♣")
                .Current(Seat.West, "7♥")
                .Build();

            Assert.AreEqual(C("A♥"), Play(view));
        }

        [Test]
        public void ChooseCard_NeedsTricksNotLastWithoutTheTopRemainingCard_PlaysLow()
        {
            // The A♥ is still out, so the K♥ is kept and the cheaper 10♥ beats the 7♥.
            PlayerView view = BotViews.For(Seat.South, "K♥ 10♥ 4♥ K♠ 8♠ 4♠ K♦ 9♦ 6♦ 2♦ J♣ 8♣ 5♣")
                .Current(Seat.West, "7♥")
                .Build();

            Assert.AreEqual(C("10♥"), Play(view));
        }

        [Test]
        public void ChooseCard_NeedsTricksFollowingASpadeLeadWithTheTopSpade_PlaysIt()
        {
            PlayerView view = BotViews.For(Seat.South, "A♠ 9♠ 4♠ K♥ 10♥ 4♥ K♦ 9♦ 6♦ 2♦ J♣ 8♣ 5♣")
                .Current(Seat.West, "7♠")
                .Build();

            Assert.AreEqual(C("A♠"), Play(view));
        }

        [Test]
        public void ChooseCard_NeedsTricksTopCardButALaterSeatIsVoidWithSpades_KeepsIt()
        {
            // Rules 1 and 5: North trumped the first heart trick, so it is still void two tricks later and would
            // trump the A♥.
            PlayerView view = BotViews.For(Seat.South, "A♥ 10♥ 4♥ K♠ 8♠ 4♠ K♦ 9♦ 6♦ J♣ 8♣")
                .Trick(Seat.South, "2♥ 5♥ 3♠ 9♥")
                .Trick(Seat.North, "4♣ K♣ 2♣ 9♣")
                .Current(Seat.West, "7♥")
                .Build();

            Assert.AreEqual(C("10♥"), Play(view));
        }

        [Test]
        public void ChooseCard_NeedsTricksCannotBeatTheTrick_PlaysItsLowestCardOfTheSuit()
        {
            PlayerView view = BotViews.For(Seat.South, "K♥ 10♥ 4♥ K♠ 8♠ 4♠ K♦ 9♦ 6♦ 2♦ J♣ 8♣ 5♣")
                .Current(Seat.West, "A♥")
                .Build();

            Assert.AreEqual(C("4♥"), Play(view));
        }

        // ---- Rule 2: call made ---------------------------------------------------------------------

        [Test]
        public void ChooseCard_CallMadeNotLast_PlaysTheLowestLegalCardEvenWithTheTopCard()
        {
            // With a call of 1 already made, the A♥ that rule 1 would play stays in hand.
            PlayerView view = BotViews.For(Seat.South, "A♥ 10♥ 4♥ K♠ 8♠ 4♠ Q♦ 9♦ J♣ 8♣ 5♣")
                .Calls(1, 3, 3, 3)
                .Trick(Seat.South, "A♠ 2♠ 5♠ 9♠")
                .Trick(Seat.South, "3♦ 5♦ 7♦ K♦")
                .Current(Seat.West, "7♥")
                .Build();

            Assert.AreEqual(C("10♥"), Play(view));
        }

        [TestCase(3, "Q♣", TestName = "ChooseCard_CallMadeLastToPlayAndTheWinnerNeedsTheTrick_TakesItCheaply")]
        [TestCase(1, "4♣", TestName = "ChooseCard_CallMadeLastToPlayAndTheWinnerHasItsCall_PlaysTheLowestLegalCard")]
        public void ChooseCard_CallMadeLastToPlay(int westCall, string expected)
        {
            // Without the must-beat rule South may duck. West is winning with the J♣ and has won one trick.
            PlayerView view = BotViews.For(Seat.South, "K♣ Q♣ 4♣ K♠ 8♠ 4♠ Q♦ 9♦ A♥ 10♥")
                .Rules(RuleSet.NepaliStandard.With(mustBeatLedSuit: false))
                .Calls(1, 3, 3, westCall)
                .Trick(Seat.South, "A♠ 2♠ 5♠ 9♠")
                .Trick(Seat.South, "3♦ 7♦ 5♦ K♦")
                .Trick(Seat.West, "2♥ 3♥ K♥ 6♥")
                .Current(Seat.East, "6♣ 9♣ J♣")
                .Build();

            Assert.AreEqual(C(expected), Play(view));
        }

        [TestCase(3, "A♥", TestName = "ChooseCard_CallMadeNotLastWithACardNobodyCanBeat_StopsTheOpponentWhoNeedsTheTrick")]
        [TestCase(1, "4♥", TestName = "ChooseCard_CallMadeNotLastAndTheWinnerHasItsCall_PlaysTheLowestLegalCard")]
        public void ChooseCard_CallMadeNotLast(int westCall, string expected)
        {
            PlayerView view = BotViews.For(Seat.South, "A♥ 4♥ K♠ 8♠ 4♠ Q♦ 9♦ J♣ 8♣ 5♣ 2♣")
                .Rules(RuleSet.NepaliStandard.With(mustBeatLedSuit: false))
                .Calls(1, 3, 3, westCall)
                .Trick(Seat.South, "A♠ 2♠ 5♠ 9♠")
                .Trick(Seat.South, "3♦ 5♦ 7♦ K♦")
                .Current(Seat.West, "7♥")
                .Build();

            Assert.AreEqual(C(expected), Play(view));
        }

        [Test]
        public void ChooseCard_CallMadeNotLastAndTheWinningCardCouldBeBeaten_PlaysTheLowestLegalCard()
        {
            // The 10♥ would take the lead from West, but the J♥, Q♥ and K♥ are still out: not a sure stop.
            PlayerView view = BotViews.For(Seat.South, "10♥ 4♥ K♠ 8♠ 4♠ Q♦ 9♦ J♣ 8♣ 5♣ 2♣")
                .Rules(RuleSet.NepaliStandard.With(mustBeatLedSuit: false))
                .Calls(1, 3, 3, 3)
                .Trick(Seat.South, "A♠ 2♠ 5♠ 9♠")
                .Trick(Seat.South, "3♦ 5♦ 7♦ K♦")
                .Current(Seat.West, "7♥")
                .Build();

            Assert.AreEqual(C("4♥"), Play(view));
        }

        // ---- Rule 3: leading -----------------------------------------------------------------------

        [Test]
        public void ChooseCard_LeadingWithTheTopCardOfASuit_LeadsIt()
        {
            PlayerView view = BotViews.For(Seat.South, "K♠ 8♠ 4♠ A♦ 9♦ 6♦ 2♦ Q♥ 7♥ 3♥ J♣ 8♣ 5♣").Build();

            Assert.AreEqual(C("A♦"), Play(view));
        }

        [Test]
        public void ChooseCard_LeadingAfterTheAceWasPlayed_LeadsTheKingAsTopRemainingCard()
        {
            PlayerView view = BotViews.For(Seat.South, "K♥ 9♥ K♠ 8♠ 4♠ Q♦ 9♦ 6♦ 2♦ J♣ 8♣")
                .Trick(Seat.East, "A♥ 2♥ 4♥ 5♥")
                .Trick(Seat.East, "3♣ 6♣ 9♣ A♣")
                .Build();

            Assert.AreEqual(C("K♥"), Play(view));
        }

        [Test]
        public void ChooseCard_LeadingWithTwoTopCards_LeadsTheSuitWithMoreCardsStillOut()
        {
            // Eleven clubs are out against eight diamonds, so the A♣ is less likely to be trumped.
            PlayerView view = BotViews.For(Seat.South, "K♠ 8♠ 4♠ A♦ 9♦ 6♦ 5♦ 2♦ Q♥ 7♥ 3♥ A♣ 8♣").Build();

            Assert.AreEqual(C("A♣"), Play(view));
        }

        [Test]
        public void ChooseCard_LeadingButAnOpponentIsVoidInTheSuitAndMayTrump_AvoidsIt()
        {
            // Rules 3 and 5: North trumped hearts in the first trick, so the A♥ is not led; with no other top
            // card South leads its lowest card of a safe suit.
            PlayerView view = BotViews.For(Seat.South, "A♥ 10♥ 4♥ K♠ 8♠ 4♠ K♦ 9♦ 6♦ J♣ 8♣")
                .Trick(Seat.South, "2♥ 5♥ 3♠ 9♥")
                .Trick(Seat.North, "4♣ K♣ A♣ 9♣")
                .Build();

            Assert.AreEqual(C("6♦"), Play(view));
        }

        [Test]
        public void ChooseCard_LeadingButTheVoidOpponentHasNoSpades_StillLeadsTheTopCard()
        {
            // North discarded on the first heart trick instead of trumping, so it cannot trump the A♥ now.
            PlayerView view = BotViews.For(Seat.South, "A♥ 10♥ 4♥ K♠ 8♠ 4♠ K♦ 9♦ 6♦ J♣ 8♣")
                .Trick(Seat.South, "2♥ 5♥ 3♣ 9♥")
                .Trick(Seat.West, "4♣ A♣ K♣ 9♣")
                .Build();

            Assert.AreEqual(C("A♥"), Play(view));
        }

        [Test]
        public void ChooseCard_LeadingWithOnlyTheTopSpade_LeadsIt()
        {
            PlayerView view = BotViews.For(Seat.South, "A♠ 8♠ 4♠ K♦ 9♦ 6♦ 2♦ Q♥ 7♥ 3♥ J♣ 8♣ 5♣").Build();

            Assert.AreEqual(C("A♠"), Play(view));
        }

        [TestCase(3, "2♣", TestName = "ChooseCard_LeadingWithoutATopCardAndNeedingTricks_LeadsLowFromTheShorterSuit")]
        [TestCase(1, "2♦", TestName = "ChooseCard_LeadingWithoutATopCardAndCallMade_LeadsTheLowestLegalCard")]
        public void ChooseCard_LeadingWithoutATopCard(int southCall, string expected)
        {
            PlayerView view = BotViews.For(Seat.South, "Q♠ 8♠ 4♠ K♦ 9♦ 6♦ 2♦ Q♥ 7♥ 3♥ J♣ 2♣")
                .Calls(southCall, 3, 3, 3)
                .Trick(Seat.South, "A♠ 3♠ 5♠ 9♠")
                .Build();

            Assert.AreEqual(C(expected), Play(view));
        }

        [Test]
        public void ChooseCard_LeadingWithATopCardAndCallMade_StillCashesIt()
        {
            // "Meet its call, then take cheap extra tricks" (GDD §9 goal).
            PlayerView view = BotViews.For(Seat.South, "K♠ 8♠ 4♠ A♦ 9♦ 6♦ 2♦ Q♥ 7♥ 3♥ J♣ 8♣")
                .Calls(1, 3, 3, 3)
                .Trick(Seat.South, "A♠ 3♠ 5♠ 9♠")
                .Build();

            Assert.AreEqual(C("A♦"), Play(view));
        }

        // ---- Rule 4: trumping ----------------------------------------------------------------------

        [Test]
        public void ChooseCard_VoidInTheLedSuit_TrumpsWithTheLowestSpadeThatWins()
        {
            PlayerView view = BotViews.For(Seat.South, "K♠ 8♠ 4♠ A♥ Q♥ 7♥ 3♥ J♣ 9♣ 8♣ 5♣ 4♣ 2♣")
                .Current(Seat.West, "9♦")
                .Build();

            Assert.AreEqual(C("4♠"), Play(view));
        }

        [Test]
        public void ChooseCard_VoidAndLastToPlay_TrumpsWithTheLowestSpadeThatWins()
        {
            PlayerView view = BotViews.For(Seat.South, "K♠ 8♠ 4♠ A♥ Q♥ 7♥ 3♥ J♣ 9♣ 8♣ 5♣ 4♣ 2♣")
                .Current(Seat.East, "9♦ K♦ 2♦")
                .Build();

            Assert.AreEqual(C("4♠"), Play(view));
        }

        [Test]
        public void ChooseCard_VoidAndTheTrickIsAlreadyTrumped_OvertrumpsWithTheLowestSpadeThatWins()
        {
            PlayerView view = BotViews.For(Seat.South, "K♠ 8♠ 4♠ A♥ Q♥ 7♥ 3♥ J♣ 9♣ 8♣ 5♣ 4♣ 2♣")
                .Current(Seat.North, "9♦ 6♠")
                .Build();

            Assert.AreEqual(C("8♠"), Play(view));
        }

        [Test]
        public void ChooseCard_SomeoneTrumpedHigherThanItsSpades_DiscardsTheLowestCardOfTheLongestSideSuit()
        {
            // Six clubs against four hearts: the 4♣ goes, not the lower 2♥ and not a spade.
            PlayerView view = BotViews.For(Seat.South, "K♠ 8♠ 4♠ A♥ Q♥ 7♥ 2♥ J♣ 9♣ 8♣ 6♣ 5♣ 4♣")
                .Current(Seat.East, "9♦ A♠ 2♦")
                .Build();

            Assert.AreEqual(C("4♣"), Play(view));
        }

        [Test]
        public void ChooseCard_DiscardingWhenTheLongestSuitHoldsOnlyTopCards_KeepsThem()
        {
            // The hearts are all winners (A down to 10), so the discard comes from the clubs.
            PlayerView view = BotViews.For(Seat.South, "8♠ 6♠ 4♠ 2♠ A♥ K♥ Q♥ J♥ 10♥ 9♣ 7♣ 5♣ 3♣")
                .Current(Seat.East, "9♦ A♠ 2♦")
                .Build();

            Assert.AreEqual(C("3♣"), Play(view));
        }

        [Test]
        public void ChooseCard_TrumpedHigherButASpadeIsStillRequired_PlaysItsLowestSpade()
        {
            PlayerView view = BotViews.For(Seat.South, "K♠ 8♠ 4♠ A♥ Q♥ 7♥ 2♥ J♣ 9♣ 8♣ 6♣ 5♣ 4♣")
                .Rules(RuleSet.NepaliStandard.With(discardWhenCannotOvertrump: false))
                .Current(Seat.East, "9♦ A♠ 2♦")
                .Build();

            Assert.AreEqual(C("4♠"), Play(view));
        }

        // ---- Contract ------------------------------------------------------------------------------

        [Test]
        public void ChooseCard_OneLegalCard_PlaysIt()
        {
            PlayerView view = BotViews.For(Seat.East, "A♥ 8♥ 3♥ K♠ 7♠ 2♠ Q♦ 9♦ 5♦ A♣ J♣ 6♣ 2♣")
                .Current(Seat.South, "K♥")
                .Build();

            Assert.AreEqual(C("A♥"), Play(view));
        }

        [Test]
        public void ChooseCard_NotTheViewersPlayTurn_Throws()
        {
            PlayerView view = BotViews.For(Seat.South, SampleHand).Calls(-1, -1, -1, -1).Calling().Build();

            Assert.Throws<InvalidOperationException>(() => Play(view));
        }

        [Test]
        public void ChooseCallAndCard_NullView_Throw()
        {
            var normal = new NormalStrategy();

            Assert.Throws<ArgumentNullException>(() => normal.ChooseCall(null, new Pcg32Random(1UL)));
            Assert.Throws<ArgumentNullException>(() => normal.ChooseCard(null, new Pcg32Random(1UL)));
        }

        private static Card Play(PlayerView view)
        {
            Card card = new NormalStrategy().ChooseCard(view, new Pcg32Random(1UL));
            Assert.IsTrue(view.LegalCards().Contains(card), card + " is not legal");
            return card;
        }
    }
}
