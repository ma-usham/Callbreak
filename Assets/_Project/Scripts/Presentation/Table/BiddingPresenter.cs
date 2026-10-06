using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// GDD §11 screen 07, "Make your call". The panel appears only once the local seat's call turn
    /// has been played back; before that the prompt above the hand reads "Asha is calling…" (ADR 040). Chips
    /// come from the rule set (MinCall to MaxCall) with nothing chosen; Confirm is disabled until a
    /// chip is chosen, reads "Confirm {n}", and is final once pressed. When the call has been
    /// placed the panel slides away. If the session refuses the call the panel takes input again
    /// and says why. With the beginner assist on (Settings, off by default) a faint dot marks the
    /// call the estimator suggests. While a page covers the table (online the match runs on under
    /// the scorecard or the settings), the chips and Confirm do nothing.
    /// </summary>
    public sealed class BiddingPresenter : IStartable, IDisposable
    {
        public const string ConfirmKey = "ui.bidding.confirm";
        public const string ConfirmCallKey = "ui.bidding.confirm_n";

        private readonly BiddingPanelView _view;
        private readonly PlayedTable _table;
        private readonly IMatchSession _session;
        private readonly ScreenRouter _router;
        private readonly ISettingsService _settings;
        private readonly ILocalization _localization;
        private readonly IAudioService _audio;
        private readonly ICallAdvisor _advisor;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private TurnId _shownTurn = TurnId.None;
        private TurnId _suggestedFor = TurnId.None;
        private int _suggested;
        private int _chosen;
        private string _refusalKey;
        private bool _sent;
        private bool _started;

        /// <summary>
        /// The scope's constructor. The call advisor is the one dependency a table can be without
        /// (no assist, then): it is taken from the scope when somebody registered one.
        /// </summary>
        [Inject]
        public BiddingPresenter(
            BiddingPanelView view,
            PlayedTable table,
            IMatchSession session,
            ScreenRouter router,
            ISettingsService settings,
            ILocalization localization,
            IAudioService audio,
            IObjectResolver scope)
            : this(view, table, session, router, settings, localization, audio, AdvisorIn(scope))
        {
        }

        /// <param name="advisor">Suggests the call the beginner assist marks; null when the table has none.</param>
        public BiddingPresenter(
            BiddingPanelView view,
            PlayedTable table,
            IMatchSession session,
            ScreenRouter router,
            ISettingsService settings,
            ILocalization localization,
            IAudioService audio,
            ICallAdvisor advisor)
        {
            _view = view;
            _table = table;
            _session = session;
            _router = router;
            _settings = settings;
            _localization = localization;
            _audio = audio;
            _advisor = advisor;
        }

        /// <summary>Nothing covers the table: the only time the panel answers a touch.</summary>
        private bool TableIsOpen => TableInput.IsOpen(_router.Current);

        public void Start()
        {
            _started = true;
            _view.CallChosen += OnCallChosen;
            _view.ConfirmClicked += OnConfirm;
            _table.Played += OnPlayed;
            _table.Rebuilt += OnRebuilt;
            _localization.LanguageChanged += OnRebuilt;
            _settings.Changed += OnSettingsChanged;
            _view.ReduceMotion = _settings.ReduceMotion;
            _view.SetCallRange(_session.Rules.MinCall, _session.Rules.MaxCall);
            Render(false);
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
                _view.CallChosen -= OnCallChosen;
                _view.ConfirmClicked -= OnConfirm;
                _table.Played -= OnPlayed;
                _table.Rebuilt -= OnRebuilt;
                _localization.LanguageChanged -= OnRebuilt;
                _settings.Changed -= OnSettingsChanged;
                _lifetime.Cancel();
                _lifetime.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static ICallAdvisor AdvisorIn(IObjectResolver scope)
        {
            return scope != null && scope.TryResolve(out ICallAdvisor advisor) ? advisor : null;
        }

        /// <summary>An event has played: the panel rises as the local call turn begins, and slides away once it is over.</summary>
        private void OnPlayed(MatchEvent e) => Render(true);

        /// <summary>The table was rebuilt, or the language changed: the panel is what the state says, at once.</summary>
        private void OnRebuilt() => Render(false);

        private void OnSettingsChanged()
        {
            _view.ReduceMotion = _settings.ReduceMotion;

            // The beginner assist can be switched while the panel is open.
            if (_started && _shownTurn != TurnId.None)
            {
                _view.SetHint(SuggestedCall(_table.State));
            }
        }

        private void Render(bool slide)
        {
            TableState state = _table.State;
            bool calling = state.Phase == TablePhase.Calling && state.HasTurn;
            if (calling && state.IsLocalCallTurn)
            {
                _view.SetCalling(null);
                if (_shownTurn != state.Turn)
                {
                    // A new call turn: nothing is chosen, so a call is never made by accident.
                    _shownTurn = state.Turn;
                    _sent = false;
                    _chosen = 0;
                    _refusalKey = null;
                    _view.SetSelected(0);
                    _view.SetChipsEnabled(true);
                    _view.SetConfirm(_localization.Get(ConfirmKey), false);
                }
                else
                {
                    RenderConfirm();
                }

                _view.SetReason(_refusalKey != null ? _localization.Get(_refusalKey) : null);
                _view.SetHint(SuggestedCall(state));
                if (slide)
                {
                    _view.SlideIn(_settings.ReduceMotion);
                }
                else
                {
                    _view.ShowPanel(true);
                }

                return;
            }

            _shownTurn = TurnId.None;
            if (slide)
            {
                _view.SlideAway(_settings.ReduceMotion);
            }
            else
            {
                _view.ShowPanel(false);
            }

            // "Asha is calling…" shows in the prompt above the hand now, not in the middle of the
            // table (the owner, 6 Oct 2026, ADR 040).
            _view.SetCalling(null);
        }

        private void RenderConfirm()
        {
            int call = _view.SelectedCall;
            _view.SetConfirm(call == 0 ? _localization.Get(ConfirmKey) : _localization.Format(ConfirmCallKey, call), call != 0 && !_sent);
        }

        /// <summary>
        /// The call the assist's dot marks: the estimator's, asked once per call turn and only
        /// while the assist is on. Off (the default), this is one test of a flag.
        /// </summary>
        private int SuggestedCall(TableState state)
        {
            if (!_settings.BeginnerAssist || _advisor == null)
            {
                return 0;
            }

            if (_suggestedFor != state.Turn)
            {
                _suggestedFor = state.Turn;
                _suggested = AskAdvisor();
            }

            return _suggested;
        }

        private int AskAdvisor()
        {
            try
            {
                RuleSet rules = _session.Rules;
                int call = _advisor.SuggestCall(_session.View);
                return call >= rules.MinCall && call <= rules.MaxCall ? call : 0;
            }
            catch (Exception e)
            {
                // The assist is a hint: without it the panel works as it always does.
                Debug.LogException(e);
                return 0;
            }
        }

        private void OnCallChosen(int call)
        {
            if (_sent || !TableIsOpen)
            {
                // The row marked the chip before anybody could object: put the choice back.
                _view.SetSelected(_chosen);
                return;
            }

            _chosen = call;
            if (_refusalKey != null)
            {
                _refusalKey = null;
                _view.SetReason(null);
            }

            _audio.Play(SoundCue.UiTap);
            RenderConfirm();
        }

        private void OnConfirm()
        {
            int call = _view.SelectedCall;
            if (_sent || call == 0 || !TableIsOpen || !_table.State.IsLocalCallTurn)
            {
                return;
            }

            // Final once pressed (GDD screen 07, callout 7).
            _sent = true;
            _view.SetChipsEnabled(false);
            RenderConfirm();
            _ = SendAsync(call);
        }

        private async Task SendAsync(int call)
        {
            RejectReason refused = RejectReason.None;
            bool accepted = false;
            try
            {
                CommandResult result = await _session.PlaceCallAsync(call, _lifetime.Token);
                accepted = result.IsAccepted;
                refused = result.Reason;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (!accepted && _started && _table.State.IsLocalCallTurn)
            {
                // Refused: the panel takes input again and says why (GDD screen 07).
                _sent = false;
                _refusalKey = CallRefusal.KeyFor(refused);
                _view.SetChipsEnabled(true);
                _view.SetReason(_localization.Get(_refusalKey));
                RenderConfirm();
            }
        }
    }
}
