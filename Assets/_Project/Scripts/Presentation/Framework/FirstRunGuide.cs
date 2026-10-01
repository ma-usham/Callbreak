using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Screens;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// GDD §15 "First-time experience" in the menu: the first time the app opens, How to Play
    /// comes up by itself and, once it is closed, an easy practice match is offered. It only ever
    /// acts on the bare main menu, so an invite link or a rejoin goes first. The player's name is
    /// asked before all of it, by <see cref="NamePromptPresenter"/> (ADR 022). On that first launch
    /// the menu opens on How to Play's background and logo, without the rules, under the name
    /// panel; once the name is answered the rules appear, and the main menu comes after them.
    /// </summary>
    public sealed class FirstRunGuide : IStartable, IDisposable
    {
        private readonly ScreenRouter _router;
        private readonly MatchLauncher _launcher;
        private readonly IAppFlow _flow;
        private readonly ISettingsService _settings;
        private readonly IProfileService _profile;
        private readonly IDialogs _dialogs;
        private readonly ILocalization _localization;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _primerOpen;
        private bool _welcome;
        private bool _disposed;

        public FirstRunGuide(
            ScreenRouter router,
            MatchLauncher launcher,
            IAppFlow flow,
            ISettingsService settings,
            IProfileService profile,
            IDialogs dialogs,
            ILocalization localization)
        {
            _router = router;
            _launcher = launcher;
            _flow = flow;
            _settings = settings;
            _profile = profile;
            _dialogs = dialogs;
            _localization = localization;
        }

        public void Start()
        {
            _router.Changed += OnScreenChanged;
            if (_router.Current != ScreenId.MainMenu || _dialogs.IsOpen)
            {
                return;
            }

            bool opening = _flow.PreviousState == AppState.Boot && _flow.PendingMenuEntry == MenuEntry.MainMenu;
            if (opening && FirstRun.AsksName(_profile) && _router.Has(ScreenId.HowToPlay))
            {
                // The name panel is up: underneath it the app opens on How to Play's background and
                // logo, and the rules follow the answer.
                _welcome = true;
                _router.Get<HowToPlayView>(ScreenId.HowToPlay)?.SetRulesVisible(false);
                _router.ShowStack(new[] { ScreenId.MainMenu, ScreenId.HowToPlay });
                _profile.Changed += OnProfileChanged;
                return;
            }

            if (FirstRun.ShowsPrimer(_settings, _flow.PreviousState, _flow.PendingMenuEntry) && _router.Has(ScreenId.HowToPlay))
            {
                OpenPrimer();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _router.Changed -= OnScreenChanged;
            _profile.Changed -= OnProfileChanged;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private void OpenPrimer()
        {
            _primerOpen = true;
            _router.Push(ScreenId.HowToPlay);
        }

        /// <summary>The name is answered: the rules appear under the name panel as it fades; the main menu comes after them.</summary>
        private void OnProfileChanged()
        {
            if (!_welcome || FirstRun.AsksName(_profile))
            {
                return;
            }

            _welcome = false;
            _profile.Changed -= OnProfileChanged;
            if (_disposed)
            {
                return;
            }

            _router.Get<HowToPlayView>(ScreenId.HowToPlay)?.SetRulesVisible(true);
            if (_router.Current != ScreenId.HowToPlay)
            {
                // Something else took the screen meanwhile (an invite link): it leads on from there.
                return;
            }

            if (_settings.HowToPlaySeen)
            {
                _router.Pop();
            }
            else
            {
                _primerOpen = true;
            }
        }

        private void OnScreenChanged(ScreenId id)
        {
            // The primer marks itself seen when it is closed ("Got It" or back); until then the
            // player is still reading it, or looking at Settings from it.
            if (!_primerOpen || !_settings.HowToPlaySeen)
            {
                return;
            }

            _primerOpen = false;
            if (id == ScreenId.MainMenu && !_dialogs.IsOpen)
            {
                _ = OfferPracticeAsync();
            }
        }

        private async Task OfferPracticeAsync()
        {
            try
            {
                bool practice = await _dialogs.ConfirmAsync(
                    new DialogRequest(
                        _localization.Get("ui.dialog.practice.title"),
                        _localization.Get("ui.dialog.practice.body"),
                        _localization.Get("ui.dialog.practice.confirm"),
                        _localization.Get("ui.dialog.practice.cancel")),
                    _lifetime.Token);
                if (practice && !_disposed)
                {
                    _launcher.Launch(new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Easy));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

    }
}
