using System;
using Callbreak.Core.Rules;

namespace Callbreak.Core.Match
{
    /// <summary>
    /// Seat arithmetic. <see cref="Next"/> is the only place in Core that turns a seat into the following
    /// one, so the play direction can never be hard-coded elsewhere (GDD §22 "Next(direction) follows the
    /// RuleSet direction").
    /// </summary>
    public static class SeatExtensions
    {
        /// <summary>Number of seats at the table; the engine assumes four (GDD §5 PlayerCount).</summary>
        public const int Count = 4;

        /// <summary>
        /// The seat that acts after <paramref name="seat"/>. Anticlockwise (the default) is +1 mod 4 because
        /// the next player sits on your right; clockwise is +3 mod 4 (GDD §2, §5 Direction).
        /// </summary>
        /// <param name="seat">A valid seat.</param>
        /// <param name="direction">The rule set's direction.</param>
        /// <returns>The following seat.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The seat or direction is not defined.</exception>
        public static Seat Next(this Seat seat, Direction direction)
        {
            RequireValid(seat, nameof(seat));
            switch (direction)
            {
                case Direction.Anticlockwise:
                    return (Seat)(((int)seat + 1) % Count);
                case Direction.Clockwise:
                    return (Seat)(((int)seat + Count - 1) % Count);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction.");
            }
        }

        /// <summary>
        /// The seat reached after <paramref name="steps"/> turns from <paramref name="seat"/>. Walks with
        /// <see cref="Next"/> so it can never disagree with it.
        /// </summary>
        /// <param name="seat">A valid seat.</param>
        /// <param name="direction">The rule set's direction.</param>
        /// <param name="steps">Zero or more turns.</param>
        /// <returns>The seat <paramref name="steps"/> turns later.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
        public static Seat Advance(this Seat seat, Direction direction, int steps)
        {
            if (steps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(steps), steps, "Steps must not be negative.");
            }

            RequireValid(seat, nameof(seat));
            Seat result = seat;
            for (int i = 0; i < steps % Count; i++)
            {
                result = result.Next(direction);
            }

            return result;
        }

        /// <summary>
        /// Number of turns from <paramref name="from"/> to <paramref name="to"/> in the given direction (0–3).
        /// </summary>
        /// <param name="from">The starting seat.</param>
        /// <param name="to">The target seat.</param>
        /// <param name="direction">The rule set's direction.</param>
        /// <returns>The step count such that <c>from.Advance(direction, steps) == to</c>.</returns>
        public static int StepsTo(this Seat from, Seat to, Direction direction)
        {
            RequireValid(to, nameof(to));
            Seat current = from;
            for (int steps = 0; steps < Count; steps++)
            {
                if (current == to)
                {
                    return steps;
                }

                current = current.Next(direction);
            }

            throw new InvalidOperationException("Seat order is not a cycle of four.");
        }

        /// <summary>
        /// Where <paramref name="seat"/> is drawn for a player sitting at <paramref name="viewer"/>:
        /// <c>(seat − viewer + 4) mod 4</c>, so 0 is bottom, 1 right, 2 top and 3 left (GDD §11 table layout).
        /// This is a fixed screen mapping and does not depend on the play direction.
        /// </summary>
        /// <param name="seat">The seat to place.</param>
        /// <param name="viewer">The local player's seat.</param>
        /// <returns>0 bottom, 1 right, 2 top, 3 left.</returns>
        public static int RelativeTo(this Seat seat, Seat viewer)
        {
            RequireValid(seat, nameof(seat));
            RequireValid(viewer, nameof(viewer));
            return ((int)seat - (int)viewer + Count) % Count;
        }

        /// <summary>True for the four defined seats.</summary>
        /// <param name="seat">Any value, including one cast from untrusted input.</param>
        /// <returns>Whether <paramref name="seat"/> is 0–3.</returns>
        public static bool IsValid(this Seat seat)
        {
            return (byte)seat < Count;
        }

        private static void RequireValid(Seat seat, string parameterName)
        {
            if (!seat.IsValid())
            {
                throw new ArgumentOutOfRangeException(parameterName, seat, "Seat must be 0-3.");
            }
        }
    }
}
