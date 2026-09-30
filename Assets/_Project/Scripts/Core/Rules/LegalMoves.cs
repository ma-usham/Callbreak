using System;
using Callbreak.Core.Cards;

namespace Callbreak.Core.Rules
{
    /// <summary>
    /// The legal-play decision of GDD §3 (FIG 3.1) as the §22 algorithm. The same function runs on the
    /// server (authority), the client (highlighting) and in bots. Pure and allocation-free.
    /// </summary>
    public static class LegalMoves
    {
        /// <summary>
        /// The cards <paramref name="hand"/> may play to <paramref name="trick"/>. Never empty for a non-empty
        /// hand, and always a subset of it:
        /// leading → the whole hand (spades may be led at any time);
        /// holding the led suit → with <see cref="RuleSet.MustBeatLedSuit"/> and an untrumped trick, the
        /// led-suit cards above the highest led-suit card if any, else every led-suit card (a spade lead is
        /// ordinary following);
        /// void, holding spades, <see cref="RuleSet.MustTrumpWhenVoid"/> → spades that beat the highest
        /// spade played (any spade if none, or without <see cref="RuleSet.MustOvertrump"/>); if none can, the
        /// whole hand with <see cref="RuleSet.DiscardWhenCannotOvertrump"/>, else every spade;
        /// otherwise → the whole hand.
        /// </summary>
        /// <param name="hand">The acting player's cards.</param>
        /// <param name="trick">The trick so far.</param>
        /// <param name="rules">The match's rule set.</param>
        /// <returns>The legal cards.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public static CardSet Compute(CardSet hand, in Trick trick, RuleSet rules)
        {
            return Evaluate(hand, trick, rules, out _, out _, out _, out _);
        }

        /// <summary>
        /// Classifies the obligation behind <see cref="Compute"/> for the turn prompt and for explaining a
        /// rejected tap (GDD §11 screen 08 "Turn prompt copy").
        /// </summary>
        /// <param name="hand">The acting player's cards.</param>
        /// <param name="trick">The trick so far.</param>
        /// <param name="rules">The match's rule set.</param>
        /// <returns>The obligation and the legal cards.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public static PlayObligation Classify(CardSet hand, in Trick trick, RuleSet rules)
        {
            CardSet legal = Evaluate(hand, trick, rules, out ObligationKind kind, out Suit suit, out bool hasCardToBeat, out Card cardToBeat);
            return new PlayObligation(kind, legal, suit, hasCardToBeat, cardToBeat);
        }

        /// <summary>Whether playing <paramref name="card"/> from <paramref name="hand"/> is legal.</summary>
        /// <param name="card">The card the player wants to play.</param>
        /// <param name="hand">The acting player's cards.</param>
        /// <param name="trick">The trick so far.</param>
        /// <param name="rules">The match's rule set.</param>
        /// <returns>False for a card not in the hand or outside the legal set.</returns>
        public static bool IsLegal(Card card, CardSet hand, in Trick trick, RuleSet rules)
        {
            return Compute(hand, trick, rules).Contains(card);
        }

        private static CardSet Evaluate(
            CardSet hand,
            in Trick trick,
            RuleSet rules,
            out ObligationKind kind,
            out Suit suit,
            out bool hasCardToBeat,
            out Card cardToBeat)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            suit = Suit.Clubs;
            hasCardToBeat = false;
            cardToBeat = default;

            if (trick.IsEmpty)
            {
                kind = ObligationKind.LeadAny;
                return hand;
            }

            Suit led = trick.LedSuit;
            CardSet played = trick.Cards;
            CardSet follow = hand.InSuit(led);
            if (!follow.IsEmpty)
            {
                suit = led;
                if (rules.MustBeatLedSuit && !trick.IsTrumped)
                {
                    Card best = played.InSuit(led).Highest();
                    CardSet higher = follow & CardSet.Above(best);
                    if (!higher.IsEmpty)
                    {
                        kind = ObligationKind.BeatCard;
                        hasCardToBeat = true;
                        cardToBeat = best;
                        return higher;
                    }
                }

                kind = ObligationKind.FollowSuit;
                return follow;
            }

            CardSet spades = hand.InSuit(Suit.Spades);
            if (rules.MustTrumpWhenVoid && !spades.IsEmpty)
            {
                suit = Suit.Spades;
                kind = ObligationKind.TrumpWithSpade;
                CardSet playedSpades = played.InSuit(Suit.Spades);
                if (!playedSpades.IsEmpty && rules.MustOvertrump)
                {
                    Card topSpade = playedSpades.Highest();
                    CardSet winners = spades & CardSet.Above(topSpade);
                    if (!winners.IsEmpty)
                    {
                        hasCardToBeat = true;
                        cardToBeat = topSpade;
                        return winners;
                    }

                    if (!rules.DiscardWhenCannotOvertrump)
                    {
                        return spades;
                    }
                }
                else
                {
                    return spades;
                }
            }

            suit = Suit.Clubs;
            kind = ObligationKind.AnyCard;
            return hand;
        }
    }
}
