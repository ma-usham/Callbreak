using System.Collections.Generic;
using Callbreak.Application.Tutorial;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Tutorial
{
    /// <summary>ADR 084: the tutorial's scripted first hand, through the real engine and rules.</summary>
    public sealed class TutorialDealTests
    {
        private static readonly RuleSet Rules = RuleSet.NepaliStandard;
        private static TutorialDeal Deal => TutorialDeal.First;

        private static MatchEngine NewEngine(IRandom then)
        {
            var seats = new[]
            {
                new SeatProfile("You", "0", "classic", false),
                new SeatProfile("Suman", "1", "classic", true),
                new SeatProfile("Asha", "2", "classic", true),
                new SeatProfile("Bikash", "3", "classic", true),
            };
            return new MatchEngine(Rules, Deal.Random(Rules, then), new MatchSetup("tutorial-test", seats, Deal.Dealer));
        }

        [Test]
        public void Hands_AreFourDisjointThirteens_EachWithASpade()
        {
            CardSet all = CardSet.Empty;
            for (int s = 0; s < 4; s++)
            {
                CardSet hand = Deal.HandOf((Seat)s);
                Assert.AreEqual(13, hand.Count, (Seat)s + " holds 13");
                Assert.IsFalse(all.Overlaps(hand), (Seat)s + " shares no card");
                Assert.IsTrue(hand.Overlaps(CardSet.OfSuit(Suit.Spades)), (Seat)s + " has a spade, so the deal needs no redeal");
                all |= hand;
            }

            Assert.AreEqual(CardSet.FullDeck, all);
        }

        [Test]
        public void Engine_DealsTheScriptedHands_WithoutARedeal_AndThePlayerDeals()
        {
            MatchEngine engine = NewEngine(new Pcg32Random(5));
            engine.Start();
            var events = new List<MatchEvent>();
            engine.DrainEvents(events);

            foreach (MatchEvent e in events)
            {
                Assert.IsNotInstanceOf<Redealt>(e);
                if (e is HandDealt dealt)
                {
                    Assert.AreEqual(Deal.HandOf(dealt.Seat), dealt.Hand, "the hand dealt to " + dealt.Seat);
                }
            }

            Assert.AreEqual(Seat.South, engine.ViewFor(Seat.South).Dealer);
            Assert.AreEqual(Seat.East, engine.CurrentSeat, "the seat on the player's right calls first");
        }

        [Test]
        public void AfterTheFirstDeal_EveryDrawIsTheMatchGenerators()
        {
            var then = new Pcg32Random(99);
            var same = new Pcg32Random(99);
            IRandom random = Deal.Random(Rules, then);

            Deck.Shuffled(random);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(same.NextInt(52), random.NextInt(52));
            }
        }

        [Test]
        public void GuidedTricks_PlayAsTheTutorialTells_AndEveryScriptedCardIsLegal()
        {
            MatchEngine engine = NewEngine(new Pcg32Random(5));
            engine.Start();
            var bot = new TutorialBot(new LowestCard(), Deal);
            var completed = new List<TrickCompleted>();
            var events = new List<MatchEvent>();

            while (completed.Count < Deal.GuidedTricks)
            {
                Seat seat = engine.CurrentSeat;
                PlayerView view = engine.ViewFor(seat);
                CommandResult result;
                if (seat == Seat.South)
                {
                    if (engine.Phase == Phase.Calling)
                    {
                        result = engine.Execute(new PlaceCall(seat, Deal.SuggestedCall, engine.CurrentTurn));
                    }
                    else
                    {
                        Card target = Deal.PlayerCard(view.TrickIndex);
                        Assert.IsTrue(view.LegalCards().Contains(target), "the card the tutorial points at may be played: " + target);
                        result = engine.Execute(new PlayCard(seat, target, engine.CurrentTurn));
                    }
                }
                else if (engine.Phase == Phase.Calling)
                {
                    Assert.IsTrue(Deal.TryGetCall(seat, out int scripted));
                    Assert.AreEqual(scripted, bot.ChooseCall(view, null));
                    result = engine.Execute(new PlaceCall(seat, scripted, engine.CurrentTurn));
                }
                else
                {
                    Assert.IsTrue(Deal.TryGetPlay(seat, view.TrickIndex, out Card scripted));
                    Assert.IsTrue(view.LegalCards().Contains(scripted), seat + "'s scripted " + scripted + " is legal");
                    Assert.AreEqual(scripted, bot.ChooseCard(view, null));
                    result = engine.Execute(new PlayCard(seat, scripted, engine.CurrentTurn));
                }

                Assert.IsTrue(result.IsAccepted, result.ToString());
                events.Clear();
                engine.DrainEvents(events);
                foreach (MatchEvent e in events)
                {
                    if (e is TrickCompleted trick)
                    {
                        completed.Add(trick);
                    }
                }
            }

            Assert.AreEqual(Seat.East, completed[0].Trick.Leader, "a bot leads, so the player follows suit");
            Assert.AreEqual(Suit.Hearts, completed[0].Trick.LedSuit);
            Assert.AreEqual(Seat.West, completed[0].Winner, "the highest heart takes trick 1");
            Assert.AreEqual(Suit.Diamonds, completed[1].Trick.LedSuit);
            Assert.IsFalse(Deal.HandOf(Seat.South).Overlaps(CardSet.OfSuit(Suit.Diamonds)), "the player has no diamond");
            Assert.AreEqual(Seat.South, completed[1].Winner, "the player's spade takes trick 2");
        }

        [Test]
        public void TheSuggestedCall_IsNoMoreThanTheHandIsWorth()
        {
            int estimate = CallEstimator.EstimateHundredths(Deal.HandOf(Seat.South));

            Assert.GreaterOrEqual(estimate, Deal.SuggestedCall * 100, "a beginner can make the call the tutorial points at");
        }

        [Test]
        public void ScriptedPlays_EndWithTheGuidedTricks_AndNeverForThePlayer()
        {
            Assert.IsFalse(Deal.TryGetPlay(Seat.South, 0, out _));
            Assert.IsFalse(Deal.TryGetPlay(Seat.West, Deal.GuidedTricks, out _));
            Assert.IsFalse(Deal.TryGetCall(Seat.South, out _));
        }

        private sealed class LowestCard : IBotStrategy
        {
            public BotLevel Level => BotLevel.Easy;

            public int ChooseCall(PlayerView view, IRandom rng) => 1;

            public Card ChooseCard(PlayerView view, IRandom rng) => view.LegalCards().Lowest();
        }
    }
}
