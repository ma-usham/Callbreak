using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Core.Commands
{
    /// <summary>
    /// A seat plays a card to the current trick (GDD §3; §25 ServerRpc PlayCard(cardId, turnId)). Accepted
    /// only in <see cref="Phase.Playing"/>, from the current seat, for the open turn, with a card that is in
    /// hand and in the legal set.
    /// </summary>
    public readonly struct PlayCard
    {
        /// <summary>Creates the command.</summary>
        /// <param name="seat">The playing seat (on a server, the seat bound to the sender).</param>
        /// <param name="card">The card to play.</param>
        /// <param name="turnId">The open play turn, from <c>PlayTurnStarted</c> or <c>MatchEngine.CurrentTurn</c>.</param>
        /// <param name="isAuto">True when a timer or bot played for a human; shown with an "auto" tag (GDD §8).</param>
        public PlayCard(Seat seat, Card card, TurnId turnId, bool isAuto = false)
        {
            Seat = seat;
            Card = card;
            TurnId = turnId;
            IsAuto = isAuto;
        }

        /// <summary>The playing seat.</summary>
        public Seat Seat { get; }

        /// <summary>The card.</summary>
        public Card Card { get; }

        /// <summary>The turn this play answers.</summary>
        public TurnId TurnId { get; }

        /// <summary>True when made by a timer or bot on a human's behalf.</summary>
        public bool IsAuto { get; }

        /// <summary>E.g. "PlayCard seat=South card=A♥ turn=9 auto=0".</summary>
        /// <returns>The command's text.</returns>
        public override string ToString()
        {
            return "PlayCard seat=" + Seat + " card=" + Card + " turn=" + TurnId + " auto=" + (IsAuto ? "1" : "0");
        }
    }
}
