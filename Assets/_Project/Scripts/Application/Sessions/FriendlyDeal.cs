using System;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;

namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// Friendlier offline deals (ADR 056). An offline match deals every round from one seed: the
    /// engine draws the first dealer and then only shuffles, so the seed alone fixes all of its
    /// hands, whatever anybody plays. Before a new offline match starts, the session draws a few
    /// seeds and keeps the one that deals the player the strongest hands over the whole match, by
    /// the bots' own trick estimate (GDD §9 <see cref="CallEstimator"/>). Every shuffle is still the
    /// engine's fair one; only which of them the match gets is chosen. Online tables never use it.
    /// </summary>
    public static class FriendlyDeal
    {
        /// <summary>
        /// How many seeds a new offline match chooses from, by difficulty: the harder the bots, the
        /// more help (one would be a plain fair deal).
        /// </summary>
        public static int Candidates(BotDifficulty difficulty) => difficulty switch
        {
            BotDifficulty.Easy => 3,
            BotDifficulty.Hard => 12,
            _ => 6,
        };

        /// <summary>
        /// Draws <paramref name="candidates"/> seeds and returns the one that deals
        /// <paramref name="player"/> the strongest hands over the match (the first of equals).
        /// </summary>
        public static ulong ChooseSeed(Func<ulong> draw, int candidates, RuleSet rules, Seat player)
        {
            if (draw == null)
            {
                throw new ArgumentNullException(nameof(draw));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            ulong best = draw();
            int bestStrength = Strength(best, rules, player);
            for (int i = 1; i < candidates; i++)
            {
                ulong seed = draw();
                int strength = Strength(seed, rules, player);
                if (strength > bestStrength)
                {
                    best = seed;
                    bestStrength = strength;
                }
            }

            return best;
        }

        /// <summary>The player's hands over the match, in hundredths of a trick (CallEstimator), summed.</summary>
        public static int Strength(ulong seed, RuleSet rules, Seat player)
        {
            int sum = 0;
            foreach (CardSet hand in PlayerHands(seed, rules, player))
            {
                sum += CallEstimator.EstimateHundredths(hand);
            }

            return sum;
        }

        /// <summary>
        /// The hands a match started from <paramref name="seed"/> deals <paramref name="player"/>,
        /// round by round, as <see cref="MatchEngine"/> deals them: a generator seeded with it
        /// draws the first dealer, then each round shuffles until the deal needs no redeal, and the
        /// deal moves on one seat a round. <c>FriendlyDealTests</c> checks it against the engine.
        /// </summary>
        public static CardSet[] PlayerHands(ulong seed, RuleSet rules, Seat player)
        {
            var rng = new Pcg32Random(seed);
            var hands = new CardSet[rules.Rounds];
            Seat dealer = (Seat)rng.NextInt(SeatExtensions.Count);
            for (int round = 0; round < rules.Rounds; round++)
            {
                Seat first = dealer.Next(rules.Direction);
                CardSet[] dealt;
                do
                {
                    dealt = Deck.Deal(Deck.Shuffled(rng), first, rules.Direction);
                }
                while (RedealPolicy.Check(dealt, rules) != RedealReason.None);

                hands[round] = dealt[(int)player];
                dealer = dealer.Next(rules.Direction);
            }

            return hands;
        }
    }
}
