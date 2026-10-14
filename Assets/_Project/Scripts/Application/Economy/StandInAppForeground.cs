using System;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The game's foreground where there is no ad network: the Editor, the tests and desktop builds
    /// (ProjectLifetimeScope). It never moves, so no app open ad is ever due there.
    /// </summary>
    public sealed class StandInAppForeground : IAppForeground
    {
        public event Action<DateTime> WentToBackground
        {
            add { }
            remove { }
        }

        public event Action CameToForeground
        {
            add { }
            remove { }
        }
    }
}
