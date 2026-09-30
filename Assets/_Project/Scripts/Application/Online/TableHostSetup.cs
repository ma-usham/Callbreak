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
        /// <summary>
        /// A table whose call and play turns run on the same clock, as a private room's do
        /// (GDD §6: the host picks 15 or 30 s for both); 0 runs the table without a turn clock.
        /// </summary>
        public TableHostSetup(
            RuleSet rules,
            string matchId,
            SeatProfile[] seats,
            bool[] humans,
            int turnSeconds,
            ulong seed,
            BotLevel fillLevel = BotLevel.Normal,
            BotTiming timing = null,
            double summarySeconds = TableClocks.PrivateRoomSummarySeconds)
            : this(rules, matchId, seats, humans, new TableClocks(turnSeconds, turnSeconds, summarySeconds), seed, fillLevel, timing)
        {
        }

        /// <param name="clocks">The call timer, the card timer and the round-results countdown (GDD §6).</param>
        /// <param name="timing">How the bots pace themselves; null gives the hosted defaults.</param>
        public TableHostSetup(
            RuleSet rules,
            string matchId,
            SeatProfile[] seats,
            bool[] humans,
            TableClocks clocks,
            ulong seed,
            BotLevel fillLevel = BotLevel.Normal,
            BotTiming timing = null)
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
            Clocks = clocks ?? throw new ArgumentNullException(nameof(clocks));
            Seed = seed;
            FillLevel = fillLevel;
            Timing = timing ?? BotTiming.Hosted;
        }

        public RuleSet Rules { get; }

        public string MatchId { get; }

        /// <summary>Four profiles by seat number; a bot profile for every seat without a person.</summary>
        public SeatProfile[] Seats { get; }

        /// <summary>True for the seats a connected person plays.</summary>
        public bool[] Humans { get; }

        /// <summary>The table's clocks: one for call turns, one for play turns, one for the round results.</summary>
        public TableClocks Clocks { get; }

        /// <summary>The call timer in seconds (GDD §6); 0 runs call turns without a clock.</summary>
        public int CallSeconds => Clocks.CallSeconds;

        /// <summary>The card timer in seconds (GDD §6); 0 runs play turns without a clock.</summary>
        public int PlaySeconds => Clocks.PlaySeconds;

        /// <summary>The round summary's countdown (GDD §11 screen 09).</summary>
        public double SummarySeconds => Clocks.SummarySeconds;

        /// <summary>The shuffle seed; it never leaves the host (GDD §22).</summary>
        public ulong Seed { get; }

        /// <summary>The level of the bots that fill empty seats (GDD §11 screen 05: Normal).</summary>
        public BotLevel FillLevel { get; }

        /// <summary>How the table's bots pace themselves; <see cref="BotTiming.Hosted"/> unless given.</summary>
        public BotTiming Timing { get; }
    }
}
