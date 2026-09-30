using System.Globalization;
using Callbreak.Core.Cards;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;

namespace Callbreak.Core.Match
{
    /// <summary>
    /// The GDD §22 invariants ("asserted after every command"), checked against the live state. Also used
    /// to reject a snapshot that describes an impossible match. Each check returns a description of the
    /// first violation it finds, or null.
    /// </summary>
    internal static class MatchInvariants
    {
        internal static string FindViolation(MatchState s)
        {
            string violation = CheckTurn(s);
            if (violation != null)
            {
                return violation;
            }

            if (s.Round == 0)
            {
                return CheckNotStarted(s);
            }

            if (s.Round < 0 || s.Round > s.Rules.Rounds)
            {
                return "Round " + Text(s.Round) + " is outside 1-" + Text(s.Rules.Rounds) + ".";
            }

            if (s.Phase == Phase.Lobby)
            {
                return "The match is in Lobby after round 1 began.";
            }

            return CheckCards(s)
                ?? CheckTricks(s)
                ?? CheckCalls(s)
                ?? CheckPhase(s)
                ?? CheckScores(s)
                ?? CheckStandings(s);
        }

        private static string CheckTurn(MatchState s)
        {
            if (!s.FirstDealer.IsValid() || !s.Dealer.IsValid() || !s.CurrentSeat.IsValid())
            {
                return "A seat field is outside 0-3.";
            }

            if (s.Phase == Phase.Dealing || s.Phase == Phase.TrickResolve || s.Phase == Phase.RoundScoring)
            {
                return "Transient phase " + s.Phase + " is visible between commands.";
            }

            if (s.Phase > Phase.Closed)
            {
                return "Unknown phase " + Text((int)s.Phase) + ".";
            }

            // turnId strictly increases: the open turn is always the newest one issued.
            if (s.CurrentTurn.Value > s.TurnCounter)
            {
                return "Open turn " + s.CurrentTurn + " is newer than the last issued turn " + Text(s.TurnCounter) + ".";
            }

            bool awaitingTurn = s.Phase == Phase.Calling || s.Phase == Phase.Playing;
            if (awaitingTurn && (s.CurrentTurn.IsNone || s.CurrentTurn.Value != s.TurnCounter))
            {
                return "In " + s.Phase + " the open turn must be the newest turn (" + Text(s.TurnCounter) + "), not " + s.CurrentTurn + ".";
            }

            if (!awaitingTurn && !s.CurrentTurn.IsNone)
            {
                return "Turn " + s.CurrentTurn + " is open in " + s.Phase + ".";
            }

            return null;
        }

        private static string CheckNotStarted(MatchState s)
        {
            if (s.Phase != Phase.Lobby && s.Phase != Phase.Closed)
            {
                return s.Phase + " before round 1.";
            }

            if (s.TurnCounter != 0 || s.Completed.Count != 0 || !s.Trick.IsEmpty || s.Scores.Rounds.Count != 0 || s.Standings.Count != 0)
            {
                return "State exists before the match started.";
            }

            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                if (!s.Hands[i].IsEmpty || s.Calls[i] != CallRules.NotCalled || s.Won[i] != 0)
                {
                    return "Seat " + (Seat)i + " has round state before the match started.";
                }
            }

            return null;
        }

        private static string CheckCards(MatchState s)
        {
            Seat expectedDealer = s.FirstDealer.Advance(s.Direction, s.Round - 1);
            if (s.Dealer != expectedDealer)
            {
                return "Round " + Text(s.Round) + " is dealt by " + s.Dealer + ", expected " + expectedDealer + ".";
            }

            // All 52 cards accounted for exactly once: hands, current trick and completed tricks.
            CardSet seen = CardSet.Empty;
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                if (seen.Overlaps(s.Hands[i]))
                {
                    return "Cards " + seen.Intersect(s.Hands[i]) + " are held twice.";
                }

                seen = seen.Union(s.Hands[i]);
            }

            CardSet inTrick = s.Trick.Cards;
            if (seen.Overlaps(inTrick))
            {
                return "Cards " + seen.Intersect(inTrick) + " are both held and in the current trick.";
            }

            seen = seen.Union(inTrick);
            for (int t = 0; t < s.Completed.Count; t++)
            {
                CardSet played = s.Completed[t].Trick.Cards;
                if (seen.Overlaps(played))
                {
                    return "Cards " + seen.Intersect(played) + " of trick " + Text(t) + " appear twice.";
                }

                seen = seen.Union(played);
            }

