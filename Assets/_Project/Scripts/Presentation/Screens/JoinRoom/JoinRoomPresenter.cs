using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 03: join a friend's table with its 6-character code. Join is enabled only
    /// for a complete valid code, Paste finds the code in whatever the clipboard holds (the bare
    /// code in any case, with spaces or dashes, or the whole invite message), and every error
    /// shows its own message while the boxes shake. The screen rises above the device's keyboard
    /// while the boxes have it.
    /// </summary>
    public sealed class JoinRoomPresenter : IStartable, IDisposable
    {
        private readonly JoinRoomView _view;
        private readonly ScreenRouter _router;
        private readonly IRoomService _rooms;
        private readonly IProfileService _profile;
        private readonly IClipboard _clipboard;
        private readonly ILocalization _localization;
        private readonly IDeepLinks _deepLinks;
        private readonly ISettingsService _settings;
        private readonly AvatarLibrary _avatars;
        private readonly IOnScreenKeyboard _keyboard;

        private string _code = string.Empty;
        private bool _busy;
        private CancellationTokenSource _attempt;

        public JoinRoomPresenter(
            JoinRoomView view,
            ScreenRouter router,
            IRoomService rooms,
            IProfileService profile,
            IClipboard clipboard,
            ILocalization localization,
            IDeepLinks deepLinks,
            ISettingsService settings,
            AvatarLibrary avatars,
            IOnScreenKeyboard keyboard)
        {
            _view = view;
            _router = router;
            _rooms = rooms;
            _profile = profile;
            _clipboard = clipboard;
            _localization = localization;
            _deepLinks = deepLinks;
            _settings = settings;
            _avatars = avatars;
            _keyboard = keyboard;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.BackRequested += OnBack;
            _view.ProfileClicked += OnProfile;
            _view.SettingsClicked += OnSettings;
            _view.CodeChanged += OnCodeChanged;
            _view.PasteClicked += OnPaste;
            _view.JoinClicked += OnJoin;
            _router.Changed += OnScreenChanged;
            _profile.Changed += RenderHeader;
            _settings.Changed += RenderMotion;

            // The code boxes rise above the device's keyboard, so they are never under it.
            IOnScreenKeyboard keyboard = _keyboard;
            _view.KeyboardHeight = keyboard != null ? () => keyboard.Height : (Func<float>)null;
            RenderHeader();
            RenderMotion();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.BackRequested -= OnBack;
            _view.ProfileClicked -= OnProfile;
            _view.SettingsClicked -= OnSettings;
            _view.CodeChanged -= OnCodeChanged;
            _view.PasteClicked -= OnPaste;
            _view.JoinClicked -= OnJoin;
            _router.Changed -= OnScreenChanged;
            _profile.Changed -= RenderHeader;
            _settings.Changed -= RenderMotion;
            _attempt?.Cancel();
        }

        private void OnScreenChanged(ScreenId id)
        {
            if (id != ScreenId.JoinRoom || _busy)
            {
                return;
            }

            // An invite link fills the boxes (GDD §11 screen 03).
            string invited = _deepLinks.TakeJoinCode();
            if (!string.IsNullOrEmpty(invited))
            {
                _code = invited;
                _view.ClearError();
            }

            _view.SetCode(_code);
            _view.SetBusy(false);
            _view.SetJoinInteractable(RoomCode.IsValid(_code));
        }

        private void RenderHeader()
        {
            _view.SetPlayerName(_profile.DisplayName);
            _view.SetAvatar(_avatars.Get(_profile.AvatarId));
        }

        private void RenderMotion() => _view.SetReduceMotion(_settings.ReduceMotion);

        /// <summary>Back while joining gives the attempt up (GDD §27: "Cancel available").</summary>
        private void OnBack()
        {
            _attempt?.Cancel();
            _router.Pop();
        }

        private void OnProfile() => Open(ScreenId.Profile);

        private void OnSettings() => Open(ScreenId.Settings);

        private void Open(ScreenId screen)
        {
            if (!_busy)
            {
                _router.Push(screen);
            }
        }

        private void OnCodeChanged(string code)
        {
            _code = RoomCode.Normalize(code);
            _view.ClearError();
            _view.SetJoinInteractable(!_busy && RoomCode.IsValid(_code));
        }

        private void OnPaste()
        {
            if (_busy)
            {
                return;
            }

            // A clipboard with no code in it leaves what the player typed alone.
            string pasted = InviteLink.FromPastedText(_clipboard.GetText());
            if (pasted.Length == 0)
            {
                return;
            }

            _code = pasted;
            _view.SetCode(_code);
            _view.ClearError();
            _view.SetJoinInteractable(RoomCode.IsValid(_code));
        }

        private void OnJoin()
        {
            if (_busy || !RoomCode.IsValid(_code))
            {
                return;
            }

            _ = JoinAsync();
        }

        private async Task JoinAsync()
        {
            var attempt = new CancellationTokenSource();
            _attempt = attempt;
            _busy = true;
            _view.SetBusy(true);
            _view.SetJoinInteractable(false);
            _view.ClearError();
            try
            {
                RoomResult result = await _rooms.JoinRoomAsync(_code, attempt.Token);
                bool walkedAway = attempt.IsCancellationRequested || _router.Current != ScreenId.JoinRoom;
                if (result.Succeeded && walkedAway)
                {
                    // Nobody is looking at the seat that was just taken: give it back.
                    _ = result.Session.LeaveAsync(CancellationToken.None);
                }
                else if (result.Succeeded)
                {
                    _code = string.Empty;
                    _router.Replace(ScreenId.PrivateLobby);
                }
                else if (!walkedAway)
                {
                    _view.ShowError(_localization.Get(RoomMessages.KeyFor(result.Error)));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _view.ShowError(_localization.Get("ui.toast.connect_failed"));
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
                _view.SetJoinInteractable(RoomCode.IsValid(_code));
            }
        }
    }
}
