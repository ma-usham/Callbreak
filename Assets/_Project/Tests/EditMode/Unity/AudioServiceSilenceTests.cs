using System;
using Callbreak.Application.Config;
using Callbreak.Application.Ports;
using Callbreak.Infrastructure.Audio;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AppSettings = Callbreak.Application.Settings.SettingsService;     // UnityEditor has a SettingsService too

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The game silent under a full-screen ad (the owner, 8 Oct 2026; ADR 035), as the audio service
    /// does it: a hold pauses the listener, and every source with it, and no sound effect plays while
    /// one is out; the last hold brings the listener back as it was, still paused if it was paused
    /// before; holds overlap, and one let go twice can't end another's silence; the player's Sound
    /// and Music settings are never touched. These tests need the engine, so they live outside the
    /// folders the dotnet harness compiles. Each puts the listener back as it found it.
    /// </summary>
    public sealed class AudioServiceSilenceTests
    {
        private const string LibraryPath = "Assets/_Project/Config/Audio/AudioCueLibrary.asset";

        private bool _listenerWasPaused;
        private GameObject _host;
        private AppSettings _settings;
        private AudioService _audio;

        [SetUp]
        public void SetUp()
        {
            _listenerWasPaused = AudioListener.pause;
            AudioListener.pause = false;
            var library = AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(LibraryPath);
            Assert.IsNotNull(library, LibraryPath + " exists");
            Assert.IsTrue(library.TryGetCue(SoundCue.CardPlay, out AudioCueLibrary.CueEntry entry) && entry.Clips.Count > 0, "the card sound has a clip");
            _host = new GameObject(nameof(AudioServiceSilenceTests)) { hideFlags = HideFlags.HideAndDontSave };
            _settings = new AppSettings(new InMemorySaveStore());
            _audio = new AudioService(_host, library, _settings);
        }

        [TearDown]
        public void TearDown()
        {
            _audio?.Dispose();
            if (_host != null)
            {
                UnityEngine.Object.DestroyImmediate(_host);
            }

            AudioListener.pause = _listenerWasPaused;
        }

        [Test]
        public void AHold_PausesTheListener_AndLettingItGo_BringsTheSoundBack()
        {
            IDisposable hold = _audio.Silence();

            Assert.IsTrue(AudioListener.pause, "every source pauses under the ad");
            Assert.IsTrue(_audio.Silenced);

            hold.Dispose();

            Assert.IsFalse(AudioListener.pause, "the sound is back");
            Assert.IsFalse(_audio.Silenced);
        }

        [Test]
        public void NoSoundEffectPlays_WhileSilenced_NorAfterwards()
        {
            _audio.Play(SoundCue.CardPlay);
            Assert.AreEqual(1, _audio.CuesPlayed, "a card sound plays");

            IDisposable hold = _audio.Silence();
            _audio.Play(SoundCue.CardPlay);
            _audio.Play(SoundCue.TrickCollect);

            Assert.AreEqual(1, _audio.CuesPlayed, "nothing is queued under the ad to burst out after it");

            hold.Dispose();
            _audio.Play(SoundCue.CardPlay);

            Assert.AreEqual(2, _audio.CuesPlayed, "sound effects play again once the ad has gone");
        }

        [Test]
        public void TwoHoldsAtOnce_KeepTheGameSilent_UntilBothHaveGone()
        {
            IDisposable first = _audio.Silence();
            IDisposable second = _audio.Silence();

            first.Dispose();
            Assert.IsTrue(AudioListener.pause, "the other ad is still up");

            first.Dispose();
            Assert.IsTrue(AudioListener.pause, "a hold let go twice can't end the other's silence");
            Assert.IsTrue(_audio.Silenced);

            second.Dispose();
            Assert.IsFalse(AudioListener.pause);
            Assert.IsFalse(_audio.Silenced);
        }

        [Test]
        public void AListenerPausedBefore_StaysPaused_AfterTheAd()
        {
            AudioListener.pause = true;

            IDisposable hold = _audio.Silence();
            hold.Dispose();

            Assert.IsTrue(AudioListener.pause, "what paused the game before still has it paused");
        }

        [Test]
        public void ThePlayersSettings_AreAsTheyWere_AfterTheAd()
        {
            _settings.SetMusic(false);
            _settings.SetSoundEffects(true);
            _settings.SetVolume(0.4f);

            IDisposable hold = _audio.Silence();
            Assert.IsFalse(_settings.Music);
            Assert.IsTrue(_settings.SoundEffects);
            hold.Dispose();

            Assert.IsFalse(_settings.Music, "music the player turned off stays off");
            Assert.IsTrue(_settings.SoundEffects);
            Assert.AreEqual(0.4f, _settings.Volume, 1e-6f);

            _settings.SetSoundEffects(false);
            hold = _audio.Silence();
            hold.Dispose();
            _audio.Play(SoundCue.CardPlay);

            Assert.IsFalse(_settings.SoundEffects);
            Assert.AreEqual(0, _audio.CuesPlayed, "sound effects the player turned off stay off");
        }

        [Test]
        public void DisposingTheService_LetsEveryHoldGo()
        {
            IDisposable hold = _audio.Silence();

            _audio.Dispose();

            Assert.IsFalse(AudioListener.pause, "a hold still out never leaves the game silent");
            Assert.DoesNotThrow(() => hold.Dispose());
            _audio.Silence().Dispose();
            Assert.IsFalse(AudioListener.pause, "a disposed service silences nothing");
        }
    }
}
