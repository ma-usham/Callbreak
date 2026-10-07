using System;
using System.Globalization;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Recording;
using Callbreak.Application.Settings;
using Callbreak.Core.Scoring;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 14: the player's name, avatar preset and play statistics. Save stays disabled
    /// until the name changes to something valid, an invalid name says why, and a valid new name
    /// says that it then stays 7 days (ADR 041); while it stays, the field takes no typing, its line
    /// says how long, and a tap on the field nudges that line. The pencil (or the avatar) opens the avatar picker: a tap on a portrait
    /// saves it at once, Done, a tap beside the panel or Android back closes it. The screen rises
    /// above the device's keyboard while the name field has it. While it is on top the session
    /// recording keeps no picture of the screen, as the field holds the player's name (ADR 044).
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
        private readonly ISettingsService _settings;
        private readonly IOnScreenKeyboard _keyboard;
        private readonly ISessionRecording _recording;

        private string _draft = string.Empty;
        private IDisposable _hiddenFromRecording;

        public ProfilePresenter(
            ProfileView view,
            ScreenRouter router,
            IProfileService profile,
            IStatsService stats,
            ILocalization localization,
            IToast toast,
            AvatarLibrary avatars,
            ISettingsService settings,
            IOnScreenKeyboard keyboard,
            ISessionRecording recording)
        {
            _view = view;
            _router = router;
            _profile = profile;
            _stats = stats;
            _localization = localization;
            _toast = toast;
            _avatars = avatars;
            _settings = settings;
            _keyboard = keyboard;
            _recording = recording;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.BackRequested += OnBackRequested;
            _view.EditAvatarClicked += OnEditAvatar;
            _view.AvatarPicked += OnAvatarPicked;
            _view.AvatarPickerCloseClicked += OnAvatarPickerClose;
            _view.LockedNameTapped += OnLockedNameTapped;
            _view.NameChanged += OnNameChanged;
            _view.SaveClicked += OnSave;
            _router.Changed += OnScreenChanged;
            _profile.Changed += RenderIdentity;
            _stats.Changed += RenderStats;
            _localization.LanguageChanged += RenderAll;
            _settings.Changed += RenderMotion;

            // The name field rises above the device's keyboard, so it is never under it.
            IOnScreenKeyboard keyboard = _keyboard;
            _view.KeyboardHeight = keyboard != null ? () => keyboard.Height : (Func<float>)null;
            RenderAll();
            RenderMotion();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.BackRequested -= OnBackRequested;
            _view.EditAvatarClicked -= OnEditAvatar;
            _view.AvatarPicked -= OnAvatarPicked;
            _view.AvatarPickerCloseClicked -= OnAvatarPickerClose;
            _view.LockedNameTapped -= OnLockedNameTapped;
            _view.NameChanged -= OnNameChanged;
            _view.SaveClicked -= OnSave;
            _router.Changed -= OnScreenChanged;
            _profile.Changed -= RenderIdentity;
            _stats.Changed -= RenderStats;
            _localization.LanguageChanged -= RenderAll;
            _settings.Changed -= RenderMotion;
            HideFromRecording(false);
        }

        private void RenderMotion() => _view.ReduceMotion = _settings.ReduceMotion;

        private void OnScreenChanged(ScreenId id)
        {
            HideFromRecording(id == ScreenId.Profile);
            if (id == ScreenId.Profile)
            {
                RenderAll();
            }
        }

        /// <summary>Keeps the screen out of the session recording while the Profile screen, with the player's name, is on top.</summary>
        private void HideFromRecording(bool hide)
        {
            if (hide)
            {
                _hiddenFromRecording = _hiddenFromRecording ?? _recording?.HideScreen();
                return;
            }

            _hiddenFromRecording?.Dispose();
            _hiddenFromRecording = null;
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
            // ADR 041: a saved name stays 7 days; until then the field takes no typing.
            TimeSpan wait = _profile.NameChangeWait;
            bool editable = wait <= TimeSpan.Zero;
            _view.SetNameEditable(editable);
            if (!editable)
            {
                _view.SetSaveInteractable(false);
                _view.SetNameMessage(NameWait.Text(wait, _localization), false);
                return;
            }

            NameCheck check = _profile.CheckName(_draft);
            _view.SetSaveInteractable(check == NameCheck.Ok);
            switch (check)
            {
                case NameCheck.Ok:
                    // Said before Save: the name then stays a week.
                    _view.SetNameMessage(_localization.Get(NameWait.WeekNoteKey), false);
                    break;
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

        /// <summary>Android back closes the avatar picker first, then the screen.</summary>
        private void OnBackRequested()
        {
            if (_view.IsAvatarPickerOpen)
            {
                _view.HideAvatarPicker();
                return;
            }

            _router.Pop();
        }

        /// <summary>
        /// A tap on the locked field: the line under it says how long the name stays and is nudged.
        /// A week that ended while the screen was open unlocks the field instead.
        /// </summary>
        private void OnLockedNameTapped()
        {
            RenderNameState();
            if (!_view.NameEditable)
            {
                _view.NudgeNameMessage();
            }
        }

        private void OnNameChanged(string text)
        {
            _draft = text ?? string.Empty;
            RenderNameState();
        }

        private void OnSave()
        {
            if (_profile.TrySetDisplayName(_draft) != NameCheck.Ok)
            {
                // Not saved (TooSoon included: the line then says how long the name stays).
                RenderNameState();
                return;
            }

            _draft = _profile.DisplayName;
            _view.SetNameField(_draft);
            RenderNameState();
            _toast.Show(_localization.Get("ui.toast.profile_saved"));
        }

        /// <summary>The pencil or the avatar: the picker opens with the player's portrait ringed.</summary>
        private void OnEditAvatar() => _view.ShowAvatarPicker(_avatars.Get, _profile.AvatarId);

        /// <summary>A portrait is saved at the tap; the big avatar follows (Changed) and the ring moves.</summary>
        private void OnAvatarPicked(int index)
        {
            if (index < 0 || index >= Math.Min(_profile.AvatarCount, _avatars.Count))
            {
                return;
            }

            _profile.SetAvatar(index);
            _view.ChooseAvatar(_profile.AvatarId);
        }

        private void OnAvatarPickerClose() => _view.HideAvatarPicker();
    }
}
