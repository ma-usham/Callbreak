using System;

namespace Callbreak.Application.Recording
{
    /// <summary>
    /// Whether the law asks for the player's consent, and whether they have answered, as Google's
    /// User Messaging Platform (UMP) keeps it for the ads (its ConsentStatus, which outlives the
    /// session).
    /// </summary>
    public enum ConsentState
    {
        /// <summary>Not known yet: the consent information was never updated on this device (a first launch, offline).</summary>
        Unknown,

        /// <summary>No consent is needed where the player is (outside the EEA, the UK and Switzerland, as Google decides).</summary>
        NotRequired,

        /// <summary>Consent is needed and the player has not answered Google's consent form yet.</summary>
        Required,

        /// <summary>The player answered Google's consent form; what they chose is in <see cref="ConsentSignals.PurposeConsents"/>.</summary>
        Obtained,
    }

    /// <summary>
    /// The player's privacy choice as the consent platform stored it on the device: the UMP state, and
    /// the IAB TCF v2 signals Google's consent form writes where the GDPR applies
    /// (IABTCF_gdprApplies, IABTCF_PurposeConsents). Plain values, so the decision on them
    /// (<see cref="RecordingConsentPolicy"/>) is engine-free and tested.
    /// </summary>
    public sealed class ConsentSignals : IEquatable<ConsentSignals>
    {
        /// <summary>Nothing known: no state and no TCF signals.</summary>
        public static readonly ConsentSignals None = new ConsentSignals(ConsentState.Unknown, null, string.Empty);

        public ConsentSignals(ConsentState state, bool? gdprApplies, string purposeConsents)
        {
            State = state;
            GdprApplies = gdprApplies;
            PurposeConsents = purposeConsents ?? string.Empty;
        }

        public ConsentState State { get; }

        /// <summary>IABTCF_gdprApplies: true where the GDPR applies to the player; null when the form never wrote it.</summary>
        public bool? GdprApplies { get; }

        /// <summary>
        /// IABTCF_PurposeConsents: one character per TCF purpose, purpose 1 first, '1' where the player
        /// consented and '0' where not; empty when the form never wrote it.
        /// </summary>
        public string PurposeConsents { get; }

        public bool Equals(ConsentSignals other)
        {
            return other != null
                && State == other.State
                && GdprApplies == other.GdprApplies
                && string.Equals(PurposeConsents, other.PurposeConsents, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as ConsentSignals);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)State;
                hash = (hash * 397) ^ GdprApplies.GetHashCode();
                return (hash * 397) ^ PurposeConsents.GetHashCode();
            }
        }
    }

    /// <summary>
    /// The player's privacy choice (port), read by <see cref="SessionRecording"/> to decide whether
    /// the session may be recorded. On phones it is the choice made in Google's consent form for the
    /// ads (UMP): at launch the one stored from an earlier session, then whatever the form and Settings'
    /// "Privacy options" change. Everything happens on the main thread, <see cref="Changed"/> included.
    /// </summary>
    public interface IRecordingConsent
    {
        /// <summary>The choice as stored now; <see cref="ConsentSignals.None"/> when nothing is known.</summary>
        ConsentSignals Current { get; }

        /// <summary>The stored choice may have changed: Google's consent information was updated, or one of its forms closed.</summary>
        event Action Changed;
    }
}
