using System;

namespace Callbreak.Application.Profile
{
    /// <summary>
    /// Who the player is at the table (GDD §11 screen 14, §29 profile.json): display name and avatar.
    /// Nothing personal beyond the display name is stored.
    /// </summary>
    public interface IProfileService
    {
        event Action Changed;

        /// <summary>"Player" until the player picks a name.</summary>
        string DisplayName { get; }

        /// <summary>Index into the avatar presets (0 = the default silhouette).</summary>
        int AvatarId { get; }

        int AvatarCount { get; }

        /// <summary>Validates without saving; trims first.</summary>
        NameCheck CheckName(string candidate);

        /// <summary>Validates, and saves when the result is <see cref="NameCheck.Ok"/>.</summary>
        NameCheck TrySetDisplayName(string candidate);

        void SetAvatar(int avatarId);
    }
}
