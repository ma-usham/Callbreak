using System;

namespace Callbreak.Core.Cards
{
    /// <summary>
    /// One playing card, stored as its id (GDD §22 "Card readonly struct (byte)"; Appendix A
    /// "id = suit × 13 + (rank − 2)"), so 2♣ = 0, A♥ = 38 and A♠ = 51. The id is also the card's bit in a
    /// <see cref="CardSet"/> and its wire format. <c>default(Card)</c> is 2♣.
    /// </summary>
    public readonly struct Card : IEquatable<Card>, IComparable<Card>
    {
        /// <summary>Number of distinct cards (and ids) in the deck.</summary>
        public const int Count = 52;

        /// <summary>Cards per suit.</summary>
        public const int PerSuit = 13;

        private readonly byte _id;

        /// <summary>Creates the card of the given suit and rank.</summary>
        /// <param name="suit">One of the four suits.</param>
        /// <param name="rank">Two through Ace.</param>
        /// <exception cref="ArgumentOutOfRangeException">The suit or rank is not defined.</exception>
        public Card(Suit suit, Rank rank)
        {
            if ((byte)suit > (byte)Suit.Spades)
            {
                throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit.");
            }

            if (rank < Rank.Two || rank > Rank.Ace)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "Unknown rank.");
            }

            _id = (byte)(((int)suit * PerSuit) + ((int)rank - 2));
        }

        private Card(byte id)
        {
            _id = id;
        }

        /// <summary>The card id, 0–51.</summary>
        public int Id => _id;

        /// <summary>The suit, <c>id / 13</c>.</summary>
        public Suit Suit => (Suit)(_id / PerSuit);

        /// <summary>The rank, <c>id % 13 + 2</c>.</summary>
        public Rank Rank => (Rank)((_id % PerSuit) + 2);

        /// <summary>True for spades, the permanent trump suit (GDD §2).</summary>
        public bool IsTrump => _id >= (int)Suit.Spades * PerSuit;

        /// <summary>Returns the card with the given id.</summary>
        /// <param name="id">0–51.</param>
        /// <returns>The card.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is outside 0–51.</exception>
        public static Card FromId(int id)
        {
            if ((uint)id >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Card id must be 0-51.");
            }

            return new Card((byte)id);
        }

        /// <summary>
        /// Parses notation such as "A♠", "10♥", "Q♦", or the ASCII forms "AS", "10h", "Th", "qd".
        /// </summary>
        /// <param name="text">The card text, without surrounding spaces.</param>
        /// <param name="card">The parsed card, or 2♣ when parsing fails.</param>
        /// <returns>Whether <paramref name="text"/> named exactly one card.</returns>
        public static bool TryParse(string text, out Card card)
        {
            if (text == null)
            {
                card = default;
                return false;
            }

            return CardText.TryParseCard(text, 0, text.Length, out card);
        }

        /// <summary>Parses notation accepted by <see cref="TryParse"/>.</summary>
        /// <param name="text">The card text.</param>
        /// <returns>The card.</returns>
        /// <exception cref="FormatException"><paramref name="text"/> is not a card.</exception>
        public static Card Parse(string text)
        {
            if (!TryParse(text, out Card card))
            {
                throw new FormatException("Not a card: '" + text + "'.");
            }

            return card;
        }

        /// <summary>Compares by id, i.e. by suit (♣ ♦ ♥ ♠) and then rank.</summary>
        /// <param name="other">The card to compare with.</param>
        /// <returns>Negative, zero or positive, like <see cref="int.CompareTo(int)"/>.</returns>
        public int CompareTo(Card other)
        {
            return _id.CompareTo(other._id);
        }

        /// <inheritdoc />
        public bool Equals(Card other)
        {
            return _id == other._id;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Card other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _id;
        }

        /// <summary>Rank then suit symbol, e.g. "A♠" or "10♥" (GDD notation, culture-invariant).</summary>
        /// <returns>The card's text.</returns>
        public override string ToString()
        {
            return CardText.RankText(Rank) + CardText.SuitSymbol(Suit);
        }

        /// <summary>Equality by id.</summary>
        /// <param name="left">A card.</param>
        /// <param name="right">Another card.</param>
        /// <returns>Whether both are the same card.</returns>
        public static bool operator ==(Card left, Card right)
        {
            return left._id == right._id;
        }

        /// <summary>Inequality by id.</summary>
        /// <param name="left">A card.</param>
        /// <param name="right">Another card.</param>
        /// <returns>Whether the cards differ.</returns>
        public static bool operator !=(Card left, Card right)
        {
            return left._id != right._id;
        }
    }
}
