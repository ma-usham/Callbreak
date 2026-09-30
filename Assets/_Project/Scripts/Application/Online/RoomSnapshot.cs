using System.Collections.Generic;
using Callbreak.Application.Rooms;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// The lobby as the host publishes it (GDD §11 screen 04): code, settings and four seats. Sent
    /// whole on every change, so every member shows the same thing within one message.
    /// </summary>
    public sealed class RoomSnapshot
    {
        public const int SeatCount = 4;

        private readonly Member[] _seats;

        public RoomSnapshot(string code, RoomSettings settings, IReadOnlyList<Member> seats, bool inMatch)
        {
            Code = code ?? string.Empty;
            Settings = settings;
            _seats = new Member[SeatCount];
            for (int i = 0; i < SeatCount; i++)
            {
                _seats[i] = i < seats.Count && seats[i] != null ? seats[i] : Member.Empty;
            }

            InMatch = inMatch;
        }

        public string Code { get; }

        public RoomSettings Settings { get; }

        /// <summary>Four entries; the host is seat 0.</summary>
        public IReadOnlyList<Member> Seats => _seats;

        /// <summary>True while the room's match is running (late joiners are turned away).</summary>
        public bool InMatch { get; }

        public void WriteTo(WireWriter w)
        {
            w.String(Code);
            w.String(Settings.Name);
            w.Byte((byte)Settings.TurnSeconds);
            w.Byte((byte)Settings.Rounds);
            w.Bool(InMatch);
            for (int i = 0; i < SeatCount; i++)
            {
                Member m = _seats[i];
                w.Bool(m.Occupied);
                w.String(m.Name);
                w.Byte((byte)m.AvatarId);
                w.String(m.DeckThemeId);
                w.Bool(m.IsHost);
                w.Bool(m.IsReady);
                w.Bool(m.IsBot);
                w.Bool(m.IsConnected);
            }
        }

        public static RoomSnapshot Read(WireReader r)
        {
            string code = r.String();
            string name = r.String();
            int turnSeconds = r.Byte();
            int rounds = r.Byte();
            bool inMatch = r.Bool();
            var seats = new Member[SeatCount];
            for (int i = 0; i < SeatCount; i++)
            {
                bool occupied = r.Bool();
                string memberName = r.String();
                int avatar = r.Byte();
                string deck = r.String();
                bool isHost = r.Bool();
                bool isReady = r.Bool();
                bool isBot = r.Bool();
                bool isConnected = r.Bool();
                seats[i] = new Member(occupied, memberName, avatar, deck, isHost, isReady, isBot, isConnected);
            }

            return new RoomSnapshot(code, new RoomSettings(name, turnSeconds, rounds), seats, inMatch);
        }

        /// <summary>One lobby seat on the wire.</summary>
        public sealed class Member
        {
            public Member(bool occupied, string name, int avatarId, string deckThemeId, bool isHost, bool isReady, bool isBot, bool isConnected)
            {
                Occupied = occupied;
                Name = name ?? string.Empty;
                AvatarId = avatarId;
                DeckThemeId = deckThemeId ?? string.Empty;
                IsHost = isHost;
                IsReady = isReady;
                IsBot = isBot;
                IsConnected = isConnected;
            }

            public static Member Empty { get; } = new Member(false, string.Empty, 0, string.Empty, false, false, false, false);

            public bool Occupied { get; }

            public string Name { get; }

            public int AvatarId { get; }

            public string DeckThemeId { get; }

            public bool IsHost { get; }

            public bool IsReady { get; }

            public bool IsBot { get; }

            public bool IsConnected { get; }
        }
    }
}
