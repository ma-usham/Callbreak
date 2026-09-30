using System.Globalization;
using Callbreak.Core.Rules;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// A deal was collected and the same dealer shuffles and deals again (GDD §2 "Redeal": the toast "A hand
    /// had no spades — redealing"). One event per redeal. Public; the discarded hands are never sent.
    /// </summary>
    public sealed class Redealt : MatchEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="round">The round being dealt.</param>
        /// <param name="reason">Why the deal was discarded.</param>
        public Redealt(int round, RedealReason reason)
            : base(EventVisibility.Public)
        {
            Round = round;
            Reason = reason;
        }

        /// <summary>The round being dealt.</summary>
        public int Round { get; }

        /// <summary>NoSpade or NoHonour.</summary>
        public RedealReason Reason { get; }

        private protected override string Describe()
        {
            return "Redealt round=" + Round.ToString(CultureInfo.InvariantCulture) + " reason=" + Reason;
        }
    }
}
