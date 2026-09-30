using System;
using Callbreak.Application.Sessions;

namespace Callbreak.Application.Settings
{
    /// <summary>
    /// Player settings (GDD §11 screen 11, §29 settings.json). Every setter applies at once, saves
    /// immediately and raises <see cref="Changed"/>; nothing needs a Save button.
    /// </summary>
    public interface ISettingsService
    {
        event Action Changed;

        bool Music { get; }

        bool SoundEffects { get; }

        /// <summary>Master volume, 0-1 (default 0.7).</summary>
        float Volume { get; }

        /// <summary>Language code, "en" by default.</summary>
        string Language { get; }

        bool Vibration { get; }

        bool ReduceMotion { get; }

        HandSortOrder SortOrder { get; }

        /// <summary>Last turn time chosen on Create Room: 15 or 30 (default 30).</summary>
        int TurnSeconds { get; }

        /// <summary>Last difficulty chosen on Offline setup (default Normal).</summary>
        BotDifficulty Difficulty { get; }

        /// <summary>How to Play is shown once before the first match (GDD §11 screen 12).</summary>
        bool HowToPlaySeen { get; }

        /// <summary>Completed matches, for first-match tips that switch off after 3 (GDD §15).</summary>
        int CompletedMatches { get; }

        /// <summary>Optional beginner assist on the call panel (off by default, GDD §11 screen 07).</summary>
        bool BeginnerAssist { get; }

        /// <summary>The nudge to pick a name and avatar is shown once, after the first match (GDD §15).</summary>
        bool NameNudgeSeen { get; }

        void SetMusic(bool on);

        void SetSoundEffects(bool on);

        void SetVolume(float volume);

        /// <summary>
        /// Applies a volume at once and raises <see cref="Changed"/> without writing the file: a
        /// slider being dragged calls this on every movement and <see cref="SetVolume"/> once it
        /// settles. Any later save writes the previewed value too.
        /// </summary>
        void PreviewVolume(float volume);

        void SetLanguage(string language);

        void SetVibration(bool on);

        void SetReduceMotion(bool on);

        void SetSortOrder(HandSortOrder order);

        void SetTurnSeconds(int seconds);

        void SetDifficulty(BotDifficulty difficulty);

        void MarkHowToPlaySeen();

        void IncrementCompletedMatches();

        void MarkNameNudgeSeen();

        void SetBeginnerAssist(bool on);
    }
}
