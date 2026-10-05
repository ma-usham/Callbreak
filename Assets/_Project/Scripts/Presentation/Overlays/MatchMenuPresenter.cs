using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Overlays
{
    /// <summary>
    /// GDD §11 screen 13, the match menu, opened by Home and by Android back on the table. Resume
    /// and the close × go back to the table; Scorecard opens screen 16 over the menu; the Sound
    /// switch mutes music and effects together; Leave Match asks first (the shared dialog), then
    /// leaves the session and returns to the main menu. Offline the match is paused while the menu
    /// is open (the table presenter pauses it for every screen that covers the table) and leaving
    /// keeps the match for later; online the game goes on, so the menu says "Online game" and its
    /// first button reads "Back to Game" (<see cref="MatchMenuText"/>), and leaving counts as a
    /// loss. The host of a private room is told that leaving closes the room for everybody (GDD §8:
    /// the host's device is the room). Once the player has left (a private room too), a full-screen
    /// ad shows before the main menu when one is due (the owner, 5 Oct 2026; <see cref="IBetweenMatchAds"/>).
    /// </summary>
    public sealed class MatchMenuPresenter : IStartable, IDisposable
    {
        public const string ModeOfflineKey = MatchMenuText.ModeOfflineKey;
        public const string ModeOnlineKey = MatchMenuText.ModeOnlineKey;
        public const string LeaveTitleKey = "ui.dialog.leave_match.title";
        public const string LeaveBodyKey = MatchMenuText.LeaveBodyKey;
        public const string LeaveBodyOfflineKey = MatchMenuText.LeaveBodyOfflineKey;
        public const string LeaveBodyHostKey = MatchMenuText.LeaveBodyHostKey;
        public const string LeaveConfirmKey = "ui.dialog.leave_match.confirm";
        public const string LeaveCancelKey = "ui.dialog.leave_match.cancel";

        private readonly MatchMenuView _view;
        private readonly ScreenRouter _router;
        private readonly IMatchSession _session;
        private readonly ISettingsService _settings;
        private readonly ILocalization _localization;
        private readonly IDialogs _dialogs;
        private readonly IAppFlow _flow;
        private readonly IAudioService _audio;
        private readonly IRoomService _rooms;
        private readonly IBetweenMatchAds _ads;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _started;
        private bool _leaving;

        /// <param name="ads">The full-screen ad after a match left early; none when null.</param>
        public MatchMenuPresenter(
            MatchMenuView view,
            ScreenRouter router,
            IMatchSession session,
            ISettingsService settings,
            ILocalization localization,
            IDialogs dialogs,
            IAppFlow flow,
            IAudioService audio,
            IRoomService rooms,
            IBetweenMatchAds ads = null)
        {
            _view = view;
            _router = router;
            _session = session;
            _settings = settings;
            _localization = localization;
            _dialogs = dialogs;
            _flow = flow;
            _audio = audio;
            _rooms = rooms;
            _ads = ads;
        }

        private bool IsOffline => _session.Mode == MatchMode.Offline;

        /// <summary>
        /// This device hosts the private room the match is played in (one room at a time), and its
        /// leaving closes the room: not when the next host in line would take the match over (ADR 030).
        /// </summary>
        private bool LeavingClosesTheRoom
        {
            get
            {
                IRoomSession room = _session.Mode == MatchMode.PrivateRoom && _rooms != null ? _rooms.Current : null;
                return room != null && room.IsHost && !(room is IHandOverRoom handOver && handOver.HandsOverWhenLeaving);
            }
        }

        public void Start()
        {
            _started = true;
            _view.CloseClicked += OnResume;
            _view.ResumeClicked += OnResume;
            _view.ScorecardClicked += OnScorecard;
            _view.LeaveClicked += OnLeave;
            _view.SoundChanged += OnSoundChanged;
            _settings.Changed += Render;
            _localization.LanguageChanged += Render;
            Render();
        }

        /// <summary>Runs after the scene's views are destroyed: it only undoes subscriptions, and never throws.</summary>
        public void Dispose()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            try
            {
                _view.CloseClicked -= OnResume;
                _view.ResumeClicked -= OnResume;
                _view.ScorecardClicked -= OnScorecard;
                _view.LeaveClicked -= OnLeave;
                _view.SoundChanged -= OnSoundChanged;
                _settings.Changed -= Render;
                _localization.LanguageChanged -= Render;
                _lifetime.Cancel();
                _lifetime.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void Render()
        {
            // An online game cannot pause: its tag just says it is online, and its first button goes back to the game.
            _view.SetMode(_localization.Get(MatchMenuText.ModeKeyFor(_session.Mode)));
            _view.SetResumeLabel(_localization.Get(MatchMenuText.ResumeKeyFor(_session.Mode)));
            _view.SetPausedNoteVisible(IsOffline);
            _view.SetSound(_settings.Music || _settings.SoundEffects);
            _view.ReduceMotion = _settings.ReduceMotion;
        }

        private void OnResume()
        {
            _audio.Play(SoundCue.UiBack);
            if (_router.Current == ScreenId.MatchMenu)
            {
                _router.Pop();
            }
        }

        private void OnScorecard()
        {
            if (_router.Has(ScreenId.Scorecard))
            {
                _audio.Play(SoundCue.UiTap);
                _router.Push(ScreenId.Scorecard);
            }
        }

        private void OnSoundChanged(bool on)
        {
            // One switch for everything that makes a sound; Settings has them separately.
            _settings.SetMusic(on);
            _settings.SetSoundEffects(on);
            _audio.Play(SoundCue.Toggle);
        }

        private void OnLeave()
        {
            if (_leaving)
            {
                return;
            }

            _leaving = true;
            _ = LeaveAsync();
        }

        private async Task LeaveAsync()
        {
            try
            {
                var request = new DialogRequest(
                    _localization.Get(LeaveTitleKey),
                    _localization.Get(MatchMenuText.LeaveBodyKeyFor(_session.Mode, LeavingClosesTheRoom)),
                    _localization.Get(LeaveConfirmKey),
                    _localization.Get(LeaveCancelKey));
                bool confirmed = await _dialogs.ConfirmAsync(request, _lifetime.Token);
                if (!confirmed || !_started)
                {
                    _leaving = false;
                    return;
                }

                await _session.LeaveAsync(CancellationToken.None);
                await LeaveRoomAsync();
                if (_ads != null)
                {
                    await _ads.ShowIfDueAsync(AfterMatch.LeftEarly, CancellationToken.None);
                }

                await _flow.GoToMenuAsync(MenuEntry.MainMenu, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                _leaving = false;
            }
            catch (Exception e)
            {
                _leaving = false;
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// A private room is left before the ad, so the others see at once that this player has gone;
        /// the menu, which would otherwise leave it as it opens, then finds no room to leave.
        /// </summary>
        private async Task LeaveRoomAsync()
        {
            IRoomSession room = _session.Mode == MatchMode.PrivateRoom && _rooms != null ? _rooms.Current : null;
            if (room == null)
            {
                return;
            }

            try
            {
                await room.LeaveAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                // The room counts itself closed before it reports a fault: the player has left all the same.
                Debug.LogException(e);
            }
        }
    }
}
