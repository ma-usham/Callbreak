using System;
using Callbreak.Application.Flow;
using Callbreak.Application.Rooms;
using Callbreak.Application.Updates;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// The menu's moments for a newer version on Google Play (the owner, 5 Oct 2026; ADR 036):
    /// whenever the main menu becomes the top screen (the Menu scene opening on it, a screen above it
    /// closing) and whenever the app comes back to the front while it is there, <see cref="ForcedUpdate"/>
    /// may ask the store. Only on the main menu with no room open, so never in a match, in matchmaking
    /// or in a room; the update itself waits while a rejoin may still come. The launch asks first,
    /// before the menu loads (GameEntryPoint).
    /// </summary>
    public sealed class ForcedUpdateMoments : IStartable, IDisposable
    {
        private readonly ScreenRouter _router;
        private readonly IAppFlow _flow;
        private readonly IRoomService _rooms;
        private readonly ForcedUpdate _update;

        private bool _listening;

        public ForcedUpdateMoments(ScreenRouter router, IAppFlow flow, IRoomService rooms, ForcedUpdate update)
        {
            _router = router;
            _flow = flow;
            _rooms = rooms;
            _update = update;
        }

        public void Start()
        {
            _router.Changed += OnScreenChanged;
            UnityEngine.Application.focusChanged += OnFocusChanged;
            _listening = true;
            Check();
        }

        public void Dispose()
        {
            // The Menu scene's scope goes with every match: nothing may keep listening.
            if (!_listening)
            {
                return;
            }

            _listening = false;
            _router.Changed -= OnScreenChanged;
            UnityEngine.Application.focusChanged -= OnFocusChanged;
        }

        private void OnScreenChanged(ScreenId id)
        {
            if (id == ScreenId.MainMenu)
            {
                Check();
            }
        }

        private void OnFocusChanged(bool focused)
        {
            if (focused)
            {
                Check();
            }
        }

        private void Check()
        {
            if (OnMainMenu())
            {
                _ = _update.CheckAsync(OnMainMenu);
            }
        }

        /// <summary>Where an update may take the screen: the main menu on top, with no room open.</summary>
        private bool OnMainMenu()
        {
            return _listening && _flow.State == AppState.Menu && _router.Current == ScreenId.MainMenu && _rooms.Current == null;
        }
    }
}
