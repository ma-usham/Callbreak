using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Callbreak.Application.Profile
{
    /// <summary>
    /// The on-device profanity filter for display names (GDD §11 screen 14; the server checks
    /// again). A short curated block list of English and romanised Nepali/Hindi abuse, matched
    /// case-insensitively after folding simple leetspeak (0→o, 1→i, 3→e, 4→a, 5→s, 7→t) and Latin
    /// accents. Two kinds of entry keep real names usable: fragments are blocked anywhere once
    /// spaces are removed ("f u c k"), while short words that hide inside ordinary names
    /// (ass in Hassan, shit in Kshitij, tit in Titiksha) are blocked only as a whole word or as the
    /// whole name.
    /// </summary>
    public sealed class NameFilter
    {
        // Blocked anywhere in the name once spaces are removed. Nothing here may occur inside a
        // common Nepali or English name.
        private const string BuiltInFragments =
            "fuck fvck cunt bitch whore slut nigger nigga faggot retard rapist asshole arsehole bastard " +
            "bullshit shithead dickhead cocksucker dildo wanker pussy penis vagina porno hitler twat jizz " +
            "titties boobies " +
            "machikne machhikne machikney chikne chikney chiknu mujiko randiko putiko bhosdi bhosadi bhosda " +
            "madarchod maderchod madarchot bhenchod behenchod benchod chutiya chutia chootiya gandu lawda " +
            "lavda jhatu jhaatu jhant harami haraami kuttiya besya beshya";

        // Blocked only as a whole word, or as the entire name with its spaces removed ("a s s").
        private const string BuiltInWords =
            "ass arse shit shits shitty dick dicks cock cocks cum tits boob boobs sex sexy porn rape raped " +
            "fag fags piss pissed prick wank anus horny nazi kkk milf spic chink gook fuk fck " +
            "muji randi lado laado puti chaak lund lauda loda lodu";

        private readonly string[] _fragments;
        private readonly HashSet<string> _words;

        public NameFilter()
        {
            _fragments = BuiltInFragments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            _words = new HashSet<string>(BuiltInWords.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        }

        /// <summary>False when the name contains a blocked word. Empty names are allowed here (length is checked elsewhere).</summary>
        public bool IsAllowed(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            string folded = Fold(name);
            string joined = folded.Replace(" ", string.Empty);
            foreach (string fragment in _fragments)
            {
                if (joined.IndexOf(fragment, StringComparison.Ordinal) >= 0)
                {
                    return false;
                }
            }

            if (_words.Contains(joined))
            {
                return false;
            }

            foreach (string token in folded.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (_words.Contains(token))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Lower case, Latin accents removed (ü → u), the leetspeak digits turned into letters and
        /// every whitespace character made a plain space. Other scripts pass through unchanged.
        /// </summary>
        internal static string Fold(string text)
        {
            string decomposed;
            try
            {
                decomposed = text.Normalize(NormalizationForm.FormD);
            }
            catch (ArgumentException)
            {
                decomposed = text; // ill-formed UTF-16; the name validator rejects it anyway
            }

            var sb = new StringBuilder(decomposed.Length);
            char previousBase = ' ';
            foreach (char raw in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark && previousBase < '\u0080')
                {
                    continue;
                }

                char c = char.IsWhiteSpace(raw) ? ' ' : char.ToLowerInvariant(raw);
                switch (c)
                {
                    case '0': c = 'o'; break;
                    case '1': c = 'i'; break;
                    case '3': c = 'e'; break;
                    case '4': c = 'a'; break;
                    case '5': c = 's'; break;
                    case '7': c = 't'; break;
                }

                sb.Append(c);
                previousBase = c;
            }

            return sb.ToString();
        }
    }
}
