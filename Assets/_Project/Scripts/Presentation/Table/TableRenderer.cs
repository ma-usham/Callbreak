using System.Collections.Generic;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// Puts a <see cref="TableState"/> on the <see cref="TableView"/>: the round line, the four
    /// seats, the fans, the trick and the local hand. It is idempotent and instant: after every
    /// played event (and after a resync) the views are brought to the state, whatever an animation
    /// left behind. The local player's card that is still in its optimistic flight is left alone.
    /// </summary>
    public sealed class TableRenderer
    {
        public const string RoundKey = "ui.table.round";
        public const string TrickKey = "ui.table.trick";
        public const string YouKey = "ui.table.you";

        private readonly TableView _view;
        private readonly TableSkin _skin;
        private readonly IMatchSession _session;
        private readonly ISettingsService _settings;
        private readonly ILocalization _localization;
        private readonly List<Card> _hand = new List<Card>(Deck.HandSize);

        public TableRenderer(TableView view, TableSkin skin, IMatchSession session, ISettingsService settings, ILocalization localization)
        {
            _view = view;
            _skin = skin;
            _session = session;
            _settings = settings;
            _localization = localization;
        }

        /// <summary>The mockups do not draw the GDD's "Trick 6 / 13" line, so it stays off unless this is set.</summary>
        public bool ShowTrickCounter { get; set; }

        /// <summary>
        /// The deck this device draws every card in: the one this player chose (ADR 031), which the
        /// session gives the local seat. Every face and every back on this screen is in it, the
        /// opponents' fans, the deal and the shuffle pile included, whatever deck anyone else chose.
        /// </summary>
        public string LocalDeck => _session.Seats[(int)_session.LocalSeat].DeckThemeId;

        public int RelativeSeat(Seat seat) => seat.RelativeTo(_session.LocalSeat);

        public Seat SeatAt(int relativeSeat) => (Seat)(((int)_session.LocalSeat + relativeSeat) % SeatExtensions.Count);

        /// <summary>The name on a seat's plate: "You" for the local seat.</summary>
        public string NameOf(Seat seat)
        {
            return seat == _session.LocalSeat ? _localization.Get(YouKey) : _session.Seats[(int)seat].Name;
        }

        /// <summary>Everything at once.</summary>
        /// <param name="state">The table to show.</param>
        /// <param name="pendingPlay">The local card in its optimistic flight, if any: it is neither in the hand nor removed from the trick.</param>
        /// <param name="handInteractable">Whether the hand takes input now.</param>
        /// <param name="animateHand">Slide hand cards to their places instead of jumping.</param>
        public void Render(TableState state, Card? pendingPlay, bool handInteractable, bool animateHand)
        {
            RenderHud(state);
            RenderSeats(state);
            RenderTrick(state, pendingPlay.HasValue);
            RenderHand(state, pendingPlay, handInteractable, animateHand);
        }

        public void RenderHud(TableState state)
        {
            RuleSet rules = state.Rules;
            _view.SetRound(_localization.Format(RoundKey, state.Round < 1 ? 1 : state.Round, rules.Rounds));
            bool counting = ShowTrickCounter && state.Phase == TablePhase.Playing;
            _view.SetTrick(counting ? _localization.Format(TrickKey, System.Math.Min(state.TricksCompleted + 1, Deck.HandSize), Deck.HandSize) : null);
        }

        public void RenderSeats(TableState state)
        {
            bool playing = state.AllCalled && state.Phase != TablePhase.Calling && state.Phase != TablePhase.Dealing && state.Phase != TablePhase.Idle;
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                var seat = (Seat)i;
                SessionSeat who = _session.Seats[i];
                int rel = RelativeSeat(seat);
                SeatView seatView = _view.Seat(rel);
                bool turn = state.HasTurn && state.TurnSeat == seat;
                seatView.SetName(NameOf(seat));
                seatView.SetAvatar(_skin.Avatar(who.AvatarId));
                var info = new SeatStatusInfo(
                    rel == 0,
                    who.Connection,
                    turn && state.Phase == TablePhase.Calling,
                    state.CallOf(seat),
                    state.IsAutoCall(seat),
                    playing,
                    state.WonBy(seat));
                seatView.SetStatus(SeatStatus.Text(info, _localization), SeatStatus.IsMet(info));

                // The line stays whoever plays the seat; how its player is connected is the chip's.
                // A bot that fills a seat looks like any other player: no tag (ADR 025).
                seatView.SetChip(SeatStatus.Chip(who.Connection, _localization), SeatStatus.ChipIsBusy(who.Connection));
                seatView.SetTurn(turn);
                if (seatView.Fan != null)
                {
                    seatView.Fan.SetBack(_skin.Back(LocalDeck));
                    seatView.Fan.SetCount(state.HandCountOf(seat));
                }
            }
        }

        /// <summary>The trick's cards at their slots; slots the state leaves empty give their cards back to the pool.</summary>
        public void RenderTrick(TableState state, bool keepLocalSlot)
        {
            TrickAreaView trick = _view.Trick;
            Trick current = state.Trick;
            for (int rel = 0; rel < TrickAreaView.Slots; rel++)
            {
                Seat seat = SeatAt(rel);
                bool played = current.TryGetCardOf(seat, out Card card);
                CardView shown = trick.CardAt(rel);
                if (!played)
                {
                    if (shown != null && !(rel == 0 && keepLocalSlot))
                    {
                        _view.Cards.Return(trick.Release(rel));
                    }

                    continue;
                }

                if (shown == null || !shown.IsFaceUp || shown.Card != card)
                {
                    if (shown != null)
                    {
                        _view.Cards.Return(trick.Release(rel));
                    }

                    shown = _view.Cards.Rent(trick.Area);
                    shown.ShowFace(card, _skin.Face(LocalDeck, card));
                    trick.Adopt(rel, shown);
                }

                shown.SetAlpha(1f);
                shown.SetGlow(false);
                trick.Settle(rel);
            }

            // Cards lie in the order they were played: the latest on top.
            for (int i = 0; i < current.Count; i++)
            {
                CardView card = trick.CardAt(RelativeSeat(current.SeatAt(i)));
                if (card != null)
                {
                    card.Rect.SetAsLastSibling();
                }
            }
        }

        public void RenderHand(TableState state, Card? pendingPlay, bool interactable, bool animate)
        {
            HandView hand = _view.Hand;
            HandSorter.Sort(state.Hand, _hand);
            if (pendingPlay.HasValue)
            {
                _hand.Remove(pendingPlay.Value);
            }

            hand.SetDeck(_skin.Cards, LocalDeck);
            hand.SetCards(_hand, animate && !_settings.ReduceMotion);

            // Your play turn: legal cards rise and glow, the rest are subdued (GDD §13). Once a card
            // is on its way, or it is not your turn, every card is plain.
            bool choosing = state.IsLocalPlayTurn && !pendingPlay.HasValue;
            CardSet lifted = choosing ? state.Legal : CardSet.Empty;
            CardSet dimmed = choosing ? state.Hand - state.Legal : CardSet.Empty;
            hand.SetHighlights(lifted, dimmed, animate && !_settings.ReduceMotion);
            hand.Interactable = interactable && choosing;
        }
    }
}
