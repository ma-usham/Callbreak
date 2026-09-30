using System;

namespace Callbreak.Core.Views
{
    /// <summary>A trick inside a <see cref="MatchSnapshot"/>: its leader and cards in play order. Plain serializable data.</summary>
    [Serializable]
    public sealed class TrickSnapshot
    {
        /// <summary>The leading seat, 0–3.</summary>
        public int leader;

        /// <summary>Card ids in play order: 0–3 entries for the trick in progress, 4 for a finished trick.</summary>
        public int[] cards;
    }
}
