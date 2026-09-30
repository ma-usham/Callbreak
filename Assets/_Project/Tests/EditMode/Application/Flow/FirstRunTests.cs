using Callbreak.Application.Flow;
using Callbreak.Application.Profile;
using Callbreak.Application.Settings;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Flow
{
    /// <summary>GDD §15 "First-time experience": the primer at the first launch, the name nudge after the first match.</summary>
    public sealed class FirstRunTests
    {
        private InMemorySaveStore _saves;
        private SettingsService _settings;
        private ProfileService _profile;

        [SetUp]
        public void SetUp()
        {
            _saves = new InMemorySaveStore();
            _settings = new SettingsService(_saves);
            _profile = new ProfileService(_saves, new NameFilter());
        }

        [Test]
        public void ShowsPrimer_FirstLaunch_IsTrue()
        {
            Assert.IsTrue(FirstRun.ShowsPrimer(_settings, AppState.Boot, MenuEntry.MainMenu));
        }

        [Test]
        public void ShowsPrimer_OnceSeen_IsFalse()
        {
            _settings.MarkHowToPlaySeen();

            Assert.IsFalse(FirstRun.ShowsPrimer(_settings, AppState.Boot, MenuEntry.MainMenu));
        }

        [TestCase(AppState.InMatch, MenuEntry.MainMenu)]
        [TestCase(AppState.Boot, MenuEntry.PrivateLobby)]
        [TestCase(AppState.Menu, MenuEntry.MainMenu)]
        public void ShowsPrimer_NotAtTheAppsOpening_IsFalse(AppState previous, MenuEntry entry)
        {
            Assert.IsFalse(FirstRun.ShowsPrimer(_settings, previous, entry));
        }

        [Test]
        public void NudgesName_BackFromTheFirstMatchStillCalledPlayer_IsTrue()
        {
            _settings.IncrementCompletedMatches();

            Assert.IsTrue(FirstRun.NudgesName(_settings, _profile, AppState.InMatch, MenuEntry.MainMenu));
        }

        [Test]
        public void NudgesName_NoMatchFinishedYet_IsFalse()
        {
            Assert.IsFalse(FirstRun.NudgesName(_settings, _profile, AppState.InMatch, MenuEntry.MainMenu));
        }

        [Test]
        public void NudgesName_AlreadyNamed_IsFalse()
        {
            _settings.IncrementCompletedMatches();
            Assert.AreEqual(NameCheck.Ok, _profile.TrySetDisplayName("Asha"));

            Assert.IsFalse(FirstRun.NudgesName(_settings, _profile, AppState.InMatch, MenuEntry.MainMenu));
        }

        [Test]
        public void NudgesName_AlreadyAsked_IsFalse()
        {
            _settings.IncrementCompletedMatches();
            _settings.MarkNameNudgeSeen();

            Assert.IsFalse(FirstRun.NudgesName(_settings, _profile, AppState.InMatch, MenuEntry.MainMenu));
        }

        [TestCase(AppState.Boot, MenuEntry.MainMenu)]
        [TestCase(AppState.InMatch, MenuEntry.PrivateLobby)]
        [TestCase(AppState.InMatch, MenuEntry.Matchmaking)]
        [TestCase(AppState.InMatch, MenuEntry.OfflineSetup)]
        public void NudgesName_OnlyOnTheMainMenuAfterAMatch(AppState previous, MenuEntry entry)
        {
            _settings.IncrementCompletedMatches();

            Assert.IsFalse(FirstRun.NudgesName(_settings, _profile, previous, entry));
        }
    }
}
