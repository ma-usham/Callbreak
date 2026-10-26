using System;

namespace Callbreak.Application.Ports
{
    /// <summary>
    /// The game's moves between the background and the foreground (port): the player switching to
    /// another app or the home screen, and back. The game's own full-screen ads are not a move. On
    /// phones, Google Mobile Ads' app state; in the Editor, in the tests and on desktop, a stand-in that
    /// never moves. Both events come on the main thread. The game sleeps while it is away, so the move
    /// to the background is told on the return, with the time it happened.
    /// </summary>
    public interface IAppForeground
    {
        /// <summary>The game went to the background, at the given time on the wall clock (UTC).</summary>
        event Action<DateTime> WentToBackground;

        /// <summary>The game came back to the foreground.</summary>
        event Action CameToForeground;
    }
}
