using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 02: name the table, pick the turn clock, open the room. The name must be
    /// 3-20 characters and pass the name filter, the last clock choice is remembered, and Create
    /// shows a spinner and cannot be tapped twice. On success the lobby replaces this screen.
    /// </summary>
    public sealed class CreateRoomPresenter : IStartable, IDisposable
    {
        private const int MinNameLength = 3;
        private const int MaxNameLength = 20;

        private readonly CreateRoomView _view;
        private readonly ScreenRouter _router;
        private readonly IRoomService _rooms;
        private readonly IProfileService _profile;
        private readonly ISettingsService _settings;
        private readonly ILocalization _localization;
        private readonly IToast _toast;
        private readonly NameFilter _names;

        private string _roomName = string.Empty;
        private bool _busy;
        private CancellationTokenSource _attempt;

        public CreateRoomPresenter(
            CreateRoomView view,
            ScreenRouter router,
            IRoomService rooms,
            IProfileService profile,
            ISettingsService settings,
            ILocalization localization,
            IToast toast,
            NameFilter names)
        {
            _view = view;
            _router = router;
            _rooms = rooms;
            _profile = profile;
            _settings = settings;
            _localization = localization;
            _toast = toast;
            _names = names;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.ProfileClicked += OnProfile;
            _view.SettingsClicked += OnSettings;
            _view.ShopClicked += OnShop;
            _view.CreateClicked += OnCreate;
            _view.RoomNameChanged += OnRoomNameChanged;
            _view.TurnSecondsChanged += OnTurnSecondsChanged;
            _router.Changed += OnScreenChanged;
            _profile.Changed += RenderHeader;
            _localization.LanguageChanged += RenderFixedValues;
            RenderHeader();
            RenderFixedValues();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.ProfileClicked -= OnProfile;
            _view.SettingsClicked -= OnSettings;
            _view.ShopClicked -= OnShop;
            _view.CreateClicked -= OnCreate;
            _view.RoomNameChanged -= OnRoomNameChanged;
            _view.TurnSecondsChanged -= OnTurnSecondsChanged;
            _router.Changed -= OnScreenChanged;
            _profile.Changed -= RenderHeader;
            _localization.LanguageChanged -= RenderFixedValues;
            _attempt?.Cancel();
        }

        private void OnScreenChanged(ScreenId id)
        {
            if (id != ScreenId.CreateRoom || _busy)
            {
                return;
            }

            // A fresh form every time the screen opens: "{Name}'s Table" and the last used clock.
            if (string.IsNullOrEmpty(_roomName))
            {
                _roomName = _localization.Format("ui.create_room.default_name", _profile.DisplayName);
                if (_roomName.Length > MaxNameLength)
                {
                    _roomName = _roomName.Substring(0, MaxNameLength).Trim();
                }
            }

            _view.SetRoomName(_roomName);
            _view.SetTurnSeconds(_settings.TurnSeconds);
            _view.SetBusy(false);
            _view.SetCreateInteractable(IsValid(_roomName));
        }

        private void RenderHeader() => _view.SetPlayerName(_profile.DisplayName);

        private void RenderFixedValues()
        {
            _view.SetPlayersValue(_localization.Get("ui.create_room.players_value"));
            _view.SetMatchLengthValue(_localization.Format("ui.create_room.match_length_value", 5));
        }

        /// <summary>Back while the room is being opened gives the attempt up (GDD §27: "Cancel available").</summary>
        private void OnBack()
        {
            _attempt?.Cancel();
            _router.Pop();
        }

        private void OnProfile() => Open(ScreenId.Profile);

        private void OnSettings() => Open(ScreenId.Settings);

        private void OnShop() => Open(ScreenId.CardCollection);

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
            _view.SetBusy(true);
            _view.SetCreateInteractable(false);
            try
            {
                var settings = new RoomSettings(ProfileService.NormalizeName(_roomName), _settings.TurnSeconds);
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
                    _router.Replace(ScreenId.PrivateLobby);
                }
                else if (!walkedAway)
                {
                    _toast.Show(_localization.Get(RoomMessages.KeyFor(result.Error)));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _toast.Show(_localization.Get("ui.toast.connect_failed"));
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
        private bool IsValid(string name)
        {
            string trimmed = ProfileService.NormalizeName(name);
            return trimmed.Length >= MinNameLength && trimmed.Length <= MaxNameLength && _names.IsAllowed(trimmed);
        }
    }
}
