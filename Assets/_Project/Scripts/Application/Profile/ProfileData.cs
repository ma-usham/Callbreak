using System;

namespace Callbreak.Application.Profile
{
    /// <summary>
    /// profile.json (GDD §29): the display name and avatar preset, nothing else personal. Plain
    /// serializable data read and written by <see cref="ProfileService"/>.
    /// </summary>
    [Serializable]
    public sealed class ProfileData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;
        public string displayName = ProfileService.DefaultDisplayName;
        public int avatarId;

        /// <summary>The player has answered the name question at the start (ADR 022), even with "Player".</summary>
        public bool nameChosen;

        /// <summary>
        /// When a name was last saved on the Profile screen, in Unix seconds (UTC); 0 = never. The
        /// next change waits 7 days from it (ADR 041).
        /// </summary>
        public long nameChangedAt;
    }
}
