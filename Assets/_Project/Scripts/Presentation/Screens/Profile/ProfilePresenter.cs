using System;
using System.Globalization;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Core.Scoring;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 14: the player's name, avatar preset and play statistics. Save stays disabled
    /// until the name changes to something valid, an invalid name says why, and the pencil steps
    /// through the avatar presets.
    /// </summary>
    public sealed class ProfilePresenter : IStartable, IDisposable
    {
        private readonly ProfileView _view;
        private readonly ScreenRouter _router;
        private readonly IProfileService _profile;
        private readonly IStatsService _stats;
        private readonly ILocalization _localization;
        private readonly IToast _toast;
        private readonly AvatarLibrary _avatars;

        private string _draft = string.Empty;

        public ProfilePresenter(
            ProfileView view,
            ScreenRouter router,
            IProfileService profile,
            IStatsService stats,
            ILocalization localization,
            IToast toast,
            AvatarLibrary avatars)
        {
            _view = view;
            _router = router;
            _profile = profile;
            _stats = stats;
            _localization = localization;
            _toast = toast;
            _avatars = avatars;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.EditAvatarClicked += OnEditAvatar;
            _view.NameChanged += OnNameChanged;
            _view.SaveClicked += OnSave;
            _router.Changed += OnScreenChanged;
            _profile.Changed += RenderIdentity;
            _stats.Changed += RenderStats;
            _localization.LanguageChanged += RenderAll;
            RenderAll();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.EditAvatarClicked -= OnEditAvatar;
            _view.NameChanged -= OnNameChanged;
            _view.SaveClicked -= OnSave;
            _router.Changed -= OnScreenChanged;
            _profile.Changed -= RenderIdentity;
            _stats.Changed -= RenderStats;
            _localization.LanguageChanged -= RenderAll;
        }

        private void OnScreenChanged(ScreenId id)
        {
            if (id == ScreenId.Profile)
            {
                RenderAll();
            }
        }

        private void RenderAll()
        {
            _draft = _profile.DisplayName;
            _view.SetNameField(_draft);
            RenderIdentity();
            RenderStats();
            RenderNameState();
        }

        private void RenderIdentity()
        {
            _view.SetName(_profile.DisplayName);
            _view.SetAvatar(_avatars.Get(_profile.AvatarId));
        }

        /// <summary>GDD: win = 1st place (including shared), win rate = wins / completed games, best = highest final total.</summary>
        private void RenderStats()
        {
            PlayerStats stats = _stats.Current;
            string winRate = Mathf.RoundToInt(stats.WinRate * 100f).ToString(CultureInfo.InvariantCulture) + "%";
            string best = stats.hasBest ? ScoreTenths.Format(stats.bestTotalTenths) : _localization.Get("ui.profile.no_best");
            _view.SetStats(
                stats.gamesPlayed.ToString(CultureInfo.InvariantCulture),
                stats.wins.ToString(CultureInfo.InvariantCulture),
                winRate,
                best);
        }

        private void RenderNameState()
        {
            NameCheck check = _profile.CheckName(_draft);
            _view.SetSaveInteractable(check == NameCheck.Ok);
            switch (check)
            {
                case NameCheck.TooShort:
                    _view.SetNameMessage(_localization.Get("ui.error.name_too_short"), true);
                    break;
                case NameCheck.TooLong:
                    _view.SetNameMessage(_localization.Get("ui.error.name_too_long"), true);
                    break;
                case NameCheck.InvalidCharacters:
                    _view.SetNameMessage(_localization.Get("ui.error.name_invalid"), true);
                    break;
                case NameCheck.NotAllowed:
                    _view.SetNameMessage(_localization.Get("ui.error.name_not_allowed"), true);
                    break;
                default:
                    _view.SetNameMessage(_localization.Get("ui.profile.name_note"), false);
                    break;
            }
        }

        private void OnBack() => _router.Pop();

        private void OnNameChanged(string text)
        {
            _draft = text ?? string.Empty;
            RenderNameState();
        }

        private void OnSave()
        {
            if (_profile.TrySetDisplayName(_draft) != NameCheck.Ok)
            {
                RenderNameState();
                return;
            }

            _draft = _profile.DisplayName;
            _view.SetNameField(_draft);
            RenderNameState();
            _toast.Show(_localization.Get("ui.toast.profile_saved"));
        }

        /// <summary>The preset picker: each tap shows the next preset, saved at once.</summary>
        private void OnEditAvatar()
        {
            int count = Math.Max(1, Math.Min(_profile.AvatarCount, _avatars.Count));
            _profile.SetAvatar((_profile.AvatarId + 1) % count);
        }
    }
}
