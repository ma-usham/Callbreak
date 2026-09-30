using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;

namespace Callbreak.Tests.EditMode.Bots
{
    /// <summary>
    /// Builds a rigged <see cref="PlayerView"/> for bot scenarios: the viewer's hand, the calls, finished tricks
    /// (winners, tricks won and hand counts follow from them) and the trick in progress. Round 1; the dealer
    /// sits before the first leader. Checks that no card appears twice and that the viewer's hand has the
    /// size the history implies.
    /// </summary>
    public sealed class BotViews
    {
        private readonly Seat _viewer;
        private readonly CardSet _hand;
        private readonly List<Trick> _completed = new List<Trick>();
        private RuleSet _rules = RuleSet.NepaliStandard;
        private int[] _calls = { 3, 3, 3, 3 };
        private Seat? _currentLeader;
        private string _currentCards = string.Empty;
        private Phase _phase = Phase.Playing;

        private BotViews(Seat viewer, CardSet hand)
        {
            _viewer = viewer;
            _hand = hand;
        }

        /// <summary>Starts a view for <paramref name="viewer"/> holding <paramref name="hand"/>.</summary>
        public static BotViews For(Seat viewer, string hand)
        {
            return new BotViews(viewer, CardSet.Parse(hand));
        }

        /// <summary>
        /// A copy of <paramref name="view"/> that claims other hand counts (by seat: South, East, North, West):
        /// a malformed view, for robustness tests. The viewer's count must still match its hand.
        /// </summary>
        public static PlayerView WithHandCounts(PlayerView view, int south, int east, int north, int west)
        {
            var calls = new int[4];
            var won = new int[4];
            var ready = new bool[4];
            for (int s = 0; s < 4; s++)
            {
                calls[s] = view.CallOf((Seat)s);
                won[s] = view.TricksWonBy((Seat)s);
                ready[s] = view.IsReady((Seat)s);
            }

            return new PlayerView(
                view.Viewer,
                view.Rules,
                view.MatchId,
                view.Seats,
                view.Phase,
                view.Round,
                view.Dealer,
                view.FirstDealer,
                view.CurrentSeat,
                view.CurrentTurn,
                view.Hand,
                calls,
                won,
                new[] { south, east, north, west },
                ready,
                view.CurrentTrick,
                view.CompletedTricks,
                view.Scores,
                view.FinalStandings);
        }

        public BotViews Rules(RuleSet rules)
        {
            _rules = rules;
            return this;
        }

        /// <summary>Calls by seat: South, East, North, West (−1 = not called yet).</summary>
        public BotViews Calls(int south, int east, int north, int west)
        {
            _calls = new[] { south, east, north, west };
            return this;
        }

        /// <summary>A finished trick: its leader and four cards in play order.</summary>
        public BotViews Trick(Seat leader, string cards)
        {
            Trick trick = Notation.TrickOf(leader, _rules.Direction, cards);
            if (!trick.IsComplete)
            {
                throw new ArgumentException("A finished trick has four cards: " + cards);
            }

            _completed.Add(trick);
            return this;
        }

        /// <summary>The trick in progress (cards in play order; empty when the viewer leads).</summary>
        public BotViews Current(Seat leader, string cards)
        {
            _currentLeader = leader;
            _currentCards = cards;
            return this;
        }

        /// <summary>The call turn of the viewer instead of a play turn.</summary>
        public BotViews Calling()
        {
            _phase = Phase.Calling;
            return this;
        }

        public PlayerView Build()
        {
            Direction direction = _rules.Direction;
            var completed = new List<CompletedTrick>();
            var won = new int[4];
            var played = new int[4];
            CardSet seen = _hand;
            for (int i = 0; i < _completed.Count; i++)
            {
                var finished = new CompletedTrick(i, _completed[i]);
                completed.Add(finished);
                won[(int)finished.Winner]++;
                seen = Add(seen, finished.Trick, played);
            }

            Seat leader = _currentLeader ?? (completed.Count > 0 ? completed[completed.Count - 1].Winner : _viewer);
            Trick current = _currentCards.Trim().Length == 0 ? new Trick(leader, direction) : Notation.TrickOf(leader, direction, _currentCards);
            Add(seen, current, played);

            var handCounts = new int[4];
            for (int s = 0; s < 4; s++)
            {
                handCounts[s] = Deck.HandSize - played[s];
            }

            if (handCounts[(int)_viewer] != _hand.Count)
            {
                throw new ArgumentException("The viewer's hand should have " + handCounts[(int)_viewer] + " cards, not " + _hand.Count + ".");
            }

            Seat firstLeader = _completed.Count > 0 ? _completed[0].Leader : current.Leader;
            Seat dealer = firstLeader.Advance(direction, 3);
            Seat currentSeat = _viewer;
            if (_phase == Phase.Playing && current.NextSeat != _viewer)
            {
                throw new ArgumentException("It is " + current.NextSeat + "'s turn in that trick, not " + _viewer + "'s.");
            }

            return new PlayerView(
                _viewer,
                _rules,
                "bot-test",
                TestSetups.AllBots().Seats,
                _phase,
                1,
                dealer,
                dealer,
                currentSeat,
                new TurnId(5 + (4 * _completed.Count) + current.Count),
                _hand,
                _calls,
                won,
                handCounts,
                new bool[4],
                current,
                completed,
                ScoreSheet.Empty,
                new Standing[0]);
        }

        private static CardSet Add(CardSet seen, Trick trick, int[] played)
        {
            for (int k = 0; k < trick.Count; k++)
            {
                if (seen.Contains(trick[k]))
                {
                    throw new ArgumentException(trick[k] + " appears twice.");
                }

                seen = seen.With(trick[k]);
                played[(int)trick.SeatAt(k)]++;
            }

            return seen;
        }
    }
}
