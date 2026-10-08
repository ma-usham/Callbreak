using System;

namespace Callbreak.Application.Ports
{
    /// <summary>
    /// Holds the game silent while a full-screen ad is on screen (the owner, 8 Oct 2026: "when the
    /// ad plays the game sound also plays"; ADR 035). On Android the game keeps running under an ad,
    /// so its music and sound effects would play over it. <see cref="Silence"/> pauses all of the
    /// game's audio, and no new sound effect plays, until every hold it gave is disposed; then the
    /// audio is as it was: what was playing goes on, the player's Sound and Music settings are
    /// untouched, and audio that was paused before stays paused. Disposing a hold twice does nothing,
    /// so one ad can't end another's silence. Main thread only; never throws.
    /// </summary>
    public interface IAudioSilencer
    {
        /// <summary>Silences the game until the returned hold, and every other one, is disposed.</summary>
        IDisposable Silence();
    }
}
