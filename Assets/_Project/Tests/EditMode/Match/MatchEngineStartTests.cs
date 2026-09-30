using System;
using System.Collections.Generic;
using System.Linq;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Match
{
    public sealed class MatchEngineStartTests
    {
        // Every hand holds a spade and an honour, so no redeal rule fires.
        internal static CardSet[] OrdinaryHands => new[]
        {
            Set("A♣ K♣ Q♣ J♣ 10♣ 9♣ 8♣ 7♣ 6♣ 5♣ 4♣ 3♣ K♠"),
            Set("2♣ A♠ A♥ K♥ Q♥ J♥ 10♥ 9♥ 8♥ 7♥ 6♥ 5♥ 4♥"),
            Set("3♥ 2♥ Q♠ A♦ K♦ Q♦ J♦ 10♦ 9♦ 8♦ 7♦ 6♦ 5♦"),
            Set("4♦ 3♦ 2♦ J♠ 10♠ 9♠ 8♠ 7♠ 6♠ 5♠ 4♠ 3♠ 2♠"),
        };

        // South holds no spade; the rest are ordinary.
        internal static CardSet[] SpadelessHands => new[]
        {
            CardSet.OfSuit(Suit.Hearts),
            CardSet.OfSuit(Suit.Spades).Except(Set("2♠ 3♠ 4♠")).Union(Set("2♣ 3♣ 4♣")),
            CardSet.OfSuit(Suit.Diamonds).Except(Set("2♦")).Union(Set("2♠")),
            CardSet.OfSuit(Suit.Clubs).Except(Set("2♣ 3♣ 4♣")).Union(Set("3♠ 4♠ 2♦")),
        };

        // Every hand holds a spade; East and West hold nothing above 10.
        internal static CardSet[] HonourlessHands => new[]
        {
            Set("A♠ K♠ Q♠ J♠ A♥ K♥ Q♥ J♥ A♦ K♦ Q♦ J♦ A♣"),
            Set("10♠ 9♥ 8♥ 7♥ 6♥ 5♥ 4♥ 3♥ 2♥ 10♥ 9♦ 8♦ 7♦"),
            Set("9♠ 8♠ 7♠ K♣ Q♣ J♣ 10♣ 9♣ 8♣ 7♣ 6♦ 5♦ 4♦"),
            Set("6♠ 5♠ 4♠ 3♠ 2♠ 6♣ 5♣ 4♣ 3♣ 2♣ 10♦ 3♦ 2♦"),
        };

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new MatchEngine(null, new Pcg32Random(1UL), TestSetups.Humans()));
            Assert.Throws<ArgumentNullException>(() => new MatchEngine(RuleSet.NepaliStandard, null, TestSetups.Humans()));
            Assert.Throws<ArgumentNullException>(() => new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(1UL), null));
        }

        [Test]
        public void Constructor_NewEngine_WaitsInLobbyWithNoEvents()
        {
            var engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(1UL), TestSetups.Humans());
            var events = new List<MatchEvent>();

            Assert.AreEqual(Phase.Lobby, engine.Phase);
            Assert.AreEqual(TurnId.None, engine.CurrentTurn);
            Assert.AreEqual(0, engine.Round);
            Assert.AreEqual(0, engine.DrainEvents(events));
            Assert.IsNull(engine.FindInvariantViolation());
        }

        [Test]
        public void Start_FixedDealerWest_EventSequenceUpToSouthsCallTurn()
        {
            var engine = new MatchEngine(RuleSet.NepaliStandard, RiggedDeal.Random(OrdinaryHands, Seat.West, Direction.Anticlockwise), TestSetups.Humans(Seat.West));

            engine.Start();
            List<MatchEvent> events = Drain(engine);

            Assert.AreEqual(7, events.Count);
            var started = (MatchStarted)events[0];
            Assert.AreEqual("test-match", started.MatchId);
            Assert.AreEqual("nepali-standard", started.RuleSetId);
            Assert.AreEqual(Seat.West, started.FirstDealer);
            Assert.AreEqual("Asha", started.Seats[(int)Seat.North].Name);
            var round = (RoundStarted)events[1];
            Assert.AreEqual(1, round.Round);
            Assert.AreEqual(Seat.West, round.Dealer);
            Seat[] dealOrder = { Seat.South, Seat.East, Seat.North, Seat.West };
            for (int i = 0; i < 4; i++)
            {
                var dealt = (HandDealt)events[2 + i];
                Assert.AreEqual(dealOrder[i], dealt.Seat);
                Assert.AreEqual(OrdinaryHands[(int)dealOrder[i]], dealt.Hand);
                Assert.AreEqual(EventVisibility.Private(dealOrder[i]), dealt.Visibility);
            }

            var callTurn = (CallTurnStarted)events[6];
            Assert.AreEqual(Seat.South, callTurn.Seat);
            Assert.AreEqual(new TurnId(1), callTurn.TurnId);
            Assert.AreEqual(Phase.Calling, engine.Phase);
            Assert.AreEqual(Seat.South, engine.CurrentSeat);
            Assert.AreEqual(new TurnId(1), engine.CurrentTurn);
            Assert.AreEqual(1, engine.Round);
        }

        [Test]
        public void Start_RandomFirstDealer_DrawnFromTheInjectedRandom()
        {
            var rng = new SequenceRandom(new[] { 2 }, 5UL);
            var engine = new MatchEngine(RuleSet.NepaliStandard, rng, TestSetups.Humans(firstDealer: null));

            engine.Start();
            List<MatchEvent> events = Drain(engine);

            Assert.AreEqual(Seat.North, ((MatchStarted)events[0]).FirstDealer);
            Assert.AreEqual(Seat.North, ((RoundStarted)events[1]).Dealer);
            Assert.AreEqual(Seat.West, engine.CurrentSeat, "the dealer's right calls first");
        }

        [Test]
        public void Start_SeededDeal_MatchesDeckShuffleDealtFromTheDealersRight()
        {
            const ulong seed = 12UL;
            var engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(seed), TestSetups.Humans(Seat.North));

            engine.Start();
            List<MatchEvent> events = Drain(engine);

            CardSet[] expected = Deck.Deal(Deck.Shuffled(new Pcg32Random(seed)), Seat.West, Direction.Anticlockwise);
            Assert.IsFalse(events.OfType<Redealt>().Any(), "fixture: seed 12 needs no redeal");
            foreach (HandDealt dealt in events.OfType<HandDealt>())
            {
                Assert.AreEqual(expected[(int)dealt.Seat], dealt.Hand);
            }

            Assert.AreEqual(Seat.West, events.OfType<HandDealt>().First().Seat, "first card goes to the dealer's right");
        }

        [Test]
        public void Start_Twice_Throws()
        {
            var engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(1UL), TestSetups.Humans());
            engine.Start();

            Assert.Throws<InvalidOperationException>(() => engine.Start());
        }

        [Test]
        public void Start_SpadelessHandAndRedealOn_RedealsWithTheSameDealer()
        {
            SequenceRandom rng = RiggedDeal.Sequence(
                3UL,
                RiggedDeal.DrawsFor(SpadelessHands, Seat.West, Direction.Anticlockwise),
                RiggedDeal.DrawsFor(OrdinaryHands, Seat.West, Direction.Anticlockwise));
            var engine = new MatchEngine(RuleSet.NepaliStandard, rng, TestSetups.Humans(Seat.West));

            engine.Start();
            List<MatchEvent> events = Drain(engine);

            CollectionAssert.AreEqual(
                new[] { "MatchStarted", "RoundStarted", "Redealt", "HandDealt", "HandDealt", "HandDealt", "HandDealt", "CallTurnStarted" },
                events.Select(e => e.GetType().Name).ToArray());
            var redealt = (Redealt)events[2];
            Assert.AreEqual(RedealReason.NoSpade, redealt.Reason);
            Assert.AreEqual(1, redealt.Round);
            Assert.IsTrue(redealt.Visibility.IsPublic);
            Assert.AreEqual(OrdinaryHands[(int)Seat.South], events.OfType<HandDealt>().Single(h => h.Seat == Seat.South).Hand);
            Assert.AreEqual(Seat.West, ((RoundStarted)events[1]).Dealer);
            Assert.AreEqual(Seat.South, engine.CurrentSeat);
        }

        [Test]
        public void Start_TwoBadDealsInARow_OneRedealtEventEach()
        {
            int[] spadeless = RiggedDeal.DrawsFor(SpadelessHands, Seat.West, Direction.Anticlockwise);
            SequenceRandom rng = RiggedDeal.Sequence(3UL, spadeless, spadeless, RiggedDeal.DrawsFor(OrdinaryHands, Seat.West, Direction.Anticlockwise));
            var engine = new MatchEngine(RuleSet.NepaliStandard, rng, TestSetups.Humans(Seat.West));

            engine.Start();

            Assert.AreEqual(2, Drain(engine).OfType<Redealt>().Count());
            Assert.AreEqual(0, rng.Remaining);
        }

        [Test]
        public void Start_SpadelessHandAndRedealOff_KeepsTheDeal()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(redealIfNoSpade: false);
            var engine = new MatchEngine(rules, RiggedDeal.Random(SpadelessHands, Seat.West, Direction.Anticlockwise), TestSetups.Humans(Seat.West));

            engine.Start();
            List<MatchEvent> events = Drain(engine);

            Assert.IsFalse(events.OfType<Redealt>().Any());
            Assert.AreEqual(CardSet.OfSuit(Suit.Hearts), events.OfType<HandDealt>().Single(h => h.Seat == Seat.South).Hand);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Start_HonourlessHand_RedealsOnlyWhenTheHonourRuleIsOn(bool redealIfNoHonour)
        {
            RuleSet rules = RuleSet.NepaliStandard.With(redealIfNoHonour: redealIfNoHonour);
            SequenceRandom rng = RiggedDeal.Sequence(
                3UL,
                RiggedDeal.DrawsFor(HonourlessHands, Seat.West, Direction.Anticlockwise),
                RiggedDeal.DrawsFor(OrdinaryHands, Seat.West, Direction.Anticlockwise));
            var engine = new MatchEngine(rules, rng, TestSetups.Humans(Seat.West));

            engine.Start();
            List<MatchEvent> events = Drain(engine);

            Redealt[] redeals = events.OfType<Redealt>().ToArray();
            Assert.AreEqual(redealIfNoHonour ? 1 : 0, redeals.Length);
            if (redealIfNoHonour)
            {
                Assert.AreEqual(RedealReason.NoHonour, redeals[0].Reason);
            }

            CardSet[] expected = redealIfNoHonour ? OrdinaryHands : HonourlessHands;
            Assert.AreEqual(expected[(int)Seat.East], events.OfType<HandDealt>().Single(h => h.Seat == Seat.East).Hand);
        }

        [Test]
        public void Start_BrokenRandomThatAlwaysDealsTheSameBadHand_ThrowsInsteadOfLooping()
        {
            var rng = new CyclingRandom(RiggedDeal.DrawsFor(SpadelessHands, Seat.West, Direction.Anticlockwise));
            var engine = new MatchEngine(RuleSet.NepaliStandard, rng, TestSetups.Humans(Seat.West));

            Assert.Throws<InvalidOperationException>(() => engine.Start());
        }

        [TestCase(Direction.Anticlockwise, new[] { Seat.West, Seat.South, Seat.East, Seat.North, Seat.West })]
        [TestCase(Direction.Clockwise, new[] { Seat.West, Seat.North, Seat.East, Seat.South, Seat.West })]
        public void Rounds_FullMatch_DealerPassesInPlayDirectionAndDealersNextCallsFirst(Direction direction, Seat[] dealers)
        {
            RuleSet rules = RuleSet.NepaliStandard.With(direction: direction);
            var driver = new MatchDriver(new MatchEngine(rules, new Pcg32Random(21UL), TestSetups.AllBots(Seat.West)), randomPolicy: false);

            driver.Start();
            driver.PlayToEnd();

            RoundStarted[] rounds = driver.Events.OfType<RoundStarted>().ToArray();
            CollectionAssert.AreEqual(dealers, rounds.Select(r => r.Dealer).ToArray());
            for (int r = 0; r < rounds.Length; r++)
            {
                int index = driver.Events.IndexOf(rounds[r]);
                CallTurnStarted firstCall = driver.Events.Skip(index).OfType<CallTurnStarted>().First();
                Assert.AreEqual(dealers[r].Next(direction), firstCall.Seat, "round " + (r + 1));
                HandDealt firstHand = driver.Events.Skip(index).OfType<HandDealt>().First();
                Assert.AreEqual(dealers[r].Next(direction), firstHand.Seat, "round " + (r + 1) + " deal starts at the dealer's right");
            }
        }

        [Test]
        public void Calling_Clockwise_TurnsGoSouthWestNorthEast()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(direction: Direction.Clockwise);
            var engine = new MatchEngine(rules, new Pcg32Random(4UL), TestSetups.Humans(Seat.East));
            engine.Start();

            var order = new List<Seat>();
            for (int i = 0; i < 4; i++)
            {
                order.Add(engine.CurrentSeat);
                Assert.IsTrue(engine.Execute(new PlaceCall(engine.CurrentSeat, 2, engine.CurrentTurn)).IsAccepted);
            }

            CollectionAssert.AreEqual(new[] { Seat.South, Seat.West, Seat.North, Seat.East }, order);
            Assert.AreEqual(Seat.South, engine.CurrentSeat, "the first caller leads");
        }

        internal static List<MatchEvent> Drain(MatchEngine engine)
        {
            var events = new List<MatchEvent>();
            engine.DrainEvents(events);
            return events;
        }

        /// <summary>Replays one script forever, so every shuffle deals the same cards.</summary>
        private sealed class CyclingRandom : IRandom
        {
            private readonly int[] _script;
            private int _next;

            public CyclingRandom(int[] script)
            {
                _script = script;
            }

            public int NextInt(int maxExclusive)
            {
                int value = _script[_next];
                _next = (_next + 1) % _script.Length;
                return value;
            }
        }
    }
}
