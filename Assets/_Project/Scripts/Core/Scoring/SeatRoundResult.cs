using System;
using System.Globalization;
using Callbreak.Core.Match;

namespace Callbreak.Core.Scoring
{
    /// <summary>One seat's line of a scored round (GDD §11 screen 09 "Player · Bid · Won · Round · Total").</summary>
    public readonly struct SeatRoundResult : IEquatable<SeatRoundResult>
    {
        /// <summary>Creates a line; used by the engine and when rebuilding results from the wire.</summary>
        /// <param name="seat">The seat.</param>
        /// <param name="call">Its call.</param>
        /// <param name="won">Tricks it won.</param>
        /// <param name="deltaTenths">Its round score in tenths.</param>
        /// <param name="totalTenths">Its match total after this round, in tenths.</param>
        /// <exception cref="ArgumentOutOfRangeException">The seat is not defined.</exception>
        public SeatRoundResult(Seat seat, int call, int won, int deltaTenths, int totalTenths)
        {
            if (!seat.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(seat), seat, "Seat must be 0-3.");
            }

            Seat = seat;
            Call = call;
            Won = won;
            DeltaTenths = deltaTenths;
            TotalTenths = totalTenths;
        }

        /// <summary>The seat.</summary>
        public Seat Seat { get; }

        /// <summary>The seat's call this round.</summary>
        public int Call { get; }

        /// <summary>Tricks the seat won this round.</summary>
        public int Won { get; }

        /// <summary>Round score in tenths (e.g. 41 is +4.1).</summary>
        public int DeltaTenths { get; }

        /// <summary>Match total after this round, in tenths.</summary>
        public int TotalTenths { get; }

        /// <summary>Whether the call was met (won ≥ call); the tie-break counts these (GDD §4).</summary>
        public bool Met => ScoreCalculator.IsMet(Call, Won);

        /// <inheritdoc />
        public bool Equals(SeatRoundResult other)
        {
            return Seat == other.Seat
                && Call == other.Call
                && Won == other.Won
                && DeltaTenths == other.DeltaTenths
                && TotalTenths == other.TotalTenths;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is SeatRoundResult other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((((((int)Seat * 31) + Call) * 31 + Won) * 31 + DeltaTenths) * 31) + TotalTenths;
            }
        }

        /// <summary>E.g. "South call=4 won=5 delta=41 total=41" (tenths, culture-invariant).</summary>
        /// <returns>The line's text.</returns>
        public override string ToString()
        {
            return Seat
                + " call=" + Call.ToString(CultureInfo.InvariantCulture)
                + " won=" + Won.ToString(CultureInfo.InvariantCulture)
                + " delta=" + DeltaTenths.ToString(CultureInfo.InvariantCulture)
                + " total=" + TotalTenths.ToString(CultureInfo.InvariantCulture);
        }
    }
}
