using System;
using System.Text;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Core.Rules
{
    /// <summary>
    /// The cards played to one trick so far, in play order, with the leader and the play direction
    /// (GDD §22 "TrickState"). An immutable value: <see cref="With"/> returns a new trick. It records facts
    /// only; the rules that read them live in <see cref="LegalMoves"/> and <see cref="TrickResolver"/>.
    /// <c>default(Trick)</c> is an empty anticlockwise trick led by South.
    /// </summary>
    public readonly struct Trick : IEquatable<Trick>
    {
        /// <summary>Cards in a complete trick: one per seat.</summary>
        public const int Size = SeatExtensions.Count;

        // Card ids in play order, one byte each (byte i = the i-th card played).
        private readonly uint _cards;
        private readonly byte _count;
        private readonly Seat _leader;
        private readonly Direction _direction;

        /// <summary>Starts an empty trick.</summary>
        /// <param name="leader">The seat that leads (plays first).</param>
        /// <param name="direction">The rule set's direction, which maps play order to seats.</param>
        /// <exception cref="ArgumentOutOfRangeException">The seat or direction is not defined.</exception>
        public Trick(Seat leader, Direction direction)
        {
            if (!leader.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(leader), leader, "Seat must be 0-3.");
            }

            if (direction != Direction.Anticlockwise && direction != Direction.Clockwise)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction.");
            }

            _leader = leader;
            _direction = direction;
            _cards = 0u;
            _count = 0;
        }

        private Trick(Seat leader, Direction direction, uint cards, byte count)
        {
            _leader = leader;
            _direction = direction;
            _cards = cards;
            _count = count;
        }

        /// <summary>The seat that played (or will play) the first card.</summary>
        public Seat Leader => _leader;

        /// <summary>The direction used to map play order to seats.</summary>
        public Direction Direction => _direction;

        /// <summary>Cards played so far, 0–4.</summary>
        public int Count => _count;

        /// <summary>True before the lead.</summary>
        public bool IsEmpty => _count == 0;

        /// <summary>True once every seat has played.</summary>
        public bool IsComplete => _count == Size;

        /// <summary>The seat whose card comes next.</summary>
        /// <exception cref="InvalidOperationException">The trick is complete.</exception>
        public Seat NextSeat
        {
            get
            {
                if (IsComplete)
                {
                    throw new InvalidOperationException("A complete trick has no next seat.");
                }

                return SeatAt(_count);
            }
        }

        /// <summary>The suit of the first card (GDD Appendix A "Led suit").</summary>
        /// <exception cref="InvalidOperationException">The trick is empty.</exception>
        public Suit LedSuit
        {
            get
            {
                if (_count == 0)
                {
                    throw new InvalidOperationException("An empty trick has no led suit.");
                }

                return this[0].Suit;
            }
        }

        /// <summary>All cards played to the trick.</summary>
        public CardSet Cards
        {
            get
            {
                CardSet set = CardSet.Empty;
                for (int i = 0; i < _count; i++)
                {
                    set = set.With(this[i]);
                }

                return set;
            }
        }

        /// <summary>True when any spade has been played, including a spade lead.</summary>
        public bool HasSpade => Cards.HasSuit(Suit.Spades);

        /// <summary>
        /// True when a spade was played on a non-spade lead (GDD §22 "IsTrumped"). A spade lead is never
        /// "trumped": following spades is ordinary following.
        /// </summary>
        public bool IsTrumped => _count > 0 && LedSuit != Suit.Spades && HasSpade;

        /// <summary>The card at a position in play order.</summary>
        /// <param name="index">0 for the lead, up to <see cref="Count"/> − 1.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a played position.</exception>
        public Card this[int index]
        {
            get
            {
                if ((uint)index >= _count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "No card at that position.");
                }

                return Card.FromId((int)((_cards >> (index * 8)) & 0xFFu));
            }
        }

        /// <summary>The seat that plays the card at <paramref name="index"/> (0 is the leader).</summary>
        /// <param name="index">0–3.</param>
        /// <returns>The seat for that position.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0–3.</exception>
        public Seat SeatAt(int index)
        {
            if ((uint)index >= Size)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A trick has positions 0-3.");
            }

            return _leader.Advance(_direction, index);
        }

        /// <summary>Returns the trick with <paramref name="card"/> played next.</summary>
        /// <param name="card">A card not already in the trick.</param>
        /// <returns>The longer trick.</returns>
        /// <exception cref="InvalidOperationException">The trick is complete.</exception>
        /// <exception cref="ArgumentException">The card is already in the trick.</exception>
        public Trick With(Card card)
        {
            if (IsComplete)
            {
                throw new InvalidOperationException("The trick already has four cards.");
            }

            if (Cards.Contains(card))
            {
                throw new ArgumentException("Card " + card + " is already in the trick.", nameof(card));
            }

            uint cards = _cards | ((uint)card.Id << (_count * 8));
            return new Trick(_leader, _direction, cards, (byte)(_count + 1));
        }

        /// <summary>Finds the card a seat played to this trick.</summary>
        /// <param name="seat">Any seat.</param>
        /// <param name="card">The seat's card, or 2♣ when it has not played.</param>
        /// <returns>Whether <paramref name="seat"/> has played to this trick.</returns>
        public bool TryGetCardOf(Seat seat, out Card card)
        {
            int index = _leader.StepsTo(seat, _direction);
            if (index < _count)
            {
                card = this[index];
                return true;
            }

            card = default;
            return false;
        }

        /// <inheritdoc />
        public bool Equals(Trick other)
        {
            return _cards == other._cards
                && _count == other._count
                && _leader == other._leader
                && _direction == other._direction;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Trick other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)_cards * 31) + (_count << 4) + ((int)_leader << 1) + (int)_direction;
            }
        }

        /// <summary>Leader and cards in play order, e.g. "West: 9♥ Q♥" or "West: -" when empty.</summary>
        /// <returns>The trick's text.</returns>
        public override string ToString()
        {
            var text = new StringBuilder(24);
            text.Append(_leader.ToString()).Append(':');
            if (_count == 0)
            {
                text.Append(" -");
            }

            for (int i = 0; i < _count; i++)
            {
                text.Append(' ').Append(this[i].ToString());
            }

            return text.ToString();
        }

        /// <summary>Value equality.</summary>
        /// <param name="left">A trick.</param>
        /// <param name="right">Another trick.</param>
        /// <returns>Whether leader, direction and cards match.</returns>
        public static bool operator ==(Trick left, Trick right)
        {
            return left.Equals(right);
        }

        /// <summary>Value inequality.</summary>
        /// <param name="left">A trick.</param>
        /// <param name="right">Another trick.</param>
        /// <returns>Whether the tricks differ.</returns>
        public static bool operator !=(Trick left, Trick right)
        {
            return !left.Equals(right);
        }
    }
}
