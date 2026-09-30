using System.Globalization;
using Callbreak.Core.Match;

namespace Callbreak.Core.Commands
{
    /// <summary>
    /// A seat's call for the round (GDD §3 "The call"; §25 ServerRpc PlaceCall(call, turnId)). Accepted only
    /// in <see cref="Phase.Calling"/>, from the current seat, for the open turn, with a call in range.
    /// </summary>
    public readonly struct PlaceCall
    {
        /// <summary>Creates the command.</summary>
        /// <param name="seat">The calling seat (on a server, the seat bound to the sender).</param>
        /// <param name="call">Tricks the seat commits to win.</param>
        /// <param name="turnId">The open call turn, from <c>CallTurnStarted</c> or <c>MatchEngine.CurrentTurn</c>.</param>
        /// <param name="isAuto">True when a timer or bot called for a human; shown as "(auto)" (GDD §8).</param>
        public PlaceCall(Seat seat, int call, TurnId turnId, bool isAuto = false)
        {
            Seat = seat;
            Call = call;
            TurnId = turnId;
            IsAuto = isAuto;
        }

        /// <summary>The calling seat.</summary>
        public Seat Seat { get; }

        /// <summary>The call.</summary>
        public int Call { get; }

        /// <summary>The turn this call answers.</summary>
        public TurnId TurnId { get; }

        /// <summary>True when made by a timer or bot on a human's behalf.</summary>
        public bool IsAuto { get; }

        /// <summary>E.g. "PlaceCall seat=South call=4 turn=1 auto=0".</summary>
        /// <returns>The command's text.</returns>
        public override string ToString()
        {
            return "PlaceCall seat=" + Seat + " call=" + Call.ToString(CultureInfo.InvariantCulture)
                + " turn=" + TurnId + " auto=" + (IsAuto ? "1" : "0");
        }
    }
}
