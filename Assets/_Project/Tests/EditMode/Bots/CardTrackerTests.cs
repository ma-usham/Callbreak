using System;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Bots
{
    /// <summary>
    /// GDD §9 "CardTracker: seen cards · voids · calls vs won" and rule 5 (memory). Seats play South, East,
    /// North, West (anticlockwise).
    /// </summary>
    public sealed class CardTrackerTests
    {
        [Test]
        public void Constructor_NullView_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CardTracker(null));
        }

        [Test]
        public void Constructor_CallTurn_KnowsOnlyTheHand()
        {
            PlayerView view = BotViews.For(Seat.South, "A♠ K♠ J♠ 9♠ 4♠ A♥ J♥ 8♥ K♦ 10♦ 5♦ Q♣ 7♣")
                .Calls(-1, -1, -1, -1)
                .Calling()
                .Build();

            var tracker = new CardTracker(view);

            Assert.AreEqual(Seat.South, tracker.Viewer);
            Assert.AreEqual(view.Hand, tracker.Hand);
            Assert.AreEqual(CardSet.Empty, tracker.Played);
            Assert.AreEqual(CardSet.FullDeck - view.Hand, tracker.Unseen);
            Assert.AreEqual(0, tracker.TrickIndex);
            Assert.IsTrue(tracker.CurrentTrick.IsEmpty);
            for (int s = 0; s < 4; s++)
            {
                var seat = (Seat)s;
                Assert.AreEqual(13, tracker.HandCountOf(seat));
                Assert.AreEqual(CardSet.Empty, tracker.CannotHold(seat));
                Assert.AreEqual(CardSet.Empty, tracker.PlayedBy(seat));
                Assert.AreEqual(CallRules.NotCalled, tracker.CallOf(seat));
                Assert.AreEqual(0, tracker.TricksNeededBy(seat), "no call yet, nothing needed");
                Assert.AreEqual(seat == Seat.South ? view.Hand : tracker.Unseen, tracker.PossibleCards(seat));
            }
        }

        [Test]
        public void Played_AfterTwoTricks_HandPlayedAndUnseenPartitionTheDeck()
        {
            var tracker = new CardTracker(SouthLeadsTheThirdTrick().Build());

            Assert.AreEqual(Set("A♥ 5♥ 9♣ 2♥ K♦ 4♦ 8♦ 6♦"), tracker.Played);
            Assert.AreEqual(33, tracker.Unseen.Count);
            Assert.AreEqual(CardSet.FullDeck, tracker.Hand | tracker.Played | tracker.Unseen);
            Assert.IsFalse(tracker.Unseen.Overlaps(tracker.Hand | tracker.Played));
            Assert.AreEqual(2, tracker.TrickIndex);
            Assert.AreEqual(Set("A♥ K♦"), tracker.PlayedBy(Seat.South));
            Assert.AreEqual(Set("9♣ 8♦"), tracker.PlayedBy(Seat.North));
            for (int s = 0; s < 4; s++)
            {
                Assert.AreEqual(11, tracker.HandCountOf((Seat)s));
            }
        }

        [Test]
        public void IsVoid_SeatFailedToFollowSuit_VoidForTheRestOfTheRound()
        {
            // Rule 5: North showed out of hearts in the first trick; two tricks later it is still void.
            var tracker = new CardTracker(SouthLeadsTheThirdTrick().Build());

            Assert.IsTrue(tracker.IsVoid(Seat.North, Suit.Hearts));
            Assert.IsFalse(tracker.IsVoid(Seat.North, Suit.Diamonds));
            Assert.IsFalse(tracker.IsVoid(Seat.East, Suit.Hearts));
            Assert.IsFalse(tracker.IsVoid(Seat.West, Suit.Hearts));
        }

        [Test]
        public void MayHoldSpades_VoidSeatDiscardedInsteadOfTrumping_NoSpades()
        {
            // Void in the led suit with no spade in the trick, a seat must trump if it can (GDD §3).
            var tracker = new CardTracker(SouthLeadsTheThirdTrick().Build());

            Assert.IsFalse(tracker.MayHoldSpades(Seat.North));
            Assert.IsTrue(tracker.IsVoid(Seat.North, Suit.Spades));
            Assert.IsTrue(tracker.MayHoldSpades(Seat.East));
            Assert.AreEqual(CardSet.OfSuit(Suit.Hearts) | CardSet.OfSuit(Suit.Spades) | Set("A♦"), tracker.CannotHold(Seat.North));
        }

        [Test]
        public void MayHoldSpades_VoidSeatDiscardedMustTrumpOff_OnlyTheVoidIsKnown()
        {
            PlayerView view = SouthLeadsTheThirdTrick().Rules(RuleSet.NepaliStandard.With(mustTrumpWhenVoid: false)).Build();

            var tracker = new CardTracker(view);

            Assert.IsTrue(tracker.IsVoid(Seat.North, Suit.Hearts));
            Assert.IsTrue(tracker.MayHoldSpades(Seat.North));
        }

        [Test]
        public void MayHoldSpades_VoidSeatTrumped_StillMayHoldSpades()
        {
            PlayerView view = BotViews.For(Seat.West, "A♥ K♥ 9♥ Q♠ 8♠ K♦ 7♦ 3♦ A♣ 10♣ 6♣ 5♣")
                .Trick(Seat.South, "7♥ 5♥ 3♠ 2♥")
                .Current(Seat.North, "4♣")
                .Build();

            var tracker = new CardTracker(view);

            Assert.IsTrue(tracker.IsVoid(Seat.North, Suit.Hearts));
            Assert.IsTrue(tracker.MayHoldSpades(Seat.North));
            Assert.AreEqual(CardSet.OfSuit(Suit.Hearts), tracker.CannotHold(Seat.North));
        }

        [Test]
        public void CannotHold_FollowedBelowTheBestCard_NothingHigherInThatSuit()
        {
            // East had to beat the 10♥ if it could (GDD §3 rule 2) and played the 4♥.
            var tracker = new CardTracker(NorthToPlayAfterTenAndFourOfHearts(RuleSet.NepaliStandard));

            Assert.AreEqual(Set("J♥ Q♥ K♥ A♥"), tracker.CannotHold(Seat.East));
            Assert.IsFalse(tracker.IsVoid(Seat.East, Suit.Hearts));
            Assert.IsFalse(tracker.PossibleCards(Seat.East).Overlaps(Set("J♥ Q♥ K♥")));
        }

        [Test]
        public void CannotHold_FollowedBelowTheBestCardMustBeatOff_NoInference()
        {
            var tracker = new CardTracker(NorthToPlayAfterTenAndFourOfHearts(RuleSet.NepaliStandard.With(mustBeatLedSuit: false)));

            Assert.AreEqual(CardSet.Empty, tracker.CannotHold(Seat.East));
        }

        [Test]
        public void CannotHold_FollowedBelowTheBestSpadeOnASpadeLead_NoHigherSpade()
        {
            PlayerView view = BotViews.For(Seat.North, "A♥ 8♥ 3♥ K♠ 7♠ 2♠ Q♦ 9♦ 5♦ A♣ J♣ 6♣ 2♣")
                .Current(Seat.South, "Q♠ 5♠")
                .Build();

            var tracker = new CardTracker(view);

            Assert.AreEqual(Set("K♠ A♠"), tracker.CannotHold(Seat.East));
            Assert.IsTrue(tracker.MayHoldSpades(Seat.East));
        }

        [Test]
        public void CannotHold_FollowedLowAfterTheTrickWasTrumped_NoInference()
        {
            // Once a spade is in the trick, any card of the led suit may be played.
            PlayerView view = BotViews.For(Seat.West, "A♥ 8♥ 3♥ K♠ 7♠ 3♠ Q♦ 9♦ 5♦ A♣ J♣ 6♣ 2♣")
                .Current(Seat.South, "10♥ 2♠ 4♥")
                .Build();

            var tracker = new CardTracker(view);

            Assert.AreEqual(CardSet.Empty, tracker.CannotHold(Seat.North));
            Assert.AreEqual(CardSet.OfSuit(Suit.Hearts), tracker.CannotHold(Seat.East));
        }

        [Test]
        public void CannotHold_VoidSeatDiscardedUnderAHigherTrump_NoHigherSpadeButMaybeLower()
        {
            // North could not beat the 9♠, so it was free to discard (GDD §3 rule 3, §5 free discard).
            var tracker = new CardTracker(EastLeadsAfterTrumpingWithTheNine(RuleSet.NepaliStandard, "4♣"));

            Assert.AreEqual(CardSet.OfSuit(Suit.Hearts) | Set("10♠ J♠ Q♠ K♠ A♠"), tracker.CannotHold(Seat.North));
            Assert.IsTrue(tracker.MayHoldSpades(Seat.North));
        }

        [Test]
        public void CannotHold_VoidSeatUndertrumped_NoHigherSpade()
        {
            var tracker = new CardTracker(EastLeadsAfterTrumpingWithTheNine(RuleSet.NepaliStandard, "6♠"));

            Assert.AreEqual(CardSet.OfSuit(Suit.Hearts) | Set("10♠ J♠ Q♠ K♠ A♠"), tracker.CannotHold(Seat.North));
        }

        [Test]
        public void CannotHold_VoidSeatUndertrumpedMustOvertrumpOff_OnlyTheVoidIsKnown()
        {
            var tracker = new CardTracker(EastLeadsAfterTrumpingWithTheNine(RuleSet.NepaliStandard.With(mustOvertrump: false), "6♠"));

            Assert.AreEqual(CardSet.OfSuit(Suit.Hearts), tracker.CannotHold(Seat.North));
        }

        [TestCase(false, true, TestName = "CannotHold_VoidSeatDiscardedUnderAHigherTrumpMustOvertrumpOff_NoSpades")]
        [TestCase(true, false, TestName = "CannotHold_VoidSeatDiscardedUnderAHigherTrumpNoFreeDiscard_NoSpades")]
        public void CannotHold_VoidSeatDiscardedWhenASpadeWasStillRequired(bool mustOvertrump, bool freeDiscard)
        {
            RuleSet rules = RuleSet.NepaliStandard.With(mustOvertrump: mustOvertrump, discardWhenCannotOvertrump: freeDiscard);

            var tracker = new CardTracker(EastLeadsAfterTrumpingWithTheNine(rules, "4♣"));

            Assert.IsFalse(tracker.MayHoldSpades(Seat.North));
        }

        [Test]
        public void IsTopRemaining_HigherCardsPlayedOrInOwnHand_True()
        {
            var tracker = new CardTracker(SouthLeadsTheThirdTrick().Build());

            Assert.IsTrue(tracker.IsTopRemaining(C("K♥")), "the A♥ has been played");
            Assert.IsTrue(tracker.IsTopRemaining(C("A♠")));
            Assert.IsTrue(tracker.IsTopRemaining(C("Q♦")), "the K♦ is played and the A♦ is in the viewer's own hand");
            Assert.IsFalse(tracker.IsTopRemaining(C("7♥")), "8♥ to Q♥ are still out");
            Assert.IsFalse(tracker.IsTopRemaining(C("9♠")));
            Assert.IsFalse(tracker.IsTopRemaining(C("J♣")));
        }

        [Test]
        public void TricksNeededBy_CallsVersusTricksWon_NeverNegative()
        {
            var tracker = new CardTracker(SouthLeadsTheThirdTrick().Calls(1, 2, -1, 4).Build());

            Assert.AreEqual(2, tracker.TricksWonBy(Seat.South));
            Assert.AreEqual(0, tracker.TricksNeededBy(Seat.South), "call 1, won 2");
            Assert.AreEqual(2, tracker.TricksNeededBy(Seat.East));
            Assert.AreEqual(0, tracker.TricksNeededBy(Seat.North), "not called");
            Assert.AreEqual(4, tracker.TricksNeededBy(Seat.West));
            Assert.AreEqual(4, tracker.CallOf(Seat.West));
        }

        [Test]
        public void Queries_UndefinedSeat_Throw()
        {
            var tracker = new CardTracker(SouthLeadsTheThirdTrick().Build());

            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.CannotHold((Seat)4));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.PossibleCards((Seat)4));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.TricksNeededBy((Seat)9));
        }

        [Test]
        public void CannotHold_RandomPlayUnderEveryLegalityCombination_NeverExcludesAHeldCard()
        {
            int checkedViews = 0;
            ulong seed = 300UL;
            var bots = new IBotStrategy[] { new RandomBot(), new RandomBot(), new RandomBot(), new RandomBot() };
            foreach (RuleSet legality in RuleVariants.LegalityCombinations())
            {
                RuleSet rules = legality.With(rounds: 2, direction: seed % 2 == 0 ? Direction.Anticlockwise : Direction.Clockwise);
                BotMatches.Play(rules, seed++, bots, null, (engine, acting) =>
                {
                    CardSet[] truth = BotMatches.TrueHands(engine);
                    for (int v = 0; v < 4; v++)
                    {
                        var tracker = new CardTracker(engine.ViewFor((Seat)v));
                        CardSet others = CardSet.Empty;
                        for (int s = 0; s < 4; s++)
                        {
                            var seat = (Seat)s;
                            Assert.IsFalse(truth[s].Overlaps(tracker.CannotHold(seat)), rules + ": " + seat + " holds a card it 'cannot hold'");
                            Assert.IsTrue(truth[s].IsSubsetOf(tracker.PossibleCards(seat)));
                            Assert.AreEqual(truth[s].Count, tracker.HandCountOf(seat));
                            if (s != v)
                            {
                                others |= truth[s];
                            }
                        }

                        Assert.AreEqual(others, tracker.Unseen);
                        Assert.AreEqual(truth[v], tracker.Hand);
                        checkedViews++;
                    }
                });
            }

            Assert.Greater(checkedViews, 5000);
        }

        [Test]
        public void Record_EveryPlayOfARound_MatchesTheTrackerRebuiltFromTheView()
        {
            int compared = 0;
            PlayerView start = null;
            var bots = new IBotStrategy[] { new RandomBot(), new RandomBot(), new RandomBot(), new RandomBot() };
            BotMatches.Play(RuleSet.NepaliStandard.With(rounds: 3), 77UL, bots, null, (engine, acting) =>
            {
                PlayerView now = engine.ViewFor(Seat.West);
                if (now.CompletedTricks.Count == 0 && now.CurrentTrick.IsEmpty)
                {
                    start = now;
                    return;
                }

                var advanced = new CardTracker(start);
                foreach (CompletedTrick completed in now.CompletedTricks)
                {
                    RecordAll(advanced, completed.Trick);
                }

                RecordAll(advanced, now.CurrentTrick);
                AssertSame(new CardTracker(now), advanced);
                compared++;
            });

            Assert.Greater(compared, 100);
        }

        private static void RecordAll(CardTracker tracker, Trick trick)
        {
            for (int i = 0; i < trick.Count; i++)
            {
                tracker.Record(trick.SeatAt(i), trick[i]);
            }
        }

        private static void AssertSame(CardTracker expected, CardTracker actual)
        {
            Assert.AreEqual(expected.Hand, actual.Hand);
            Assert.AreEqual(expected.Played, actual.Played);
            Assert.AreEqual(expected.Unseen, actual.Unseen);
            Assert.AreEqual(expected.CurrentTrick, actual.CurrentTrick);
            Assert.AreEqual(expected.TrickIndex, actual.TrickIndex);
            for (int s = 0; s < 4; s++)
            {
                var seat = (Seat)s;
                Assert.AreEqual(expected.CallOf(seat), actual.CallOf(seat));
                Assert.AreEqual(expected.TricksWonBy(seat), actual.TricksWonBy(seat));
                Assert.AreEqual(expected.HandCountOf(seat), actual.HandCountOf(seat));
                Assert.AreEqual(expected.PlayedBy(seat), actual.PlayedBy(seat));
                Assert.AreEqual(expected.CannotHold(seat), actual.CannotHold(seat));
                Assert.AreEqual(expected.PossibleCards(seat), actual.PossibleCards(seat));
            }
        }

        /// <summary>
        /// South won the first two tricks (A♥, K♦) and is on lead. North discarded the 9♣ on the heart lead;
        /// nobody could beat the K♦, so the A♦ can only be South's.
        /// </summary>
        private static BotViews SouthLeadsTheThirdTrick()
        {
            return BotViews.For(Seat.South, "A♦ Q♦ 3♦ K♥ 7♥ 3♥ A♠ 9♠ 4♠ J♣ 5♣")
                .Trick(Seat.South, "A♥ 5♥ 9♣ 2♥")
                .Trick(Seat.South, "K♦ 4♦ 8♦ 6♦");
        }

        private static PlayerView NorthToPlayAfterTenAndFourOfHearts(RuleSet rules)
        {
            return BotViews.For(Seat.North, "A♥ 8♥ 3♥ K♠ 7♠ 2♠ Q♦ 9♦ 5♦ A♣ J♣ 6♣ 2♣")
                .Rules(rules)
                .Current(Seat.South, "10♥ 4♥")
                .Build();
        }

        /// <summary>South led the 7♥, East trumped with the 9♠, North played <paramref name="northCard"/>, West the 2♥.</summary>
        private static PlayerView EastLeadsAfterTrumpingWithTheNine(RuleSet rules, string northCard)
        {
            return BotViews.For(Seat.East, "K♠ 5♠ A♦ Q♦ 9♦ 6♦ 3♦ K♣ J♣ 8♣ 5♣ 2♣")
                .Rules(rules)
                .Trick(Seat.South, "7♥ 9♠ " + northCard + " 2♥")
                .Build();
        }
    }
}
