using System;
using Callbreak.Application.Sessions;
using Callbreak.Bots;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;

namespace Callbreak.Application.Online
{
    /// <summary>What a hosted table is built from: who sits where, the rules and the clocks.</summary>
    public sealed class TableHostSetup
    {
        public TableHostSetup(
            RuleSet rules,
            string matchId,
            SeatProfile[] seats,
            bool[] humans,
            int turnSeconds,
            ulong seed,
            BotLevel fillLevel = BotLevel.Normal,
            BotTiming timing = null,
            double summarySeconds = Protocol.SummarySeconds)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            MatchId = matchId ?? throw new ArgumentNullException(nameof(matchId));
            if (seats == null || seats.Length != SeatExtensions.Count)
            {
                throw new ArgumentException("A table has four seats.", nameof(seats));
            }

            if (humans == null || humans.Length != SeatExtensions.Count)
            {
                throw new ArgumentException("A table has four seats.", nameof(humans));
            }

            Seats = seats;
            Humans = humans;
            TurnSeconds = turnSeconds;
            Seed = seed;
            FillLevel = fillLevel;
            Timing = timing ?? BotTiming.Offline;
            SummarySeconds = summarySeconds;
        }

        public RuleSet Rules { get; }

        public string MatchId { get; }

        /// <summary>Four profiles by seat number; a bot profile for every seat without a person.</summary>
        public SeatProfile[] Seats { get; }

        /// <summary>True for the seats a connected person plays.</summary>
        public bool[] Humans { get; }

        /// <summary>The turn clock (15 or 30, GDD §11 screen 02); 0 runs the table without a clock.</summary>
        public int TurnSeconds { get; }

        /// <summary>The shuffle seed; it never leaves the host (GDD §22).</summary>
        public ulong Seed { get; }

        /// <summary>The level of the bots that fill empty seats (GDD §11 screen 05: Normal).</summary>
        public BotLevel FillLevel { get; }

        public BotTiming Timing { get; }

        /// <summary>The round summary's countdown (GDD §11 screen 09).</summary>
        public double SummarySeconds { get; }
    }
}
