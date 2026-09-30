using System;
using System.Globalization;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Core.Rules
{
    /// <summary>
    /// A finished trick with its position in the round and its winner. Public information: bots use the
    /// history of these to track played cards and infer voids (GDD §9 "Memory").
    /// </summary>
    public readonly struct CompletedTrick : IEquatable<CompletedTrick>
    {
        /// <summary>Records a finished trick.</summary>
        /// <param name="index">Position in the round, 0–12.</param>
        /// <param name="trick">A trick holding four cards.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0–12.</exception>
        /// <exception cref="ArgumentException"><paramref name="trick"/> is not complete.</exception>
        public CompletedTrick(int index, Trick trick)
        {
            if (index < 0 || index >= Deck.HandSize)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Trick index must be 0-12.");
            }

            if (!trick.IsComplete)
            {
                throw new ArgumentException("Only a four-card trick is complete.", nameof(trick));
            }

            Index = index;
            Trick = trick;
            Winner = TrickResolver.Winner(trick);
        }

        /// <summary>Position in the round, 0 for the first trick (displayed as "Trick 1 / 13").</summary>
        public int Index { get; }

        /// <summary>The four cards in play order, with the leader and direction.</summary>
        public Trick Trick { get; }

        /// <summary>The seat that led this trick.</summary>
        public Seat Leader => Trick.Leader;

        /// <summary>The seat that won this trick and led the next one.</summary>
        public Seat Winner { get; }

        /// <summary>The card that won this trick.</summary>
        public Card WinningCard => TrickResolver.WinningCard(Trick);

        /// <inheritdoc />
        public bool Equals(CompletedTrick other)
        {
            return Index == other.Index && Trick == other.Trick;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is CompletedTrick other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return unchecked((Trick.GetHashCode() * 31) + Index);
        }

        /// <summary>E.g. "#0 South: 9♥ Q♥ 10♥ A♥ -> West".</summary>
        /// <returns>The trick's text.</returns>
        public override string ToString()
        {
            return "#" + Index.ToString(CultureInfo.InvariantCulture) + " " + Trick + " -> " + Winner;
        }
    }
}
