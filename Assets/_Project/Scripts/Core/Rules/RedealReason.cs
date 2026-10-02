namespace Callbreak.Core.Rules
{
    /// <summary>Why a deal was collected and dealt again by the same dealer (GDD §2 "Redeal").</summary>
    public enum RedealReason : byte
    {
        /// <summary>No redeal is needed.</summary>
        None = 0,

        /// <summary>A hand had no spade (<see cref="RuleSet.RedealIfNoSpade"/>).</summary>
        NoSpade = 1,

        /// <summary>A hand had no card above 10 (<see cref="RuleSet.RedealIfNoHonour"/>).</summary>
        NoHonour = 2,

        /// <summary>
        /// Online only (ADR 030): the host and another player were lost at once, so nobody could tell
        /// whose cards were whose, and the round in progress was dealt again. The engine never gives
        /// this reason itself.
        /// </summary>
        HostLost = 3,
    }
}
