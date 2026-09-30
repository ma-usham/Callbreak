using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 06: pick the bots' difficulty and start an offline match. The last choice is
    /// remembered, nothing here needs a connection, and starting over an unfinished match asks
    /// first ("This replaces your unfinished match.").
    /// </summary>
    public sealed class OfflineSetupPresenter : IStartable, IDisposable
    {
        private readonly OfflineSetupView _view;
        private readonly ScreenRouter _router;
        private readonly IProfileService _profile;
        private readonly ISettingsService _settings;
        private readonly IOfflineMatchStore _store;
        private readonly IDialogs _dialogs;
        private readonly ILocalization _localization;
        private readonly MatchLauncher _launcher;

        private bool _asking;

        public OfflineSetupPresenter(
            OfflineSetupView view,
            ScreenRouter router,
            IProfileService profile,
            ISettingsService settings,
            IOfflineMatchStore store,
            IDialogs dialogs,
            ILocalization localization,
            MatchLauncher launcher)
        {
            _view = view;
            _router = router;
            _profile = profile;
            _settings = settings;
            _store = store;
            _dialogs = dialogs;
            _localization = localization;
            _launcher = launcher;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.ProfileClicked += OnProfile;
            _view.SettingsClicked += OnSettings;
            _view.DifficultyChanged += OnDifficultyChanged;
            _view.StartClicked += OnStart;
            _profile.Changed += Render;
            _settings.Changed += Render;
            Render();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.ProfileClicked -= OnProfile;
            _view.SettingsClicked -= OnSettings;
            _view.DifficultyChanged -= OnDifficultyChanged;
            _view.StartClicked -= OnStart;
            _profile.Changed -= Render;
            _settings.Changed -= Render;
        }

        private void Render()
        {
            _view.SetPlayerName(_profile.DisplayName);
            _view.SetDifficulty((int)_settings.Difficulty);
        }

        private void OnBack() => _router.Pop();

        private void OnProfile() => _router.Push(ScreenId.Profile);

        private void OnSettings() => _router.Push(ScreenId.Settings);

        private void OnDifficultyChanged(int difficulty) => _settings.SetDifficulty((BotDifficulty)difficulty);

        private void OnStart()
        {
            if (_asking)
            {
                return;
            }

            if (_store.HasSave)
            {
                _ = ConfirmAndStartAsync();
                return;
            }

            Launch();
        }

        private async Task ConfirmAndStartAsync()
        {
            _asking = true;
            try
            {
                bool replace = await _dialogs.ConfirmAsync(
                    new DialogRequest(
                        _localization.Get("ui.dialog.replace_match.title"),
                        _localization.Get("ui.dialog.replace_match.body"),
                        _localization.Get("ui.dialog.replace_match.confirm"),
                        _localization.Get("ui.dialog.replace_match.cancel")),
                    CancellationToken.None);
                if (replace)
                {
                    Launch();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _asking = false;
            }
        }

        private void Launch() => _launcher.Launch(new MatchLaunchRequest(MatchMode.Offline, _settings.Difficulty));
    }
}
