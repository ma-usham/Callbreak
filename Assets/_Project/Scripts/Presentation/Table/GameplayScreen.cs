using Callbreak.Presentation.Framework;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The bottom of the in-match screen stack: an empty, see-through screen with the id
    /// <see cref="ScreenId.Gameplay"/>. The table itself never leaves the screen and is not part of
    /// the <see cref="ScreenRouter"/>'s stack; this stands in for it, so the match menu, the
    /// scorecard, settings and the results can be pushed over the table and popped back to it, and
    /// Android back on the bare table reaches <see cref="UIScreen.BackRequested"/> (which opens the
    /// match menu).
    /// </summary>
    public sealed class GameplayScreen : UIScreen
    {
    }
}
