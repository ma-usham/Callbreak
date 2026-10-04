using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Settings;
using Callbreak.Core.Rules;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 02: name the table, pick the turn clock, open the room. The name must be
    /// 3-20 characters and pass the name filter; until the player types one of their own it is
    /// "{Name}'s Table". The last clock choice is remembered, and Create shows a spinner and
    /// cannot be tapped twice. On success the lobby replaces this screen; a failure stays here
    /// with its message in the screen and the button reading Retry. The screen rises above the
    /// device's keyboard while the name field has it.
    /// </summary>
    public sealed class CreateRoomPresenter : IStartable, IDisposable
    {
        private readonly CreateRoomView _view;
        private readonly ScreenRouter _router;
        private readonly IRoomService _rooms;
        private readonly IProfileService _profile;
        private readonly ISettingsService _settings;
        private readonly ILocalization _localization;
        private readonly NameFilter _names;
        private readonly AvatarLibrary _avatars;
        private readonly IOnScreenKeyboard _keyboard;

        private string _roomName = string.Empty;
        private bool _nameEdited;
        private bool _failed;
        private bool _busy;
        private CancellationTokenSource _attempt;

        public CreateRoomPresenter(
            CreateRoomView view,
            ScreenRouter router,
            IRoomService rooms,
            IProfileService profile,
            ISettingsService settings,
            ILocalization localization,
            NameFilter names,
            AvatarLibrary avatars,
            IOnScreenKeyboard keyboard)
        {
            _view = view;
            _router = router;
            _rooms = rooms;
            _profile = profile;
            _settings = settings;
            _localization = localization;
            _names = names;
            _avatars = avatars;
            _keyboard = keyboard;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.BackRequested += OnBack;
            _view.ProfileClicked += OnProfile;
            _view.SettingsClicked += OnSettings;
            _view.ShopClicked += OnShop;
            _view.CreateClicked += OnCreate;
            _view.RoomNameChanged += OnRoomNameChanged;
            _view.TurnSecondsChanged += OnTurnSecondsChanged;
            _router.Changed += OnScreenChanged;
            _profile.Changed += RenderHeader;
            _localization.LanguageChanged += RenderFixedValues;
            _settings.Changed += RenderMotion;

            // The name field rises above the device's keyboard, so it is never under it.
            IOnScreenKeyboard keyboard = _keyboard;
            _view.KeyboardHeight = keyboard != null ? () => keyboard.Height : (Func<float>)null;
            RenderHeader();
            RenderFixedValues();
            RenderMotion();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.BackRequested -= OnBack;
            _view.ProfileClicked -= OnProfile;
            _view.SettingsClicked -= OnSettings;
            _view.ShopClicked -= OnShop;
            _view.CreateClicked -= OnCreate;
            _view.RoomNameChanged -= OnRoomNameChanged;
            _view.TurnSecondsChanged -= OnTurnSecondsChanged;
            _router.Changed -= OnScreenChanged;
            _profile.Changed -= RenderHeader;
            _localization.LanguageChanged -= RenderFixedValues;
            _settings.Changed -= RenderMotion;
            _attempt?.Cancel();
        }

        private void RenderMotion() => _view.ReduceMotion = _settings.ReduceMotion;

        private void OnScreenChanged(ScreenId id)
        {
            if (id != ScreenId.CreateRoom || _busy)
            {
                return;
            }

            // A fresh form every time the screen opens: "{Name}'s Table" for the player's current
            // name, until they type a name of their own, and the last used clock.
            if (!_nameEdited)
            {
                _roomName = RoomNames.DefaultFor(_profile.DisplayName, _localization.Get("ui.create_room.default_name"));
            }

            ClearFailure();
            _view.SetRoomName(_roomName);
            _view.SetTurnSeconds(_settings.TurnSeconds);
            _view.SetBusy(false);
            _view.SetCreateInteractable(IsValid(_roomName));
        }

        private void RenderHeader()
        {
            _view.SetPlayerName(_profile.DisplayName);
            _view.SetAvatar(_avatars.Get(_profile.AvatarId));
        }

        private void RenderFixedValues()
        {
            _view.SetPlayersValue(_localization.Get("ui.create_room.players_value"));
            _view.SetMatchLengthValue(_localization.Format("ui.create_room.match_length_value", RuleSet.NepaliStandard.Rounds));

            // A message in the old language would stand next to a button in the new one.
            ClearFailure();
        }

        /// <summary>GDD screen 02: a failed create is an "inline message with Retry; stays on this screen".</summary>
        private void ShowFailure(string message)
        {
            _failed = true;
            _view.ShowError(message);
            _view.SetCreateLabel(_localization.Get("ui.common.retry"));
        }

        private void ClearFailure()
        {
            if (!_failed)
            {
                return;
            }

            _failed = false;
            _view.ClearError();
            _view.SetCreateLabel(_localization.Get("ui.create_room.cta"));
        }

        /// <summary>Back while the room is being opened gives the attempt up (GDD §27: "Cancel available").</summary>
        private void OnBack()
        {
            _attempt?.Cancel();
            _router.Pop();
        }

        private void OnProfile() => Open(ScreenId.Profile);

        private void OnSettings() => Open(ScreenId.Settings);

        private void OnShop() => Open(ScreenId.Shop);

        private void Open(ScreenId screen)
        {
            if (!_busy)
            {
                _router.Push(screen);
            }
        }

        private void OnRoomNameChanged(string text)
        {
            _roomName = text ?? string.Empty;

            // An emptied field is no name of the player's own: the default returns with the screen.
            _nameEdited = _roomName.Trim().Length > 0;
            ClearFailure();
            _view.SetCreateInteractable(!_busy && IsValid(_roomName));
        }

        private void OnTurnSecondsChanged(int seconds) => _settings.SetTurnSeconds(seconds);

        private void OnCreate()
        {
            if (_busy || !IsValid(_roomName))
            {
                return;
            }

            _ = CreateAsync();
        }

        private async Task CreateAsync()
        {
            var attempt = new CancellationTokenSource();
            _attempt = attempt;
            _busy = true;
            ClearFailure();
            _view.SetBusy(true);
            _view.SetCreateInteractable(false);
            try
            {
                var settings = new RoomSettings(RoomNames.Normalize(_roomName), _settings.TurnSeconds);
                RoomResult result = await _rooms.CreateRoomAsync(settings, attempt.Token);
                bool walkedAway = attempt.IsCancellationRequested || _router.Current != ScreenId.CreateRoom;
                if (result.Succeeded && walkedAway)
                {
                    // Nobody is looking at the room that just opened: close it again.
                    _ = result.Session.LeaveAsync(CancellationToken.None);
                }
                else if (result.Succeeded)
                {
                    _roomName = string.Empty;
                    _nameEdited = false;
                    _router.Replace(ScreenId.PrivateLobby);
                }
                else if (!walkedAway)
                {
                    ShowFailure(_localization.Get(RoomMessages.KeyFor(result.Error)));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (_router.Current == ScreenId.CreateRoom)
                {
                    ShowFailure(_localization.Get("ui.toast.connect_failed"));
                }
            }
            finally
            {
                if (_attempt == attempt)
                {
                    _attempt = null;
                }

                attempt.Dispose();
                _busy = false;
                _view.SetBusy(false);
                _view.SetCreateInteractable(IsValid(_roomName));
            }
        }

        /// <summary>GDD: 3-20 characters, trimmed, and profanity-filtered (the host checks again).</summary>
        private bool IsValid(string name) => RoomNames.IsValid(name, _names);
    }
}
