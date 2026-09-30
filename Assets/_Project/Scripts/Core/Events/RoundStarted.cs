using System;
using System.Globalization;
using Callbreak.Core.Match;

namespace Callbreak.Core.Events
{
    /// <summary>A round began with this dealer, before any card is dealt (GDD §22: round number, dealer seat). Public.</summary>
    public sealed class RoundStarted : MatchEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="round">The round number, starting at 1.</param>
        /// <param name="dealer">The dealer; the seat after it calls and leads first.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="round"/> is below 1.</exception>
        public RoundStarted(int round, Seat dealer)
            : base(EventVisibility.Public)
        {
            if (round < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(round), round, "Rounds are numbered from 1.");
            }

            Round = round;
            Dealer = dealer;
        }

        /// <summary>The round number, starting at 1.</summary>
        public int Round { get; }

        /// <summary>The dealer.</summary>
        public Seat Dealer { get; }

        private protected override string Describe()
        {
            return "RoundStarted round=" + Round.ToString(CultureInfo.InvariantCulture) + " dealer=" + Dealer;
        }
    }
}
