using System;
using Callbreak.Bots;
using Callbreak.Core.Randomness;
using Callbreak.Core.Views;

namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// The call the game itself would make with a hand: the strongest bot's call estimate (GDD §9).
    /// The optional beginner assist marks it on the call panel with a faint dot (GDD §11 screen
    /// 07). It is given the player's own view, so it knows nothing the player does not, and the
    /// same hand always gets the same suggestion: the estimate runs on a generator that starts
    /// afresh each time, so looking twice never moves the dot.
    /// </summary>
    public sealed class BotCallAdvisor
    {
        private const ulong AdviceSeed = 0x0CA11AD71CEUL;

        private readonly IBotStrategy _estimator;

        public BotCallAdvisor(IBotFactory bots)
        {
            if (bots == null)
            {
                throw new ArgumentNullException(nameof(bots));
            }

            _estimator = bots.Create(BotLevel.Hard);
        }

        /// <summary>A call within the rule set's range for the hand in <paramref name="view"/>.</summary>
        public int Suggest(PlayerView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            int call = _estimator.ChooseCall(view, new Pcg32Random(AdviceSeed));
            return Math.Max(view.Rules.MinCall, Math.Min(view.Rules.MaxCall, call));
        }
    }
}
