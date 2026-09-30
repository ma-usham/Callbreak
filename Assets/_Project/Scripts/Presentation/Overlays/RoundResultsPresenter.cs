using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
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
    /// GDD §11 screen 09, round results. Opens once the round's RoundScored event has been played
    /// back (after the last trick was swept) and shows exactly what that event says: bid, won, the
    /// round's score and the running total per seat, the local player's row in gold, negatives with
    /// a true minus sign. The panel rises, rows reveal top to bottom 60 ms apart and the round
    /// scores count up (GDD §14); with Reduce motion, or for a summary rebuilt after a reconnect or
    /// a restart, everything is simply there. Next Round tells the session this seat is ready;
    /// after the last round the button reads "See Final Results"; online it also shows the host's
    /// countdown, after the last round as after any other. It waits for a menu or the scorecard to
    /// close before it opens, and the next round (or the final standings) takes it away again.
    /// </summary>
    public sealed class RoundResultsPresenter : IStartable, ITickable, IDisposable
    {
        public const string RoundTitleKey = "ui.round_results.round_title";
        public const string NextKey = ResultsText.NextKey;
        public const string NextCountdownKey = ResultsText.NextCountdownKey;
        public const string SeeFinalKey = ResultsText.SeeFinalKey;
        public const string SeeFinalCountdownKey = ResultsText.SeeFinalCountdownKey;
        public const string FooterNextKey = "ui.round_results.footer_next";

        private const double RowStagger = 0.06;
        private const double CountUpSeconds = 0.4;

        private readonly RoundResultsView _view;
        private readonly AvatarLibrary _avatars;
        private readonly PlayedTable _table;
        private readonly IMatchSession _session;
        private readonly ScreenRouter _router;
        private readonly ISettingsService _settings;
        private readonly ILocalization _localization;
        private readonly IAudioService _audio;
        private readonly IClock _clock;
        private readonly Seat[] _rows = new Seat[SeatExtensions.Count];
        private readonly int[] _shownDelta = new int[SeatExtensions.Count];
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private RoundResult _result;
        private double _revealStart;
        private bool _wanted;
        private bool _shown;
        private bool _animate;
        private bool _revealing;
        private bool _readySent;
        private int _countdown = -1;
        private bool _started;

        public RoundResultsPresenter(
            RoundResultsView view,
            AvatarLibrary avatars,
            PlayedTable table,
            IMatchSession session,
            ScreenRouter router,
            ISettingsService settings,
            ILocalization localization,
            IAudioService audio,
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
            _clock = clock;
        }

        private bool IsLastRound => _result != null && _result.Round >= _session.Rules.Rounds;

        public void Start()
        {
            _started = true;
            _view.NextClicked += OnNext;
            _view.ScorecardClicked += OnScorecard;
            _view.SettingsClicked += OnSettings;
            _view.BackClicked += OnBack;
            _view.BackRequested += OnBack;
            _table.Played += OnPlayed;
            _table.Rebuilt += OnRebuilt;
            _session.SeatsChanged += OnSeatsChanged;
            _localization.LanguageChanged += OnLanguageChanged;
            _settings.Changed += RenderMotion;
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
                _view.NextClicked -= OnNext;
                _view.ScorecardClicked -= OnScorecard;
                _view.SettingsClicked -= OnSettings;
                _view.BackClicked -= OnBack;
                _view.BackRequested -= OnBack;
                _table.Played -= OnPlayed;
                _table.Rebuilt -= OnRebuilt;
                _session.SeatsChanged -= OnSeatsChanged;
                _localization.LanguageChanged -= OnLanguageChanged;
                _settings.Changed -= RenderMotion;
                _lifetime.Cancel();
                _lifetime.Dispose();
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

            ScreenId top = _router.Current;
            if (!_wanted)
            {
                // The next round, or the final standings, has taken over.
                if (top == ScreenId.RoundResults && _router.Has(ScreenId.Gameplay))
                {
                    _router.Show(ScreenId.Gameplay);
                }

                return;
            }

            if (!_shown)
            {
                // Over the bare table only: a menu or the scorecard the player has open stays open.
                if ((top == ScreenId.Gameplay || top == ScreenId.None) && _router.Has(ScreenId.RoundResults))
                {
                    RenderMotion();
                    _router.Push(ScreenId.RoundResults);
                    BeginReveal();
                }
                else if (top == ScreenId.RoundResults)
                {
                    BeginReveal();
                }
            }

            TickReveal();
            TickCountdown();
        }

        private void OnPlayed(MatchEvent e)
        {
            switch (e)
            {
                case RoundScored scored:
                    Want(scored.Result, !_settings.ReduceMotion);
                    break;

                case ReadyConfirmed ready when ready.Seat == _session.LocalSeat && _wanted:
                    RenderNext();
                    break;

                case MatchStarted _:
                case RoundStarted _:
                case MatchEnded _:
                    _wanted = false;
                    _revealing = false;
                    break;
            }
        }

        private void OnRebuilt()
        {
            TableState state = _table.State;
            if (state.Phase == TablePhase.RoundEnded && state.LastRoundResult != null)
            {
                Want(state.LastRoundResult, false);
            }
            else
            {
                _wanted = false;
                _revealing = false;
            }
        }

        private void OnSeatsChanged()
        {
            if (_wanted && !_revealing)
            {
                Fill(false);
            }
        }

        private void OnLanguageChanged()
        {
            if (_wanted)
            {
                _revealing = false;
                Fill(false);
            }
        }

        /// <summary>The round summary is what the table should show now; it opens at the next tick that finds the table bare.</summary>
        private void Want(RoundResult result, bool animate)
        {
            _result = result;
            _wanted = true;
            _shown = false;
            _animate = animate;
            _revealing = false;
            _readySent = false;
            _countdown = -1;
            Fill(animate);
        }

        // A summary rebuilt after a reconnect or a restart does not rise either: it is simply there.
        private void RenderMotion() => _view.ReduceMotion = _settings.ReduceMotion || !_animate;

        private void BeginReveal()
        {
            _shown = true;
            _revealing = _animate;
            _revealStart = _clock.Now;
        }

        private void Fill(bool hidden)
        {
            // The button first: whatever else this screen shows, the player must be able to go on.
            RenderNext();
            ResultsText.RoundRowOrder(_session.LocalSeat, _rows);
            _view.SetHighlightRow(0);
            for (int row = 0; row < _rows.Length; row++)
            {
                Sprite avatar = _avatars != null ? _avatars.Get(_session.Seats[(int)_rows[row]].AvatarId) : null;
                if (avatar != null)
                {
                    _view.SetRowAvatar(row, avatar);
                }

                _shownDelta[row] = hidden ? 0 : _result[_rows[row]].DeltaTenths;
                SetRow(row, _shownDelta[row]);
                _view.SetRowVisible(row, !hidden);
            }

            _view.SetRoundTitle(_localization.Format(RoundTitleKey, _result.Round, _session.Rules.Rounds));
            _view.SetFooter(IsLastRound ? string.Empty : _localization.Format(FooterNextKey, _result.Round + 1));
        }

        private void SetRow(int row, int shownDeltaTenths)
        {
            Seat seat = _rows[row];
            SeatRoundResult line = _result[seat];
            string name = seat == _session.LocalSeat ? _localization.Get(ResultsText.YouKey) : _session.Seats[(int)seat].Name;
            _view.SetRow(
                row,
                name,
                line.Call.ToString(CultureInfo.InvariantCulture),
                line.Won.ToString(CultureInfo.InvariantCulture),
                ScoreTenths.FormatSigned(shownDeltaTenths),
                ScoreTenths.Format(line.TotalTenths),
                line.DeltaTenths < 0,
                line.TotalTenths < 0);
        }

        /// <summary>Rows appear top to bottom 60 ms apart and each round score counts up to its value.</summary>
        private void TickReveal()
        {
            if (!_revealing)
            {
                return;
            }

            double elapsed = _clock.Now - _revealStart;
            bool done = true;
            for (int row = 0; row < _rows.Length; row++)
            {
                double since = elapsed - (row * RowStagger);
                if (since < 0)
                {
                    done = false;
                    continue;
                }

                _view.SetRowVisible(row, true);
                int target = _result[_rows[row]].DeltaTenths;
                double t = Math.Min(1.0, since / CountUpSeconds);
                int shown = t >= 1.0 ? target : (int)Math.Round(target * (1.0 - ((1.0 - t) * (1.0 - t))));
                if (shown != _shownDelta[row])
                {
                    _shownDelta[row] = shown;
                    SetRow(row, shown);
                }

                done &= t >= 1.0;
            }

            _revealing = !done;
        }

        /// <summary>
        /// Online the host counts the summary down (GDD screen 09: 10 s), the last round's too: when
        /// it ends the final results open. Offline there is no clock.
        /// </summary>
        private void TickCountdown()
        {
            int seconds = -1;
            TurnTimerState timer = _session.Timer;
            if (timer.IsRunning && _table.State.Phase == TablePhase.RoundEnded)
            {
                seconds = Math.Max(0, (int)Math.Ceiling(timer.Deadline - _clock.Now));
            }

            if (seconds != _countdown)
            {
                _countdown = seconds;
                RenderNext();
            }
        }

        private void RenderNext()
        {
            _view.SetNextLabel(ResultsText.NextLabel(IsLastRound, _countdown, _localization));
            _view.SetNextInteractable(!_readySent && !_table.State.IsReady(_session.LocalSeat));
        }

        private void OnNext()
        {
            // A session that cannot take it now (reconnecting) would drop the tap: keep the button.
            if (!_wanted || _readySent || _session.Status != SessionStatus.Running)
            {
                return;
            }

            // Offline the next round (or the final results) follows at once; online this is "I'm ready".
            _audio.Play(SoundCue.UiTap);
            _readySent = true;
            RenderNext();
            _ = ReadyAsync();
        }

        private async Task ReadyAsync()
        {
            try
            {
                await _session.ReadyNextAsync(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (_started && _wanted)
                {
                    _readySent = false;
                    RenderNext();
                }
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

        private void OnSettings()
        {
            if (_router.Has(ScreenId.Settings))
            {
                _audio.Play(SoundCue.UiTap);
                _router.Push(ScreenId.Settings);
            }
        }

        /// <summary>Back never skips the results: it opens the match menu, like Home on the table.</summary>
        private void OnBack()
        {
            if (_router.Has(ScreenId.MatchMenu) && _router.Current == ScreenId.RoundResults)
            {
                _audio.Play(SoundCue.UiTap);
                _router.Push(ScreenId.MatchMenu);
            }
        }
    }
}
