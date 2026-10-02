using System;
using System.Collections.Generic;
using Callbreak.Core.Match;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// A room as the player who took it over from a lost host rebuilt it (ADR 030): the seat it hosts
    /// from, the rebuilt table, who sits where, and the players who brought their reports, already
    /// connected, by their connections.
    /// </summary>
    internal sealed class RoomTakeOver
    {
        public RoomTakeOver(int hostSeat, TableHost table, IReadOnlyList<SeatInfo> seats, IReadOnlyList<KeyValuePair<PeerId, int>> reporters, double now)
        {
            if (seats == null || seats.Count != SeatExtensions.Count)
            {
                throw new ArgumentException("A table has four seats.", nameof(seats));
            }

            HostSeat = hostSeat;
            Table = table ?? throw new ArgumentNullException(nameof(table));
            Seats = seats;
            Reporters = reporters ?? new KeyValuePair<PeerId, int>[0];
            Now = now;
        }

        /// <summary>The seat of the player who took the room over: the room is hosted from it.</summary>
        public int HostSeat { get; }

        public TableHost Table { get; }

        /// <summary>The lobby's members by seat; null for a seat that has none (a private room's bots are not members).</summary>
        public IReadOnlyList<SeatInfo> Seats { get; }

        /// <summary>The players connected already, each with its seat.</summary>
        public IReadOnlyList<KeyValuePair<PeerId, int>> Reporters { get; }

        public double Now { get; }

        /// <summary>One member of the room taken over: how it shows, and for a person, who it is (its room identity).</summary>
        public sealed class SeatInfo
        {
            public SeatInfo(string name, int avatarId, string deckThemeId, string identity, bool isBot)
            {
                Name = name ?? string.Empty;
                AvatarId = avatarId;
                DeckThemeId = deckThemeId ?? string.Empty;
                Identity = identity;
                IsBot = isBot;
            }

            public string Name { get; }

            public int AvatarId { get; }

            public string DeckThemeId { get; }

            public string Identity { get; }

            /// <summary>A bot sitting in a Quick Match lobby (ADR 029).</summary>
            public bool IsBot { get; }
        }
    }
}
