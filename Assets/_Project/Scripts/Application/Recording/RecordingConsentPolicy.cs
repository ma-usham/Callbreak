namespace Callbreak.Application.Recording
{
    /// <summary>
    /// Where the session may be recorded (ADR 044; the owner, 8 Oct 2026: "dont record in europe that
    /// requires some conditions, record in else regions"). The game asks no question of its own: it
    /// reads where the player is from Google's consent information for the ads.
    /// <list type="bullet">
    /// <item>Nothing known yet: no recording.</item>
    /// <item>Where the GDPR applies (the EEA, the UK and Switzerland, where Google's consent form is
    /// required): never, whatever the player answers in the form.</item>
    /// <item>Elsewhere (no consent needed, or a non-GDPR message such as the US states' one, which is
    /// about selling data for ads): recording is allowed, and the privacy policy says so.</item>
    /// </list>
    /// The GDPR is taken to apply while Google says consent is required, when the form says so
    /// (IABTCF_gdprApplies), or, where it never wrote that, when it wrote purpose consents at all
    /// (Google's form writes them only where the GDPR applies).
    /// </summary>
    public static class RecordingConsentPolicy
    {
        /// <summary>True when <paramref name="signals"/> allow the session to be recorded.</summary>
        public static bool Allows(ConsentSignals signals)
        {
            if (signals == null || signals.State == ConsentState.Unknown || signals.State == ConsentState.Required)
            {
                return false;
            }

            bool gdpr = signals.GdprApplies ?? signals.PurposeConsents.Length > 0;
            return !gdpr;
        }
    }
}
