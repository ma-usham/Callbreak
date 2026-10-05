using Callbreak.Presentation.Framework;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// When the table takes input (GDD §11 screens 09-13 and 16). The results, the scorecard, the
    /// match menu, the settings and the rules open over the table, and online the match runs on
    /// under them. A touch that reaches the table while one of them is the router's top screen
    /// must not play a card, make a call or open the emotes: the
    /// table and the call panel act only while nothing covers them. The pages
    /// take every touch themselves; this is the second lock on the same door.
    /// </summary>
    public static class TableInput
    {
        /// <param name="top">The router's top screen.</param>
        /// <returns>True for the bare table: the empty gameplay screen, or no screen at all.</returns>
        public static bool IsOpen(ScreenId top)
        {
            return top == ScreenId.Gameplay || top == ScreenId.None;
        }
    }
}
