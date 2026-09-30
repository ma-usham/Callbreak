using System;
using System.Text;

namespace Callbreak.Core.Cards
{
    /// <summary>
    /// An immutable set of cards held as a 52-bit mask, bit <c>id</c> for each card (GDD §22 "CardSet
    /// readonly struct (ulong)"; Appendix A). Set algebra, suit filters, <see cref="Above"/>,
    /// <see cref="Highest"/> and <see cref="Count"/> are O(1), and no member allocates except
    /// <see cref="ToString"/> and <see cref="Parse"/>, which produce or read text.
    /// </summary>
    public readonly struct CardSet : IEquatable<CardSet>
    {
        private const ulong DeckMask = (1UL << Card.Count) - 1UL;
        private const ulong SuitMask = (1UL << Card.PerSuit) - 1UL;

        private readonly ulong _bits;

        private CardSet(ulong bits)
        {
            _bits = bits;
        }

        /// <summary>The set with no cards.</summary>
        public static CardSet Empty => default;

        /// <summary>All 52 cards.</summary>
        public static CardSet FullDeck => new CardSet(DeckMask);

        /// <summary>The raw mask: bit <c>id</c> is set for each card. This is the wire format (GDD §25).</summary>
        public ulong Bits => _bits;

        /// <summary>Number of cards in the set.</summary>
        public int Count => BitMath.PopCount(_bits);

        /// <summary>True when the set has no cards.</summary>
        public bool IsEmpty => _bits == 0UL;

        /// <summary>Rebuilds a set from <see cref="Bits"/>.</summary>
        /// <param name="bits">A mask using only bits 0–51.</param>
        /// <returns>The set.</returns>
        /// <exception cref="ArgumentOutOfRangeException">A bit above 51 is set.</exception>
        public static CardSet FromBits(ulong bits)
        {
            if ((bits & ~DeckMask) != 0UL)
            {
                throw new ArgumentOutOfRangeException(nameof(bits), bits, "Only bits 0-51 are cards.");
            }

            return new CardSet(bits);
        }

        /// <summary>The set holding just <paramref name="card"/>.</summary>
        /// <param name="card">Any card.</param>
        /// <returns>A one-card set.</returns>
        public static CardSet Of(Card card)
        {
            return new CardSet(1UL << card.Id);
        }

        /// <summary>All 13 cards of a suit: a 13-bit mask shifted by <c>suit × 13</c> (GDD Appendix A).</summary>
        /// <param name="suit">The suit.</param>
        /// <returns>The full suit.</returns>
        public static CardSet OfSuit(Suit suit)
        {
            if ((byte)suit > (byte)Suit.Spades)
            {
                throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit.");
            }

            return new CardSet(SuitMask << ((int)suit * Card.PerSuit));
        }

        /// <summary>
        /// Every card of <paramref name="card"/>'s suit that outranks it, e.g. Above(K♦) = {A♦}. This is the
        /// "higher card" test behind the must-beat and must-overtrump rules (GDD §3, §22 "above(card)").
        /// </summary>
        /// <param name="card">The card to beat.</param>
        /// <returns>The higher cards of the same suit; empty for an ace.</returns>
        public static CardSet Above(Card card)
        {
            ulong higherIds = ~((2UL << card.Id) - 1UL);
            return new CardSet(higherIds & OfSuit(card.Suit)._bits);
        }

        /// <summary>
        /// Parses cards separated by spaces or commas, e.g. "A♠ K♠ 10♥" or "AS KS 10H"; "" and "-" are the
        /// empty set. Each card may appear once.
        /// </summary>
        /// <param name="text">The card list.</param>
        /// <param name="set">The parsed set, or empty on failure.</param>
        /// <returns>Whether every token was a card and no card repeated.</returns>
        public static bool TryParse(string text, out CardSet set)
        {
            set = Empty;
            if (text == null)
            {
                return false;
            }

            ulong bits = 0UL;
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == ' ' || c == ',' || c == '\t')
                {
                    i++;
                    continue;
                }

                int start = i;
                while (i < text.Length && text[i] != ' ' && text[i] != ',' && text[i] != '\t')
                {
                    i++;
                }

                int length = i - start;
                if (length == 1 && text[start] == '-')
                {
                    continue;
                }

                if (!CardText.TryParseCard(text, start, length, out Card card))
                {
                    return false;
                }

                ulong bit = 1UL << card.Id;
                if ((bits & bit) != 0UL)
                {
                    return false;
                }

                bits |= bit;
            }

