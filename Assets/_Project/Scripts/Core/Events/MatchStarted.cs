using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Callbreak.Core.Match;

namespace Callbreak.Core.Events
{
    /// <summary>
    /// The match began (GDD §22: seats with names, avatars, deck ids and bot flags; rule set id; first
    /// dealer). Public. First event of every match.
    /// </summary>
    public sealed class MatchStarted : MatchEvent
    {
        private readonly ReadOnlyCollection<SeatProfile> _seats;

        /// <summary>Creates the event.</summary>
        /// <param name="matchId">The match id.</param>
        /// <param name="ruleSetId">The rule set's id.</param>
        /// <param name="firstDealer">The round-1 dealer.</param>
        /// <param name="seats">The four profiles, indexed by seat number.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">There are not four seats.</exception>
        public MatchStarted(string matchId, string ruleSetId, Seat firstDealer, IReadOnlyList<SeatProfile> seats)
            : base(EventVisibility.Public)
        {
            if (seats == null)
            {
                throw new ArgumentNullException(nameof(seats));
            }

            if (seats.Count != SeatExtensions.Count)
            {
                throw new ArgumentException("A match has four seats.", nameof(seats));
            }

            var copy = new SeatProfile[SeatExtensions.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = seats[i] ?? throw new ArgumentNullException(nameof(seats));
            }

            MatchId = matchId ?? throw new ArgumentNullException(nameof(matchId));
            RuleSetId = ruleSetId ?? throw new ArgumentNullException(nameof(ruleSetId));
            FirstDealer = firstDealer;
            _seats = new ReadOnlyCollection<SeatProfile>(copy);
        }

        /// <summary>The match id.</summary>
        public string MatchId { get; }

        /// <summary>The rule set's id.</summary>
        public string RuleSetId { get; }

        /// <summary>The round-1 dealer.</summary>
        public Seat FirstDealer { get; }

        /// <summary>The four seat profiles, indexed by seat number.</summary>
        public IReadOnlyList<SeatProfile> Seats => _seats;

        private protected override string Describe()
        {
            var text = new StringBuilder(200);
            text.Append("MatchStarted match=").Append(MatchId)
                .Append(" rules=").Append(RuleSetId)
                .Append(" firstDealer=").Append(FirstDealer.ToString());
            for (int i = 0; i < _seats.Count; i++)
            {
                text.Append(" [").Append(((Seat)i).ToString()).Append(' ').Append(_seats[i].ToString()).Append(']');
            }

            return text.ToString();
        }
    }
}
