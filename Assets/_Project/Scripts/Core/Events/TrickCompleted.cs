using System.Globalization;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// The fourth card resolved a trick (GDD §22: winner seat, trick index, the 4 cards). Public. The winner's
    /// "Won" count rises by one and the winner leads the next trick.
    /// </summary>
    public sealed class TrickCompleted : MatchEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="completed">The finished trick.</param>
        public TrickCompleted(CompletedTrick completed)
            : base(EventVisibility.Public)
        {
            Completed = completed;
        }

        /// <summary>The finished trick with its index and winner.</summary>
        public CompletedTrick Completed { get; }

        /// <summary>The trick's index in the round, 0–12 ("Trick 1 / 13" is index 0).</summary>
        public int TrickIndex => Completed.Index;

        /// <summary>The four cards in play order, with the leader.</summary>
        public Trick Trick => Completed.Trick;

        /// <summary>The seat that won the trick.</summary>
        public Seat Winner => Completed.Winner;

        private protected override string Describe()
        {
            return "TrickCompleted trick=" + TrickIndex.ToString(CultureInfo.InvariantCulture) + " " + Trick + " winner=" + Winner;
        }
    }
}
