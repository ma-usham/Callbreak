using Callbreak.Application.Recording;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// Where the session may be recorded (ADR 044; the owner, 8 Oct 2026: "dont record in europe that
    /// requires some conditions, record in else regions"): never before Google's consent information
    /// is known; never where the GDPR applies (the EEA, the UK and Switzerland, where Google's consent
    /// form is required), whatever the player answers there; elsewhere, once Google says no consent is
    /// needed or the player answered a non-GDPR message. Signals that say the GDPR applies, even
    /// alongside others that don't, never allow it.
    /// </summary>
    public sealed class RecordingConsentPolicyTests
    {
        // IABTCF_PurposeConsents as Google's form writes it: one character per purpose, purpose 1 first.
        private const string AllEleven = "11111111111";
        private const string NoneOfEleven = "00000000000";

        // Purposes 1, 8 and 10 only: what recording would have needed under the earlier rule.
        private const string JustStorageMeasurementAndImprovement = "10000001010";

        [Test]
        public void NothingKnown_RecordsNothing()
        {
            Assert.IsFalse(RecordingConsentPolicy.Allows(null));
            Assert.IsFalse(RecordingConsentPolicy.Allows(ConsentSignals.None));
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Unknown, false, string.Empty)));
        }

        [Test]
        public void WhereConsentIsRequired_ItRecordsNothing_BeforeTheFormIsAnswered()
        {
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Required, true, AllEleven)));
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Required, null, string.Empty)));
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Required, false, string.Empty)));
        }

        [Test]
        public void WhereNoConsentIsNeeded_ItRecords()
        {
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.NotRequired, null, string.Empty)));
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.NotRequired, false, string.Empty)));
        }

        [TestCase(AllEleven)]
        [TestCase(JustStorageMeasurementAndImprovement)]
        [TestCase(NoneOfEleven)]
        [TestCase("")]
        [TestCase(null)]
        public void UnderTheGdpr_ItRecordsNothing_WhateverThePlayerAnswered(string purposeConsents)
        {
            // The owner: not in Europe, where it comes with conditions, even when the player consents.
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, true, purposeConsents)));
        }

        [Test]
        public void GdprFlagMissing_ButPurposesStored_IsJudgedAsGdpr()
        {
            // Google's form writes TCF purposes only where the GDPR applies.
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, null, NoneOfEleven)));
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, null, AllEleven)));
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.NotRequired, null, AllEleven)));
        }

        [Test]
        public void ANonGdprMessageAnswered_Records()
        {
            // E.g. the US states' message, which is about selling data for ads and writes no TCF purposes.
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, false, string.Empty)));
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.Obtained, null, string.Empty)));
        }

        [Test]
        public void NoConsentNeeded_YetTheGdprSaidToApply_RecordsNothing()
        {
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.NotRequired, true, string.Empty)));
            Assert.IsFalse(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.NotRequired, true, AllEleven)));
        }

        [Test]
        public void TheGdprSaidNotToApply_OutweighsStoredPurposes()
        {
            Assert.IsTrue(RecordingConsentPolicy.Allows(new ConsentSignals(ConsentState.NotRequired, false, AllEleven)));
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