            set = new CardSet(bits);
            return true;
        }

        /// <summary>Parses a card list accepted by <see cref="TryParse"/>.</summary>
        /// <param name="text">The card list.</param>
        /// <returns>The set.</returns>
        /// <exception cref="FormatException">A token is not a card, or a card repeats.</exception>
        public static CardSet Parse(string text)
        {
            if (!TryParse(text, out CardSet set))
            {
                throw new FormatException("Not a card list: '" + text + "'.");
            }

            return set;
        }

        /// <summary>Whether <paramref name="card"/> is in the set.</summary>
        /// <param name="card">Any card.</param>
        /// <returns>True if present.</returns>
        public bool Contains(Card card)
        {
            return (_bits & (1UL << card.Id)) != 0UL;
        }

        /// <summary>Whether the set holds at least one card of <paramref name="suit"/>.</summary>
        /// <param name="suit">The suit.</param>
        /// <returns>False when void in that suit.</returns>
        public bool HasSuit(Suit suit)
        {
            return (_bits & OfSuit(suit)._bits) != 0UL;
        }

        /// <summary>The cards of this set that belong to <paramref name="suit"/>.</summary>
        /// <param name="suit">The suit.</param>
        /// <returns>A subset with one suit.</returns>
        public CardSet InSuit(Suit suit)
        {
            return new CardSet(_bits & OfSuit(suit)._bits);
        }

        /// <summary>This set plus <paramref name="card"/>.</summary>
        /// <param name="card">The card to add.</param>
        /// <returns>A new set.</returns>
        public CardSet With(Card card)
        {
            return new CardSet(_bits | (1UL << card.Id));
        }

        /// <summary>This set minus <paramref name="card"/>.</summary>
        /// <param name="card">The card to remove.</param>
        /// <returns>A new set.</returns>
        public CardSet Without(Card card)
        {
            return new CardSet(_bits & ~(1UL << card.Id));
        }

        /// <summary>Cards in either set.</summary>
        /// <param name="other">The other set.</param>
        /// <returns>The union.</returns>
        public CardSet Union(CardSet other)
        {
            return new CardSet(_bits | other._bits);
        }

        /// <summary>Cards in both sets.</summary>
        /// <param name="other">The other set.</param>
        /// <returns>The intersection.</returns>
        public CardSet Intersect(CardSet other)
        {
            return new CardSet(_bits & other._bits);
        }

        /// <summary>Cards in this set but not in <paramref name="other"/>.</summary>
        /// <param name="other">The cards to remove.</param>
        /// <returns>The difference.</returns>
        public CardSet Except(CardSet other)
        {
            return new CardSet(_bits & ~other._bits);
        }

        /// <summary>Whether every card here is also in <paramref name="other"/>.</summary>
        /// <param name="other">The candidate superset.</param>
        /// <returns>True for a subset (including equal sets).</returns>
        public bool IsSubsetOf(CardSet other)
        {
            return (_bits & ~other._bits) == 0UL;
        }

        /// <summary>Whether the sets share at least one card.</summary>
        /// <param name="other">The other set.</param>
        /// <returns>True if they intersect.</returns>
        public bool Overlaps(CardSet other)
        {
            return (_bits & other._bits) != 0UL;
        }

        /// <summary>
        /// The card with the highest id (suits ordered ♣ ♦ ♥ ♠, then rank). On a one-suit set, such as
        /// <c>hand.InSuit(Suit.Hearts)</c>, this is simply the highest-ranked card.
        /// </summary>
        /// <returns>The highest card.</returns>
        /// <exception cref="InvalidOperationException">The set is empty.</exception>
        public Card Highest()
        {
            if (_bits == 0UL)
            {
                throw new InvalidOperationException("An empty set has no highest card.");
            }

            return Card.FromId(BitMath.HighestBitIndex(_bits));
        }

        /// <summary>The card with the lowest id; on a one-suit set, the lowest-ranked card.</summary>
        /// <returns>The lowest card.</returns>
        /// <exception cref="InvalidOperationException">The set is empty.</exception>
        public Card Lowest()
        {
            if (_bits == 0UL)
            {
                throw new InvalidOperationException("An empty set has no lowest card.");
            }

            return Card.FromId(BitMath.LowestBitIndex(_bits));
        }

        /// <summary>
        /// The card at <paramref name="index"/> in ascending id order, e.g. for picking the k-th legal card
        /// with a random index.
        /// </summary>
        /// <param name="index">0 to <see cref="Count"/> − 1.</param>
        /// <returns>The card.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
        public Card ElementAt(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index is outside the set.");
            }

            ulong remaining = _bits;
            for (int i = 0; i < index; i++)
            {
                remaining &= remaining - 1UL;
            }

            return Card.FromId(BitMath.LowestBitIndex(remaining));
        }

        /// <summary>Enumerates in ascending id order without allocating (<c>foreach</c> binds to this struct).</summary>
        /// <returns>A struct enumerator.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(_bits);
        }

        /// <inheritdoc />
        public bool Equals(CardSet other)
        {
            return _bits == other._bits;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is CardSet other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _bits.GetHashCode();
        }

        /// <summary>
        /// Cards from the highest id down, e.g. "A♠ K♠ 10♥ 5♦" (the default ♠ ♥ ♦ ♣ hand order of GDD §2);
        /// "-" for the empty set. Culture-invariant, and accepted by <see cref="Parse"/>.
        /// </summary>
        /// <returns>The set's text.</returns>
        public override string ToString()
        {
            if (_bits == 0UL)
            {
                return "-";
            }

            var builder = new StringBuilder(Count * 4);
            ulong remaining = _bits;
            while (remaining != 0UL)
            {
                int id = BitMath.HighestBitIndex(remaining);
                remaining &= ~(1UL << id);
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(Card.FromId(id).ToString());
            }

            return builder.ToString();
        }

        /// <summary>Union.</summary>
        /// <param name="left">A set.</param>
        /// <param name="right">Another set.</param>
        /// <returns>Cards in either set.</returns>
        public static CardSet operator |(CardSet left, CardSet right)
        {
            return new CardSet(left._bits | right._bits);
        }

        /// <summary>Intersection.</summary>
        /// <param name="left">A set.</param>
        /// <param name="right">Another set.</param>
        /// <returns>Cards in both sets.</returns>
        public static CardSet operator &(CardSet left, CardSet right)
        {
            return new CardSet(left._bits & right._bits);
        }

        /// <summary>Difference.</summary>
        /// <param name="left">A set.</param>
        /// <param name="right">Cards to remove.</param>
        /// <returns>Cards in <paramref name="left"/> but not in <paramref name="right"/>.</returns>
        public static CardSet operator -(CardSet left, CardSet right)
        {
            return new CardSet(left._bits & ~right._bits);
        }

        /// <summary>Equality of contents.</summary>
        /// <param name="left">A set.</param>
        /// <param name="right">Another set.</param>
        /// <returns>Whether both hold the same cards.</returns>
        public static bool operator ==(CardSet left, CardSet right)
        {
            return left._bits == right._bits;
        }

        /// <summary>Inequality of contents.</summary>
        /// <param name="left">A set.</param>
        /// <param name="right">Another set.</param>
        /// <returns>Whether the sets differ.</returns>
        public static bool operator !=(CardSet left, CardSet right)
        {
            return left._bits != right._bits;
        }

        /// <summary>Allocation-free enumerator over a <see cref="CardSet"/>, lowest id first.</summary>
        public struct Enumerator
        {
            private ulong _remaining;
            private Card _current;

            internal Enumerator(ulong bits)
            {
                _remaining = bits;
                _current = default;
            }

            /// <summary>The card at the current position.</summary>
            public Card Current => _current;

            /// <summary>Advances to the next card.</summary>
            /// <returns>False after the last card.</returns>
            public bool MoveNext()
            {
                if (_remaining == 0UL)
                {
                    return false;
                }

                int id = BitMath.LowestBitIndex(_remaining);
                _remaining &= _remaining - 1UL;
                _current = Card.FromId(id);
                return true;
            }
        }
    }
}
