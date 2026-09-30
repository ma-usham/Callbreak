using System;
using Callbreak.Core.Cards;
using Callbreak.Core.Rules;

namespace Callbreak.Bots
{
    /// <summary>
    /// The call estimator every bot level starts from (GDD §9 "Call estimator (all levels)"). It adds up the
    /// tricks a hand should take, in hundredths of a trick so the GDD's quarter values stay exact:
    /// <list type="bullet">
    /// <item>Spades: A♠ 1.0; K♠ 1.0 with 2+ spades, else 0.5; Q♠ 0.75 with 3+ spades; each spade beyond the
    /// 3rd 0.5.</item>
    /// <item>Side suits: ace 1.0, or 0.8 when the suit has 6+ cards; king 0.5 with 2–4 cards in the suit;
    /// queen 0.25 with 3–4 cards.</item>
    /// <item>Short side suits: void 0.8, singleton 0.4, but only while a spare spade remains to trump with.
    /// A spare spade is one the spade line did not already count as an honour winner (A♠, K♠, and Q♠ when
    /// it scored); long spades stay spare. Each void, then each singleton, uses up one spare spade.</item>
    /// </list>
    /// The call is <c>clamp(⌊E + 0.3⌋, MinCall, MaxCall)</c>: rounding is conservative because a missed call
    /// costs the whole call while an extra trick earns only 0.1. Pure and allocation-free.
    /// </summary>
    public static class CallEstimator
    {
        /// <summary>One trick, in the estimator's unit (hundredths of a trick).</summary>
        public const int OneTrick = 100;

        /// <summary>The 0.3 added before rounding down (GDD §9 "⌊E + 0.3⌋").</summary>
        public const int RoundingAllowance = 30;

        /// <summary>A♠, always a winner.</summary>
        public const int AceOfSpades = 100;

        /// <summary>K♠ with at least one other spade to guard it.</summary>
        public const int GuardedKingOfSpades = 100;

        /// <summary>A bare K♠.</summary>
        public const int BareKingOfSpades = 50;

        /// <summary>Q♠ with at least two other spades.</summary>
        public const int GuardedQueenOfSpades = 75;

        /// <summary>Each spade beyond the third.</summary>
        public const int LongSpade = 50;

        /// <summary>A side-suit ace.</summary>
        public const int SideAce = 100;

        /// <summary>A side-suit ace in a suit of 6+ cards, which is likelier to be trumped.</summary>
        public const int LongSuitSideAce = 80;

        /// <summary>A side-suit king in a suit of 2–4 cards.</summary>
        public const int SideKing = 50;

        /// <summary>A side-suit queen in a suit of 3–4 cards.</summary>
        public const int SideQueen = 25;

        /// <summary>A void side suit with a spare spade to trump it.</summary>
        public const int RuffableVoid = 80;

        /// <summary>A singleton side suit with a spare spade to trump it once it is gone.</summary>
        public const int RuffableSingleton = 40;

        // A suit's thirteen cards shifted down to bits 0-12 (two … ace).
        private const ulong OneSuit = (1UL << Card.PerSuit) - 1UL;
        private const ulong Ace = 1UL << ((int)Rank.Ace - (int)Rank.Two);
        private const ulong King = 1UL << ((int)Rank.King - (int)Rank.Two);
        private const ulong Queen = 1UL << ((int)Rank.Queen - (int)Rank.Two);

        /// <summary>
        /// The expected tricks of <paramref name="hand"/> in hundredths (450 = 4.5), per the GDD §9 lines above.
        /// The GDD §2 sample hand (A♠ K♠ J♠ 9♠ 4♠ · A♥ J♥ 8♥ · K♦ 10♦ 5♦ · Q♣ 7♣) is 450.
        /// </summary>
        /// <param name="hand">Any set of cards, normally a freshly dealt 13-card hand.</param>
        /// <returns>The estimate, never negative.</returns>
        public static int EstimateHundredths(CardSet hand)
        {
            return Estimate(hand.Bits);
        }

        /// <summary><see cref="EstimateHundredths"/> for a hand given as a card mask (see <see cref="Bits"/>).</summary>
        internal static int Estimate(ulong hand)
        {
            ulong spades = SuitCards(hand, Bits.Spades);
            int spadeCount = Bits.Count(spades);
            int total = 0;
            int honourWinners = 0;

            if ((spades & Ace) != 0UL)
            {
                total += AceOfSpades;
                honourWinners++;
            }

            if ((spades & King) != 0UL)
            {
                total += spadeCount >= 2 ? GuardedKingOfSpades : BareKingOfSpades;
                honourWinners++;
            }

            if (spadeCount >= 3 && (spades & Queen) != 0UL)
            {
                total += GuardedQueenOfSpades;
                honourWinners++;
            }

            if (spadeCount > 3)
            {
                total += LongSpade * (spadeCount - 3);
            }

            int voids = 0;
            int singletons = 0;
            for (int suit = 0; suit < Bits.Spades; suit++)
            {
                ulong cards = SuitCards(hand, suit);
                int length = Bits.Count(cards);
                if (length == 0)
                {
                    voids++;
                    continue;
                }

                if (length == 1)
                {
                    singletons++;
                }

                if ((cards & Ace) != 0UL)
                {
                    total += length >= 6 ? LongSuitSideAce : SideAce;
                }

                if (length >= 2 && length <= 4 && (cards & King) != 0UL)
                {
                    total += SideKing;
                }

                if (length >= 3 && length <= 4 && (cards & Queen) != 0UL)
                {
                    total += SideQueen;
                }
            }

            int spare = spadeCount - honourWinners;
            int ruffedVoids = Math.Min(voids, spare);
            total += RuffableVoid * ruffedVoids;
            spare -= ruffedVoids;
            total += RuffableSingleton * Math.Min(singletons, spare);
            return total;
        }

        /// <summary>
        /// <c>⌊E + 0.3⌋</c> for an estimate in hundredths, before clamping to the rule set's range.
        /// </summary>
        /// <param name="estimateHundredths">An estimate from <see cref="EstimateHundredths"/>.</param>
        /// <returns>The rounded-down call; 4 for 450.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The estimate is negative.</exception>
        public static int RoundDown(int estimateHundredths)
        {
            if (estimateHundredths < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(estimateHundredths), estimateHundredths, "An estimate is never negative.");
            }

            return (estimateHundredths + RoundingAllowance) / OneTrick;
        }

        /// <summary>
        /// The estimator's call: <c>clamp(⌊E + 0.3⌋, MinCall, MaxCall)</c> (GDD §9). Normal calls this, Easy starts
        /// from it, and Hard uses it for the seats whose calls it has to guess.
        /// </summary>
        /// <param name="hand">The hand to call on.</param>
        /// <param name="rules">The match's rule set, for MinCall and MaxCall.</param>
        /// <returns>A legal call.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public static int Call(CardSet hand, RuleSet rules)
        {
            return Call(hand.Bits, rules);
        }

        /// <summary><see cref="Call(CardSet, RuleSet)"/> for a hand given as a card mask.</summary>
        internal static int Call(ulong hand, RuleSet rules)
        {
            return CallRules.Clamp(RoundDown(Estimate(hand)), rules);
        }

        private static ulong SuitCards(ulong hand, int suit)
        {
            return (hand >> (suit * Card.PerSuit)) & OneSuit;
        }
    }
}
