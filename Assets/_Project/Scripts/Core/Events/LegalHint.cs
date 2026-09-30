using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// The legal cards for the play turn just opened (GDD §22: "An optional hint; clients also compute it
    /// locally"). PRIVATE to <see cref="Seat"/>, since it is derived from that seat's hand. Always follows the
    /// matching <see cref="PlayTurnStarted"/>.
    /// </summary>
    public sealed class LegalHint : MatchEvent
    {
        /// <summary>Creates the event, private to <paramref name="seat"/>.</summary>
        /// <param name="seat">The seat to play.</param>
        /// <param name="turnId">The turn the hint belongs to.</param>
        /// <param name="legal">The legal cards.</param>
        public LegalHint(Seat seat, TurnId turnId, CardSet legal)
            : base(EventVisibility.Private(seat))
        {
            Seat = seat;
            TurnId = turnId;
            Legal = legal;
        }

        /// <summary>The seat to play.</summary>
        public Seat Seat { get; }

        /// <summary>The turn the hint belongs to; ignore hints for any other turn.</summary>
        public TurnId TurnId { get; }

        /// <summary>The legal cards.</summary>
        public CardSet Legal { get; }

        private protected override string Describe()
        {
            return "LegalHint seat=" + Seat + " turn=" + TurnId + " legal=" + Legal;
        }
    }
}
