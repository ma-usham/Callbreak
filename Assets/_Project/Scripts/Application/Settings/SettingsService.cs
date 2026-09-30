using System;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;

namespace Callbreak.Application.Settings
{
    /// <summary>
    /// <see cref="ISettingsService"/> over the <c>settings</c> save file (GDD §11 screen 11, §29).
    /// Every setter validates its argument (throwing on values no control can produce), applies at
    /// once, saves and raises <see cref="Changed"/>; setting the current value again does nothing.
    /// Values read from disk are repaired rather than trusted. A file from a newer app version is
    /// ignored and never overwritten: the settings then live in memory for this session.
    /// </summary>
    public sealed class SettingsService : ISettingsService
    {
        public const string SaveKey = "settings";
        public const float DefaultVolume = 0.7f;
        public const string DefaultLanguage = "en";
        public const int ShortTurnSeconds = 15;
        public const int LongTurnSeconds = 30;

        private const int MaxLanguageLength = 16;

        private readonly ISaveStore _store;
        private readonly SettingsData _data;
        private readonly bool _readOnly;

        public SettingsService(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            SettingsData loaded = VersionedSave.Load<SettingsData>(store, SaveKey, SettingsData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new SettingsData();
            Repair(_data);
        }

        public event Action Changed;

        public bool Music => _data.music;

        public bool SoundEffects => _data.soundEffects;

        public float Volume => _data.volume;

        public string Language => _data.language;

        public bool Vibration => _data.vibration;

        public bool ReduceMotion => _data.reduceMotion;

        public HandSortOrder SortOrder => _data.sortOrder;

        public int TurnSeconds => _data.turnSeconds;

        public BotDifficulty Difficulty => _data.difficulty;

        public bool HowToPlaySeen => _data.howToPlaySeen;

        public int CompletedMatches => _data.completedMatches;

        public bool BeginnerAssist => _data.beginnerAssist;

        public bool NameNudgeSeen => _data.nameNudgeSeen;

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved.</summary>
        public bool IsReadOnly => _readOnly;

        public void SetMusic(bool on)
        {
            if (_data.music == on)
            {
                return;
            }

            _data.music = on;
            Commit();
        }

        public void SetSoundEffects(bool on)
        {
            if (_data.soundEffects == on)
            {
                return;
            }

            _data.soundEffects = on;
            Commit();
        }

        public void SetVolume(float volume)
        {
            if (float.IsNaN(volume) || volume < 0f || volume > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(volume), volume, "Volume must be between 0 and 1.");
            }

            if (_data.volume == volume)
            {
                return;
            }

            _data.volume = volume;
            Commit();
        }

        public void SetLanguage(string language)
        {
            string code = NormalizeLanguage(language);
            if (code == null)
            {
                throw new ArgumentException($"'{language}' is not a language code such as \"en\" or \"ne\".", nameof(language));
            }

            if (_data.language == code)
            {
                return;
            }

            _data.language = code;
            Commit();
        }

        public void SetVibration(bool on)
        {
            if (_data.vibration == on)
            {
                return;
            }

            _data.vibration = on;
            Commit();
        }

        public void SetReduceMotion(bool on)
        {
            if (_data.reduceMotion == on)
            {
                return;
            }

            _data.reduceMotion = on;
            Commit();
        }

        public void SetSortOrder(HandSortOrder order)
        {
            if (!Enum.IsDefined(typeof(HandSortOrder), order))
            {
                throw new ArgumentOutOfRangeException(nameof(order), order, "Unknown sort order.");
            }

            if (_data.sortOrder == order)
            {
                return;
            }

            _data.sortOrder = order;
            Commit();
        }

        public void SetTurnSeconds(int seconds)
        {
            if (!IsValidTurnSeconds(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, $"Turn time is {ShortTurnSeconds} or {LongTurnSeconds} seconds.");
            }

            if (_data.turnSeconds == seconds)
            {
                return;
            }

            _data.turnSeconds = seconds;
            Commit();
        }

        public void SetDifficulty(BotDifficulty difficulty)
        {
            if (!Enum.IsDefined(typeof(BotDifficulty), difficulty))
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown difficulty.");
            }

            if (_data.difficulty == difficulty)
            {
                return;
            }

            _data.difficulty = difficulty;
            Commit();
        }

        public void MarkHowToPlaySeen()
        {
            if (_data.howToPlaySeen)
            {
                return;
            }

            _data.howToPlaySeen = true;
            Commit();
        }

        public void MarkNameNudgeSeen()
        {
            if (_data.nameNudgeSeen)
            {
                return;
            }

            _data.nameNudgeSeen = true;
            Commit();
        }

        public void IncrementCompletedMatches()
        {
            if (_data.completedMatches == int.MaxValue)
            {
                return;
            }

            _data.completedMatches++;
            Commit();
        }

        public void SetBeginnerAssist(bool on)
        {
            if (_data.beginnerAssist == on)
            {
                return;
            }

            _data.beginnerAssist = on;
            Commit();
        }

        public static bool IsValidTurnSeconds(int seconds) => seconds == ShortTurnSeconds || seconds == LongTurnSeconds;

        /// <summary>
        /// Lower-cases a language code and checks its shape ("en", "ne", "ne-np"); returns null
        /// when it is not one. Whether the game has strings for it is the localization's business.
        /// </summary>
        public static string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return null;
            }

            string code = language.Trim().Replace('_', '-').ToLowerInvariant();
            if (code.Length < 2 || code.Length > MaxLanguageLength)
            {
                return null;
            }

            int primary = code.IndexOf('-');
            int primaryLength = primary < 0 ? code.Length : primary;
            if (primaryLength < 2 || primaryLength > 3 || code[code.Length - 1] == '-')
            {
                return null;
            }

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                bool letter = c >= 'a' && c <= 'z';
                bool digit = c >= '0' && c <= '9';
                if (i < primaryLength ? !letter : !(letter || digit || (c == '-' && code[i - 1] != '-')))
                {
                    return null;
                }
            }

            return code;
        }

        private void Commit()
        {
            if (!_readOnly)
            {
                _store.Save(SaveKey, _data);
            }

            Changed?.Invoke();
        }

        /// <summary>Brings a loaded file up to the current schema and replaces anything invalid with its default.</summary>
        private static void Repair(SettingsData data)
        {
            data.schemaVersion = SettingsData.CurrentVersion;
            if (float.IsNaN(data.volume) || float.IsInfinity(data.volume))
            {
                data.volume = DefaultVolume;
            }
            else if (data.volume < 0f || data.volume > 1f)
            {
                data.volume = Math.Max(0f, Math.Min(1f, data.volume));
            }

            data.language = NormalizeLanguage(data.language) ?? DefaultLanguage;
            if (!Enum.IsDefined(typeof(HandSortOrder), data.sortOrder))
            {
                data.sortOrder = HandSortOrder.BySuit;
            }

            if (!IsValidTurnSeconds(data.turnSeconds))
            {
                data.turnSeconds = LongTurnSeconds;
            }

            if (!Enum.IsDefined(typeof(BotDifficulty), data.difficulty))
            {
                data.difficulty = BotDifficulty.Normal;
            }

            if (data.completedMatches < 0)
            {
                data.completedMatches = 0;
            }
        }
    }
}
