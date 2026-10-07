using System;

namespace Callbreak.Application.Recording
{
    /// <summary>
    /// The session recording where nothing is recorded: the Editor, the tests, desktop builds, and a
    /// phone build without a Smartlook project key (ProjectLifetimeScope). Screens may still hide
    /// themselves; it only counts the handles, so a test can see that a screen did.
    /// </summary>
    public sealed class StandInSessionRecording : ISessionRecording
    {
        /// <summary>How many <see cref="HideScreen"/> handles are open.</summary>
        public int HiddenCount { get; private set; }

        public IDisposable HideScreen()
        {
            HiddenCount++;
            return new Handle(this);
        }

        private sealed class Handle : IDisposable
        {
            private StandInSessionRecording _owner;

            public Handle(StandInSessionRecording owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }

                _owner.HiddenCount--;
                _owner = null;
            }
        }
    }
}
