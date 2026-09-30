using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// The match is over (GDD §22: standings with rank, total and shared flag, ranked with the §4
    /// tie-breaks). Public. Last event of a match; the results screen headline comes from this.
    /// </summary>
    public sealed class MatchEnded : MatchEvent
    {
        private readonly ReadOnlyCollection<Standing> _standings;

        /// <summary>Creates the event.</summary>
        /// <param name="standings">Four standings sorted by rank, then seat.</param>
        /// <exception cref="ArgumentNullException"><paramref name="standings"/> is null.</exception>
        /// <exception cref="ArgumentException">There are not four standings.</exception>
        public MatchEnded(IReadOnlyList<Standing> standings)
            : base(EventVisibility.Public)
        {
            if (standings == null)
            {
                throw new ArgumentNullException(nameof(standings));
            }

            if (standings.Count != SeatExtensions.Count)
            {
                throw new ArgumentException("Standings need one entry per seat.", nameof(standings));
            }

            var copy = new Standing[standings.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = standings[i];
            }

            _standings = new ReadOnlyCollection<Standing>(copy);
        }

        /// <summary>Four standings sorted by rank, then seat number.</summary>
        public IReadOnlyList<Standing> Standings => _standings;

        private protected override string Describe()
        {
            var text = new StringBuilder(160);
            text.Append("MatchEnded");
            for (int i = 0; i < _standings.Count; i++)
            {
                text.Append(i == 0 ? " " : " | ").Append(_standings[i].ToString());
            }

            return text.ToString();
        }
    }
}
