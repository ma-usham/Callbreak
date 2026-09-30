using System;
using System.Globalization;

namespace Callbreak.Core.Match
{
    /// <summary>
    /// A match-wide, strictly increasing id for each call or play turn (GDD Appendix A "turnId: a monotonic
    /// id per call or play turn; makes intents idempotent"). A command must carry the id of the open turn,
    /// so a retried or double-tapped intent for an earlier turn is rejected as stale (GDD §26).
    /// </summary>
    public readonly struct TurnId : IEquatable<TurnId>, IComparable<TurnId>
    {
        /// <summary>Wraps an id.</summary>
        /// <param name="value">0 for <see cref="None"/>, otherwise the turn number (the first turn is 1).</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
        public TurnId(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Turn ids are not negative.");
            }

            Value = value;
        }

        /// <summary>"No open turn": the value in phases that do not take call or play commands.</summary>
        public static TurnId None => default;

        /// <summary>The id; fits a ushort on the wire for any match of up to 10 rounds.</summary>
        public int Value { get; }

        /// <summary>True for <see cref="None"/>.</summary>
        public bool IsNone => Value == 0;

        /// <inheritdoc />
        public bool Equals(TurnId other)
        {
            return Value == other.Value;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is TurnId other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Value;
        }

        /// <summary>Orders ids by value; later turns compare greater.</summary>
        /// <param name="other">Another id.</param>
        /// <returns>Negative, zero or positive.</returns>
        public int CompareTo(TurnId other)
        {
            return Value.CompareTo(other.Value);
        }

        /// <summary>The number, culture-invariant; "none" for <see cref="None"/>.</summary>
        /// <returns>The id's text.</returns>
        public override string ToString()
        {
            return IsNone ? "none" : Value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Equality.</summary>
        /// <param name="left">An id.</param>
        /// <param name="right">Another id.</param>
        /// <returns>Whether both are the same turn.</returns>
        public static bool operator ==(TurnId left, TurnId right)
        {
            return left.Value == right.Value;
        }

        /// <summary>Inequality.</summary>
        /// <param name="left">An id.</param>
        /// <param name="right">Another id.</param>
        /// <returns>Whether the turns differ.</returns>
        public static bool operator !=(TurnId left, TurnId right)
        {
            return left.Value != right.Value;
        }
    }
}
