using System;
using System.Collections.Generic;
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
    /// (Escape in the Input System) to what sits above the menu first (the name question), then to
    /// an open dialog, then to the top screen.
    /// </summary>
    public sealed class MenuEntryPoint : IStartable, ITickable, IDisposable
    {
        private readonly ScreenRouter _router;
        private readonly IAppFlow _flow;
        private readonly ISettingsService _settings;
        private readonly IAudioService _audio;
        private readonly IDialogs _dialogs;
        private readonly IRoomService _rooms;
        private readonly IReadOnlyList<IBackHandler> _above;

        public MenuEntryPoint(
            ScreenRouter router,
            IAppFlow flow,
            ISettingsService settings,
            IAudioService audio,
            IDialogs dialogs,
            IRoomService rooms,
            IReadOnlyList<IBackHandler> above)
        {
            _router = router;
            _flow = flow;
            _settings = settings;
            _audio = audio;
            _dialogs = dialogs;
            _rooms = rooms;
            _above = above ?? Array.Empty<IBackHandler>();
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
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !AboveHandlesBack() && !_dialogs.HandleBack() && _router.Back())
            {
                // The back button on screen has its own cue; the device's back key gets the same one.
                _audio.Play(SoundCue.UiBack);
            }
        }

        private bool AboveHandlesBack()
        {
            for (int i = 0; i < _above.Count; i++)
            {
                if (_above[i].HandleBack())
                {
                    return true;
                }
            }

            return false;
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
                    // The search is reached through Play Online, so its Cancel leads back there.
                    return _router.Has(ScreenId.PlayOnline)
                        ? new[] { ScreenId.MainMenu, ScreenId.PlayOnline, ScreenId.Matchmaking }
                        : new[] { ScreenId.MainMenu, ScreenId.Matchmaking };
                case MenuEntry.OfflineSetup when _router.Has(ScreenId.OfflineSetup):
                    return new[] { ScreenId.MainMenu, ScreenId.OfflineSetup };
                default:
                    return new[] { ScreenId.MainMenu };
            }
        }
    }
}
