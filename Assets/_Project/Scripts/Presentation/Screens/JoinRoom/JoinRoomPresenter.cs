using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 03: join a friend's table with its 6-character code. Join is enabled only
    /// for a complete valid code, Paste takes the clipboard (spaces, dashes and case do not
    /// matter), and every error shows its own message while the boxes shake.
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
            IDeepLinks deepLinks)
        {
            _view = view;
            _router = router;
            _rooms = rooms;
            _profile = profile;
            _clipboard = clipboard;
            _localization = localization;
            _deepLinks = deepLinks;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.ProfileClicked += OnProfile;
            _view.SettingsClicked += OnSettings;
            _view.CodeChanged += OnCodeChanged;
            _view.PasteClicked += OnPaste;
            _view.JoinClicked += OnJoin;
            _router.Changed += OnScreenChanged;
            _profile.Changed += RenderHeader;
            RenderHeader();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.ProfileClicked -= OnProfile;
            _view.SettingsClicked -= OnSettings;
            _view.CodeChanged -= OnCodeChanged;
            _view.PasteClicked -= OnPaste;
            _view.JoinClicked -= OnJoin;
            _router.Changed -= OnScreenChanged;
            _profile.Changed -= RenderHeader;
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

        private void RenderHeader() => _view.SetPlayerName(_profile.DisplayName);

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

            _code = RoomCode.Normalize(_clipboard.GetText());
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
