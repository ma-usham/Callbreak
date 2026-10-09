using System.Collections.Generic;
using Callbreak.Application.Ports;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>The game's sounds, heard instead of played: register it with GameFixture.Boot's <c>extra</c>.</summary>
    public sealed class HeardSounds : IAudioService
    {
        private readonly List<SoundCue> _heard = new List<SoundCue>();

        public int Count(SoundCue cue) => _heard.FindAll(c => c == cue).Count;

        public void Play(SoundCue cue) => _heard.Add(cue);

        public void PlayMusic(MusicTrack track)
        {
        }

        public void Duck(bool ducked)
        {
        }
    }
}
