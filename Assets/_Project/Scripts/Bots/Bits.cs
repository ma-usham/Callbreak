using Callbreak.Core.Cards;

namespace Callbreak.Bots
{
    /// <summary>
    /// Card sets as raw masks (<see cref="CardSet.Bits"/>: bit = card id = suit × 13 + rank − 2) and cards as
    /// ids, for the code that runs inside Hard's rollouts. A decision plays tens of thousands of cards, so that
    /// code reads and writes plain integers instead of calling through <see cref="CardSet"/>, <see cref="Card"/>
    /// and <c>Trick</c>: it stays fast even where those small methods are not inlined (the Editor, debug builds,
    /// calls across assemblies on IL2CPP). Table-free, so there is no static data.
    /// </summary>
    internal static class Bits
    {
        /// <summary>The spade suit's number.</summary>
        internal const int Spades = (int)Suit.Spades;

        /// <summary>The id of the 2♠; every id from here up is a spade.</summary>
        internal const int FirstSpade = Spades * Card.PerSuit;

        /// <summary>All 52 cards.</summary>
        internal const ulong Deck = (1UL << Card.Count) - 1UL;

        /// <summary>The thirteen spades.</summary>
        internal const ulong SpadeCards = LowestSuit << FirstSpade;

        private const ulong LowestSuit = (1UL << Card.PerSuit) - 1UL;

        /// <summary>The thirteen cards of a suit.</summary>
        internal static ulong OfSuit(int suit)
        {
            return LowestSuit << (suit * Card.PerSuit);
        }

        /// <summary>The suit number of a card id.</summary>
        internal static int SuitOf(int id)
        {
            return id / Card.PerSuit;
        }

        /// <summary>A card's rank within its suit, 0 for a two up to 12 for an ace.</summary>
        internal static int RankOf(int id)
        {
            return id % Card.PerSuit;
        }

        /// <summary>The cards of the same suit that outrank <paramref name="id"/> (<see cref="CardSet.Above"/>).</summary>
        internal static ulong Above(int id)
        {
            return ~((2UL << id) - 1UL) & OfSuit(id / Card.PerSuit);
        }

        /// <summary>Number of cards in a mask.</summary>
        internal static int Count(ulong bits)
        {
            bits -= (bits >> 1) & 0x5555555555555555UL;
            bits = (bits & 0x3333333333333333UL) + ((bits >> 2) & 0x3333333333333333UL);
            bits = (bits + (bits >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)(unchecked(bits * 0x0101010101010101UL) >> 56);
        }

        /// <summary>The lowest card id in a non-empty mask; within one suit, its lowest card.</summary>
        internal static int Lowest(ulong bits)
        {
            return Count((bits & unchecked(~bits + 1UL)) - 1UL);
        }

        /// <summary>The highest card id in a non-empty mask; within one suit, its highest card.</summary>
        internal static int Highest(ulong bits)
        {
            bits |= bits >> 1;
            bits |= bits >> 2;
            bits |= bits >> 4;
            bits |= bits >> 8;
            bits |= bits >> 16;
            bits |= bits >> 32;
            return Count(bits) - 1;
        }
    }
}
