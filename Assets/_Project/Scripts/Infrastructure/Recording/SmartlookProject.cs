namespace Callbreak.Infrastructure.Recording
{
    /// <summary>
    /// The owner's Smartlook project (ADR 044), kept here and nowhere else, as <c>AdMobIds</c> keeps
    /// the AdMob ids. The project key is a client key: it ships inside the app, as every Smartlook
    /// mobile key does, and only lets an app send recordings to the project. It is found in the
    /// Smartlook dashboard under Project settings, Recording, Project key.
    /// <para>
    /// Empty means no recording: phone builds then register the stand-in and log that the key is
    /// missing. The key itself is never logged.
    /// </para>
    /// </summary>
    public static class SmartlookProject
    {
        /// <summary>The Smartlook project key (40 characters); empty until the owner puts the Callbreak project's key here.</summary>
        public const string ProjectKey = "";
    }
}
