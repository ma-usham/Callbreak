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

        /// <summary>
        /// True once the player has picked a name: answered the question at the start (ADR 022) or
        /// saved one on the Profile screen. A file that already held a name of its own counts too.
        /// </summary>
        bool HasChosenName { get; }

        /// <summary>
        /// The answer to the name question: like <see cref="TrySetDisplayName"/>, but keeping the
        /// current name (<see cref="NameCheck.Unchanged"/>) is an answer too. Either way the name
        /// counts as chosen and is saved; any other result changes nothing.
        /// </summary>
        NameCheck ChooseName(string candidate);

        void SetAvatar(int avatarId);
    }
}
