using Callbreak.Core.Match;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// A call turn opened (GDD §22: seat, turnId; the server adds a deadline). Public. The seat answers with
    /// <c>PlaceCall</c> carrying <see cref="TurnId"/>.
    /// </summary>
    public sealed class CallTurnStarted : MatchEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="seat">The seat to call.</param>
        /// <param name="turnId">The new turn's id.</param>
        public CallTurnStarted(Seat seat, TurnId turnId)
            : base(EventVisibility.Public)
        {
            Seat = seat;
            TurnId = turnId;
        }

        /// <summary>The seat to call.</summary>
        public Seat Seat { get; }

        /// <summary>The turn's id.</summary>
        public TurnId TurnId { get; }

        private protected override string Describe()
        {
            return "CallTurnStarted seat=" + Seat + " turn=" + TurnId;
        }
    }
}
