using System;
using System.Collections.Generic;
using System.Globalization;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
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
    /// GDD §11 screen 16, the scorecard: every played round's score per seat, the totals and the
    /// live rank, rows sorted by rank with the local player's row in gold. It reads the score sheet
    /// of the rounds that have been played back, so it never shows a round before the table has;
    /// rounds still to come show a dash. The button reads "Back to Game" during a match and "Back
    /// to Results" after it; both, the back arrow and Android back close it. Opening it never
    /// changes the match (the table presenter pauses an offline match under any overlay).
    /// </summary>
    public sealed class ScorecardPresenter : IStartable, IDisposable
    {
        public const string FooterFinalKey = "ui.scorecard.footer_final";
        public const string BackToResultsKey = "ui.scorecard.back_to_results";
        public const string BackToGameKey = "ui.scorecard.back_to_game";

        private readonly ScorecardView _view;
        private readonly AvatarLibrary _avatars;
        private readonly PlayedTable _table;
        private readonly IMatchSession _session;
        private readonly ScreenRouter _router;
        private readonly ILocalization _localization;
        private readonly IAudioService _audio;
        private readonly string[] _rounds = new string[ScorecardView.RoundCount];
        private readonly bool[] _negative = new bool[ScorecardView.RoundCount];

        private bool _started;

        public ScorecardPresenter(
            ScorecardView view,
            AvatarLibrary avatars,
            PlayedTable table,
            IMatchSession session,
            ScreenRouter router,
            ILocalization localization,
            IAudioService audio)
        {
            _view = view;
            _avatars = avatars;
            _table = table;
            _session = session;
            _router = router;
            _localization = localization;
            _audio = audio;
        }

        public void Start()
        {
            _started = true;
            _view.BackClicked += Close;
            _view.PrimaryClicked += Close;
            _view.BackRequested += Close;
            _view.SettingsClicked += OnSettings;
            _table.Played += OnPlayed;
            _table.Rebuilt += Render;
            _session.SeatsChanged += Render;
            _localization.LanguageChanged += Render;
            Render();
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
                _view.BackClicked -= Close;
                _view.PrimaryClicked -= Close;
                _view.BackRequested -= Close;
                _view.SettingsClicked -= OnSettings;
                _table.Played -= OnPlayed;
                _table.Rebuilt -= Render;
                _session.SeatsChanged -= Render;
                _localization.LanguageChanged -= Render;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnPlayed(MatchEvent e)
        {
            if (e is RoundScored || e is MatchEnded || e is MatchStarted)
            {
                Render();
            }
        }

        private void Render()
        {
            TableState state = _table.State;
            ScoreSheet sheet = state.Scores;
            bool over = state.Phase == TablePhase.MatchEnded && state.FinalStandings.Count == SeatExtensions.Count;

            // The rank is live: the same tie-breaks as the final standings, on the rounds so far.
            IReadOnlyList<Standing> standings = over ? state.FinalStandings : Standings.Compute(sheet);
            int played = sheet.Rounds.Count;
            int length = state.Rules.Rounds;
            int highlight = -1;
            for (int row = 0; row < standings.Count && row < ScorecardView.RowCount; row++)
            {
                Standing standing = standings[row];
                Seat seat = standing.Seat;
                SessionSeat who = _session.Seats[(int)seat];
                bool isLocal = seat == _session.LocalSeat;
                if (isLocal)
                {
                    highlight = row;
                }

                for (int i = 0; i < ScorecardView.RoundCount; i++)
                {
                    if (i < played)
                    {
                        int delta = sheet.Rounds[i][seat].DeltaTenths;
                        _rounds[i] = ScoreTenths.Format(delta);
                        _negative[i] = delta < 0;
                    }
                    else
                    {
                        // A round still to come shows a dash; a column the match has no round for stays empty.
                        _rounds[i] = i < length ? ResultsText.Unplayed : string.Empty;
                        _negative[i] = false;
                    }
                }

                int total = sheet.TotalOf(seat);
                _view.SetRow(
                    row,
                    isLocal ? _localization.Get(ResultsText.YouKey) : who.Name,
                    _rounds,
                    _negative,
                    ScoreTenths.Format(total),
                    total < 0,
                    standing.Rank.ToString(CultureInfo.InvariantCulture));
                Sprite avatar = _avatars != null ? _avatars.Get(who.AvatarId) : null;
                if (avatar != null)
                {
                    _view.SetRowAvatar(row, avatar);
                }
            }

            _view.SetHighlightRow(highlight);
            // "Final standings · 5 rounds completed" after the match; "2 rounds completed" during it.
            string completed = ResultsText.RoundsCompleted(played, _localization);
            _view.SetFooter(over ? _localization.Format(FooterFinalKey, completed) : completed);
            _view.SetPrimaryLabel(_localization.Get(over ? BackToResultsKey : BackToGameKey));
        }

        private void Close()
        {
            if (_router.Current == ScreenId.Scorecard)
            {
                _audio.Play(SoundCue.UiBack);
                _router.Pop();
            }
        }

        private void OnSettings()
        {
            if (_router.Has(ScreenId.Settings) && _router.Current == ScreenId.Scorecard)
            {
                _audio.Play(SoundCue.UiTap);
                _router.Push(ScreenId.Settings);
            }
        }
    }
}
