using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;

namespace Callbreak.Core.Rules
{
    /// <summary>
    /// Automatic redeal conditions (GDD §2 "Redeal"; §5 RedealIfNoSpade, RedealIfNoHonour). The engine
    /// checks a fresh deal with <see cref="Check"/> and, while it returns a reason, reshuffles and deals
    /// again with the same dealer.
    /// </summary>
    public static class RedealPolicy
    {
        /// <summary>
        /// The reason the deal must be redealt, or <see cref="RedealReason.None"/>. When both conditions
        /// hold and both rules are on, <see cref="RedealReason.NoSpade"/> is reported.
        /// </summary>
        /// <param name="hands">The four dealt hands.</param>
        /// <param name="rules">The match's rule set.</param>
        /// <returns>Why to redeal, or None.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static RedealReason Check(IReadOnlyList<CardSet> hands, RuleSet rules)
        {
            if (hands == null)
            {
                throw new ArgumentNullException(nameof(hands));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            bool missingSpade = false;
            bool missingHonour = false;
            for (int i = 0; i < hands.Count; i++)
            {
                missingSpade |= !hands[i].HasSuit(Suit.Spades);
                missingHonour |= !HasHonour(hands[i]);
            }

            if (rules.RedealIfNoSpade && missingSpade)
            {
                return RedealReason.NoSpade;
            }

            return rules.RedealIfNoHonour && missingHonour ? RedealReason.NoHonour : RedealReason.None;
        }

        /// <summary>Whether the hand holds a card above 10: a jack, queen, king or ace of any suit.</summary>
        /// <param name="hand">Any set of cards.</param>
        /// <returns>True if some card outranks 10.</returns>
        public static bool HasHonour(CardSet hand)
        {
            return hand.Overlaps(CardSet.Above(new Card(Suit.Clubs, Rank.Ten)))
                || hand.Overlaps(CardSet.Above(new Card(Suit.Diamonds, Rank.Ten)))
                || hand.Overlaps(CardSet.Above(new Card(Suit.Hearts, Rank.Ten)))
                || hand.Overlaps(CardSet.Above(new Card(Suit.Spades, Rank.Ten)));
        }
    }
}
