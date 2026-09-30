using System;

namespace Callbreak.Core.Views
{
    /// <summary>One seat inside a <see cref="MatchSnapshot"/>. Plain serializable data.</summary>
    [Serializable]
    public sealed class SeatSnapshot
    {
        /// <summary>Display name.</summary>
        public string name;

        /// <summary>Avatar catalogue id.</summary>
        public string avatarId;

        /// <summary>Deck theme id.</summary>
        public string deckThemeId;

        /// <summary>True for a bot seat.</summary>
        public bool isBot;

        /// <summary>The seat's cards as ids 0–51, ascending. Secret: snapshots never leave the authority.</summary>
        public int[] hand;

        /// <summary>This round's call, or −1 before the seat calls.</summary>
        public int call;

        /// <summary>Tricks won this round.</summary>
        public int tricksWon;

        /// <summary>Ready flag in the round summary.</summary>
        public bool ready;
    }
}
