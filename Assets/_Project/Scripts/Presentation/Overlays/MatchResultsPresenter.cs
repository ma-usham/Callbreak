using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Table;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Overlays
{
    /// <summary>
    /// GDD §11 screen 10, match results. Opens once MatchEnded has been played back and shows its
    /// standings: the headline for the local player's place ("You Win!", "Shared Victory!",
    /// "2nd Place"…), the trophy for a sole win, a crown on every winner's row, the local row in
    /// gold. The panel rises and the trophy drops in (GDD §14) unless Reduce motion is on, or the
    /// results were rebuilt after a reconnect or a restart: those are not celebrated again.
    /// Play Again starts what the mode calls for (offline: a new match at the same difficulty;
    /// private room: back to the lobby; Quick Match: search again); Main Menu leaves the match and
    /// goes home. It replaces the last round's results, and waits for a menu or the scorecard the
    /// player has open to close first. A negative total is burgundy, like every negative score
    /// (GDD §12). The music ducks under the victory sting (GDD §14). If the private room is gone
    /// by the time Play Again is pressed (the host left), the player is told so on the way to the
    /// main menu.
    /// </summary>
    public sealed class MatchResultsPresenter : IStartable, ITickable, IDisposable
    {
        /// <summary>How long the music stays ducked: the length of the victory sting (SFX_Victory_01, 3 s).</summary>
        public const double VictoryStingSeconds = 3.0;

        private readonly MatchResultsView _view;
        private readonly AvatarLibrary _avatars;
        private readonly PlayedTable _table;
        private readonly IMatchSession _session;
        private readonly ScreenRouter _router;
        private readonly ISettingsService _settings;
        private readonly ILocalization _localization;
        private readonly IAudioService _audio;
        private readonly IHaptics _haptics;
        private readonly IAppFlow _flow;
        private readonly IRoomService _rooms;
        private readonly IToast _toast;
        private readonly IClock _clock;

        private IReadOnlyList<Standing> _standings;
        private IRoomSession _room;
        private RoomError _roomClosed = RoomError.None;
        private double _duckUntil;
        private bool _ducked;
        private bool _started;
        private bool _wanted;
        private bool _shown;
        private bool _celebrate;
        private bool _leaving;

        public MatchResultsPresenter(
            MatchResultsView view,
            AvatarLibrary avatars,
            PlayedTable table,
            IMatchSession session,
            ScreenRouter router,
            ISettingsService settings,
            ILocalization localization,
            IAudioService audio,
            IHaptics haptics,
            IAppFlow flow,
            IRoomService rooms,
            IToast toast,
            IClock clock)
        {
            _view = view;
            _avatars = avatars;
            _table = table;
            _session = session;
            _router = router;
            _settings = settings;
            _localization = localization;
            _audio = audio;
            _haptics = haptics;
            _flow = flow;
            _rooms = rooms;
            _toast = toast;
            _clock = clock;
        }

        public void Start()
        {
            _started = true;
            _view.PlayAgainClicked += OnPlayAgain;
            _view.MainMenuClicked += OnMainMenu;
            _view.HomeClicked += OnMainMenu;
            _view.ScorecardClicked += OnScorecard;
            _view.ScoresClicked += OnScorecard;
            _view.SettingsClicked += OnSettings;
            _view.BackRequested += OnBack;
            _table.Played += OnPlayed;
            _table.Rebuilt += OnRebuilt;
            _session.SeatsChanged += OnTextChanged;
            _localization.LanguageChanged += OnTextChanged;
            _settings.Changed += RenderMotion;

            // A private room can close under the results (the host leaves): remember why.
            _room = _session.Mode == MatchMode.PrivateRoom && _rooms != null ? _rooms.Current : null;
            if (_room != null)
            {
                _room.Closed += OnRoomClosed;
            }
        }

        public void Dispose()
        {
            if (!_started)
            {
                return;
            }

            // After the scene's views are destroyed: only subscriptions are undone, and nothing throws.
            _started = false;
            try
            {
                _view.PlayAgainClicked -= OnPlayAgain;
                _view.MainMenuClicked -= OnMainMenu;
                _view.HomeClicked -= OnMainMenu;
                _view.ScorecardClicked -= OnScorecard;
                _view.ScoresClicked -= OnScorecard;
                _view.SettingsClicked -= OnSettings;
                _view.BackRequested -= OnBack;
                _table.Played -= OnPlayed;
                _table.Rebuilt -= OnRebuilt;
                _session.SeatsChanged -= OnTextChanged;
                _localization.LanguageChanged -= OnTextChanged;
                _settings.Changed -= RenderMotion;
                if (_room != null)
                {
                    _room.Closed -= OnRoomClosed;
                }

                // The music must not stay ducked for the scene that comes next.
                Unduck();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void Tick()
        {
            if (!_started)
            {
                return;
            }

            if (_ducked && _clock.Now >= _duckUntil)
            {
                Unduck();
            }

            if (!_wanted || _shown || !_router.Has(ScreenId.MatchResults))
            {
                return;
            }

            // Over the bare table, or in place of the last round's results; anything else the
            // player has open (menu, scorecard, settings) is left to be closed first.
            ScreenId top = _router.Current;
            if (top != ScreenId.Gameplay && top != ScreenId.RoundResults && top != ScreenId.None)
            {
                return;
            }

            if (top == ScreenId.RoundResults && _router.Has(ScreenId.Gameplay))
            {
                _router.Show(ScreenId.Gameplay);
            }

            RenderMotion();
            _router.Push(ScreenId.MatchResults);
            _shown = true;
            if (_celebrate && LocalStanding().Rank == 1)
            {
                // GDD §14: music ducks 3 dB under the victory sting, and comes back when it ends.
                _audio.Duck(true);
                _ducked = true;
                _duckUntil = _clock.Now + VictoryStingSeconds;
                _audio.Play(SoundCue.Victory);
                _haptics.Play(HapticPattern.Success);
            }
        }

        // Standings rebuilt after a reconnect or a restart are not celebrated again: the panel is
        // simply there and so is the trophy.
        private void RenderMotion() => _view.ReduceMotion = _settings.ReduceMotion || !_celebrate;

        private void Unduck()
        {
            if (_ducked)
            {
                _ducked = false;
                _audio.Duck(false);
            }
        }

        private void OnRoomClosed(RoomError reason)
        {
            _roomClosed = reason;
        }

        private void OnPlayed(MatchEvent e)
        {
            if (e is MatchEnded ended)
            {
                Want(ended.Standings, true);
            }
        }

        private void OnRebuilt()
        {
            TableState state = _table.State;
            if (state.Phase == TablePhase.MatchEnded && state.FinalStandings.Count == SeatExtensions.Count)
            {
                Want(state.FinalStandings, false);
            }
        }

        private void OnTextChanged()
        {
            if (_standings != null)
            {
                Fill();
            }
        }

        private void Want(IReadOnlyList<Standing> standings, bool celebrate)
        {
            _standings = standings;
            _wanted = true;
            _celebrate = celebrate && !_shown;
            Fill();
        }

        private Standing LocalStanding()
        {
            for (int i = 0; i < _standings.Count; i++)
            {
                if (_standings[i].Seat == _session.LocalSeat)
                {
                    return _standings[i];
                }
            }

            return _standings[0];
        }

        private void Fill()
        {
            Standing local = LocalStanding();
            _view.SetHeadline(_localization.Get(ResultsText.HeadlineKey(local)));
            _view.SetTrophyVisible(ResultsText.ShowsTrophy(local));
            _view.SetFooter(ResultsText.RoundsCompleted(_table.State.Scores.Rounds.Count, _localization));
            int highlight = -1;
            for (int row = 0; row < _standings.Count && row < MatchResultsView.RowCount; row++)
            {
                Standing standing = _standings[row];
                SessionSeat seat = _session.Seats[(int)standing.Seat];
                bool isLocal = standing.Seat == _session.LocalSeat;
                if (isLocal)
                {
                    highlight = row;
                }

                _view.SetRow(
                    row,
                    standing.Rank.ToString(CultureInfo.InvariantCulture),
                    isLocal ? _localization.Get(ResultsText.YouKey) : seat.Name,
                    ScoreTenths.Format(standing.TotalTenths),
                    standing.Rank == 1,
                    standing.TotalTenths < 0);
                Sprite avatar = _avatars != null ? _avatars.Get(seat.AvatarId) : null;
                if (avatar != null)
                {
                    _view.SetRowAvatar(row, avatar);
                }
            }

            _view.SetHighlightRow(highlight);
        }

        private void OnPlayAgain()
        {
            if (_leaving)
            {
                return;
            }

            _leaving = true;
            _audio.Play(SoundCue.UiTap);
            _ = PlayAgainAsync();
        }

        private void OnMainMenu()
        {
            if (_leaving)
            {
                return;
            }

            _leaving = true;
            _audio.Play(SoundCue.UiTap);
            _ = MainMenuAsync();
        }

        /// <summary>GDD screen 10 "Play Again": what each mode calls for.</summary>
        private async Task PlayAgainAsync()
        {
            try
            {
                switch (_session.Mode)
                {
                    case MatchMode.Offline:
                        // "A new match at the same difficulty": the one just played, which a practice
                        // or a resumed match did not take from the offline setup's remembered choice.
                        BotDifficulty difficulty = _session is OfflineMatchSession played ? played.Difficulty : _settings.Difficulty;
                        await _flow.StartMatchAsync(new MatchLaunchRequest(MatchMode.Offline, difficulty), CancellationToken.None);
                        break;

                    case MatchMode.PrivateRoom:
                        if (_rooms != null && _rooms.Current == null)
                        {
                            // The room closed under the results (the host left): there is no lobby
                            // to go back to. The player is told why this is the main menu (GDD §8).
                            RoomError why = _roomClosed == RoomError.None ? RoomError.HostLeft : _roomClosed;
                            _toast.Show(_localization.Get(RoomMessages.KeyFor(why)));
                            await _flow.GoToMenuAsync(MenuEntry.MainMenu, CancellationToken.None);
                            break;
                        }

                        // The room is a lobby again after the match: same friends, ready flags reset.
                        await _flow.GoToMenuAsync(MenuEntry.PrivateLobby, CancellationToken.None);
                        break;

                    default:
                        await _flow.GoToMenuAsync(MenuEntry.Matchmaking, CancellationToken.None);
                        break;
                }
            }
            catch (Exception e)
            {
                _leaving = false;
                Debug.LogException(e);
            }
        }

        private async Task MainMenuAsync()
        {
            try
            {
                if (_session.Status != SessionStatus.Ended)
                {
                    await _session.LeaveAsync(CancellationToken.None);
                }

                await _flow.GoToMenuAsync(MenuEntry.MainMenu, CancellationToken.None);
            }
            catch (Exception e)
            {
                _leaving = false;
                Debug.LogException(e);
            }
        }

        private void OnScorecard()
        {
            if (!_leaving && _router.Has(ScreenId.Scorecard))
            {
                _audio.Play(SoundCue.UiTap);
                _router.Push(ScreenId.Scorecard);
            }
        }

        private void OnSettings()
        {
            if (!_leaving && _router.Has(ScreenId.Settings))
            {
                _audio.Play(SoundCue.UiTap);
                _router.Push(ScreenId.Settings);
            }
        }

        /// <summary>Android back stays on the results: the match is over and the table behind them is done.</summary>
        private void OnBack()
        {
        }
    }
}
