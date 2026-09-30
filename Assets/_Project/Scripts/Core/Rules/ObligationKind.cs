namespace Callbreak.Core.Rules
{
    /// <summary>
    /// What the rules require of the player about to act, matching the turn-prompt copy of GDD §11
    /// screen 08. Core returns this enum; the UI owns the localized text.
    /// </summary>
    public enum ObligationKind : byte
    {
        /// <summary>Leading the trick: any card, spades included ("Your turn · Lead any card").</summary>
        LeadAny = 0,

        /// <summary>Following and able to beat: only higher cards of the led suit ("Beat Q♥").</summary>
        BeatCard = 1,

        /// <summary>Following but unable to beat, or the trick is trumped: any led-suit card ("Follow ♥").</summary>
        FollowSuit = 2,

        /// <summary>Void in the led suit and must play a spade ("Trump with a spade").</summary>
        TrumpWithSpade = 3,

        /// <summary>No obligation: void and cannot or need not trump ("Any card").</summary>
        AnyCard = 4,
    }
}
