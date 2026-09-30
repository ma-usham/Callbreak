using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Callbreak.Core.Match
{
    /// <summary>
    /// Everything a new match needs besides rules and randomness (GDD §22 "MatchEngine(RuleSet, IRandom,
    /// MatchSetup) // seats, first dealer"): the match id, the four seat profiles and, optionally, a fixed
    /// round-1 dealer. Immutable.
    /// </summary>
    public sealed class MatchSetup
    {
        private readonly ReadOnlyCollection<SeatProfile> _seats;

        /// <summary>Creates a setup.</summary>
        /// <param name="matchId">Id carried by <c>MatchStarted</c>, snapshots and logs (GDD §17 "every log line carries the matchId").</param>
        /// <param name="seats">Four profiles, indexed by seat number (South, East, North, West).</param>
        /// <param name="firstDealer">
        /// The round-1 dealer, or null to draw it from the engine's <see cref="Randomness.IRandom"/> (GDD §2
        /// "The round-1 dealer is chosen at random").
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="matchId"/>, <paramref name="seats"/> or a profile is null.</exception>
        /// <exception cref="ArgumentException">There are not exactly four seats.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="firstDealer"/> is not a defined seat.</exception>
        public MatchSetup(string matchId, IReadOnlyList<SeatProfile> seats, Seat? firstDealer = null)
        {
            if (seats == null)
            {
                throw new ArgumentNullException(nameof(seats));
            }

            if (seats.Count != SeatExtensions.Count)
            {
                throw new ArgumentException("A match has exactly four seats.", nameof(seats));
            }

            var copy = new SeatProfile[SeatExtensions.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = seats[i] ?? throw new ArgumentNullException(nameof(seats), "Seat " + i + " has no profile.");
            }

            if (firstDealer.HasValue && !firstDealer.Value.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(firstDealer), firstDealer.Value, "Seat must be 0-3.");
            }

            MatchId = matchId ?? throw new ArgumentNullException(nameof(matchId));
            _seats = new ReadOnlyCollection<SeatProfile>(copy);
            FirstDealer = firstDealer;
        }

        /// <summary>The match id.</summary>
        public string MatchId { get; }

        /// <summary>The four profiles, indexed by seat number.</summary>
        public IReadOnlyList<SeatProfile> Seats => _seats;

        /// <summary>The fixed round-1 dealer, or null for a random one.</summary>
        public Seat? FirstDealer { get; }
    }
}