            if (seen != CardSet.FullDeck)
            {
                return "Cards " + CardSet.FullDeck.Except(seen) + " are missing.";
            }

            // Each hand holds 13 - completed tricks, one fewer after playing to the current trick.
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                int expected = Deck.HandSize - s.Completed.Count - (s.Trick.TryGetCardOf((Seat)i, out _) ? 1 : 0);
                if (s.Hands[i].Count != expected)
                {
                    return (Seat)i + " holds " + Text(s.Hands[i].Count) + " cards, expected " + Text(expected) + ".";
                }
            }

            return null;
        }

        private static string CheckTricks(MatchState s)
        {
            int done = s.Completed.Count;
            if (done > Deck.HandSize)
            {
                return Text(done) + " tricks completed in one round.";
            }

            var won = new int[SeatExtensions.Count];
            Seat leader = s.FirstSeat;
            for (int t = 0; t < done; t++)
            {
                CompletedTrick trick = s.Completed[t];
                if (trick.Index != t || !trick.Trick.IsComplete || trick.Trick.Direction != s.Direction)
                {
                    return "Completed trick " + Text(t) + " is malformed: " + trick + ".";
                }

                if (trick.Leader != leader)
                {
                    return "Trick " + Text(t) + " was led by " + trick.Leader + ", expected " + leader + ".";
                }

                won[(int)trick.Winner]++;
                leader = trick.Winner;
            }

            if (s.Trick.Leader != leader || s.Trick.Direction != s.Direction)
            {
                return "The current trick is led by " + s.Trick.Leader + ", expected " + leader + ".";
            }

            if (s.Trick.IsComplete || (done == Deck.HandSize && !s.Trick.IsEmpty))
            {
                return "The current trick " + s.Trick + " should already be resolved.";
            }

            // Tricks won sum to tricks completed, seat by seat.
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                if (s.Won[i] != won[i])
                {
                    return (Seat)i + " is credited " + Text(s.Won[i]) + " tricks but won " + Text(won[i]) + ".";
                }
            }

            return null;
        }

        private static string CheckCalls(MatchState s)
        {
            // Calls are placed in turn order from the dealer's right, each within MinCall..MaxCall.
            int placed = 0;
            bool gap = false;
            Seat seat = s.FirstSeat;
            for (int k = 0; k < SeatExtensions.Count; k++)
            {
                int call = s.Calls[(int)seat];
                if (call == CallRules.NotCalled)
                {
                    gap = true;
                }
                else
                {
                    if (gap)
                    {
                        return seat + " called out of turn order.";
                    }

                    if (!CallRules.IsValid(call, s.Rules))
                    {
                        return seat + " called " + Text(call) + ", outside " + Text(s.Rules.MinCall) + "-" + Text(s.Rules.MaxCall) + ".";
                    }

                    placed++;
                }

                seat = seat.Next(s.Direction);
            }

            bool cardsPlayed = s.Completed.Count > 0 || !s.Trick.IsEmpty;
            if (cardsPlayed && placed != SeatExtensions.Count)
            {
                return "A card was played before all four calls were placed.";
            }

            return null;
        }

        private static string CheckPhase(MatchState s)
        {
            int placed = s.CallsPlaced();
            int done = s.Completed.Count;
            switch (s.Phase)
            {
                case Phase.Calling:
                {
                    Seat expected = s.FirstSeat.Advance(s.Direction, placed);
                    if (placed == SeatExtensions.Count || done != 0 || !s.Trick.IsEmpty)
                    {
                        return "Calling with " + Text(placed) + " calls placed and cards on the table.";
                    }

                    if (s.CurrentSeat != expected)
                    {
                        return "Call turn belongs to " + s.CurrentSeat + ", expected " + expected + ".";
                    }

                    return CheckNotReady(s);
                }

                case Phase.Playing:
                    if (done >= Deck.HandSize)
                    {
                        return "Playing after the 13th trick.";
                    }

                    if (s.CurrentSeat != s.Trick.NextSeat)
                    {
                        return "Play turn belongs to " + s.CurrentSeat + ", expected " + s.Trick.NextSeat + ".";
                    }

                    return CheckNotReady(s);

                case Phase.RoundSummary:
                case Phase.MatchOver:
                    if (done != Deck.HandSize)
                    {
                        return s.Phase + " after only " + Text(done) + " tricks.";
                    }

                    if (s.Phase == Phase.RoundSummary)
                    {
                        for (int i = 0; i < SeatExtensions.Count; i++)
                        {
                            if (s.Profiles[i].IsBot && !s.Ready[i])
                            {
                                return "Bot seat " + (Seat)i + " is not ready in the round summary.";
                            }
                        }
                    }

                    return null;

                default:
                    return null;
            }
        }

        private static string CheckNotReady(MatchState s)
        {
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                if (s.Ready[i])
                {
                    return (Seat)i + " is flagged ready during " + s.Phase + ".";
                }
            }

            return null;
        }

        private static string CheckScores(MatchState s)
        {
            int rounds = s.Scores.Rounds.Count;
            bool roundScored = s.Phase == Phase.RoundSummary
                || s.Phase == Phase.MatchOver
                || (s.Phase == Phase.Closed && rounds == s.Round && s.Completed.Count == Deck.HandSize);
            int expectedRounds = roundScored ? s.Round : s.Round - 1;
            if (rounds != expectedRounds)
            {
                return Text(rounds) + " rounds scored in round " + Text(s.Round) + " (" + s.Phase + ").";
            }

            // Each total equals the sum of that seat's round deltas; each delta follows the rules.
            var totals = new int[SeatExtensions.Count];
            for (int r = 0; r < rounds; r++)
            {
                RoundResult result = s.Scores.Rounds[r];
                if (result.Round != r + 1)
                {
                    return "Round result " + Text(r) + " is numbered " + Text(result.Round) + ".";
                }

                for (int i = 0; i < SeatExtensions.Count; i++)
                {
                    SeatRoundResult line = result[(Seat)i];
                    if (!CallRules.IsValid(line.Call, s.Rules)
                        || line.DeltaTenths != ScoreCalculator.RoundScore(line.Call, line.Won, s.Rules))
                    {
                        return "Round " + Text(r + 1) + " scores " + line + " against the rules.";
                    }

                    totals[i] += line.DeltaTenths;
                    if (line.TotalTenths != totals[i])
                    {
                        return "Total of " + (Seat)i + " after round " + Text(r + 1) + " is not the sum of its deltas.";
                    }
                }
            }

            if (roundScored)
            {
                RoundResult last = s.Scores.Rounds[rounds - 1];
                for (int i = 0; i < SeatExtensions.Count; i++)
                {
                    if (last[(Seat)i].Call != s.Calls[i] || last[(Seat)i].Won != s.Won[i])
                    {
                        return "The scored round disagrees with the calls and tricks on the table.";
                    }
                }
            }

            return null;
        }

        private static string CheckStandings(MatchState s)
        {
            // Standings exist exactly once MatchEnded was emitted; a table closed afterwards keeps them.
            bool ended = s.Standings.Count > 0;
            if (s.Phase == Phase.MatchOver && !ended)
            {
                return "MatchOver without final standings.";
            }

            if (!ended)
            {
                return null;
            }

            if (s.Phase != Phase.MatchOver && s.Phase != Phase.Closed)
            {
                return "Standings exist before the match ended.";
            }

            if (s.Round != s.Rules.Rounds || s.Scores.Rounds.Count != s.Rules.Rounds)
            {
                return "The match ended after round " + Text(s.Round) + " of " + Text(s.Rules.Rounds) + ".";
            }

            if (s.Standings.Count != SeatExtensions.Count)
            {
                return "Final standings need four entries.";
            }

            // Standings are sorted by rank (then seat) and consistent with the GDD §4 tie-breaks.
            int seenSeats = 0;
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                Standing standing = s.Standings[i];
                seenSeats |= 1 << (int)standing.Seat;
                if (standing.TotalTenths != s.Scores.TotalOf(standing.Seat)
                    || standing.RoundsMet != s.Scores.RoundsMetBy(standing.Seat)
                    || standing.BestRoundTenths != s.Scores.BestRoundOf(standing.Seat))
                {
                    return "Standing " + standing + " disagrees with the score sheet.";
                }

                if (i > 0)
                {
                    Standing previous = s.Standings[i - 1];
                    if (previous.Rank > standing.Rank || (previous.Rank == standing.Rank && previous.Seat > standing.Seat))
                    {
                        return "Standings are not sorted by rank and seat.";
                    }
                }

                int better = 0;
                bool shared = false;
                for (int j = 0; j < SeatExtensions.Count; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }

                    int order = Standings.Compare(s.Standings[j], standing);
                    better += order < 0 ? 1 : 0;
                    shared |= order == 0;
                }

                if (standing.Rank != better + 1 || standing.IsShared != shared)
                {
                    return "Standing " + standing + " breaks the tie-break rules.";
                }
            }

            return seenSeats == 0xF ? null : "Standings do not list every seat once.";
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
