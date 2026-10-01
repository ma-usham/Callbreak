using System;
using System.Globalization;
using System.Text;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Profile
{
    /// <summary>
    /// <see cref="IProfileService"/> over the <c>profile</c> save file (GDD §11 screen 14, §29).
    /// Display names are normalised (trimmed, inner runs of whitespace made one space, Unicode NFC)
    /// and must then be 3-16 characters (counted as Unicode code points) of letters from any script,
    /// with their combining marks, digits and single spaces, and pass the <see cref="NameFilter"/>.
    /// A file from a newer app version is ignored and never overwritten.
    /// </summary>
    public sealed class ProfileService : IProfileService
    {
        public const string SaveKey = "profile";
        public const string DefaultDisplayName = "Player";
        public const int MinNameLength = 3;
        public const int MaxNameLength = 16;

        /// <summary>The avatar presets of the picker; 0 is the default silhouette.</summary>
        public const int AvatarPresetCount = 5;

        private readonly ISaveStore _store;
        private readonly NameFilter _filter;
        private readonly ProfileData _data;
        private readonly bool _readOnly;

        public ProfileService(ISaveStore store, NameFilter nameFilter)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _filter = nameFilter ?? throw new ArgumentNullException(nameof(nameFilter));
            ProfileData loaded = VersionedSave.Load<ProfileData>(store, SaveKey, ProfileData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new ProfileData();
            Repair(_data);
        }

        public event Action Changed;

        public string DisplayName => _data.displayName;

        public int AvatarId => _data.avatarId;

        public int AvatarCount => AvatarPresetCount;

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved.</summary>
        public bool IsReadOnly => _readOnly;

        public NameCheck CheckName(string candidate)
        {
            return Validate(candidate, out _);
        }

        public bool HasChosenName => _data.nameChosen;

        public NameCheck TrySetDisplayName(string candidate)
        {
            NameCheck check = Validate(candidate, out string name);
            if (check == NameCheck.Ok)
            {
                _data.displayName = name;
                _data.nameChosen = true;
                Commit();
            }

            return check;
        }

        public NameCheck ChooseName(string candidate)
        {
            NameCheck check = Validate(candidate, out string name);
            if (check == NameCheck.Ok)
            {
                _data.displayName = name;
            }
            else if (check != NameCheck.Unchanged)
            {
                return check;
            }

            // Keeping the name it already has ("Player") is an answer too.
            _data.nameChosen = true;
            Commit();
            return check;
        }

        public void SetAvatar(int avatarId)
        {
            if (avatarId < 0 || avatarId >= AvatarPresetCount)
            {
                throw new ArgumentOutOfRangeException(nameof(avatarId), avatarId, $"Avatar presets are 0-{AvatarPresetCount - 1}.");
            }

            if (_data.avatarId == avatarId)
            {
                return;
            }

            _data.avatarId = avatarId;
            Commit();
        }

        /// <summary>
        /// Trims, turns every run of whitespace inside the name into a single space and composes
        /// the text (NFC), so the same name typed on two keyboards is stored the same way.
        /// </summary>
        public static string NormalizeName(string candidate)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(candidate.Length);
            bool pendingSpace = false;
            foreach (char c in candidate)
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = sb.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    sb.Append(' ');
                    pendingSpace = false;
                }

                sb.Append(c);
            }

            string name = sb.ToString();
            try
            {
                return name.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                return name; // ill-formed UTF-16 (a lone surrogate): rejected as invalid characters
            }
        }

        /// <summary>Length in Unicode code points: a character outside the BMP counts once.</summary>
        internal static int CountCodePoints(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                }

                count++;
            }

            return count;
        }

        /// <summary>
        /// Letters of any script (a combining mark or a zero-width joiner may follow a letter, as in
        /// Devanagari), decimal digits and single spaces. Emoji, symbols and punctuation fail.
        /// </summary>
        internal static bool HasOnlyNameCharacters(string name)
        {
            bool afterLetter = false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == ' ')
                {
                    if (i == 0 || name[i - 1] == ' ')
                    {
                        return false;
                    }

                    afterLetter = false;
                    continue;
                }

                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= name.Length || !char.IsLowSurrogate(name[i + 1]))
                    {
                        return false;
                    }
                }

                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(name, i);
                if (char.IsHighSurrogate(c))
                {
                    i++;
                }

                switch (category)
                {
                    case UnicodeCategory.UppercaseLetter:
                    case UnicodeCategory.LowercaseLetter:
                    case UnicodeCategory.TitlecaseLetter:
                    case UnicodeCategory.ModifierLetter:
                    case UnicodeCategory.OtherLetter:
                        afterLetter = true;
                        break;
                    case UnicodeCategory.DecimalDigitNumber:
                        afterLetter = false;
                        break;
                    case UnicodeCategory.NonSpacingMark:
                    case UnicodeCategory.SpacingCombiningMark:
                    case UnicodeCategory.EnclosingMark:
                        if (!afterLetter)
                        {
                            return false;
                        }

                        break;
                    case UnicodeCategory.Format:
                        // Zero-width non-joiner / joiner shape Devanagari conjuncts.
                        if (!afterLetter || (c != '‌' && c != '‍'))
                        {
                            return false;
                        }

                        break;
                    default:
                        return false;
                }
            }

            return true;
        }

        private NameCheck Validate(string candidate, out string name)
        {
            name = NormalizeName(candidate);
            NameCheck check = CheckShape(name);
            if (check != NameCheck.Ok)
            {
                return check;
            }

            return string.Equals(name, _data.displayName, StringComparison.Ordinal) ? NameCheck.Unchanged : NameCheck.Ok;
        }

        /// <summary>Everything except the "unchanged" comparison, on an already normalised name.</summary>
        private NameCheck CheckShape(string name)
        {
            if (name.Length == 0)
            {
                return NameCheck.TooShort;
            }

            if (!HasOnlyNameCharacters(name))
            {
                return NameCheck.InvalidCharacters;
            }

            int length = CountCodePoints(name);
            if (length < MinNameLength)
            {
                return NameCheck.TooShort;
            }

            if (length > MaxNameLength)
            {
                return NameCheck.TooLong;
            }

            return _filter.IsAllowed(name) ? NameCheck.Ok : NameCheck.NotAllowed;
        }

        private void Commit()
        {
            if (!_readOnly)
            {
                _store.Save(SaveKey, _data);
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// A stored name or avatar that is no longer valid falls back to the default, and a player
        /// whose name fell back is asked again. A file from before the question was asked counts as
        /// answered when it holds a name of the player's own.
        /// </summary>
        private void Repair(ProfileData data)
        {
            data.schemaVersion = ProfileData.CurrentVersion;
            string name = NormalizeName(data.displayName);
            bool valid = CheckShape(name) == NameCheck.Ok;
            data.displayName = valid ? name : DefaultDisplayName;
            data.nameChosen = valid && (data.nameChosen || !string.Equals(name, DefaultDisplayName, StringComparison.Ordinal));
            if (data.avatarId < 0 || data.avatarId >= AvatarPresetCount)
            {
                data.avatarId = 0;
            }
        }
    }
}
