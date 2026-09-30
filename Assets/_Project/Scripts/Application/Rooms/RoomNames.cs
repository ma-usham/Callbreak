using System;
using System.Globalization;
using Callbreak.Application.Profile;

namespace Callbreak.Application.Rooms
{
    /// <summary>
    /// Room names (GDD §11 screen 02): 3-20 characters, trimmed and profanity-filtered, with
    /// "{Name}'s Table" as the default. The Create Room screen checks a name with
    /// <see cref="IsValid"/> and the host checks it again the same way.
    /// </summary>
    public static class RoomNames
    {
        public const int MinLength = 3;
        public const int MaxLength = 20;

        /// <summary>Fewer characters of a player's name than this no longer say whose table it is.</summary>
        private const int MinKeptOfName = 3;

        /// <summary>The name as it is kept and shown: trimmed, inner runs of white space made one space.</summary>
        public static string Normalize(string name) => ProfileService.NormalizeName(name);

        /// <summary>Whether <paramref name="name"/>, once normalised, is 3-20 characters and passes the filter.</summary>
        public static bool IsValid(string name, NameFilter filter)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            string normalized = Normalize(name);
            return normalized.Length >= MinLength && normalized.Length <= MaxLength && filter.IsAllowed(normalized);
        }

        /// <summary>
        /// The default room name for a player: <paramref name="format"/> with the player's name as
        /// <c>{0}</c> ("{0}'s Table"), fitted into <paramref name="maxLength"/> characters. The
        /// whole name is used when the result fits; otherwise the name's first word; otherwise
        /// that word cut short, so the format's own words survive ("Bartholomew's Table", never
        /// "Bartholomew Smith's"). A format that leaves no room for a name, or that is broken,
        /// gives the name alone. The result is always <see cref="MinLength"/> to
        /// <paramref name="maxLength"/> characters and already normalised.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is below <see cref="MinLength"/>.</exception>
        public static string DefaultFor(string playerName, string format, int maxLength = MaxLength)
        {
            if (maxLength < MinLength)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, $"A room name has at least {MinLength} characters.");
            }

            string name = Normalize(playerName);
            if (name.Length == 0)
            {
                name = ProfileService.DefaultDisplayName;
            }

            if (TryFill(format, name, maxLength, out string fitted))
            {
                return fitted;
            }

            int space = name.IndexOf(' ');
            string word = space < 0 ? name : name.Substring(0, space);
            if (space >= 0 && TryFill(format, word, maxLength, out fitted))
            {
                return fitted;
            }

            for (int keep = Math.Min(word.Length - 1, maxLength); keep >= MinKeptOfName; keep--)
            {
                string cut = Cut(word, keep);
                if (cut.Length >= MinKeptOfName && TryFill(format, cut, maxLength, out fitted))
                {
                    return fitted;
                }
            }

            string alone = Normalize(Cut(name, maxLength));
            return alone.Length >= MinLength ? alone : Cut(ProfileService.DefaultDisplayName, maxLength);
        }

        /// <summary>The format with <paramref name="name"/> filled in, when that gives a room name of the right length.</summary>
        private static bool TryFill(string format, string name, int maxLength, out string filled)
        {
            filled = null;
            if (format == null)
            {
                return false;
            }

            string text;
            try
            {
                text = string.Format(CultureInfo.InvariantCulture, format, name);
            }
            catch (FormatException)
            {
                return false;
            }

            text = Normalize(text);
            if (text.Length < MinLength || text.Length > maxLength)
            {
                return false;
            }

            filled = text;
            return true;
        }

        /// <summary>
        /// At most <paramref name="length"/> characters from the start of <paramref name="text"/>,
        /// never ending inside a character: a surrogate pair stays whole, and a letter keeps its
        /// combining marks (a Devanagari vowel sign, say) or goes with them.
        /// </summary>
        private static string Cut(string text, int length)
        {
            if (text.Length <= length)
            {
                return text;
            }

            int end = length;
            while (end > 0 && ContinuesACharacter(text, end))
            {
                end--;
            }

            while (end > 0 && IsJoiner(text[end - 1]))
            {
                end--;
            }

            return text.Substring(0, end);
        }

        private static bool ContinuesACharacter(string text, int index)
        {
            char c = text[index];
            if (char.IsLowSurrogate(c))
            {
                return char.IsHighSurrogate(text[index - 1]);
            }

            if (IsJoiner(c))
            {
                return true;
            }

            switch (CharUnicodeInfo.GetUnicodeCategory(text, index))
            {
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.EnclosingMark:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Zero-width non-joiner and joiner: they shape the letters around them and mean nothing at an end.</summary>
        private static bool IsJoiner(char c) => c == '‌' || c == '‍';
    }
}
