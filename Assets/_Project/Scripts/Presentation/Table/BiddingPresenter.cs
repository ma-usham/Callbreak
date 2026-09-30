using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// GDD §11 screen 07, "Make your call". The panel appears only once the local seat's call turn
    /// has been played back; before that the centre of the table reads "Asha is calling…". Chips
    /// come from the rule set (MinCall to MaxCall) with nothing chosen; Confirm is disabled until a
    /// chip is chosen, reads "Confirm {n}", and is final once pressed. If the session refuses the
    /// call the panel opens again.
    /// </summary>
    public sealed class BiddingPresenter : IStartable, IDisposable
    {
        public const string ConfirmKey = "ui.bidding.confirm";
        public const string ConfirmCallKey = "ui.bidding.confirm_n";

        private readonly BiddingPanelView _view;
        private readonly PlayedTable _table;
        private readonly IMatchSession _session;
        private readonly ILocalization _localization;
        private readonly IAudioService _audio;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private TurnId _shownTurn = TurnId.None;
        private bool _sent;
        private bool _started;

        public BiddingPresenter(BiddingPanelView view, PlayedTable table, IMatchSession session, ILocalization localization, IAudioService audio)
        {
            _view = view;
            _table = table;
            _session = session;
            _localization = localization;
            _audio = audio;
        }

        public void Start()
        {
            _started = true;
            _view.CallChosen += OnCallChosen;
            _view.ConfirmClicked += OnConfirm;
            _table.Played += OnPlayed;
            _table.Rebuilt += Render;
            _localization.LanguageChanged += Render;
            _view.SetCallRange(_session.Rules.MinCall, _session.Rules.MaxCall);
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
                _view.CallChosen -= OnCallChosen;
                _view.ConfirmClicked -= OnConfirm;
                _table.Played -= OnPlayed;
                _table.Rebuilt -= Render;
                _localization.LanguageChanged -= Render;
                _lifetime.Cancel();
                _lifetime.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnPlayed(MatchEvent e) => Render();

        private void Render()
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
                    _view.SetSelected(0);
                    _view.SetChipsEnabled(true);
                    _view.SetConfirm(_localization.Get(ConfirmKey), false);
                }
                else
                {
                    RenderConfirm();
                }

                _view.ShowPanel(true);
                return;
            }

            _shownTurn = TurnId.None;
            _view.ShowPanel(false);
            _view.SetCalling(calling ? TurnPrompt.Calling(NameOf(state.TurnSeat), _localization) : null);
        }

        private void RenderConfirm()
        {
            int call = _view.SelectedCall;
            _view.SetConfirm(call == 0 ? _localization.Get(ConfirmKey) : _localization.Format(ConfirmCallKey, call), call != 0 && !_sent);
        }

        private string NameOf(Seat seat) => _session.Seats[(int)seat].Name;

        private void OnCallChosen(int call)
        {
            if (_sent)
            {
                return;
            }

            _audio.Play(SoundCue.UiTap);
            RenderConfirm();
        }

        private void OnConfirm()
        {
            int call = _view.SelectedCall;
            if (_sent || call == 0 || !_table.State.IsLocalCallTurn)
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
            bool accepted = false;
            try
            {
                CommandResult result = await _session.PlaceCallAsync(call, _lifetime.Token);
                accepted = result.IsAccepted;
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
                // Refused: the panel takes input again.
                _sent = false;
                _view.SetChipsEnabled(true);
                RenderConfirm();
            }
        }
    }
}
