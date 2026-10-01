using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// GDD §15 "First-time experience" in the menu: the first time the app opens, How to Play
    /// comes up by itself and, once it is closed, an easy practice match is offered. It only ever
    /// acts on the bare main menu, so an invite link or a rejoin goes first. The player's name is
    /// asked before all of it, by <see cref="NamePromptPresenter"/> (ADR 022).
    /// </summary>
    public sealed class FirstRunGuide : IStartable, IDisposable
    {
        private readonly ScreenRouter _router;
        private readonly MatchLauncher _launcher;
        private readonly IAppFlow _flow;
        private readonly ISettingsService _settings;
        private readonly IDialogs _dialogs;
        private readonly ILocalization _localization;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _primerOpen;
        private bool _disposed;

        public FirstRunGuide(
            ScreenRouter router,
            MatchLauncher launcher,
            IAppFlow flow,
            ISettingsService settings,
            IDialogs dialogs,
            ILocalization localization)
        {
            _router = router;
            _launcher = launcher;
            _flow = flow;
            _settings = settings;
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

            if (FirstRun.ShowsPrimer(_settings, _flow.PreviousState, _flow.PendingMenuEntry) && _router.Has(ScreenId.HowToPlay))
            {
                _primerOpen = true;
                _router.Push(ScreenId.HowToPlay);
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
            _lifetime.Cancel();
            _lifetime.Dispose();
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
