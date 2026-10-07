using System;
using System.Collections.Generic;

namespace Callbreak.Application.Recording
{
    /// <summary>
    /// When the player's privacy choice allows the session to be recorded (ADR 044). The game asks no
    /// question of its own: it reuses the answer the player gave in Google's consent form for the ads.
    /// <list type="bullet">
    /// <item>Nothing known yet, or a consent form still unanswered: no recording.</item>
    /// <item>Where the GDPR applies: only when the player consented to every purpose in
    /// <see cref="Purposes"/> (TCF v2 purposes; the form's "Consent" button grants them all, "Do not
    /// consent" none, and "Manage options" lets the player pick).</item>
    /// <item>Elsewhere (no consent needed, or a non-GDPR message such as the US states' one, which is
    /// about selling data for ads): recording is allowed, and the privacy policy says so.</item>
    /// </list>
    /// The GDPR is taken to apply when the form says so (IABTCF_gdprApplies), or, where it never
    /// wrote that, when it wrote purpose consents at all. The owner can change the purposes here, in
    /// one place.
    /// </summary>
    public static class RecordingConsentPolicy
    {
        /// <summary>
        /// The TCF purposes the player must consent to where the GDPR applies: 1 "Store and/or access
        /// information on a device" (the SDK keeps a visitor id), 8 "Measure content performance" and
        /// 10 "Develop and improve services" (what watching sessions is for).
        /// </summary>
        public static IReadOnlyList<int> Purposes { get; } = Array.AsReadOnly(new[] { 1, 8, 10 });

        /// <summary>True when <paramref name="signals"/> allow the session to be recorded.</summary>
        public static bool Allows(ConsentSignals signals)
        {
            if (signals == null || signals.State == ConsentState.Unknown || signals.State == ConsentState.Required)
            {
                return false;
            }

            bool gdpr = signals.GdprApplies ?? signals.PurposeConsents.Length > 0;
            if (!gdpr)
            {
                return true;
            }

            foreach (int purpose in Purposes)
            {
                if (!Granted(signals.PurposeConsents, purpose))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when <paramref name="purposeConsents"/> (IABTCF_PurposeConsents) grants TCF purpose <paramref name="purpose"/> (1-based).</summary>
        public static bool Granted(string purposeConsents, int purpose)
        {
            return purpose >= 1
                && purposeConsents != null
                && purposeConsents.Length >= purpose
                && purposeConsents[purpose - 1] == '1';
        }
    }
}
