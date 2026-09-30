using Callbreak.Application.Profile;
using Callbreak.Application.Settings;

namespace Callbreak.Application.Flow
{
    /// <summary>
    /// GDD §15 "First-time experience": what the menu does by itself on a player's first visits.
    /// The primer opens once, the first time the app reaches the main menu; after the first
    /// finished match a player still called "Player" is asked, once, to pick a name and avatar.
    /// </summary>
    public static class FirstRun
    {
        /// <summary>True when the menu should open How to Play by itself (screen 12, once).</summary>
        public static bool ShowsPrimer(ISettingsService settings, AppState previous, MenuEntry entry)
        {
            return previous == AppState.Boot && entry == MenuEntry.MainMenu && !settings.HowToPlaySeen;
        }

        /// <summary>True when the menu should ask for a display name and avatar.</summary>
        public static bool NudgesName(ISettingsService settings, IProfileService profile, AppState previous, MenuEntry entry)
        {
            return previous == AppState.InMatch
                && entry == MenuEntry.MainMenu
                && settings.CompletedMatches >= 1
                && !settings.NameNudgeSeen
                && profile.DisplayName == ProfileService.DefaultDisplayName;
        }
    }
}
