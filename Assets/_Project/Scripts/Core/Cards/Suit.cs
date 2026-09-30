namespace Callbreak.Core.Cards
{
    /// <summary>
    /// The four suits in card-id order (GDD Appendix A "Card id encoding": ♣ 0 · ♦ 1 · ♥ 2 · ♠ 3).
    /// Spades are permanent trump (GDD §2).
    /// </summary>
    public enum Suit : byte
    {
        /// <summary>♣, ids 0–12.</summary>
        Clubs = 0,

        /// <summary>♦, ids 13–25.</summary>
        Diamonds = 1,

        /// <summary>♥, ids 26–38.</summary>
        Hearts = 2,

        /// <summary>♠, ids 39–51. Always trump.</summary>
        Spades = 3,
    }
}
