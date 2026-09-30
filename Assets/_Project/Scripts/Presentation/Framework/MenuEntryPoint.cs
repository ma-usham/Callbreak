using System;
using System.Threading;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;
using Callbreak.Application.Settings;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Starts the Menu scene (GDD §21): opens the screen <see cref="IAppFlow.PendingMenuEntry"/>
    /// asks for (Play Again returns to the lobby or the search, GDD §11 screen 10), starts the menu
    /// theme, keeps the router's Reduce motion in step with Settings, and routes Android back
    /// (Escape in the Input System) to an open dialog first, then to the top screen.
    /// </summary>
    public sealed class MenuEntryPoint : IStartable, ITickable, IDisposable
    {
        private readonly ScreenRouter _router;
        private readonly IAppFlow _flow;
        private readonly ISettingsService _settings;
        private readonly IAudioService _audio;
        private readonly IDialogs _dialogs;
        private readonly IRoomService _rooms;

        public MenuEntryPoint(ScreenRouter router, IAppFlow flow, ISettingsService settings, IAudioService audio, IDialogs dialogs, IRoomService rooms)
        {
            _router = router;
            _flow = flow;
            _settings = settings;
            _audio = audio;
            _dialogs = dialogs;
            _rooms = rooms;
        }

        public void Start()
        {
            _settings.Changed += ApplySettings;
            ApplySettings();

            // Coming back to anywhere but the lobby means the player is done with the room.
            IRoomSession room = _rooms.Current;
            if (room != null && _flow.PendingMenuEntry != MenuEntry.PrivateLobby)
            {
                _ = room.LeaveAsync(CancellationToken.None);
            }

            _router.ShowStack(StackFor(_flow.PendingMenuEntry));
            _audio.PlayMusic(MusicTrack.Menu);
        }

        public void Tick()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !_dialogs.HandleBack())
            {
                _router.Back();
            }
        }

        public void Dispose()
        {
            _settings.Changed -= ApplySettings;
        }

        private void ApplySettings() => _router.ReduceMotion = _settings.ReduceMotion;

        private ScreenId[] StackFor(MenuEntry entry)
        {
            switch (entry)
            {
                case MenuEntry.PrivateLobby when _router.Has(ScreenId.PrivateLobby) && _rooms.Current != null:
                    return new[] { ScreenId.MainMenu, ScreenId.PlayWithFriends, ScreenId.PrivateLobby };
                case MenuEntry.Matchmaking when _router.Has(ScreenId.Matchmaking):
                    return new[] { ScreenId.MainMenu, ScreenId.Matchmaking };
                case MenuEntry.OfflineSetup when _router.Has(ScreenId.OfflineSetup):
                    return new[] { ScreenId.MainMenu, ScreenId.OfflineSetup };
                default:
                    return new[] { ScreenId.MainMenu };
            }
        }
    }
}
