using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// A card was played to the current trick (GDD §22: seat, card, isAuto). Public.
    /// <see cref="IsForced"/> marks a play the engine made itself because only one card was possible
    /// (<c>AutoPlayLastTrick</c> or <c>AutoPlayForcedMoves</c>); such plays have no turn of their own and also
    /// carry <see cref="IsAuto"/> = true. A timer or bot acting for a human gives IsAuto = true, IsForced = false,
    /// which is when the UI shows its "auto" tag.
    /// </summary>
    public sealed class CardPlayed : MatchEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="seat">The playing seat.</param>
        /// <param name="card">The card.</param>
        /// <param name="isAuto">True when not chosen by the seat's player.</param>
        /// <param name="isForced">True when the engine played the only possible card.</param>
        public CardPlayed(Seat seat, Card card, bool isAuto, bool isForced)
            : base(EventVisibility.Public)
        {
            Seat = seat;
            Card = card;
            IsAuto = isAuto;
            IsForced = isForced;
        }

        /// <summary>The playing seat.</summary>
        public Seat Seat { get; }

        /// <summary>The card.</summary>
        public Card Card { get; }

        /// <summary>True when a timer, bot or the engine played it rather than the seat's player.</summary>
        public bool IsAuto { get; }

        /// <summary>True when the engine played it because it was the only possible card.</summary>
        public bool IsForced { get; }

        private protected override string Describe()
        {
            return "CardPlayed seat=" + Seat + " card=" + Card + " auto=" + (IsAuto ? "1" : "0") + " forced=" + (IsForced ? "1" : "0");
        }
    }
}
