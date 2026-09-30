using System;

namespace Callbreak.Core.Views
{
    /// <summary>
    /// One scored round inside a <see cref="MatchSnapshot"/>, per seat in seat order. Totals are not stored:
    /// they are the running sums of the deltas. Plain serializable data.
    /// </summary>
    [Serializable]
    public sealed class RoundSnapshot
    {
        /// <summary>The round number, starting at 1.</summary>
        public int round;

        /// <summary>Four calls.</summary>
        public int[] calls;

        /// <summary>Four trick counts, totalling 13.</summary>
        public int[] won;

        /// <summary>Four round scores in tenths; checked against the rule set on restore.</summary>
        public int[] deltas;
    }
}
