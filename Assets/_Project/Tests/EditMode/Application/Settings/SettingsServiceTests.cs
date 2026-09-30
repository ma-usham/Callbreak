using System;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §11 screen 11 and §29: defaults, instant save, validation, and the schemaVersion policy.</summary>
    public sealed class SettingsServiceTests
    {
        private InMemorySaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemorySaveStore();
        }

        [Test]
        public void NoFile_StartsWithTheDesignDefaults_WithoutSaving()
        {
            var settings = new SettingsService(_store);

            Assert.IsTrue(settings.Music);
            Assert.IsTrue(settings.SoundEffects);
            Assert.AreEqual(0.7f, settings.Volume);
            Assert.AreEqual("en", settings.Language);
            Assert.IsTrue(settings.Vibration);
            Assert.IsFalse(settings.ReduceMotion);
            Assert.AreEqual(HandSortOrder.BySuit, settings.SortOrder);
            Assert.AreEqual(30, settings.TurnSeconds);
            Assert.AreEqual(BotDifficulty.Normal, settings.Difficulty);
            Assert.IsFalse(settings.HowToPlaySeen);
            Assert.AreEqual(0, settings.CompletedMatches);
            Assert.IsFalse(settings.BeginnerAssist);
            Assert.IsFalse(settings.IsReadOnly);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void Setter_WithANewValue_SavesAndRaisesChangedOnce()
        {
            var settings = new SettingsService(_store);
            int changed = 0;
            settings.Changed += () => changed++;

            settings.SetMusic(false);

            Assert.AreEqual(1, changed);
            Assert.AreEqual(1, _store.SavesOf(SettingsService.SaveKey));
            Assert.IsFalse(_store.Peek<SettingsData>(SettingsService.SaveKey).music);
        }

        [Test]
        public void EverySetter_WithTheCurrentValue_NeitherSavesNorRaises()
        {
            var settings = new SettingsService(_store);
            int changed = 0;
            settings.Changed += () => changed++;

            settings.SetMusic(true);
            settings.SetSoundEffects(true);
            settings.SetVolume(0.7f);
            settings.SetLanguage("en");
            settings.SetLanguage(" EN ");
            settings.SetVibration(true);
            settings.SetReduceMotion(false);
            settings.SetSortOrder(HandSortOrder.BySuit);
            settings.SetTurnSeconds(30);
            settings.SetDifficulty(BotDifficulty.Normal);
            settings.SetBeginnerAssist(false);

            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void EveryChange_SurvivesARestart()
        {
            var settings = new SettingsService(_store);
            settings.SetMusic(false);
            settings.SetSoundEffects(false);
            settings.SetVolume(0.25f);
            settings.SetLanguage("ne");
            settings.SetVibration(false);
            settings.SetReduceMotion(true);
            settings.SetSortOrder(HandSortOrder.AlternatingColours);
            settings.SetTurnSeconds(15);
            settings.SetDifficulty(BotDifficulty.Hard);
            settings.MarkHowToPlaySeen();
            settings.IncrementCompletedMatches();
            settings.IncrementCompletedMatches();
            settings.SetBeginnerAssist(true);

            var reloaded = new SettingsService(_store);

            Assert.IsFalse(reloaded.Music);
            Assert.IsFalse(reloaded.SoundEffects);
            Assert.AreEqual(0.25f, reloaded.Volume);
            Assert.AreEqual("ne", reloaded.Language);
            Assert.IsFalse(reloaded.Vibration);
            Assert.IsTrue(reloaded.ReduceMotion);
            Assert.AreEqual(HandSortOrder.AlternatingColours, reloaded.SortOrder);
            Assert.AreEqual(15, reloaded.TurnSeconds);
            Assert.AreEqual(BotDifficulty.Hard, reloaded.Difficulty);
            Assert.IsTrue(reloaded.HowToPlaySeen);
            Assert.AreEqual(2, reloaded.CompletedMatches);
            Assert.IsTrue(reloaded.BeginnerAssist);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(0.35f)]
        public void SetVolume_WithinZeroToOne_IsKept(float volume)
        {
            var settings = new SettingsService(_store);

            settings.SetVolume(volume);

            Assert.AreEqual(volume, settings.Volume);
        }

        [TestCase(-0.01f)]
        [TestCase(1.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void SetVolume_OutsideZeroToOne_Throws_AndChangesNothing(float volume)
        {
            var settings = new SettingsService(_store);

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetVolume(volume));
            Assert.AreEqual(0.7f, settings.Volume);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [TestCase(15)]
        [TestCase(30)]
        public void SetTurnSeconds_FifteenOrThirty_IsKept(int seconds)
        {
            var settings = new SettingsService(_store);

            settings.SetTurnSeconds(seconds);

            Assert.AreEqual(seconds, settings.TurnSeconds);
        }

        [TestCase(0)]
        [TestCase(10)]
        [TestCase(20)]
        [TestCase(45)]
        [TestCase(-15)]
        public void SetTurnSeconds_AnythingElse_Throws(int seconds)
        {
            var settings = new SettingsService(_store);

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetTurnSeconds(seconds));
            Assert.AreEqual(30, settings.TurnSeconds);
        }

        [Test]
        public void SetDifficultyAndSortOrder_UndefinedValues_Throw()
        {
            var settings = new SettingsService(_store);

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetDifficulty((BotDifficulty)7));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetSortOrder((HandSortOrder)7));
            Assert.AreEqual(0, _store.SaveCount);
        }

        [TestCase("ne", "ne")]
        [TestCase("NE", "ne")]
        [TestCase(" en ", "en")]
        [TestCase("ne_NP", "ne-np")]
        [TestCase("zh-Hans", "zh-hans")]
        public void SetLanguage_StoresTheLowerCaseCode(string given, string stored)
        {
            var settings = new SettingsService(_store);

            settings.SetLanguage(given);

            Assert.AreEqual(stored, settings.Language);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("e")]
        [TestCase("english")]
        [TestCase("en-")]
        [TestCase("en--us")]
        [TestCase("1n")]
        [TestCase("नेपाली")]
        public void SetLanguage_NotALanguageCode_Throws(string given)
        {
            var settings = new SettingsService(_store);

            Assert.Throws<ArgumentException>(() => settings.SetLanguage(given));
            Assert.AreEqual("en", settings.Language);
        }

        [Test]
        public void MarkHowToPlaySeen_ChangesOnlyTheFirstTime()
        {
            var settings = new SettingsService(_store);
            int changed = 0;
            settings.Changed += () => changed++;

            settings.MarkHowToPlaySeen();
            settings.MarkHowToPlaySeen();

            Assert.IsTrue(settings.HowToPlaySeen);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(1, _store.SaveCount);
        }

        [Test]
        public void IncrementCompletedMatches_CountsEveryCall()
        {
            var settings = new SettingsService(_store);

            settings.IncrementCompletedMatches();
            settings.IncrementCompletedMatches();
            settings.IncrementCompletedMatches();

            Assert.AreEqual(3, settings.CompletedMatches);
            Assert.AreEqual(3, _store.SaveCount);
        }

        [Test]
        public void FileFromANewerVersion_IsIgnored_AndNeverOverwritten()
        {
            _store.Put(SettingsService.SaveKey, new SettingsData { schemaVersion = SettingsData.CurrentVersion + 1, music = false, volume = 0.2f });
            var settings = new SettingsService(_store);
            int changed = 0;
            settings.Changed += () => changed++;

            Assert.IsTrue(settings.IsReadOnly);
            Assert.IsTrue(settings.Music, "the newer file's values are not used");
            Assert.AreEqual(0.7f, settings.Volume);

            settings.SetMusic(false);

            Assert.IsFalse(settings.Music, "changes still apply for this session");
            Assert.AreEqual(1, changed);
            Assert.AreEqual(0, _store.SaveCount);
            SettingsData onDisk = _store.Peek<SettingsData>(SettingsService.SaveKey);
            Assert.AreEqual(SettingsData.CurrentVersion + 1, onDisk.schemaVersion);
            Assert.AreEqual(0.2f, onDisk.volume);
        }

        [Test]
        public void FileWithoutAVersion_KeepsItsValues_AndIsSavedAtTheCurrentVersion()
        {
            _store.Put(SettingsService.SaveKey, new SettingsData { schemaVersion = 0, music = false, volume = 0.3f });
            var settings = new SettingsService(_store);

            Assert.IsFalse(settings.Music);
            Assert.AreEqual(0.3f, settings.Volume);

            settings.SetVibration(false);

            Assert.AreEqual(SettingsData.CurrentVersion, _store.Peek<SettingsData>(SettingsService.SaveKey).schemaVersion);
        }

        [Test]
        public void InvalidValuesOnDisk_AreRepaired()
        {
            _store.Put(SettingsService.SaveKey, new SettingsData
            {
                volume = 5f,
                language = "",
                sortOrder = (HandSortOrder)9,
                turnSeconds = 20,
                difficulty = (BotDifficulty)9,
                completedMatches = -3,
            });

            var settings = new SettingsService(_store);

            Assert.AreEqual(1f, settings.Volume);
            Assert.AreEqual("en", settings.Language);
            Assert.AreEqual(HandSortOrder.BySuit, settings.SortOrder);
            Assert.AreEqual(30, settings.TurnSeconds);
            Assert.AreEqual(BotDifficulty.Normal, settings.Difficulty);
            Assert.AreEqual(0, settings.CompletedMatches);
        }

        [Test]
        public void NotANumberVolumeOnDisk_FallsBackToTheDefault()
        {
            _store.Put(SettingsService.SaveKey, new SettingsData { volume = float.NaN });

            Assert.AreEqual(0.7f, new SettingsService(_store).Volume);
        }
    }
}
