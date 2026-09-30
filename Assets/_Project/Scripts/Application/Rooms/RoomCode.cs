using System;
using System.Text;

namespace Callbreak.Application.Rooms
{
    /// <summary>
    /// Private room codes (GDD Appendix A): six symbols from A-Z and 2-9 without the look-alikes
    /// I, O, 0 and 1 - 32 symbols, about 1.07 billion codes - case-insensitive, normalised to
    /// upper case.
    /// </summary>
    public static class RoomCode
    {
        public const int Length = 6;
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        /// <summary>
        /// Upper-cases and keeps only alphabet symbols, dropping spaces, dashes and anything else,
        /// truncated to six symbols.
        /// </summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(Length);
            foreach (char c in raw)
            {
                char u = char.ToUpperInvariant(c);
                if (Alphabet.IndexOf(u) >= 0)
                {
                    sb.Append(u);
                    if (sb.Length == Length)
                    {
                        break;
                    }
                }
            }

            return sb.ToString();
        }

        public static bool IsValid(string code)
        {
            if (code == null || code.Length != Length)
            {
                return false;
            }

            foreach (char c in code)
            {
                if (Alphabet.IndexOf(c) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A new random code; <paramref name="next"/> returns a value in [0, maxExclusive).</summary>
        public static string Generate(Func<int, int> next)
        {
            var chars = new char[Length];
            for (int i = 0; i < Length; i++)
            {
                chars[i] = Alphabet[next(Alphabet.Length)];
            }

            return new string(chars);
        }
    }
}
