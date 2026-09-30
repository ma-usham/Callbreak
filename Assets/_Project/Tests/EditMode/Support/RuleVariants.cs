using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;

namespace Callbreak.Tests.EditMode.Support
{
    /// <summary>Rule-set variants and random table situations for property tests.</summary>
    public static class RuleVariants
    {
        /// <summary>All 16 combinations of the four legality switches (GDD §5), on top of Nepali Standard.</summary>
        public static IEnumerable<RuleSet> LegalityCombinations()
        {
            for (int mask = 0; mask < 16; mask++)
            {
                yield return RuleSet.NepaliStandard.With(
                    mustBeatLedSuit: (mask & 1) != 0,
                    mustTrumpWhenVoid: (mask & 2) != 0,
                    mustOvertrump: (mask & 4) != 0,
                    discardWhenCannotOvertrump: (mask & 8) != 0);
            }
        }

        /// <summary>
        /// A random mid-trick situation: 0–3 cards already in the trick and a 1–13 card hand drawn from the
        /// rest of a shuffled deck. Small hands make voids common, so every branch of the rules is reached.
        /// </summary>
        public static void RandomSituation(Pcg32Random rng, out CardSet hand, out Trick trick)
        {
            Card[] deck = Deck.Shuffled(rng);
            int trickSize = rng.NextInt(4);
            var leader = (Seat)rng.NextInt(4);
            Direction direction = rng.NextInt(2) == 0 ? Direction.Anticlockwise : Direction.Clockwise;
            trick = new Trick(leader, direction);
            for (int i = 0; i < trickSize; i++)
            {
                trick = trick.With(deck[i]);
            }

            int handSize = 1 + rng.NextInt(13);
            hand = CardSet.Empty;
            for (int i = 0; i < handSize; i++)
            {
                hand = hand.With(deck[trickSize + i]);
            }
        }
    }
}
