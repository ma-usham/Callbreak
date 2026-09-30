using Callbreak.Core.Match;

namespace Callbreak.Core.Commands
{
    /// <summary>
    /// A seat is ready to leave the round summary (GDD §7 Round summary: "ReadyNext · exits when all humans
    /// ready or timer ends"). Accepted once per seat per summary. When every seat is ready the next round is
    /// dealt, or after the last round the match ends. Bot seats are ready automatically; a driver whose
    /// countdown expires submits this for every seat that is not ready yet.
    /// </summary>
    public readonly struct ReadyNext
    {
        /// <summary>Creates the command.</summary>
        /// <param name="seat">The seat that is ready.</param>
        public ReadyNext(Seat seat)
        {
            Seat = seat;
        }

        /// <summary>The seat that is ready.</summary>
        public Seat Seat { get; }

        /// <summary>E.g. "ReadyNext seat=South".</summary>
        /// <returns>The command's text.</returns>
        public override string ToString()
        {
            return "ReadyNext seat=" + Seat;
        }
    }
}
