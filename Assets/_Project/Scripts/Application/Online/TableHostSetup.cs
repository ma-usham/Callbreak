using System;
using Callbreak.Application.Sessions;
using Callbreak.Bots;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;

namespace Callbreak.Application.Online
{
    /// <summary>What a hosted table is built from: who sits where, the rules, the clocks and where its randomness comes from.</summary>
    public sealed class TableHostSetup
    {
        /// <summary>
        /// A table whose call and play turns run on the same clock, as a private room's do
        /// (GDD §6: the host picks 15 or 30 s for both); 0 runs the table without a turn clock.
        /// For tests and replays: every seed the table draws comes from a PCG stream seeded with
        /// <paramref name="seed"/>.
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
            : this(rules, matchId, seats, humans, new TableClocks(turnSeconds, turnSeconds, summarySeconds), SeedStream(seed), fillLevel, timing)
        {
        }

        /// <summary>As the seed source overload, with every seed from a PCG stream seeded with <paramref name="seed"/> (tests and replays).</summary>
        public TableHostSetup(
            RuleSet rules,
            string matchId,
            SeatProfile[] seats,
            bool[] humans,
            TableClocks clocks,
            ulong seed,
            BotLevel fillLevel = BotLevel.Normal,
            BotTiming timing = null)
            : this(rules, matchId, seats, humans, clocks, SeedStream(seed), fillLevel, timing)
        {
        }

        /// <param name="clocks">The call timer, the card timer and the round-results countdown (GDD §6).</param>
        /// <param name="seeds">
        /// Where the table's seeds come from: one per round's deal and one for each of the bots'
        /// generators, each drawn when it is needed. A CSPRNG in production (GDD §22).
        /// </param>
        /// <param name="timing">How the bots pace themselves; null gives the hosted defaults.</param>
        public TableHostSetup(
            RuleSet rules,
            string matchId,
            SeatProfile[] seats,
            bool[] humans,
            TableClocks clocks,
            Func<ulong> seeds,
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
            Seeds = seeds ?? throw new ArgumentNullException(nameof(seeds));
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

        /// <summary>The table's seed source; nothing it gives ever leaves the host (GDD §22).</summary>
        public Func<ulong> Seeds { get; }

        /// <summary>The level of the bots that fill empty seats (GDD §11 screen 05: Normal).</summary>
        public BotLevel FillLevel { get; }

        /// <summary>How the table's bots pace themselves; <see cref="BotTiming.Hosted"/> unless given.</summary>
        public BotTiming Timing { get; }

        /// <summary>A seed source for tests and replays: the values of a PCG stream seeded with <paramref name="seed"/>.</summary>
        private static Func<ulong> SeedStream(ulong seed)
        {
            var stream = new Pcg32Random(seed);
            return () =>
            {
                ulong high = stream.NextUInt32();
                return (high << 32) | stream.NextUInt32();
            };
        }
    }
}
