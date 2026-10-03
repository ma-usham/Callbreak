using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;

namespace Callbreak.Application.Online
{
    /// <summary>What one player brought to the host that took its table over (ADR 030): its seat, its last update number and that update's view.</summary>
    internal sealed class SeatReport
    {
        public SeatReport(Seat seat, uint seq, PlayerView view)
        {
            Seat = seat;
            Seq = seq;
            View = view ?? throw new ArgumentNullException(nameof(view));
        }

        public Seat Seat { get; }

        public uint Seq { get; }

        public PlayerView View { get; }
    }

    /// <summary>A match rebuilt by <see cref="TableRebuild"/>.</summary>
    internal sealed class RebuiltTable
    {
        public RebuiltTable(PlayerView latest, uint seq, MatchSnapshot snapshot)
        {
            Latest = latest;
            Seq = seq;
            Snapshot = snapshot;
        }

        /// <summary>The newest view any player held: the public state the match goes on from.</summary>
        public PlayerView Latest { get; }

        /// <summary>The update number of <see cref="Latest"/>: the new host numbers its updates on from it.</summary>
        public uint Seq { get; }

        /// <summary>
        /// The whole match, every hand included, from the same trick; null when the hands cannot all be
        /// told apart (two seats or more whose cards nobody brought): the round is then dealt again
        /// (<see cref="TableRebuild.Redeal"/>).
        /// </summary>
        public MatchSnapshot Snapshot { get; }

        public bool MustRedeal => Snapshot == null;
    }

    /// <summary>
    /// Rebuilds a hosted match from what its players held when the host was lost (ADR 030), so it goes
    /// on from the same trick. The public state comes from whoever holds the latest update; each
    /// person's hand from its own report; each bot's from the hand it was dealt (which the next host
    /// in line was sent) without the cards it has played since, which are public; and the one hand
    /// left, the lost host's, is the 52 cards without everything played and everything held. When
    /// two hands or more are missing (the host and another player dropped at once), nobody can tell
    /// whose leftover cards are whose: the round in progress is dealt again, and the rounds before
    /// keep their scores. A rebuilt match is checked like a restored save (GDD §22): one that
    /// describes an impossible match is never repaired, the round is dealt again instead.
    /// </summary>
    internal static class TableRebuild
    {
        /// <summary>The most turn ids one round gives out: four calls and 52 cards.</summary>
        private const int TurnsPerRound = SeatExtensions.Count + Deck.Size;

        /// <summary>A deal that keeps needing a redeal is a broken generator; the engine gives up at the same count.</summary>
        private const int MaxRedeals = 100;

        /// <param name="reports">What each player that is here brought, the new host's own included; views of another match are left out.</param>
        /// <param name="package">The bots' dealt hands, as the lost host sent them to the next host in line; null when it sent none.</param>
        public static RebuiltTable Rebuild(IReadOnlyList<SeatReport> reports, SuccessionPackage package)
        {
            SeatReport latest = Latest(reports);
            PlayerView state = latest.View;
            string matchId = state.MatchId;
            if (state.Phase != Phase.Calling && state.Phase != Phase.Playing)
            {
                // Between rounds (or before the first deal) nobody holds a card: nothing is hidden.
                var empty = new CardSet[SeatExtensions.Count];
                return Restorable(state, latest.Seq, SnapshotOf(state, empty));
            }

            CardSet played = PlayedThisRound(state);
            var hands = new CardSet[SeatExtensions.Count];
            var known = new bool[SeatExtensions.Count];
            foreach (SeatReport report in reports)
            {
                PlayerView view = report.View;
                if (view.MatchId != matchId || view.Round != state.Round || view.Viewer != report.Seat)
                {
                    continue;
                }

                // A card the host played for this player after its last update (its clock ran out) is played.
                Keep(report.Seat, view.Hand.Except(played), state, hands, known);
            }

            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                var seat = (Seat)s;
                if (!known[s] && package != null && package.Round == state.Round && package.TryGetDealtHand(seat, out CardSet dealt))
                {
                    Keep(seat, dealt.Except(played), state, hands, known);
                }
            }

            CardSet held = CardSet.Empty;
            int missing = -1;
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                if (!known[s])
                {
                    if (missing >= 0)
                    {
                        // Two hands nobody brought: whose leftover cards are whose cannot be told.
                        return new RebuiltTable(state, latest.Seq, null);
                    }

                    missing = s;
                    continue;
                }

                if (held.Overlaps(hands[s]) || played.Overlaps(hands[s]))
                {
                    // Two players claim the same card: the reports do not add up.
                    return new RebuiltTable(state, latest.Seq, null);
                }

