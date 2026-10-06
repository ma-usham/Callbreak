using System.Collections.Generic;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// Everything the Quick Match screen shows for one search report (GDD §11 screen 05), already in
    /// words: the four seat circles in the order they are drawn, the two status lines and the time in
    /// the pill. <see cref="MatchmakingDisplay.StateOf"/> works it out; the view only shows it.
    /// </summary>
    public sealed class MatchmakingScreenState
    {
        public MatchmakingScreenState(
            IReadOnlyList<Seat> seats,
            string title,
            string subtitle,
            MatchmakingDisplay.ClockKind clock,
            int clockSeconds,
            bool canLeave)
        {
            Seats = seats;
            Title = title;
            Subtitle = subtitle;
            Clock = clock;
            ClockSeconds = clockSeconds;
            CanLeave = canLeave;
        }

        /// <summary>The seat circles from left to right; the first is the player looking at the screen.</summary>
        public IReadOnlyList<Seat> Seats { get; }

        /// <summary>"Finding players..." or "Table found".</summary>
        public string Title { get; }

        /// <summary>"2 of 4 players connected".</summary>
        public string Subtitle { get; }

        /// <summary>Which time the pill shows.</summary>
        public MatchmakingDisplay.ClockKind Clock { get; }

        /// <summary>The whole seconds in the pill.</summary>
        public int ClockSeconds { get; }

        /// <summary>The pill as it reads, "00:20".</summary>
        public string ClockText => MatchmakingDisplay.FormatClock(ClockSeconds);

        /// <summary>
        /// Whether the player can leave the search (Cancel, the back arrow, Android back). Not while
        /// the table is found and locked: its start countdown runs on every screen at the table.
        /// </summary>
        public bool CanLeave { get; }

        /// <summary>One seat circle: a player (the avatar disc) or the dashed circle of a seat still searching.</summary>
        public readonly struct Seat
        {
            public Seat(bool filled, string name, string status, int avatarId = 0)
            {
                Filled = filled;
                Name = name;
                Status = status;
                AvatarId = avatarId;
            }

            public bool Filled { get; }

            /// <summary>"You", a player's name, or empty while searching.</summary>
            public string Name { get; }

            /// <summary>"Ready" or "Searching".</summary>
            public string Status { get; }

            /// <summary>A filled seat's avatar preset (ADR 041), the player's own in the first circle.</summary>
            public int AvatarId { get; }
        }
    }
}
