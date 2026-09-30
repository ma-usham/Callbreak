namespace Callbreak.Core.Cards
{
    /// <summary>
    /// Card ranks, valued so that a higher number always beats a lower one of the same suit
    /// (GDD §2 "A K Q J 10 9 8 7 6 5 4 3 2"; Appendix A: J = 11, Q = 12, K = 13, A = 14).
    /// </summary>
    public enum Rank : byte
    {
        /// <summary>2, the lowest rank.</summary>
        Two = 2,

        /// <summary>3.</summary>
        Three = 3,

        /// <summary>4.</summary>
        Four = 4,

        /// <summary>5.</summary>
        Five = 5,

        /// <summary>6.</summary>
        Six = 6,

        /// <summary>7.</summary>
        Seven = 7,

        /// <summary>8.</summary>
        Eight = 8,

        /// <summary>9.</summary>
        Nine = 9,

        /// <summary>10.</summary>
        Ten = 10,

        /// <summary>Jack.</summary>
        Jack = 11,

        /// <summary>Queen.</summary>
        Queen = 12,

        /// <summary>King.</summary>
        King = 13,

        /// <summary>Ace, the highest rank.</summary>
        Ace = 14,
    }
}
