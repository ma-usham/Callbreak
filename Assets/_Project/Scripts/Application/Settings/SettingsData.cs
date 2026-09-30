using System;
using Callbreak.Application.Sessions;

namespace Callbreak.Application.Settings
{
    /// <summary>
    /// settings.json (GDD §29): plain serializable data with public fields, read and written by
    /// <see cref="SettingsService"/>. Field initializers are the defaults, so a file written before
    /// a field existed loads with that field's default. Adding a field bumps
    /// <see cref="CurrentVersion"/>.
    /// </summary>
    [Serializable]
    public sealed class SettingsData
    {
        public const int CurrentVersion = 2;

        public int schemaVersion = CurrentVersion;
        public bool music = true;
        public bool soundEffects = true;
        public float volume = SettingsService.DefaultVolume;
        public string language = SettingsService.DefaultLanguage;
        public bool vibration = true;
        public bool reduceMotion;
        public HandSortOrder sortOrder = HandSortOrder.BySuit;
        public int turnSeconds = SettingsService.LongTurnSeconds;
        public BotDifficulty difficulty = BotDifficulty.Normal;
        public bool howToPlaySeen;
        public int completedMatches;
        public bool beginnerAssist;
        public bool nameNudgeSeen;
    }
}
