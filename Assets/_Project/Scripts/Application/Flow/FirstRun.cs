using Callbreak.Application.Profile;
using Callbreak.Application.Settings;

namespace Callbreak.Application.Flow
{
    /// <summary>
    /// GDD §15 "First-time experience": what the menu does by itself on a player's first visits.
    /// The menu asks for the player's name until one is chosen (ADR 022, in place of the GDD's
    /// nudge after the first match), and the primer opens once, the first time the app reaches the
    /// main menu.
    /// </summary>
    public static class FirstRun
    {
        /// <summary>True when the menu should open How to Play by itself (screen 12, once).</summary>
        public static bool ShowsPrimer(ISettingsService settings, AppState previous, MenuEntry entry)
        {
            return previous == AppState.Boot && entry == MenuEntry.MainMenu && !settings.HowToPlaySeen;
        }

        /// <summary>True when the menu should ask for the player's name: until one is chosen.</summary>
        public static bool AsksName(IProfileService profile)
        {
            return !profile.HasChosenName;
        }
    }
}
