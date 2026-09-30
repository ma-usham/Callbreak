using System.Globalization;
using Callbreak.Core.Match;

namespace Callbreak.Core.Events
{
    /// <summary>A seat's call, public and final (GDD §3; §22: seat, call, isAuto). Public.</summary>
    public sealed class CallPlaced : MatchEvent
    {
        /// <summary>Creates the event.</summary>
        /// <param name="seat">The calling seat.</param>
        /// <param name="call">The call.</param>
        /// <param name="isAuto">True when a timer or bot called for a human ("Bid 3 · auto").</param>
        public CallPlaced(Seat seat, int call, bool isAuto)
            : base(EventVisibility.Public)
        {
            Seat = seat;
            Call = call;
            IsAuto = isAuto;
        }

        /// <summary>The calling seat.</summary>
        public Seat Seat { get; }

        /// <summary>The call.</summary>
        public int Call { get; }

        /// <summary>True when a timer or bot called for a human.</summary>
        public bool IsAuto { get; }

        private protected override string Describe()
        {
            return "CallPlaced seat=" + Seat + " call=" + Call.ToString(CultureInfo.InvariantCulture) + " auto=" + (IsAuto ? "1" : "0");
        }
    }
}
