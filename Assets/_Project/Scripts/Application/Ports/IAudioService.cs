namespace Callbreak.Application.Ports
{
    /// <summary>The sound cues of GDD §14.</summary>
    public enum SoundCue
    {
        CardShuffle,
        CardDeal,
        CardPlay,
        TrickCollect,
        CallConfirm,
        YourTurn,
        TimerTick,
        CallMet,
        CallMissed,
        RoundEnd,
        Victory,
        UiTap,
        UiBack,
        Toggle,
    }

    public enum MusicTrack
    {
        None,
        Menu,
        Table,
    }

    /// <summary>
    /// Sound effects and music (GDD §14): mixer groups Music, SFX and UI under Master; levels and
    /// mutes come from settings. Playing is fire-and-forget and never throws.
    /// </summary>
    public interface IAudioService
    {
        void Play(SoundCue cue);

        /// <summary>Crossfades to the track over 1.5 s; the same track again is a no-op.</summary>
        void PlayMusic(MusicTrack track);

        /// <summary>Ducks music by 3 dB for the victory sting and back.</summary>
        void Duck(bool ducked);
    }
}
