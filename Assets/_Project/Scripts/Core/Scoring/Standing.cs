using System;
using System.Globalization;
using Callbreak.Core.Match;

namespace Callbreak.Core.Scoring
{
    /// <summary>
    /// One seat's place in the standings (GDD §22 MatchEnded payload: rank, total, shared flag), with the
    /// tie-break inputs that decided it.
    /// </summary>
    public readonly struct Standing : IEquatable<Standing>
    {
        /// <summary>Creates a standing; the engine builds these through <see cref="Standings.Compute(ScoreSheet)"/>.</summary>
        /// <param name="seat">The seat.</param>
        /// <param name="rank">1 for first; tied seats share a rank and the next rank is skipped.</param>
        /// <param name="isShared">True when another seat has the same rank.</param>
        /// <param name="totalTenths">Match total in tenths.</param>
        /// <param name="roundsMet">Rounds in which the call was met.</param>
        /// <param name="bestRoundTenths">Best single-round score in tenths.</param>
        /// <exception cref="ArgumentOutOfRangeException">The seat or rank is out of range.</exception>
        public Standing(Seat seat, int rank, bool isShared, int totalTenths, int roundsMet, int bestRoundTenths)
        {
            if (!seat.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(seat), seat, "Seat must be 0-3.");
            }

            if (rank < 1 || rank > SeatExtensions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be 1-4.");
            }

            Seat = seat;
            Rank = rank;
            IsShared = isShared;
            TotalTenths = totalTenths;
            RoundsMet = roundsMet;
            BestRoundTenths = bestRoundTenths;
        }

        /// <summary>The seat.</summary>
        public Seat Seat { get; }

        /// <summary>Place, 1–4. Two seats tied for first are both 1 and the next is 3 (GDD §4 tie rule 4).</summary>
        public int Rank { get; }

        /// <summary>True when the place is shared ("Shared Victory!" when <see cref="Rank"/> is 1).</summary>
        public bool IsShared { get; }

        /// <summary>Match total in tenths.</summary>
        public int TotalTenths { get; }

        /// <summary>Rounds in which the seat met its call (tie-break 2).</summary>
        public int RoundsMet { get; }

        /// <summary>Best single-round score in tenths (tie-break 3).</summary>
        public int BestRoundTenths { get; }

        /// <inheritdoc />
        public bool Equals(Standing other)
        {
            return Seat == other.Seat
                && Rank == other.Rank
                && IsShared == other.IsShared
                && TotalTenths == other.TotalTenths
                && RoundsMet == other.RoundsMet
                && BestRoundTenths == other.BestRoundTenths;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Standing other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (((((int)Seat * 31) + Rank) * 31 + TotalTenths) * 31) + (RoundsMet * 7) + BestRoundTenths + (IsShared ? 1 : 0);
            }
        }

        /// <summary>E.g. "1 South total=213 met=5 best=50" or "1= East total=100 met=4 best=30" when shared.</summary>
        /// <returns>The standing's text.</returns>
        public override string ToString()
        {
            return Rank.ToString(CultureInfo.InvariantCulture) + (IsShared ? "= " : " ") + Seat
                + " total=" + TotalTenths.ToString(CultureInfo.InvariantCulture)
                + " met=" + RoundsMet.ToString(CultureInfo.InvariantCulture)
                + " best=" + BestRoundTenths.ToString(CultureInfo.InvariantCulture);
        }
    }
}
