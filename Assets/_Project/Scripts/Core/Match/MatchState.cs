using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;

namespace Callbreak.Core.Match
{
    /// <summary>
    /// The full truth of a match (GDD §22 "MatchState · RoundState · TrickState: engine-private classes").
    /// Only <see cref="MatchEngine"/> mutates it; <see cref="MatchInvariants"/> and
    /// <see cref="MatchSnapshots"/> read it. Never exposed outside Core.
    /// </summary>
    internal sealed class MatchState
    {
        internal readonly RuleSet Rules;
        internal readonly string MatchId;
        internal readonly IReadOnlyList<SeatProfile> Profiles;
        internal readonly bool HasFixedFirstDealer;
        internal readonly CardSet[] Hands = new CardSet[SeatExtensions.Count];
        internal readonly int[] Calls = new int[SeatExtensions.Count];
        internal readonly int[] Won = new int[SeatExtensions.Count];
        internal readonly bool[] Ready = new bool[SeatExtensions.Count];
        internal readonly List<CompletedTrick> Completed = new List<CompletedTrick>(Deck.HandSize);

        internal Seat FirstDealer;
        internal Phase Phase;
        internal int Round;
        internal Seat Dealer;
        internal Seat CurrentSeat;
        internal TurnId CurrentTurn;
        internal int TurnCounter;
        internal Trick Trick;
        internal ScoreSheet Scores = ScoreSheet.Empty;
        internal IReadOnlyList<Standing> Standings = Array.Empty<Standing>();

        internal MatchState(RuleSet rules, string matchId, IReadOnlyList<SeatProfile> profiles, bool hasFixedFirstDealer, Seat firstDealer)
        {
            Rules = rules;
            MatchId = matchId;
            Profiles = profiles;
            HasFixedFirstDealer = hasFixedFirstDealer;
            FirstDealer = firstDealer;
            Phase = Phase.Lobby;
            Trick = new Trick(Seat.South, rules.Direction);
            ClearRound();
        }

        internal Direction Direction => Rules.Direction;

        /// <summary>The seat on the dealer's right: first to receive a card, call and lead (GDD §2).</summary>
        internal Seat FirstSeat => Dealer.Next(Rules.Direction);

        internal void ClearRound()
        {
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                Hands[i] = CardSet.Empty;
                Calls[i] = CallRules.NotCalled;
                Won[i] = 0;
                Ready[i] = false;
            }

            Completed.Clear();
        }

        internal int CallsPlaced()
        {
            int placed = 0;
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                if (Calls[i] != CallRules.NotCalled)
                {
                    placed++;
                }
            }

            return placed;
        }
    }
}
