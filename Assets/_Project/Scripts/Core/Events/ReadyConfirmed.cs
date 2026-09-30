using Callbreak.Core.Match;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// A seat is ready to leave the round summary: it sent <c>ReadyNext</c>, or it is a bot seat (emitted
    /// right after <see cref="RoundScored"/>). Public, so every table can show who is still deciding. Not in
    /// the GDD §22 catalogue; added so ready state travels through the same event stream as everything else.
    /// </summary>
    public sealed class ReadyConfirmed : MatchEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="seat">The ready seat.</param>
        public ReadyConfirmed(Seat seat)
            : base(EventVisibility.Public)
        {
            Seat = seat;
        }

        /// <summary>The ready seat.</summary>
        public Seat Seat { get; }

        private protected override string Describe()
        {
            return "ReadyConfirmed seat=" + Seat;
        }
    }
}
