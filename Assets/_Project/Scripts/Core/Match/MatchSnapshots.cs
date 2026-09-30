using System;
using System.Globalization;
using Callbreak.Core.Cards;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;

namespace Callbreak.Core.Match
{
    /// <summary>
    /// Converts between <see cref="MatchState"/> and the serializable <see cref="MatchSnapshot"/>. Rebuilding
    /// validates everything and finally runs <see cref="MatchInvariants"/>, so a corrupt or foreign snapshot
    /// is refused rather than repaired (GDD §22 "a snapshot that fails to load is discarded, never fixed up").
    /// </summary>
    internal static class MatchSnapshots
    {
        internal static MatchSnapshot Capture(MatchState s)
        {
            var seats = new SeatSnapshot[SeatExtensions.Count];
            for (int i = 0; i < seats.Length; i++)
            {
                SeatProfile profile = s.Profiles[i];
                seats[i] = new SeatSnapshot
                {
                    name = profile.Name,
                    avatarId = profile.AvatarId,
                    deckThemeId = profile.DeckThemeId,
                    isBot = profile.IsBot,
                    hand = Ids(s.Hands[i]),
                    call = s.Calls[i],
                    tricksWon = s.Won[i],
                    ready = s.Ready[i],
                };
            }

            var completed = new TrickSnapshot[s.Completed.Count];
            for (int t = 0; t < completed.Length; t++)
            {
                completed[t] = Capture(s.Completed[t].Trick);
            }

            var rounds = new RoundSnapshot[s.Scores.Rounds.Count];
            for (int r = 0; r < rounds.Length; r++)
            {
                RoundResult result = s.Scores.Rounds[r];
                var data = new RoundSnapshot
                {
                    round = result.Round,
                    calls = new int[SeatExtensions.Count],
                    won = new int[SeatExtensions.Count],
                    deltas = new int[SeatExtensions.Count],
                };
                for (int i = 0; i < SeatExtensions.Count; i++)
                {
                    SeatRoundResult line = result[(Seat)i];
                    data.calls[i] = line.Call;
                    data.won[i] = line.Won;
                    data.deltas[i] = line.DeltaTenths;
                }

                rounds[r] = data;
            }

            return new MatchSnapshot
            {
                schemaVersion = MatchSnapshot.CurrentSchemaVersion,
                ruleSetId = s.Rules.Id,
                matchId = s.MatchId,
                phase = (int)s.Phase,
                round = s.Round,
                hasFixedFirstDealer = s.HasFixedFirstDealer,
                firstDealer = (int)s.FirstDealer,
                dealer = (int)s.Dealer,
                currentSeat = (int)s.CurrentSeat,
                currentTurn = s.CurrentTurn.Value,
                turnCounter = s.TurnCounter,
                seats = seats,
                currentTrick = Capture(s.Trick),
                completedTricks = completed,
                rounds = rounds,
                matchEnded = s.Standings.Count > 0,
            };
        }

        internal static bool TryRebuild(MatchSnapshot snap, RuleSet rules, out MatchState state, out string error)
        {
            state = null;
            error = CheckHeader(snap, rules);
            if (error != null)
            {
                return false;
            }

            try
            {
                MatchState rebuilt = Rebuild(snap, rules);
                string violation = MatchInvariants.FindViolation(rebuilt);
                if (violation != null)
                {
                    error = "Snapshot describes an impossible match: " + violation;
                    return false;
                }

                state = rebuilt;
                return true;
            }
            catch (ArgumentException e)
            {
                error = "Snapshot holds invalid data: " + e.Message;
                return false;
            }
            catch (InvalidOperationException e)
            {
                error = "Snapshot holds invalid data: " + e.Message;
                return false;
            }
        }

        private static string CheckHeader(MatchSnapshot snap, RuleSet rules)
        {
            if (snap == null)
            {
                return "Snapshot is null.";
            }

            if (snap.schemaVersion != MatchSnapshot.CurrentSchemaVersion)
            {
                return "Unsupported snapshot schema " + Text(snap.schemaVersion) + ".";
            }

            if (!string.Equals(snap.ruleSetId, rules.Id, StringComparison.Ordinal))
            {
                return "Snapshot rule set '" + snap.ruleSetId + "' does not match '" + rules.Id + "'.";
            }

            if (snap.matchId == null || snap.seats == null || snap.seats.Length != SeatExtensions.Count)
            {
                return "Snapshot needs a match id and four seats.";
            }

            if (!IsSeat(snap.firstDealer) || !IsSeat(snap.dealer) || !IsSeat(snap.currentSeat))
            {
                return "Snapshot seat numbers must be 0-3.";
            }

            var phase = (Phase)snap.phase;
            bool waiting = phase == Phase.Lobby || phase == Phase.Calling || phase == Phase.Playing
                || phase == Phase.RoundSummary || phase == Phase.MatchOver || phase == Phase.Closed;
            if (snap.phase < 0 || snap.phase > (int)Phase.Closed || !waiting)
            {
                return "Snapshot phase " + Text(snap.phase) + " is not a waiting phase.";
            }

            if (snap.currentTurn < 0 || snap.turnCounter < 0)
            {
                return "Snapshot turn ids must not be negative.";
            }

            for (int i = 0; i < snap.seats.Length; i++)
            {
                if (snap.seats[i] == null || snap.seats[i].name == null)
                {
                    return "Snapshot seat " + Text(i) + " has no profile.";
                }
            }

            return null;
        }

