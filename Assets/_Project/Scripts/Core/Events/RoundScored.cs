using System;
using Callbreak.Core.Scoring;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// The 13th trick ended the round and it was scored (GDD §22: per seat call, won, delta and total in
    /// tenths). Public. The round results screen is built only from this (GDD §11 screen 09).
    /// </summary>
    public sealed class RoundScored : MatchEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="result">The scored round.</param>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        public RoundScored(RoundResult result)
            : base(EventVisibility.Public)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        /// <summary>The round number, starting at 1.</summary>
        public int Round => Result.Round;

        /// <summary>Call, won, delta and total for each seat.</summary>
        public RoundResult Result { get; }

        private protected override string Describe()
        {
            return "RoundScored " + Result;
        }
    }
}
