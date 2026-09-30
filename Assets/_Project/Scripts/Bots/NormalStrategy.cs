using System;
using Callbreak.Core.Cards;
using Callbreak.Core.Randomness;
using Callbreak.Core.Views;

namespace Callbreak.Bots
{
    /// <summary>
    /// The Normal bot (GDD §9 "Normal": "a solid club player"; goal "meet its call, then take cheap extra
    /// tricks"). Calls with the estimator and its conservative rounding; plays by the five heuristics:
    /// <list type="number">
    /// <item>Needs tricks: last to play, win with the cheapest winning card; otherwise play high only with the
    /// top remaining card of the suit (and only when no later seat is known able to trump it).</item>
    /// <item>Call made: the lowest legal card, unless the cheapest winning card surely takes the trick from an
    /// opponent who still needs it.</item>
    /// <item>Leading, whether or not the call is made: the top remaining card of a suit, avoiding suits an
    /// opponent is void in while it may hold spades. Without one, lead low from a safe suit while tricks are
    /// needed, else rule 2's lowest legal card.</item>
    /// <item>Trumping: the lowest spade that wins; if the trick is already trumped higher, discard the lowest card
    /// of the longest side suit (keeping a suit whose lowest card is itself a winner).</item>
    /// <item>Memory: played cards and voids, rebuilt from the view by <see cref="CardTracker"/>.</item>
    /// </list>
    /// Uses no randomness. Stateless and thread-safe.
    /// </summary>
    public sealed class NormalStrategy : IBotStrategy
    {
        /// <inheritdoc />
        public BotLevel Level => BotLevel.Normal;

        /// <summary>The estimator's call, <c>clamp(⌊E + 0.3⌋, MinCall, MaxCall)</c> (GDD §9).</summary>
        /// <param name="view">The viewer's view; only its hand and rules are read.</param>
        /// <param name="rng">Unused.</param>
        /// <returns>A legal call.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="view"/> is null.</exception>
        public int ChooseCall(PlayerView view, IRandom rng)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return CallEstimator.Call(view.Hand, view.Rules);
        }

        /// <summary>The card the GDD §9 Normal heuristics pick.</summary>
        /// <param name="view">The viewer's view on its play turn.</param>
        /// <param name="rng">Unused.</param>
        /// <returns>A card from <c>view.LegalCards()</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="view"/> is null.</exception>
        /// <exception cref="InvalidOperationException">It is not the viewer's play turn.</exception>
        public Card ChooseCard(PlayerView view, IRandom rng)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            CardSet legal = BotStrategies.RequireLegalCards(view);
            if (legal.Count == 1)
            {
                return legal.Lowest();
            }

            var tracker = new CardTracker(view);
            return Card.FromId(NormalPolicy.ChooseCard(tracker.State, (int)view.Viewer, view.Hand.Bits, legal.Bits));
        }
    }
}
