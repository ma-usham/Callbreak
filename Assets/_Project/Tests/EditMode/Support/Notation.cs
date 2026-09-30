using System;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;

namespace Callbreak.Tests.EditMode.Support
{
    /// <summary>Short card notation for tests: <c>C("A♠")</c>, <c>Set("A♠ K♠")</c>, <c>TrickOf(...)</c>.</summary>
    public static class Notation
    {
        public static Card C(string card)
        {
            return Card.Parse(card);
        }

        public static CardSet Set(string cards)
        {
            return CardSet.Parse(cards);
        }

        /// <summary>A trick led by <paramref name="leader"/> with the given cards in play order.</summary>
        public static Trick TrickOf(Seat leader, Direction direction, string cardsInPlayOrder)
        {
            var trick = new Trick(leader, direction);
            foreach (string token in cardsInPlayOrder.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                trick = trick.With(Card.Parse(token));
            }

            return trick;
        }

        /// <summary>A trick led by South, anticlockwise: the usual setting for rule tests.</summary>
        public static Trick Led(string cardsInPlayOrder)
        {
            return TrickOf(Seat.South, Direction.Anticlockwise, cardsInPlayOrder);
        }
    }
}
