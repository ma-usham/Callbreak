using System.Linq;
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
    /// One music for the menu and the table (the owner, 27 Oct 2026; ADR 049, addendum): the library
    /// gives both places the same loop, and going from the menu to the table keeps that song playing on
    /// the source it was on rather than starting it again on the other one. These tests need the engine,
    /// so they live outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class AudioServiceMusicTests
    {
        private const string LibraryPath = "Assets/_Project/Config/Audio/AudioCueLibrary.asset";

        private AudioCueLibrary _library;
        private GameObject _host;
        private AudioService _audio;

        [SetUp]
        public void SetUp()
        {
            _library = AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(LibraryPath);
            Assert.IsNotNull(_library, LibraryPath + " exists");
            _host = new GameObject(nameof(AudioServiceMusicTests)) { hideFlags = HideFlags.HideAndDontSave };
            _audio = new AudioService(_host, _library, new AppSettings(new InMemorySaveStore()));
        }

        [TearDown]
        public void TearDown()
        {
            _audio?.Dispose();
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }
        }

        [Test]
        public void TheMenuAndTheTable_PlayTheSameLoop()
        {
            Assert.IsTrue(_library.TryGetMusic(MusicTrack.Menu, out AudioCueLibrary.MusicEntry menu) && menu.Clip != null, "the menu has music");
            Assert.IsTrue(_library.TryGetMusic(MusicTrack.Table, out AudioCueLibrary.MusicEntry table) && table.Clip != null, "the table has music");
            Assert.AreSame(menu.Clip, table.Clip, "one music for both");
        }

        [Test]
        public void GoingToTheTable_KeepsTheSongOnItsSource_AndStartsNoSecondCopy()
        {
            _audio.PlayMusic(MusicTrack.Menu);
            AudioSource[] loaded = MusicSources().Where(s => s.clip != null).ToArray();
            Assert.AreEqual(1, loaded.Length, "the menu starts the song on one source");
            AudioSource playing = loaded[0];
            AudioClip song = playing.clip;

            _audio.PlayMusic(MusicTrack.Table);

            Assert.AreEqual(MusicTrack.Table, _audio.CurrentTrack);
            AudioSource[] afterwards = MusicSources().Where(s => s.clip != null).ToArray();
            Assert.AreEqual(1, afterwards.Length, "no second copy starts at the table");
            Assert.AreSame(playing, afterwards[0], "the song plays on where it was");
            Assert.AreSame(song, afterwards[0].clip);

            _audio.PlayMusic(MusicTrack.Menu);

            Assert.AreSame(playing, MusicSources().Single(s => s.clip != null), "and back on the menu it still plays on");
        }

        /// <summary>The service's music sources: the looping ones (sound effects don't loop).</summary>
        private AudioSource[] MusicSources()
        {
            return _host.GetComponentsInChildren<AudioSource>(true).Where(s => s.loop).ToArray();
        }
    }
}