        private static MatchState Rebuild(MatchSnapshot snap, RuleSet rules)
        {
            var profiles = new SeatProfile[SeatExtensions.Count];
            for (int i = 0; i < profiles.Length; i++)
            {
                SeatSnapshot seat = snap.seats[i];
                profiles[i] = new SeatProfile(seat.name, seat.avatarId ?? string.Empty, seat.deckThemeId ?? string.Empty, seat.isBot);
            }

            var state = new MatchState(
                rules,
                snap.matchId,
                Array.AsReadOnly(profiles),
                snap.hasFixedFirstDealer,
                (Seat)snap.firstDealer);
            state.Phase = (Phase)snap.phase;
            state.Round = snap.round;
            state.Dealer = (Seat)snap.dealer;
            state.CurrentSeat = (Seat)snap.currentSeat;
            state.CurrentTurn = new TurnId(snap.currentTurn);
            state.TurnCounter = snap.turnCounter;

            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                SeatSnapshot seat = snap.seats[i];
                state.Hands[i] = SetOf(seat.hand);
                state.Calls[i] = seat.call;
                state.Won[i] = seat.tricksWon;
                state.Ready[i] = seat.ready;
            }

            TrickSnapshot[] completed = snap.completedTricks ?? new TrickSnapshot[0];
            if (completed.Length > Deck.HandSize)
            {
                throw new ArgumentException("More than 13 completed tricks.");
            }

            for (int t = 0; t < completed.Length; t++)
            {
                Trick trick = TrickOf(completed[t], rules.Direction);
                if (!trick.IsComplete)
                {
                    throw new ArgumentException("Completed trick " + Text(t) + " does not hold four cards.");
                }

                state.Completed.Add(new CompletedTrick(t, trick));
            }

            state.Trick = TrickOf(snap.currentTrick, rules.Direction);

            RoundSnapshot[] rounds = snap.rounds ?? new RoundSnapshot[0];
            ScoreSheet scores = ScoreSheet.Empty;
            for (int r = 0; r < rounds.Length; r++)
            {
                RoundSnapshot round = rounds[r];
                if (round == null || round.round != r + 1)
                {
                    throw new ArgumentException("Round " + Text(r + 1) + " is missing or out of order.");
                }

                scores = scores.Append(round.calls, round.won, rules);
                for (int i = 0; i < SeatExtensions.Count; i++)
                {
                    if (round.deltas == null || round.deltas.Length != SeatExtensions.Count
                        || scores.Rounds[r][(Seat)i].DeltaTenths != round.deltas[i])
                    {
                        throw new ArgumentException("Round " + Text(r + 1) + " deltas do not follow the rule set.");
                    }
                }
            }

            state.Scores = scores;
            if (snap.matchEnded)
            {
                state.Standings = Standings.Compute(scores);
            }

            return state;
        }

        private static TrickSnapshot Capture(Trick trick)
        {
            var cards = new int[trick.Count];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = trick[i].Id;
            }

            return new TrickSnapshot { leader = (int)trick.Leader, cards = cards };
        }

        private static Trick TrickOf(TrickSnapshot data, Direction direction)
        {
            if (data == null)
            {
                throw new ArgumentException("A trick is missing.");
            }

            if (!IsSeat(data.leader))
            {
                throw new ArgumentException("Trick leader " + Text(data.leader) + " is not a seat.");
            }

            var trick = new Trick((Seat)data.leader, direction);
            int[] cards = data.cards ?? new int[0];
            if (cards.Length > Trick.Size)
            {
                throw new ArgumentException("A trick holds at most four cards.");
            }

            for (int i = 0; i < cards.Length; i++)
            {
                trick = trick.With(Card.FromId(cards[i]));
            }

            return trick;
        }

        private static int[] Ids(CardSet set)
        {
            var ids = new int[set.Count];
            int i = 0;
            foreach (Card card in set)
            {
                ids[i++] = card.Id;
            }

            return ids;
        }

        private static CardSet SetOf(int[] ids)
        {
            CardSet set = CardSet.Empty;
            if (ids == null)
            {
                return set;
            }

            for (int i = 0; i < ids.Length; i++)
            {
                Card card = Card.FromId(ids[i]);
                if (set.Contains(card))
                {
                    throw new ArgumentException("Card " + card + " appears twice in one hand.");
                }

                set = set.With(card);
            }

            return set;
        }

        private static bool IsSeat(int value)
        {
            return value >= 0 && value < SeatExtensions.Count;
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
