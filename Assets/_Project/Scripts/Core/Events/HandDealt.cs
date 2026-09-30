using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// A seat's 13 cards for the round (GDD §22: "CardSet with that seat's 13 cards. Nobody else ever
    /// receives it"). PRIVATE to <see cref="Seat"/>: the only path by which a hand leaves the engine.
    /// </summary>
    public sealed class HandDealt : MatchEvent
    {
        /// <summary>Creates the event, private to <paramref name="seat"/>.</summary>
        /// <param name="seat">The owner.</param>
        /// <param name="hand">The owner's cards.</param>
        public HandDealt(Seat seat, CardSet hand)
            : base(EventVisibility.Private(seat))
        {
            Seat = seat;
            Hand = hand;
        }

        /// <summary>The owner.</summary>
        public Seat Seat { get; }

        /// <summary>The owner's cards.</summary>
        public CardSet Hand { get; }

        private protected override string Describe()
        {
            return "HandDealt seat=" + Seat + " hand=" + Hand;
        }
    }
}
