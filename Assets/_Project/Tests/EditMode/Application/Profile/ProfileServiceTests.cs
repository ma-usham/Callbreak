using System;
using Callbreak.Application.Profile;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §11 screen 14: the display name rules, the avatar presets and persistence.</summary>
    public sealed class ProfileServiceTests
    {
        private InMemorySaveStore _store;
        private ProfileService _profile;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemorySaveStore();
            _profile = new ProfileService(_store, new NameFilter());
        }

        [Test]
        public void NoFile_IsPlayerWithTheDefaultAvatar()
        {
            Assert.AreEqual("Player", _profile.DisplayName);
            Assert.AreEqual(0, _profile.AvatarId);
            Assert.AreEqual(5, _profile.AvatarCount);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [TestCase("Ram", NameCheck.Ok)]
        [TestCase("  Ram  ", NameCheck.Ok)]
        [TestCase("Ram Bahadur", NameCheck.Ok)]
        [TestCase("Ram 2", NameCheck.Ok)]
        [TestCase("12345", NameCheck.Ok)]
        [TestCase("player", NameCheck.Ok)]
        [TestCase("Łukasz", NameCheck.Ok)]
        [TestCase("राम", NameCheck.Ok)]
        [TestCase("सुमन श्रेष्ठ", NameCheck.Ok)]
        [TestCase("ABCDEFGHIJKLMNOP", NameCheck.Ok)]
        [TestCase("Ra", NameCheck.TooShort)]
        [TestCase("  R a ", NameCheck.Ok)]
        [TestCase("", NameCheck.TooShort)]
        [TestCase("   ", NameCheck.TooShort)]
        [TestCase(null, NameCheck.TooShort)]
        [TestCase("ABCDEFGHIJKLMNOPQ", NameCheck.TooLong)]
        [TestCase("Ram!", NameCheck.InvalidCharacters)]
        [TestCase("Ram_B", NameCheck.InvalidCharacters)]
        [TestCase("Ram-B", NameCheck.InvalidCharacters)]
        [TestCase("Ram\U0001F600", NameCheck.InvalidCharacters)]
        [TestCase("́Ram", NameCheck.InvalidCharacters)]
        [TestCase("Ram​", NameCheck.InvalidCharacters)]
        [TestCase("Player", NameCheck.Unchanged)]
        [TestCase("  Player ", NameCheck.Unchanged)]
        [TestCase("Fuck", NameCheck.NotAllowed)]
        [TestCase("Big Ass", NameCheck.NotAllowed)]
        public void CheckName_ReturnsTheReason(string candidate, NameCheck expected)
        {
            Assert.AreEqual(expected, _profile.CheckName(candidate));
            Assert.AreEqual(0, _store.SaveCount, "checking never saves");
        }

        [Test]
        public void CheckName_CountsCodePoints_SoACharacterOutsideTheBmpCountsOnce()
        {
            // 𝓐 (U+1D4D0) is a letter stored as two UTF-16 units.
            Assert.AreEqual(NameCheck.Ok, _profile.CheckName("\U0001D4D0bc"));
            Assert.AreEqual(NameCheck.TooShort, _profile.CheckName("\U0001D4D0b"));
            Assert.AreEqual(NameCheck.InvalidCharacters, _profile.CheckName("ab\uD835"), "a lone surrogate");
        }

        [Test]
        public void TrySetDisplayName_Ok_SavesTheNormalisedName_AndRaisesChanged()
        {
            int changed = 0;
            _profile.Changed += () => changed++;

            NameCheck result = _profile.TrySetDisplayName("  Ram \t  Bahadur ");

            Assert.AreEqual(NameCheck.Ok, result);
            Assert.AreEqual("Ram Bahadur", _profile.DisplayName);
            Assert.AreEqual(1, changed);
            Assert.AreEqual("Ram Bahadur", _store.Peek<ProfileData>(ProfileService.SaveKey).displayName);
        }

        [Test]
        public void TrySetDisplayName_ComposesDecomposedAccents()
        {
            _profile.TrySetDisplayName("José");

            Assert.AreEqual("José", _profile.DisplayName);
        }

        [TestCase("Ra")]
        [TestCase("Ram!")]
        [TestCase("Player")]
        [TestCase("B1tch")]
        public void TrySetDisplayName_Rejected_KeepsTheOldName_WithoutSaving(string candidate)
        {
            int changed = 0;
            _profile.Changed += () => changed++;

            NameCheck result = _profile.TrySetDisplayName(candidate);

            Assert.AreNotEqual(NameCheck.Ok, result);
            Assert.AreEqual("Player", _profile.DisplayName);
            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SaveCount);
        }

        // ------------------------------------------------------------------ the name asked at the start (ADR 022)

        [Test]
        public void NoFile_HasNotChosenAName()
        {
            Assert.IsFalse(_profile.HasChosenName);
        }

        [Test]
        public void ChooseName_ANewName_SavesIt_AndCountsAsChosen()
        {
            int changed = 0;
            _profile.Changed += () => changed++;

            NameCheck result = _profile.ChooseName("  Asha ");

            Assert.AreEqual(NameCheck.Ok, result);
            Assert.AreEqual("Asha", _profile.DisplayName);
            Assert.IsTrue(_profile.HasChosenName);
            Assert.AreEqual(1, changed);
            Assert.IsTrue(new ProfileService(_store, new NameFilter()).HasChosenName, "the choice survives a restart");
        }

        [Test]
        public void ChooseName_KeepingPlayer_CountsAsChosen()
        {
            NameCheck result = _profile.ChooseName("Player");

            Assert.AreEqual(NameCheck.Unchanged, result);
            Assert.AreEqual("Player", _profile.DisplayName);
            Assert.IsTrue(_profile.HasChosenName);
            Assert.AreEqual(1, _store.SaveCount);
            Assert.IsTrue(new ProfileService(_store, new NameFilter()).HasChosenName, "a player who keeps \"Player\" is not asked again");
        }

        [TestCase("Ra")]
        [TestCase("Ram!")]
        [TestCase("ABCDEFGHIJKLMNOPQ")]
        [TestCase("B1tch")]
        public void ChooseName_Rejected_ChangesNothing(string candidate)
        {
            NameCheck result = _profile.ChooseName(candidate);

            Assert.AreNotEqual(NameCheck.Ok, result);
            Assert.AreNotEqual(NameCheck.Unchanged, result);
            Assert.AreEqual("Player", _profile.DisplayName);
            Assert.IsFalse(_profile.HasChosenName);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void TrySetDisplayName_Ok_CountsAsChosen()
        {
            _profile.TrySetDisplayName("Bikash");

            Assert.IsTrue(_profile.HasChosenName);
        }

        [Test]
        public void ASaveFromBeforeTheQuestion_WithItsOwnName_CountsAsChosen()
        {
            _store.Put(ProfileService.SaveKey, new ProfileData { displayName = "Suman" });
            Assert.IsTrue(new ProfileService(_store, new NameFilter()).HasChosenName);

            _store.Put(ProfileService.SaveKey, new ProfileData { displayName = "Player" });
            Assert.IsFalse(new ProfileService(_store, new NameFilter()).HasChosenName, "still called Player: the question comes");
        }

        [Test]
        public void NameAndAvatar_SurviveARestart()
        {
            _profile.TrySetDisplayName("Asha");
            _profile.SetAvatar(3);

            var reloaded = new ProfileService(_store, new NameFilter());

            Assert.AreEqual("Asha", reloaded.DisplayName);
            Assert.AreEqual(3, reloaded.AvatarId);
            Assert.AreEqual(NameCheck.Unchanged, reloaded.CheckName("Asha"));
        }

        [Test]
        public void SetAvatar_SameValue_DoesNothing()
        {
            int changed = 0;
            _profile.Changed += () => changed++;

            _profile.SetAvatar(0);

            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [TestCase(-1)]
        [TestCase(5)]
        [TestCase(99)]
        public void SetAvatar_OutsideThePresets_Throws(int avatarId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _profile.SetAvatar(avatarId));
            Assert.AreEqual(0, _profile.AvatarId);
        }

        [Test]
        public void FileFromANewerVersion_IsIgnored_AndNeverOverwritten()
        {
            _store.Put(ProfileService.SaveKey, new ProfileData { schemaVersion = ProfileData.CurrentVersion + 1, displayName = "Suman", avatarId = 2 });
            var profile = new ProfileService(_store, new NameFilter());

            Assert.IsTrue(profile.IsReadOnly);
            Assert.AreEqual("Player", profile.DisplayName);

            Assert.AreEqual(NameCheck.Ok, profile.TrySetDisplayName("Bikash"));

            Assert.AreEqual("Bikash", profile.DisplayName);
            Assert.AreEqual(0, _store.SaveCount);
            Assert.AreEqual("Suman", _store.Peek<ProfileData>(ProfileService.SaveKey).displayName);
        }

        [Test]
        public void InvalidValuesOnDisk_FallBackToTheDefaults()
        {
            _store.Put(ProfileService.SaveKey, new ProfileData { displayName = "x!", avatarId = 9 });

            var profile = new ProfileService(_store, new NameFilter());

            Assert.AreEqual("Player", profile.DisplayName);
            Assert.AreEqual(0, profile.AvatarId);
        }

        [Test]
        public void Constructor_RequiresItsDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new ProfileService(null, new NameFilter()));
            Assert.Throws<ArgumentNullException>(() => new ProfileService(_store, null));
        }
    }
}
