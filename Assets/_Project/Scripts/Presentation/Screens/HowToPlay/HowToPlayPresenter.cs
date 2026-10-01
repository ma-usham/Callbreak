using System;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 12: the rules primer. It opens from Settings, and once, on its own, the first
    /// time a player picks a way to play: then "Got It" carries on to that mode, and going back
    /// returns to the menu. Either way the primer counts as seen. Until it is seen it has the
    /// first-time look (ADR 022): dimmed behind the rules, with no back or settings button.
    /// </summary>
    public sealed class HowToPlayPresenter : IStartable, IDisposable
    {
        private readonly HowToPlayView _view;
        private readonly ScreenRouter _router;
        private readonly ISettingsService _settings;
        private readonly MatchLauncher _launcher;

        public HowToPlayPresenter(HowToPlayView view, ScreenRouter router, ISettingsService settings, MatchLauncher launcher)
        {
            _view = view;
            _router = router;
            _settings = settings;
            _launcher = launcher;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.BackRequested += OnBack;
            _view.SettingsClicked += OnSettings;
            _view.GotItClicked += OnGotIt;
            _router.Changed += OnScreenChanged;
            OnScreenChanged(_router.Current);
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.BackRequested -= OnBack;
            _view.SettingsClicked -= OnSettings;
            _view.GotItClicked -= OnGotIt;
            _router.Changed -= OnScreenChanged;
        }

        private void OnScreenChanged(ScreenId id)
        {
            if (id == ScreenId.HowToPlay)
            {
                _view.ReduceMotion = _settings.ReduceMotion;
                _view.SetFirstTime(!_settings.HowToPlaySeen);
            }
        }

        private void OnGotIt()
        {
            _settings.MarkHowToPlaySeen();
            if (!_launcher.ContinuePending())
            {
                _router.Pop();
            }
        }

        private void OnBack()
        {
            _settings.MarkHowToPlaySeen();
            _launcher.CancelPending();
            _router.Pop();
        }

        /// <summary>
        /// The gear leads to Settings, unless that is where the player came from. While the primer
        /// is shown for the first time (at the first launch or on the way into a match) it stays.
        /// </summary>
        private void OnSettings()
        {
            if (_launcher.HasPending || !_settings.HowToPlaySeen)
            {
                return;
            }

            if (!_router.Pop())
            {
                _router.Push(ScreenId.Settings);
            }
        }
    }
}
