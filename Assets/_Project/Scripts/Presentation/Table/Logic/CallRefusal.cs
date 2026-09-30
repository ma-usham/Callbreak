using Callbreak.Core.Commands;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// Why a call was not taken (GDD §11 screen 07: "On rejection, the reason is shown and the
    /// panel re-enables"): the line the call panel shows in place of its subtitle until the player
    /// chooses again.
    /// </summary>
    public static class CallRefusal
    {
        /// <summary>The turn the call was for had ended (the clock ran out and the call was made for the seat).</summary>
        public const string LateKey = "ui.bidding.refused.late";

        /// <summary>The call is outside what the rules allow.</summary>
        public const string InvalidKey = "ui.bidding.refused.invalid";

        /// <summary>The call could not be sent or got no answer (reconnecting, a failed request).</summary>
        public const string NotSentKey = "ui.bidding.refused.not_sent";

        public static string KeyFor(RejectReason reason)
        {
            switch (reason)
            {
                case RejectReason.StaleTurn:
                case RejectReason.NotYourTurn:
                    return LateKey;
                case RejectReason.InvalidCall:
                    return InvalidKey;
                default:
                    return NotSentKey;
            }
        }
    }
}
