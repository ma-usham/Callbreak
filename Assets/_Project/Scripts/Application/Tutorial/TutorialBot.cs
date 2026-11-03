using System;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Randomness;
using Callbreak.Core.Views;

namespace Callbreak.Application.Tutorial
{
    /// <summary>
    /// A bot of the tutorial's match (ADR 084): in the first hand it makes the call the
    /// <see cref="TutorialDeal"/> gives it and plays its cards in the guided tricks; everything else
    /// is the match's own strategy. A scripted card that is not legal is never played: the
    /// strategy decides instead.
    /// </summary>
    public sealed class TutorialBot : IBotStrategy
    {
        private readonly IBotStrategy _strategy;
        private readonly TutorialDeal _deal;

        public TutorialBot(IBotStrategy strategy, TutorialDeal deal)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _deal = deal ?? throw new ArgumentNullException(nameof(deal));
        }

        public BotLevel Level => _strategy.Level;

        public int ChooseCall(PlayerView view, IRandom rng)
        {
            return view.Round == 1 && _deal.TryGetCall(view.Viewer, out int call) ? call : _strategy.ChooseCall(view, rng);
        }

        public Card ChooseCard(PlayerView view, IRandom rng)
        {
            if (view.Round == 1 && _deal.TryGetPlay(view.Viewer, view.TrickIndex, out Card card) && view.LegalCards().Contains(card))
            {
                return card;
            }

            return _strategy.ChooseCard(view, rng);
        }
    }
}