                held = held.Union(hands[s]);
            }

            if (missing >= 0)
            {
                // The lost host's hand: whatever nobody played and nobody holds.
                hands[missing] = CardSet.FullDeck.Except(played).Except(held);
            }

            return Restorable(state, latest.Seq, SnapshotOf(state, hands));
        }

        /// <summary>
        /// The match as a backup host takes it over (the owner, 3 Oct 2026): it holds none of the bots'
        /// cards, so a round in progress is dealt again (<see cref="Redeal"/>), by the same dealer, with
        /// the rounds before as they were scored. Between rounds nobody holds a card, and the match goes
        /// on exactly as it stood.
        /// </summary>
        /// <param name="reports">What each player that is here brought, the new host's own included.</param>
        public static RebuiltTable DealAgain(IReadOnlyList<SeatReport> reports)
        {
            SeatReport latest = Latest(reports);
            PlayerView state = latest.View;
            if (state.Phase == Phase.Calling || state.Phase == Phase.Playing)
            {
                return new RebuiltTable(state, latest.Seq, null);
            }

            return Restorable(state, latest.Seq, SnapshotOf(state, new CardSet[SeatExtensions.Count]));
        }

        /// <summary>The report with the newest update of the match the first one is from: the public state the match goes on from.</summary>
        private static SeatReport Latest(IReadOnlyList<SeatReport> reports)
        {
            if (reports == null || reports.Count == 0)
            {
                throw new ArgumentException("A table is rebuilt from at least the new host's own view.", nameof(reports));
            }

            string matchId = reports[0].View.MatchId;
            SeatReport latest = reports[0];
            foreach (SeatReport report in reports)
            {
                if (report.View.MatchId == matchId && report.Seq > latest.Seq)
                {
                    latest = report;
                }
            }

            return latest;
        }

        /// <summary>
        /// The round <paramref name="state"/> is in, dealt again from <paramref name="rng"/> (the new
        /// host's own randomness): the same dealer, no calls yet, the rounds before as they were scored.
        /// A hand the rules would redeal is dealt again too, as the engine does.
        /// </summary>
        public static MatchSnapshot Redeal(PlayerView state, IRandom rng)
        {
            if (state.Phase != Phase.Calling && state.Phase != Phase.Playing)
            {
                throw new InvalidOperationException("Only a round in progress is dealt again, not one in " + state.Phase + ".");
            }

            RuleSet rules = state.Rules;
            Seat first = state.Dealer.Next(rules.Direction);
            CardSet[] dealt = null;
            for (int attempt = 0; attempt <= MaxRedeals; attempt++)
            {
                dealt = Deck.Deal(Deck.Shuffled(rng), first, rules.Direction);
                if (RedealPolicy.Check(dealt, rules) == RedealReason.None)
                {
                    break;
                }

                if (attempt == MaxRedeals)
                {
                    throw new InvalidOperationException("Every deal needed a redeal; the IRandom is not random.");
                }
            }

            // Turn ids only ever grow: past every one this round can have given out.
            int turn = Math.Max(state.CurrentTurn.Value, state.Round * TurnsPerRound) + 1;
            var seats = new SeatSnapshot[SeatExtensions.Count];
            for (int s = 0; s < seats.Length; s++)
            {
                seats[s] = SeatOf(state.Seats[s], dealt[s], CallRules.NotCalled, 0, false);
            }

            return new MatchSnapshot
            {
                schemaVersion = MatchSnapshot.CurrentSchemaVersion,
                ruleSetId = rules.Id,
                matchId = state.MatchId,
                phase = (int)Phase.Calling,
                round = state.Round,
                hasFixedFirstDealer = false,
                firstDealer = (int)state.FirstDealer,
                dealer = (int)state.Dealer,
                currentSeat = (int)first,
                currentTurn = turn,
                turnCounter = turn,
                seats = seats,
                currentTrick = new TrickSnapshot { leader = (int)first, cards = new int[0] },
                completedTricks = new TrickSnapshot[0],
                rounds = RoundsOf(state),
                matchEnded = false,
            };
        }

        /// <summary>Every card played in the round <paramref name="state"/> is in: its finished tricks and the trick on the table.</summary>
        internal static CardSet PlayedThisRound(PlayerView state)
        {
            CardSet played = state.CurrentTrick.Cards;
            foreach (CompletedTrick trick in state.CompletedTricks)
            {
                played = played.Union(trick.Trick.Cards);
            }

            return played;
        }

        /// <summary>The cards <paramref name="seat"/> has played in the round <paramref name="state"/> is in.</summary>
        internal static CardSet PlayedBy(PlayerView state, Seat seat)
        {
            CardSet played = CardSet.Empty;
            if (state.CurrentTrick.TryGetCardOf(seat, out Card current))
            {
                played = played.With(current);
            }

            foreach (CompletedTrick trick in state.CompletedTricks)
            {
                if (trick.Trick.TryGetCardOf(seat, out Card card))
                {
                    played = played.With(card);
                }
            }

            return played;
        }

        /// <summary>A hand counts when it holds as many cards as the public state says that seat has.</summary>
        private static void Keep(Seat seat, CardSet hand, PlayerView state, CardSet[] hands, bool[] known)
        {
            if (hand.Count == state.HandCountOf(seat))
            {
                hands[(int)seat] = hand;
                known[(int)seat] = true;
            }
        }

        /// <summary>The match as a save would hold it, checked the way a save is (GDD §22); one that does not restore is dealt again.</summary>
        private static RebuiltTable Restorable(PlayerView state, uint seq, MatchSnapshot snapshot)
        {
            bool restores = MatchEngine.TryRestore(snapshot, state.Rules, new Pcg32Random(0), out _, out _);
            if (!restores && (state.Phase == Phase.Calling || state.Phase == Phase.Playing))
            {
                return new RebuiltTable(state, seq, null);
            }

            if (!restores)
            {
                throw new InvalidOperationException("The table cannot be rebuilt from a view in " + state.Phase + ".");
            }

            return new RebuiltTable(state, seq, snapshot);
        }

        private static MatchSnapshot SnapshotOf(PlayerView state, CardSet[] hands)
        {
            var seats = new SeatSnapshot[SeatExtensions.Count];
            for (int s = 0; s < seats.Length; s++)
            {
                var seat = (Seat)s;
                seats[s] = SeatOf(state.Seats[s], hands[s], state.CallOf(seat), state.TricksWonBy(seat), state.IsReady(seat));
            }

            var completed = new TrickSnapshot[state.CompletedTricks.Count];
            for (int t = 0; t < completed.Length; t++)
            {
                completed[t] = TrickOf(state.CompletedTricks[t].Trick);
            }

            bool open = state.Phase == Phase.Calling || state.Phase == Phase.Playing;
            return new MatchSnapshot
            {
                schemaVersion = MatchSnapshot.CurrentSchemaVersion,
                ruleSetId = state.Rules.Id,
                matchId = state.MatchId,
                phase = (int)state.Phase,
                round = state.Round,
                hasFixedFirstDealer = false,
                firstDealer = (int)state.FirstDealer,
                dealer = (int)state.Dealer,
                currentSeat = (int)state.CurrentSeat,
                currentTurn = state.CurrentTurn.Value,

                // The open turn is the newest one given out; between rounds no view says how many there
                // were, so the count goes past every one the rounds so far can have given out.
                turnCounter = open ? state.CurrentTurn.Value : state.Round * TurnsPerRound,
                seats = seats,
                currentTrick = TrickOf(state.CurrentTrick),
                completedTricks = completed,
                rounds = RoundsOf(state),
                matchEnded = state.FinalStandings.Count > 0,
            };
        }

        private static SeatSnapshot SeatOf(SeatProfile profile, CardSet hand, int call, int won, bool ready)
        {
            var ids = new int[hand.Count];
            int i = 0;
            foreach (Card card in hand)
            {
                ids[i++] = card.Id;
            }

            return new SeatSnapshot
            {
                name = profile.Name,
                avatarId = profile.AvatarId,
                deckThemeId = profile.DeckThemeId,
                isBot = profile.IsBot,
                hand = ids,
                call = call,
                tricksWon = won,
                ready = ready,
            };
        }

        private static TrickSnapshot TrickOf(Trick trick)
        {
            var cards = new int[trick.Count];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = trick[i].Id;
            }

            return new TrickSnapshot { leader = (int)trick.Leader, cards = cards };
        }

        private static RoundSnapshot[] RoundsOf(PlayerView state)
        {
            var rounds = new RoundSnapshot[state.Scores.Rounds.Count];
            for (int r = 0; r < rounds.Length; r++)
            {
                var data = new RoundSnapshot
                {
                    round = state.Scores.Rounds[r].Round,
                    calls = new int[SeatExtensions.Count],
                    won = new int[SeatExtensions.Count],
                    deltas = new int[SeatExtensions.Count],
                };
                for (int s = 0; s < SeatExtensions.Count; s++)
                {
                    var line = state.Scores.Rounds[r][(Seat)s];
                    data.calls[s] = line.Call;
                    data.won[s] = line.Won;
                    data.deltas[s] = line.DeltaTenths;
                }

                rounds[r] = data;
            }

            return rounds;
        }
    }
}
