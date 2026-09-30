using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// A play turn opened (GDD §22: seat, turnId, led suit if any). Public. The seat answers with
    /// <c>PlayCard</c> carrying <see cref="TurnId"/>. Not emitted for plays the engine makes itself
    /// (<c>AutoPlayLastTrick</c>, <c>AutoPlayForcedMoves</c>); those appear only as a forced <c>CardPlayed</c>.
    /// </summary>
    public sealed class PlayTurnStarted : MatchEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="seat">The seat to play.</param>
        /// <param name="turnId">The new turn's id.</param>
        /// <param name="ledSuit">The trick's led suit, or null when this seat leads.</param>
        public PlayTurnStarted(Seat seat, TurnId turnId, Suit? ledSuit)
            : base(EventVisibility.Public)
        {
            Seat = seat;
            TurnId = turnId;
            LedSuit = ledSuit;
        }

        /// <summary>The seat to play.</summary>
        public Seat Seat { get; }

        /// <summary>The turn's id.</summary>
        public TurnId TurnId { get; }

        /// <summary>The led suit, or null when the seat is leading.</summary>
        public Suit? LedSuit { get; }

        private protected override string Describe()
        {
            return "PlayTurnStarted seat=" + Seat + " turn=" + TurnId + " led=" + (LedSuit.HasValue ? CardText.SuitSymbol(LedSuit.Value).ToString() : "-");
        }
    }
}
