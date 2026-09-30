namespace Callbreak.Core.Commands
{
    /// <summary>
    /// Why a command was rejected. Listed in the order the engine checks them (GDD §7 "Intent
    /// validation"): phase, then seat, then turn id, then payload. A rejected command never changes state.
    /// </summary>
    public enum RejectReason : byte
    {
        /// <summary>Not rejected (the value carried by an accepted result).</summary>
        None = 0,

        /// <summary>(1) The current phase does not accept this command.</summary>
        WrongPhase = 1,

        /// <summary>(2) The seat is not one of the four defined seats.</summary>
        InvalidSeat = 2,

        /// <summary>(2) It is another seat's turn.</summary>
        NotYourTurn = 3,

        /// <summary>(3) The command's turn id is not the open turn: a late, retried or double-tapped intent.</summary>
        StaleTurn = 4,

        /// <summary>(4) The call is outside MinCall…MaxCall.</summary>
        InvalidCall = 5,

        /// <summary>(4) The card is not in the seat's hand.</summary>
        CardNotInHand = 6,

        /// <summary>(4) The card is in hand but breaks a follow, beat or trump obligation (GDD §3).</summary>
        IllegalCard = 7,

        /// <summary>(4) The seat is already ready for the next round.</summary>
        AlreadyReady = 8,
    }
}
