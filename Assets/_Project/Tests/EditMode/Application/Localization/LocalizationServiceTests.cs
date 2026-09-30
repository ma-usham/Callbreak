using Callbreak.Application.Localization;
using Callbreak.Application.Settings;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §15: the active language comes from settings, fallbacks, live switching and culture-invariant formatting.</summary>
    public sealed class LocalizationServiceTests
    {
        private const string Ui =
            "key,en,ne\r\n" +
            "ui.play,Play,खेल्नुहोस्\r\n" +
            "ui.english_only,Only English,\r\n" +
            "ui.bid_won,Bid {0} · Won {1},कल {0} · जित {1}\r\n" +
            "ui.broken,Score {0},अंक {1\r\n" +
            "ui.score,Total {0},\r\n";

        private InMemorySaveStore _store;
        private SettingsService _settings;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemorySaveStore();
            _settings = new SettingsService(_store);
        }

        [Test]
        public void ByDefault_IsEnglish()
        {
            var localization = Create(Ui);

            Assert.AreEqual("en", localization.Language);
            Assert.AreEqual("Play", localization.Get("ui.play"));
        }

        [Test]
        public void ActiveLanguage_ComesFromSettings()
        {
            _settings.SetLanguage("ne");

            var localization = Create(Ui);

            Assert.AreEqual("ne", localization.Language);
            Assert.AreEqual("खेल्नुहोस्", localization.Get("ui.play"));
        }

        [Test]
        public void MissingInTheActiveLanguage_FallsBackToEnglish_ThenToTheKey()
        {
            _settings.SetLanguage("ne");
            var localization = Create(Ui);

            Assert.AreEqual("Only English", localization.Get("ui.english_only"));
            Assert.AreEqual("ui.nowhere", localization.Get("ui.nowhere"));
            Assert.AreEqual(string.Empty, localization.Get(null));
            Assert.AreEqual(string.Empty, localization.Get(string.Empty));
        }

        [Test]
        public void Languages_OnlyListsLanguagesThatHaveStrings()
        {
            var englishOnly = Create("key,en,ne\r\nui.play,Play,\r\n");
            var both = Create(Ui);

            CollectionAssert.AreEqual(new[] { "en" }, englishOnly.Languages);
            CollectionAssert.AreEqual(new[] { "en", "ne" }, both.Languages);
        }

        [Test]
        public void SettingsLanguageWithoutStrings_ShowsEnglish()
        {
            _settings.SetLanguage("ne");

            var localization = Create("key,en,ne\r\nui.play,Play,\r\n");

            Assert.AreEqual("en", localization.Language);
            Assert.AreEqual("Play", localization.Get("ui.play"));
        }

        [Test]
        public void SetLanguage_SavesThroughSettings_AndRaisesLanguageChangedOnce()
        {
            var localization = Create(Ui);
            int changed = 0;
            localization.LanguageChanged += () => changed++;

            localization.SetLanguage("NE");

            Assert.AreEqual("ne", localization.Language);
            Assert.AreEqual("ne", _settings.Language);
            Assert.AreEqual("ne", _store.Peek<SettingsData>(SettingsService.SaveKey).language);
            Assert.AreEqual(1, changed);
            Assert.AreEqual("खेल्नुहोस्", localization.Get("ui.play"));
        }

        [Test]
        public void SetLanguage_TheActiveOne_RaisesNothing()
        {
            var localization = Create(Ui);
            int changed = 0;
            localization.LanguageChanged += () => changed++;

            localization.SetLanguage("en");

            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [TestCase("hi")]
        [TestCase("nonsense")]
        [TestCase(null)]
        public void SetLanguage_WithoutStrings_IsIgnored(string language)
        {
            var localization = Create(Ui);
            int changed = 0;
            localization.LanguageChanged += () => changed++;

            localization.SetLanguage(language);

            Assert.AreEqual("en", localization.Language);
            Assert.AreEqual("en", _settings.Language);
            Assert.AreEqual(0, changed);
        }

        [Test]
        public void ChangingTheSettingDirectly_SwitchesLive()
        {
            var localization = Create(Ui);
            int changed = 0;
            localization.LanguageChanged += () => changed++;

            _settings.SetLanguage("ne");
            _settings.SetMusic(false);

            Assert.AreEqual("ne", localization.Language);
            Assert.AreEqual(1, changed, "only a language change is announced");
        }

        [Test]
        public void Dispose_StopsFollowingSettings()
        {
            var localization = Create(Ui);
            int changed = 0;
            localization.LanguageChanged += () => changed++;

            localization.Dispose();
            _settings.SetLanguage("ne");

            Assert.AreEqual(0, changed);
            Assert.AreEqual("en", localization.Language);
        }

        [Test]
        public void Format_FillsPlaceholders()
        {
            var localization = Create(Ui);

            Assert.AreEqual("Bid 3 · Won 1", localization.Format("ui.bid_won", 3, 1));
        }

        [Test]
        public void Format_UsesTheInvariantCulture()
        {
            var localization = Create(Ui);

            Assert.AreEqual("Total 4.1", CultureRunner.In("de-DE", () => localization.Format("ui.score", 4.1)));
            Assert.AreEqual("Total -3", CultureRunner.In("sv-SE", () => localization.Format("ui.score", -3)));
        }

        [Test]
        public void Format_BrokenTranslation_FallsBackToTheEnglishTemplate()
        {
            _settings.SetLanguage("ne");
            var localization = Create(Ui);

            Assert.AreEqual("Score 7", localization.Format("ui.broken", 7));
        }

        [Test]
        public void Format_TooFewArguments_ReturnsTheTemplate_WithoutThrowing()
        {
            var localization = Create(Ui);

            Assert.AreEqual("Bid {0} · Won {1}", localization.Format("ui.bid_won", 3));
            Assert.AreEqual("Bid {0} · Won {1}", localization.Format("ui.bid_won", null));
            Assert.AreEqual("ui.nowhere", localization.Format("ui.nowhere", 1));
        }

        private LocalizationService Create(string uiCsv)
        {
            return new LocalizationService(StringTables.FromCsv(("UI", uiCsv)), _settings);
        }
    }
}
