using Callbreak.Application.Flow;
using Callbreak.Application.Profile;
using Callbreak.Application.Settings;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Flow
{
    /// <summary>GDD §15 "First-time experience": the primer at the first launch, and the name asked when the game starts (ADR 022).</summary>
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

        // The name is asked when the game starts, until the player has chosen one (ADR 022).

        [Test]
        public void AsksName_ANewPlayer_IsTrue()
        {
            Assert.IsTrue(FirstRun.AsksName(_profile));
        }

        [Test]
        public void AsksName_OnceAName_IsChosen_IsFalse()
        {
            Assert.AreEqual(NameCheck.Ok, _profile.ChooseName("Asha"));

            Assert.IsFalse(FirstRun.AsksName(_profile));
        }

        [Test]
        public void AsksName_KeepingPlayer_IsAnAnswerToo()
        {
            Assert.AreEqual(NameCheck.Unchanged, _profile.ChooseName("Player"));

            Assert.IsFalse(FirstRun.AsksName(_profile));
        }

        [Test]
        public void AsksName_NamedOnTheProfileScreen_IsFalse()
        {
            Assert.AreEqual(NameCheck.Ok, _profile.TrySetDisplayName("Bikash"));

            Assert.IsFalse(FirstRun.AsksName(_profile));
        }
    }
}
