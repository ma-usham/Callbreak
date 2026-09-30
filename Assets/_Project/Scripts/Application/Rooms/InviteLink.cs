using System;
using System.Globalization;
using System.Text;

namespace Callbreak.Application.Rooms
{
    /// <summary>The invite link of GDD §11 screen 04: <c>callbreak://join/AB2CDE</c>.</summary>
    public static class InviteLink
    {
        /// <summary>The URL scheme the app registers with the OS.</summary>
        public const string Scheme = "callbreak";

        /// <summary>The link's host part: what the link asks the app to do.</summary>
        public const string Host = "join";

        private const string Prefix = Scheme + "://" + Host + "/";

        /// <summary>
        /// The longest pasted text still read as a piece of a code: six symbols with a space or
        /// a dash after each.
        /// </summary>
        private const int MaxFragmentLength = RoomCode.Length * 2;

        /// <summary>The room code in <paramref name="url"/>, normalised; false when it is not a valid join link.</summary>
        public static bool TryParse(string url, out string roomCode)
        {
            roomCode = null;
            if (string.IsNullOrEmpty(url) || !url.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string rest = url.Substring(Prefix.Length);
            int end = rest.IndexOfAny(new[] { '?', '#', '/' });
            if (end >= 0)
            {
                rest = rest.Substring(0, end);
            }

            string code = RoomCode.Normalize(rest);
            if (!RoomCode.IsValid(code))
            {
                return false;
            }

            roomCode = code;
            return true;
        }

        /// <summary>
        /// The room code in text taken from the clipboard (GDD §11 screen 03 "Paste Code"),
        /// normalised, or an empty string when the text holds none. Tried in this order: an invite
        /// link anywhere in the text; the whole text when, spaces and dashes aside, it is one code
        /// in any case ("ab-2c de"); inside longer text, a word of exactly six code symbols
        /// written in capitals and digits ("Code: AB2CDE"), so that an ordinary word of a
        /// sentence, such as "Player", is not taken for one; and, for a short text with no more
        /// letters and digits than a code has, whatever code symbols it holds, so that a piece of
        /// a code still fills the boxes.
        /// </summary>
        public static string FromPastedText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (TryFindLink(text, out string code) || TryReadAsOneCode(text, out code) || TryFindCodeWord(text, out code))
            {
                return code;
            }

            string trimmed = text.Trim();
            return trimmed.Length <= MaxFragmentLength && CountLettersAndDigits(trimmed) <= RoomCode.Length
                ? RoomCode.Normalize(trimmed)
                : string.Empty;
        }

        /// <summary>The first join link in the text that <see cref="TryParse"/> accepts; a link ends at the next white space.</summary>
        private static bool TryFindLink(string text, out string code)
        {
            code = null;
            int start = text.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
            while (start >= 0)
            {
                int end = start + Prefix.Length;
                while (end < text.Length && !char.IsWhiteSpace(text[end]))
                {
                    end++;
                }

                if (TryParse(text.Substring(start, end - start), out code))
                {
                    return true;
                }

                start = text.IndexOf(Prefix, start + Prefix.Length, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>GDD: "strips spaces and dashes, uppercases". True when what is left is exactly one code.</summary>
        private static bool TryReadAsOneCode(string text, out string code)
        {
            var sb = new StringBuilder(RoomCode.Length);
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.DashPunctuation)
                {
                    continue;
                }

                if (sb.Length == RoomCode.Length)
                {
                    code = null;
                    return false;
                }

                sb.Append(char.ToUpperInvariant(c));
            }

            code = sb.ToString();
            if (RoomCode.IsValid(code))
            {
                return true;
            }

            code = null;
            return false;
        }

        /// <summary>
        /// A word of exactly six code symbols, written as codes are shown (capitals and digits).
        /// Codes are random, so most hold a digit and no ordinary word does: among several such
        /// words the first with a digit wins, otherwise the first.
        /// </summary>
        private static bool TryFindCodeWord(string text, out string code)
        {
            code = null;
            int i = 0;
            while (i < text.Length)
            {
                if (!IsWordCharacter(text, i, out int width))
                {
                    i += width;
                    continue;
                }

                int start = i;
                do
                {
                    i += width;
                }
                while (i < text.Length && IsWordCharacter(text, i, out width));

                if (i - start != RoomCode.Length)
                {
                    continue;
                }

                string word = text.Substring(start, RoomCode.Length);
                if (!RoomCode.IsValid(word))
                {
                    continue;
                }

                if (HasDigit(word))
                {
                    code = word;
                    return true;
                }

                code ??= word;
            }

            return code != null;
        }

        /// <summary>Letters, digits and the marks that belong to a letter make up a word; everything else ends it.</summary>
        private static bool IsWordCharacter(string text, int index, out int width)
        {
            width = char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
            switch (CharUnicodeInfo.GetUnicodeCategory(text, index))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.EnclosingMark:
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasDigit(string word)
        {
            foreach (char c in word)
            {
                if (c >= '0' && c <= '9')
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountLettersAndDigits(string text)
        {
            int count = 0;
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
