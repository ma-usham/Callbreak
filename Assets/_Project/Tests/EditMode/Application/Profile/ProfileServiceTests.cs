using System;
using Callbreak.Application.Profile;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §11 screen 14: the display name rules, the avatar presets and persistence; ADR 041's week between name changes.</summary>
    public sealed class ProfileServiceTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

        private InMemorySaveStore _store;
        private ProfileService _profile;
        private DateTime _now;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemorySaveStore();
            _now = Start;
            _profile = new ProfileService(_store, new NameFilter(), () => _now);
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

        // ------------------------------------------------------------------ a week between name changes (ADR 041)

        [Test]
        public void NoNameSavedYet_CanChangeAtOnce()
        {
            Assert.AreEqual(TimeSpan.Zero, _profile.NameChangeWait);
        }

        [Test]
        public void TrySetDisplayName_StartsAWeek_InWhichANewNameIsTooSoon()
        {
            Assert.AreEqual(NameCheck.Ok, _profile.TrySetDisplayName("Asha"));
            Assert.AreEqual(TimeSpan.FromDays(7), _profile.NameChangeWait);
            int changed = 0;
            _profile.Changed += () => changed++;
            int saves = _store.SaveCount;

            NameCheck result = _profile.TrySetDisplayName("Bikash");

            Assert.AreEqual(NameCheck.TooSoon, result);
            Assert.AreEqual("Asha", _profile.DisplayName);
            Assert.AreEqual(0, changed);
            Assert.AreEqual(saves, _store.SaveCount);
        }

        [Test]
        public void TheWeekCountsDown_AndANewNameIsSavedOnceItIsOver()
        {
            _profile.TrySetDisplayName("Asha");

            _now = Start + TimeSpan.FromDays(6) + TimeSpan.FromHours(23);
            Assert.AreEqual(TimeSpan.FromHours(1), _profile.NameChangeWait);
            Assert.AreEqual(NameCheck.TooSoon, _profile.TrySetDisplayName("Bikash"));

            _now = Start + TimeSpan.FromDays(7);
            Assert.AreEqual(TimeSpan.Zero, _profile.NameChangeWait);
            Assert.AreEqual(NameCheck.Ok, _profile.TrySetDisplayName("Bikash"));
            Assert.AreEqual("Bikash", _profile.DisplayName);
            Assert.AreEqual(TimeSpan.FromDays(7), _profile.NameChangeWait, "the new name starts its own week");
        }

        [Test]
        public void DuringTheWeek_ANameThatCannotBeUsedStillSaysWhy()
        {
            _profile.TrySetDisplayName("Asha");

            Assert.AreEqual(NameCheck.TooShort, _profile.TrySetDisplayName("Ra"));
            Assert.AreEqual(NameCheck.NotAllowed, _profile.TrySetDisplayName("B1tch"));
            Assert.AreEqual(NameCheck.Unchanged, _profile.TrySetDisplayName("Asha"));
        }

        [Test]
        public void TheNameAnsweredAtTheStart_StartsNoWait()
        {
            Assert.AreEqual(NameCheck.Ok, _profile.ChooseName("Asha"));

            Assert.AreEqual(TimeSpan.Zero, _profile.NameChangeWait);
            Assert.AreEqual(NameCheck.Ok, _profile.TrySetDisplayName("Bikash"), "a typo at the start can be put right at once");
        }

        [Test]
        public void TheQuestionAtTheStart_IsAnsweredEvenDuringTheWeek()
        {
            // A saved name the filter no longer allows falls back to "Player", and the question comes again.
            _profile.TrySetDisplayName("Asha");
            ProfileData data = _store.Peek<ProfileData>(ProfileService.SaveKey);
            data.displayName = "B1tch";
            _store.Put(ProfileService.SaveKey, data);
            var profile = new ProfileService(_store, new NameFilter(), () => _now);
            Assert.IsFalse(profile.HasChosenName);

            Assert.AreEqual(NameCheck.Ok, profile.ChooseName("Suman"));
            Assert.AreEqual("Suman", profile.DisplayName);
        }

        [Test]
        public void TheWeek_SurvivesARestart()
        {
            _profile.TrySetDisplayName("Asha");
            _now = Start + TimeSpan.FromDays(3);

            var reloaded = new ProfileService(_store, new NameFilter(), () => _now);

            Assert.AreEqual(TimeSpan.FromDays(4), reloaded.NameChangeWait);
            Assert.AreEqual(NameCheck.TooSoon, reloaded.TrySetDisplayName("Bikash"));
        }

        [Test]
        public void AClockPutBack_NeverMakesTheWaitLongerThanAWeek()
        {
            _profile.TrySetDisplayName("Asha");

            _now = Start - TimeSpan.FromDays(30);

            Assert.AreEqual(TimeSpan.FromDays(7), _profile.NameChangeWait);
        }

        [Test]
        public void ASaveFromBeforeTheWeek_CanChangeAtOnce()
        {
            _store.Put(ProfileService.SaveKey, new ProfileData { displayName = "Suman", nameChosen = true });
            var profile = new ProfileService(_store, new NameFilter(), () => _now);

            Assert.AreEqual(TimeSpan.Zero, profile.NameChangeWait);
            Assert.AreEqual(NameCheck.Ok, profile.TrySetDisplayName("Asha"));
        }

        [TestCase(-5L)]
        [TestCase(long.MaxValue)]
        public void AnImpossibleTimeOnDisk_IsForgotten(long changedAt)
        {
            _store.Put(ProfileService.SaveKey, new ProfileData { displayName = "Suman", nameChosen = true, nameChangedAt = changedAt });
            var profile = new ProfileService(_store, new NameFilter(), () => _now);

            Assert.AreEqual(TimeSpan.Zero, profile.NameChangeWait);
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
