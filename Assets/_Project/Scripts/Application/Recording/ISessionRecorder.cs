namespace Callbreak.Application.Recording
{
    /// <summary>
    /// The session-recording SDK (port): it records what the screen shows and where it is touched,
    /// and uploads that for the owner to watch later (Smartlook on Android phones, ADR 044). Only
    /// <see cref="SessionRecording"/> drives it, and only while the player's privacy choice allows
    /// it, so nothing is set up or recorded before then. Every call comes on the main thread, and
    /// nothing here throws: an SDK error is logged and becomes a false or nothing.
    /// </summary>
    public interface ISessionRecorder
    {
        /// <summary>
        /// Starts recording; the first start also sets the SDK up. With <paramref name="hidden"/> the
        /// screen is hidden from the very first frame (see <see cref="SetHidden"/>). False when the
        /// SDK could not start: nothing is recorded then.
        /// </summary>
        bool Start(bool hidden);

        /// <summary>Stops recording. The SDK stays set up, so a later <see cref="Start"/> goes on at once.</summary>
        void Stop();

        /// <summary>
        /// While true the recording keeps no picture of the screen (a blank screen in the replay); the
        /// session's timing and touches are still recorded. Called only while recording. False when
        /// the SDK could not change it.
        /// </summary>
        bool SetHidden(bool hidden);
    }
}
