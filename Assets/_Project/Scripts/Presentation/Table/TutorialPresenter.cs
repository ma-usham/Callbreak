using System;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Application.Tutorial;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The guided first hand of the first practice match (ADR 084): shows the
    /// <see cref="MatchTutorial"/>'s step once the table has played the event that opened it, so a
    /// caption never comes before what it talks about, and points at the chip, Confirm or the card
    /// the step asks for. Next and "Skip tutorial" go to the tutorial. While a page covers the
    /// table (the match menu, the scorecard, settings) the caption steps aside; the scoring steps
    /// show over the round results. Every other match has no tutorial, and this does nothing.
    /// </summary>
    public sealed class TutorialPresenter : IStartable, ITickable, IDisposable
    {
        public const string NextKey = "ui.tutorial.next";
        public const string SkipKey = "ui.tutorial.skip";
        public const string PlayKey = "ui.tutorial.play";
        public const string DragHintKey = "ui.tutorial.drag_hint";

        // Where the fingertip goes in the target's rect (0 to 1 from the bottom left): on a card the
        // strip its neighbour leaves uncovered, on a chip or a button just below its middle.
        private static readonly Vector2 CardPoint = new Vector2(0.2f, 0.6f);
        private static readonly Vector2 ButtonPoint = new Vector2(0.5f, 0.35f);

        private readonly TutorialView _view;
        private readonly TableView _table;
        private readonly BiddingPanelView _bidding;
        private readonly PlayedTable _played;
        private readonly IMatchSession _session;
        private readonly ScreenRouter _router;
        private readonly ISettingsService _settings;
        private readonly ILocalization _localization;
        private readonly IAudioService _audio;
        private readonly MatchTutorial _tutorial;

        private int _playedCount;
        private bool _started;

        public TutorialPresenter(
            TutorialView view,
            TableView table,
            BiddingPanelView bidding,
            PlayedTable played,
            IMatchSession session,
            ScreenRouter router,
            ISettingsService settings,
            ILocalization localization,
            IAudioService audio)
        {
            _view = view;
            _table = table;
            _bidding = bidding;
            _played = played;
            _session = session;
            _router = router;
            _settings = settings;
            _localization = localization;
            _audio = audio;
            _tutorial = (session as OfflineMatchSession)?.Tutorial;
        }

        public void Start()
        {
            if (_tutorial == null)
            {
                _view.Hide();
                return;
            }

            _started = true;
            _view.ReduceMotion = _settings.ReduceMotion;
            _view.NextClicked += OnNext;
            _view.SkipClicked += OnSkip;
            _tutorial.Changed += Render;
            _played.Played += OnPlayed;
            _played.Rebuilt += OnRebuilt;
            _router.Changed += OnScreenChanged;
            _settings.Changed += OnSettingsChanged;
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
                _view.NextClicked -= OnNext;
                _view.SkipClicked -= OnSkip;
                _tutorial.Changed -= Render;
                _played.Played -= OnPlayed;
                _played.Rebuilt -= OnRebuilt;
                _router.Changed -= OnScreenChanged;
                _settings.Changed -= OnSettingsChanged;
                _localization.LanguageChanged -= Render;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void Tick()
        {
            // The call step points at the chip, then at Confirm once a chip is chosen.
            if (_started && _view.IsShown && _tutorial.Step == TutorialStep.Call)
            {
                Point();
            }
        }

        private void OnPlayed(MatchEvent e)
        {
            _playedCount++;
            if (_playedCount == _tutorial.OpenedAt)
            {
                Render();
            }
        }

        /// <summary>The table was rebuilt from the session's view: everything observed is on screen.</summary>
        private void OnRebuilt()
        {
            _playedCount = _tutorial.Observed;
            Render();
        }

        private void OnScreenChanged(ScreenId id) => Render();

        private void OnSettingsChanged() => _view.ReduceMotion = _settings.ReduceMotion;

        private void OnNext()
        {
            _audio.Play(SoundCue.UiTap);
            _tutorial.Next();
        }

        private void OnSkip()
        {
            _audio.Play(SoundCue.UiTap);
            _tutorial.Skip();
        }

        private bool IsScoring => _tutorial.Step == TutorialStep.Score || _tutorial.Step == TutorialStep.Ready;

        /// <summary>The step is on screen once its event has been played, and while nothing but its own page covers the table.</summary>
        private bool ShowsStep()
        {
            if (!_tutorial.IsActive || _tutorial.Step == TutorialStep.None || _playedCount < _tutorial.OpenedAt)
            {
                return false;
            }

            ScreenId top = _router.Current;
            return TableInput.IsOpen(top) || (IsScoring && top == ScreenId.RoundResults);
        }

        private void Render()
        {
            if (!_started)
            {
                return;
            }

            if (!ShowsStep())
            {
                _view.Hide();
                return;
            }

            string skip = _localization.Get(SkipKey);
            string next = _localization.Get(NextKey);
            switch (_tutorial.Step)
            {
                case TutorialStep.Welcome:
                    _view.Show(_localization.Get("ui.tutorial.welcome_title"), _localization.Get("ui.tutorial.welcome_body"), next, skip, true);
                    break;

                case TutorialStep.Call:
                    _view.Show(
                        _localization.Get("ui.tutorial.call_title"),
                        _localization.Format("ui.tutorial.call_body", _tutorial.TargetCall),
                        null, skip, false);
                    break;

                case TutorialStep.Follow:
                    _view.Show(
                        _localization.Get("rules.primer.follow_title"),
                        Lines(_localization.Get("rules.must_follow"), _localization.Get(DragHintKey)),
                        null, skip, false);
                    break;

                case TutorialStep.TrickWon:
                    _view.Show(
                        _localization.Get("ui.tutorial.trick_title"),
                        _localization.Format("ui.tutorial.trick_body", NameOf(_tutorial.TrickWinner)),
                        next, skip, false);
                    break;

                case TutorialStep.Trump:
                    _view.Show(
                        _localization.Get("rules.primer.trump_title"),
                        Lines(_localization.Get("ui.tutorial.trump_body"), _localization.Get(DragHintKey)),
                        null, skip, false);
                    break;

                case TutorialStep.YouWon:
                    _view.Show(_localization.Get("ui.tutorial.won_title"), _localization.Get("ui.tutorial.won_body"), next, skip, false);
                    break;

                case TutorialStep.Score:
                    _view.Show(_localization.Get("rules.primer.score_title"), ScoreText(_tutorial.Result), next, skip, true);
                    break;

                case TutorialStep.Ready:
                    _view.Show(
                        _localization.Get("ui.tutorial.ready_title"),
                        Lines(_localization.Format("ui.tutorial.ready_body", _session.Rules.Rounds - 1), _localization.Get("rules.primer.highlight")),
                        _localization.Get(PlayKey), null, true);
                    break;
            }

            Point();
        }

        /// <summary>"You called 3 and won 5: +3.2 points.", then how a call scores.</summary>
        private string ScoreText(SeatRoundResult result)
        {
            string line = _localization.Format(
                result.Met ? "ui.tutorial.score_met" : "ui.tutorial.score_missed",
                result.Call,
                result.Won,
                ScoreTenths.FormatSigned(result.DeltaTenths));
            return Lines(line, _localization.Get("rules.primer.score_body"));
        }

        private void Point()
        {
            switch (_tutorial.Step)
            {
                case TutorialStep.Call:
                    RectTransform target = _bidding.SelectedCall > 0 ? _bidding.ConfirmRect : _bidding.ChipOf(_tutorial.TargetCall);
                    _view.PointAt(_bidding.IsPanelShown ? target : null, ButtonPoint, false);
                    break;

                case TutorialStep.Follow:
                case TutorialStep.Trump:
                    Card? card = _tutorial.TargetCard;
                    CardView view = card.HasValue ? _table.Hand.ViewOf(card.Value) : null;
                    _view.PointAt(view != null ? view.Rect : null, CardPoint, true);
                    break;

                default:
                    _view.ClearPointer();
                    break;
            }
        }

        private string NameOf(Seat seat) => _session.Seats[(int)seat].Name;

        private static string Lines(string first, string second) => first + "\n" + second;
    }
}
