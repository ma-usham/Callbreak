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
    /// GDD §9 Hard: "samples deals of the unseen cards that are consistent with everything observed (played
    /// cards, voids)".
    /// </summary>
    public sealed class DealSamplerTests
    {
        private const string SouthHand = "A♠ K♠ J♠ 9♠ 4♠ A♥ J♥ 8♥ K♦ 10♦ 5♦ Q♣ 7♣";

        [Test]
        public void Deal_FirstLead_EverySeatGetsThirteenOfTheUnseenCards()
        {
            var tracker = new CardTracker(BotViews.For(Seat.South, SouthHand).Build());
            Assert.IsTrue(DealSampler.TryCreate(tracker, out DealSampler sampler));
            var rng = new Pcg32Random(5UL);

            for (int i = 0; i < 50; i++)
            {
                AssertConsistent(tracker, Deal(sampler, rng));
            }

            Assert.IsFalse(sampler.IgnoredConstraints);
        }

        [Test]
        public void Deal_SameSeed_SameDeals()
        {
            var tracker = new CardTracker(NorthVoidInHeartsAndSpades());
            Assert.IsTrue(DealSampler.TryCreate(tracker, out DealSampler first));
            Assert.IsTrue(DealSampler.TryCreate(tracker, out DealSampler second));
            var rngA = new Pcg32Random(42UL);
            var rngB = new Pcg32Random(42UL);

            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.AreEqual(Deal(first, rngA), Deal(second, rngB));
            }
        }

        [Test]
        public void Deal_SeatKnownVoid_NeverGetsThoseSuits()
        {
            var tracker = new CardTracker(NorthVoidInHeartsAndSpades());
            Assert.IsTrue(DealSampler.TryCreate(tracker, out DealSampler sampler));
            var rng = new Pcg32Random(9UL);

            for (int i = 0; i < 200; i++)
            {
                CardSet[] hands = Deal(sampler, rng);

                AssertConsistent(tracker, hands);
                Assert.IsFalse(hands[(int)Seat.North].HasSuit(Suit.Hearts));
                Assert.IsFalse(hands[(int)Seat.North].HasSuit(Suit.Spades));
                Assert.IsFalse((hands[(int)Seat.East] | hands[(int)Seat.North] | hands[(int)Seat.West]).Contains(C("A♦")));
            }
        }

        [Test]
        public void Deal_CardsOnlyOneSeatCanHold_AlwaysGoToIt()
        {
            // East and North both showed out of hearts, so the five hearts South cannot see are all West's.
            PlayerView view = BotViews.For(Seat.South, "K♥ Q♥ J♥ 10♥ 9♥ 8♥ A♠ 7♠ K♦ 5♦ A♣ 9♣")
                .Trick(Seat.South, "A♥ 3♣ 2♠ 2♥")
                .Current(Seat.North, "4♦ 9♦")
                .Build();
            var tracker = new CardTracker(view);
            Assert.IsTrue(DealSampler.TryCreate(tracker, out DealSampler sampler));
            var rng = new Pcg32Random(11UL);

            for (int i = 0; i < 100; i++)
            {
                CardSet[] hands = Deal(sampler, rng);

                AssertConsistent(tracker, hands);
                Assert.IsTrue(Set("3♥ 4♥ 5♥ 6♥ 7♥").IsSubsetOf(hands[(int)Seat.West]));
                Assert.IsFalse(hands[(int)Seat.East].HasSuit(Suit.Spades), "East discarded instead of trumping");
            }

            Assert.IsFalse(sampler.IgnoredConstraints);
        }

        [Test]
        public void Deal_Unconstrained_ACardLandsWithEachSeatAboutAThirdOfTheTime()
        {
            var tracker = new CardTracker(BotViews.For(Seat.South, SouthHand).Build());
            Assert.IsTrue(DealSampler.TryCreate(tracker, out DealSampler sampler));
            var rng = new Pcg32Random(21UL);
            var holders = new int[4];
            Card aceOfClubs = C("A♣");

            for (int i = 0; i < 3000; i++)
            {
                CardSet[] hands = Deal(sampler, rng);
                for (int s = 0; s < 4; s++)
                {
                    if (hands[s].Contains(aceOfClubs))
                    {
                        holders[s]++;
                    }
                }
            }

            Assert.AreEqual(0, holders[(int)Seat.South]);
            Assert.That(holders[(int)Seat.East], Is.InRange(900, 1100));
            Assert.That(holders[(int)Seat.North], Is.InRange(900, 1100));
            Assert.That(holders[(int)Seat.West], Is.InRange(900, 1100));
        }

        [Test]
        public void TryCreate_HandCountsDoNotAddUp_False()
        {
            PlayerView malformed = BotViews.WithHandCounts(BotViews.For(Seat.South, SouthHand).Build(), 13, 12, 13, 13);

            Assert.IsFalse(DealSampler.TryCreate(new CardTracker(malformed), out _));
        }

        [Test]
        public void TryCreate_ContradictoryExclusions_IgnoresThemAndStillDeals()
        {
            // Impossible in a real game: all three others showed out of hearts, yet hearts are still unseen.
            PlayerView view = BotViews.For(Seat.South, "K♥ 9♥ A♠ 7♠ 4♠ K♦ 5♦ 2♦ A♣ 9♣ 6♣ 2♣")
                .Trick(Seat.South, "2♥ 3♣ 4♣ 5♣")
                .Build();
            var tracker = new CardTracker(view);

            Assert.IsTrue(DealSampler.TryCreate(tracker, out DealSampler sampler));
            Assert.IsTrue(sampler.IgnoredConstraints);
            CardSet[] hands = Deal(sampler, new Pcg32Random(1UL));
            CardSet dealt = CardSet.Empty;
            for (int s = 1; s < 4; s++)
            {
                Assert.AreEqual(12, hands[s].Count);
                dealt |= hands[s];
            }

            Assert.AreEqual(tracker.Unseen, dealt);
        }

        [Test]
        public void Deal_RandomPlayUnderEveryLegalityCombination_AlwaysFitsWhatWasObserved()
        {
            int deals = 0;
            ulong seed = 900UL;
            var bots = new IBotStrategy[] { new RandomBot(), new RandomBot(), new RandomBot(), new RandomBot() };
            var rng = new Pcg32Random(4UL);
            foreach (RuleSet legality in RuleVariants.LegalityCombinations())
            {
                RuleSet rules = legality.With(rounds: 2, direction: seed % 2 == 0 ? Direction.Clockwise : Direction.Anticlockwise);
                BotMatches.Play(rules, seed++, bots, null, (engine, view) =>
                {
                    var tracker = new CardTracker(view);
                    Assert.IsTrue(DealSampler.TryCreate(tracker, out DealSampler sampler), "real views always add up");
                    Assert.IsFalse(sampler.IgnoredConstraints, "real observations never contradict each other");
                    AssertConsistent(tracker, Deal(sampler, rng));
                    deals++;
                });
            }

            Assert.Greater(deals, 1000);
        }

        /// <summary>One sampled deal, as card sets by seat (the sampler itself works on card masks).</summary>
        private static CardSet[] Deal(DealSampler sampler, IRandom rng)
        {
            var masks = new ulong[4];
            sampler.Deal(rng, masks);
            var hands = new CardSet[4];
            for (int s = 0; s < 4; s++)
            {
                hands[s] = CardSet.FromBits(masks[s]);
            }

            return hands;
        }

        /// <summary>The viewer keeps its hand; every other seat gets its count, nothing it cannot hold, and together exactly the unseen cards.</summary>
        private static void AssertConsistent(CardTracker tracker, CardSet[] hands)
        {
            Assert.AreEqual(tracker.Hand, hands[(int)tracker.Viewer]);
            CardSet dealt = CardSet.Empty;
            for (int s = 0; s < 4; s++)
            {
                var seat = (Seat)s;
                if (seat == tracker.Viewer)
                {
                    continue;
                }

                Assert.AreEqual(tracker.HandCountOf(seat), hands[s].Count, seat + " has the wrong number of cards");
                Assert.IsFalse(hands[s].Overlaps(tracker.CannotHold(seat)), seat + " was dealt a card it cannot hold");
                Assert.IsFalse(hands[s].Overlaps(dealt), "a card was dealt twice");
                dealt |= hands[s];
            }

            Assert.AreEqual(tracker.Unseen, dealt);
        }

        /// <summary>South on lead at the third trick; North discarded on a heart lead, so it has no hearts and no spades.</summary>
        private static PlayerView NorthVoidInHeartsAndSpades()
        {
            return BotViews.For(Seat.South, "A♦ Q♦ 3♦ K♥ 7♥ 3♥ A♠ 9♠ 4♠ J♣ 5♣")
                .Trick(Seat.South, "A♥ 5♥ 9♣ 2♥")
                .Trick(Seat.South, "K♦ 4♦ 8♦ 6♦")
                .Build();
        }
    }
}
