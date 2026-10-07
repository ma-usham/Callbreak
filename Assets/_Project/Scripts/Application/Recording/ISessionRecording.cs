using System;

namespace Callbreak.Application.Recording
{
    /// <summary>
    /// The session recording, as the screens see it (ADR 044). On Android phones with a Smartlook
    /// project key it is <see cref="SessionRecording"/>, which records only while the player's privacy
    /// choice allows it; in the Editor, in the tests and on desktop <see cref="StandInSessionRecording"/>
    /// records nothing. A screen that shows something the recording must not keep, such as a
    /// display-name field, hides itself while it is up. Called on the main thread.
    /// </summary>
    public interface ISessionRecording
    {
        /// <summary>
        /// Keeps the screen out of the recording until the returned handle is disposed: the replay
        /// shows a blank screen meanwhile. Handles may overlap (the screen stays hidden until the last
        /// one goes), work whether or not a recording is running, and a second dispose does nothing.
        /// </summary>
        IDisposable HideScreen();
    }
}
