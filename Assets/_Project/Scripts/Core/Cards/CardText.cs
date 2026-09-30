using System;

namespace Callbreak.Core.Cards
{
    /// <summary>
    /// Culture-invariant card notation shared by <see cref="Card"/>, <see cref="CardSet"/> and event
    /// strings, so logs and replay diffs read the same on every device.
    /// </summary>
    internal static class CardText
    {
        internal static char SuitSymbol(Suit suit)
        {
            switch (suit)
            {
                case Suit.Clubs:
                    return '♣';
                case Suit.Diamonds:
                    return '♦';
                case Suit.Hearts:
                    return '♥';
                case Suit.Spades:
                    return '♠';
                default:
                    throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit.");
            }
        }

        internal static string RankText(Rank rank)
        {
            switch (rank)
            {
                case Rank.Two:
                    return "2";
                case Rank.Three:
                    return "3";
                case Rank.Four:
                    return "4";
                case Rank.Five:
                    return "5";
                case Rank.Six:
                    return "6";
                case Rank.Seven:
                    return "7";
                case Rank.Eight:
                    return "8";
                case Rank.Nine:
                    return "9";
                case Rank.Ten:
                    return "10";
                case Rank.Jack:
                    return "J";
                case Rank.Queen:
                    return "Q";
                case Rank.King:
                    return "K";
                case Rank.Ace:
                    return "A";
                default:
                    throw new ArgumentOutOfRangeException(nameof(rank), rank, "Unknown rank.");
            }
        }

        internal static bool TryParseSuit(char c, out Suit suit)
        {
            switch (c)
            {
                case '♣':
                case 'C':
                case 'c':
                    suit = Suit.Clubs;
                    return true;
                case '♦':
                case 'D':
                case 'd':
                    suit = Suit.Diamonds;
                    return true;
                case '♥':
                case 'H':
                case 'h':
                    suit = Suit.Hearts;
                    return true;
                case '♠':
                case 'S':
                case 's':
                    suit = Suit.Spades;
                    return true;
                default:
                    suit = Suit.Clubs;
                    return false;
            }
        }

        /// <summary>Parses the rank part of a token such as "10", "T", "q" or "7".</summary>
        internal static bool TryParseRank(string text, int start, int length, out Rank rank)
        {
            rank = Rank.Two;
            if (length == 2)
            {
                if (text[start] == '1' && text[start + 1] == '0')
                {
                    rank = Rank.Ten;
                    return true;
                }

                return false;
            }

            if (length != 1)
            {
                return false;
            }

            char c = text[start];
            if (c >= '2' && c <= '9')
            {
                rank = (Rank)(c - '0');
                return true;
            }

            switch (c)
            {
                case 'T':
                case 't':
                    rank = Rank.Ten;
                    return true;
                case 'J':
                case 'j':
                    rank = Rank.Jack;
                    return true;
                case 'Q':
                case 'q':
                    rank = Rank.Queen;
                    return true;
                case 'K':
                case 'k':
                    rank = Rank.King;
                    return true;
                case 'A':
                case 'a':
                    rank = Rank.Ace;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Parses one card token occupying <c>text[start .. start + length)</c>.</summary>
        internal static bool TryParseCard(string text, int start, int length, out Card card)
        {
            card = default;
            if (length < 2 || length > 3)
            {
                return false;
            }

            if (!TryParseSuit(text[start + length - 1], out Suit suit))
            {
                return false;
            }

            if (!TryParseRank(text, start, length - 1, out Rank rank))
            {
                return false;
            }

            card = new Card(suit, rank);
            return true;
        }
    }
}
