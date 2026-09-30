using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Core.Scoring
{
    /// <summary>
    /// The scored result of one round for all four seats (GDD §22 RoundScored payload: per seat call, won,
    /// delta and total in tenths). Immutable.
    /// </summary>
    public sealed class RoundResult : IEquatable<RoundResult>
    {
        private readonly SeatRoundResult[] _seats;

        /// <summary>Creates a round result; the engine builds these through <see cref="ScoreSheet.Append(IReadOnlyList{int}, IReadOnlyList{int}, Rules.RuleSet)"/>.</summary>
        /// <param name="round">The round number, starting at 1.</param>
        /// <param name="seats">Four lines, indexed by seat number.</param>
        /// <exception cref="ArgumentNullException"><paramref name="seats"/> is null.</exception>
        /// <exception cref="ArgumentException">The lines are not one per seat in seat order, or tricks won do not sum to 13.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="round"/> is below 1.</exception>
        public RoundResult(int round, IReadOnlyList<SeatRoundResult> seats)
        {
            if (seats == null)
            {
                throw new ArgumentNullException(nameof(seats));
            }

            if (round < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(round), round, "Rounds are numbered from 1.");
            }

            if (seats.Count != SeatExtensions.Count)
            {
                throw new ArgumentException("A round result has one line per seat.", nameof(seats));
            }

            _seats = new SeatRoundResult[SeatExtensions.Count];
            int tricks = 0;
            for (int i = 0; i < _seats.Length; i++)
            {
                if ((int)seats[i].Seat != i)
                {
                    throw new ArgumentException("Line " + i + " belongs to " + seats[i].Seat + ".", nameof(seats));
                }

                _seats[i] = seats[i];
                tricks += seats[i].Won;
            }

            // GDD §4 invariant: every round's tricks total exactly 13.
            if (tricks != Deck.HandSize)
            {
                throw new ArgumentException("Tricks won must total 13, not " + tricks + ".", nameof(seats));
            }

            Round = round;
        }

        /// <summary>The round number, starting at 1 ("ROUND 1 OF 5").</summary>
        public int Round { get; }

        /// <summary>The line for <paramref name="seat"/>.</summary>
        /// <param name="seat">Any seat.</param>
        /// <exception cref="ArgumentOutOfRangeException">The seat is not defined.</exception>
        public SeatRoundResult this[Seat seat]
        {
            get
            {
                if (!seat.IsValid())
                {
                    throw new ArgumentOutOfRangeException(nameof(seat), seat, "Seat must be 0-3.");
                }

                return _seats[(int)seat];
            }
        }

        /// <inheritdoc />
        public bool Equals(RoundResult other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || Round != other.Round)
            {
                return false;
            }

            for (int i = 0; i < _seats.Length; i++)
            {
                if (!_seats[i].Equals(other._seats[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals(obj as RoundResult);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Round;
                for (int i = 0; i < _seats.Length; i++)
                {
                    hash = (hash * 397) ^ _seats[i].GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>E.g. "round=1 [South call=4 won=5 delta=41 total=41] [East ...] ...".</summary>
        /// <returns>The result's text.</returns>
        public override string ToString()
        {
            var text = new StringBuilder(160);
            text.Append("round=").Append(Round.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < _seats.Length; i++)
            {
                text.Append(" [").Append(_seats[i].ToString()).Append(']');
            }

            return text.ToString();
        }
    }
}
