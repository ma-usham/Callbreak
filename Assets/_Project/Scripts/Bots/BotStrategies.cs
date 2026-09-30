using System;
using Callbreak.Core.Cards;
using Callbreak.Core.Views;

namespace Callbreak.Bots
{
    /// <summary>
    /// Makes the strategy for a bot level (GDD §9 "Easy · Normal · Hard"). Strategies are stateless, so one
    /// instance may serve any number of seats and tables, on any thread.
    /// </summary>
    public static class BotStrategies
    {
        /// <summary>A new strategy for <paramref name="level"/>, with default settings.</summary>
        /// <param name="level">The bot strength.</param>
        /// <returns><see cref="EasyStrategy"/>, <see cref="NormalStrategy"/> or <see cref="HardStrategy"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is not defined.</exception>
        public static IBotStrategy Create(BotLevel level)
        {
            switch (level)
            {
                case BotLevel.Easy:
                    return new EasyStrategy();
                case BotLevel.Normal:
                    return new NormalStrategy();
                case BotLevel.Hard:
                    return new HardStrategy();
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown bot level.");
            }
        }

        /// <summary>The viewer's legal cards; a strategy asked outside its play turn is a caller bug.</summary>
        internal static CardSet RequireLegalCards(PlayerView view)
        {
            CardSet legal = view.LegalCards();
            if (legal.IsEmpty)
            {
                throw new InvalidOperationException(
                    "ChooseCard needs the viewer's play turn; the view is " + view.Viewer + " in " + view.Phase + ".");
            }

            return legal;
        }
    }
}
