using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Sessions;

namespace Callbreak.Application.Flow
{
    /// <summary>The app's top-level states (GDD §21 FIG 21.2).</summary>
    public enum AppState
    {
        Boot,
        Menu,
        InMatch,
    }

    /// <summary>Where the Menu scene opens when the app returns to it (GDD §10: Play Again and Main Menu).</summary>
    public enum MenuEntry
    {
        MainMenu,
        PrivateLobby,
        Matchmaking,
        OfflineSetup,
    }

    /// <summary>
    /// Owns scene loads and what lives across them (GDD §21): the launch request the Table scene
    /// starts from, and the screen the Menu scene opens on when a match ends. Screens only raise
    /// intents; they never load scenes themselves.
    /// </summary>
    public interface IAppFlow
    {
        event Action<AppState> StateChanged;

        AppState State { get; }

        /// <summary>The request the Table scene is starting (or last started).</summary>
        MatchLaunchRequest CurrentLaunch { get; }

        /// <summary>The screen the Menu scene should show first.</summary>
        MenuEntry PendingMenuEntry { get; }

        /// <summary>Loads the Table scene for <paramref name="request"/>.</summary>
        Task StartMatchAsync(MatchLaunchRequest request, CancellationToken ct);

        /// <summary>Leaves the table (or boot) for the Menu scene, opening <paramref name="entry"/>.</summary>
        Task GoToMenuAsync(MenuEntry entry, CancellationToken ct);
    }
}
