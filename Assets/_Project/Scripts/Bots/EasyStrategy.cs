using System;
using Callbreak.Core.Cards;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;

namespace Callbreak.Bots
{
    /// <summary>
    /// The Easy bot (GDD §9 "Easy": "a relaxed beginner who sometimes misses its call"). Calls
    /// estimate − 1 (at least MinCall) and then ±1 at random; plays a random legal card, biased 60% toward the
    /// lowest. No memory. Stateless and thread-safe; deterministic for the same view and random sequence.
    /// </summary>
    public sealed class EasyStrategy : IBotStrategy
    {
        /// <summary>How often, in percent, Easy simply plays its lowest legal card (GDD §9 "biased 60%").</summary>
        public const int LowestCardPercent = 60;

        /// <inheritdoc />
        public BotLevel Level => BotLevel.Easy;

        /// <summary>
        /// <c>max(⌊E + 0.3⌋ − 1, MinCall)</c> from <see cref="CallEstimator"/>, plus −1, 0 or +1 with equal chance
        /// (one draw), clamped to MinCall…MaxCall.
        /// </summary>
        /// <param name="view">The viewer's view; only its hand and rules are read.</param>
        /// <param name="rng">One draw per call.</param>
        /// <returns>A legal call.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public int ChooseCall(PlayerView view, IRandom rng)
        {
            RequireArguments(view, rng);
            int call = Math.Max(CallEstimator.RoundDown(CallEstimator.EstimateHundredths(view.Hand)) - 1, view.Rules.MinCall);
            call += rng.NextInt(3) - 1;
            return CallRules.Clamp(call, view.Rules);
        }

        /// <summary>
        /// With 60% chance the lowest legal card (GDD §9 rule 2's "lowest legal card"), otherwise a uniformly random
        /// legal card. A single legal card is returned without drawing.
        /// </summary>
        /// <param name="view">The viewer's view on its play turn.</param>
        /// <param name="rng">One or two draws per card.</param>
        /// <returns>A card from <c>view.LegalCards()</c>.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">It is not the viewer's play turn.</exception>
        public Card ChooseCard(PlayerView view, IRandom rng)
        {
            RequireArguments(view, rng);
            CardSet legal = BotStrategies.RequireLegalCards(view);
            if (legal.Count == 1)
            {
                return legal.Lowest();
            }

            if (rng.NextInt(100) < LowestCardPercent)
            {
                return NormalPolicy.LowestLegal(legal, view.Hand);
            }

            return legal.ElementAt(rng.NextInt(legal.Count));
        }

        private static void RequireArguments(PlayerView view, IRandom rng)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }
        }
    }
}
