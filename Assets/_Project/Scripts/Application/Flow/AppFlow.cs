using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;

namespace Callbreak.Application.Flow
{
    /// <summary>
    /// The app's scene-level flow (GDD §21 FIG 21.2): Boot → Menu ⇄ InMatch. It remembers the
    /// launch request the Table scene builds its session from, and which screen the Menu scene
    /// opens on when a match ends (Play Again in a private room returns to the lobby, GDD §11
    /// screen 10).
    /// </summary>
    public sealed class AppFlow : IAppFlow
    {
        private readonly ISceneLoader _scenes;

        public AppFlow(ISceneLoader scenes)
        {
            _scenes = scenes;
        }

        public event Action<AppState> StateChanged;

        public AppState State { get; private set; } = AppState.Boot;

        public MatchLaunchRequest CurrentLaunch { get; private set; }

        public MenuEntry PendingMenuEntry { get; private set; } = MenuEntry.MainMenu;

        public async Task StartMatchAsync(MatchLaunchRequest request, CancellationToken ct)
        {
            CurrentLaunch = request ?? throw new ArgumentNullException(nameof(request));
            SetState(AppState.InMatch);
            await _scenes.LoadAsync(GameScene.Table, ct);
        }

        public async Task GoToMenuAsync(MenuEntry entry, CancellationToken ct)
        {
            PendingMenuEntry = entry;
            SetState(AppState.Menu);
            await _scenes.LoadAsync(GameScene.Menu, ct);
        }

        private void SetState(AppState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
