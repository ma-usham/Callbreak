using Callbreak.Application.Recording;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// When the player's privacy choice allows the session to be recorded (ADR 044): never before the
    /// choice is known or while Google's consent form is unanswered; where the GDPR applies, only with
    /// consent to TCF purposes 1, 8 and 10 (the form's "Consent" grants every purpose, "Do not
    /// consent" none); elsewhere, once Google says no consent is needed or the player answered a
    /// non-GDPR message. Signals that disagree or are cut short never allow it.
    /// </summary>
    public sealed class RecordingConsentPolicyTests
    {
        // IABTCF_PurposeConsents as Google's form writes it: one character per purpose, purpose 1 first.
        private const string AllEleven = "11111111111";
        private const string NoneOfEleven = "00000000000";

        // Purposes 1, 8 and 10 only.
        private const string JustTheNeededOnes = "10000001010";

        [Test]
        public void ThePurposes_AreStorage_ContentMeasurement_AndImprovingTheService()
        {
            CollectionAssert.AreEqual(new[] { 1, 8, 10 }, RecordingConsentPolicy.Purposes);
        }

        [Test]
        public void NothingKnown_RecordsNothing()
        {
            Assert.IsFalse(RecordingConsentPolicy.Allows(null));
            Assert.IsFalse(RecordingConsentPolicy.Allows(ConsentSignals.None));
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Unknown, false, string.Empty)));
        }

        [Test]
        public void AnUnansweredConsentForm_RecordsNothing_EvenWithOldConsentsStored()
        {
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Required, true, AllEleven)));
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Required, null, string.Empty)));
        }

        [Test]
        public void WhereNoConsentIsNeeded_ItRecords()
        {
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.NotRequired, null, string.Empty)));
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.NotRequired, false, string.Empty)));
        }

        [Test]
        public void UnderTheGdpr_ConsentToEverything_Records()
        {
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, true, AllEleven)));
        }

        [Test]
        public void UnderTheGdpr_DoNotConsent_RecordsNothing()
        {
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, true, NoneOfEleven)));
        }

        [Test]
        public void UnderTheGdpr_JustTheNeededPurposes_Record()
        {
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, true, JustTheNeededOnes)));
        }

        [TestCase(1)]
        [TestCase(8)]
        [TestCase(10)]
        public void UnderTheGdpr_AnyNeededPurposeRefused_RecordsNothing(int refused)
        {
            char[] purposes = AllEleven.ToCharArray();
            purposes[refused - 1] = '0';

            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, true, new string(purposes))));
        }

        [Test]
        public void UnderTheGdpr_PurposesCutShort_OrMissing_RecordNothing()
        {
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, true, "111111111")), "no character for purpose 10");
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, true, string.Empty)));
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, true, null)));
        }

        [Test]
        public void GdprFlagMissing_ButPurposesStored_IsJudgedAsGdpr()
        {
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, null, NoneOfEleven)));
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, null, AllEleven)));
        }

        [Test]
        public void ANonGdprMessageAnswered_Records()
        {
            // E.g. the US states' message, which is about selling data for ads and writes no TCF purposes.
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, false, string.Empty)));
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, null, string.Empty)));
        }

        [Test]
        public void NoConsentNeeded_YetTheGdprSaidToApply_FollowsThePurposes()
        {
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.NotRequired, true, string.Empty)));
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.NotRequired, true, AllEleven)));
        }

        [TestCase("1", 1, true)]
        [TestCase("0", 1, false)]
        [TestCase("1", 2, false)]
        [TestCase("1", 0, false)]
        [TestCase("1", -3, false)]
        [TestCase("", 1, false)]
        [TestCase("0000000100", 8, true)]
        [TestCase("1111111x11", 8, false)]
        [TestCase("1111111x11", 9, true)]
        public void Granted_ReadsOneCharacterPerPurpose(string purposeConsents, int purpose, bool granted)
        {
            Assert.AreEqual(granted, RecordingConsentPolicy.Granted(purposeConsents, purpose));
        }

        [Test]
        public void Granted_WithNoString_IsFalse()
        {
            Assert.IsFalse(RecordingConsentPolicy.Granted(null, 1));
        }

        [Test]
        public void Signals_AreEqual_ByValue()
        {
            var a = new ConsentSignals(ConsentState.Obtained, true, AllEleven);
            var b = new ConsentSignals(ConsentState.Obtained, true, AllEleven);

            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a.Equals(new ConsentSignals(ConsentState.Obtained, false, AllEleven)));
            Assert.IsFalse(a.Equals(new ConsentSignals(ConsentState.Obtained, true, NoneOfEleven)));
            Assert.IsFalse(a.Equals(new ConsentSignals(ConsentState.NotRequired, true, AllEleven)));
            Assert.IsFalse(a.Equals(null));
            Assert.AreEqual(string.Empty, new ConsentSignals(ConsentState.Unknown, null, null).PurposeConsents, "no string reads as none");
        }
    }
}
