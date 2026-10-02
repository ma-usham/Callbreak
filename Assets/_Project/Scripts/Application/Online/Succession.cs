using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// Who takes a table over if its host is lost, as the host last said (ADR 030): the room's
    /// generation (0 for the host that opened it, one more after every hand-over), the host's own
    /// seat and address, and the next host in line's. Every player is sent it, so each knows where
    /// to go when the host goes quiet: the next host in line takes the table over at the next
    /// generation, and the others connect to it.
    /// </summary>
    public sealed class NextHostNotice : IEquatable<NextHostNotice>
    {
        /// <summary>The seat number when nobody is named.</summary>
        public const int NoSeat = -1;

        private const byte NoSeatOnWire = 255;

        public NextHostNotice(int generation, int hostSeat, string hostAddress, int nextSeat, string nextAddress)
        {
            Generation = generation;
            HostSeat = hostSeat;
            HostAddress = hostAddress ?? string.Empty;
            NextSeat = nextSeat >= 0 && nextSeat < SeatExtensions.Count ? nextSeat : NoSeat;
            NextAddress = NextSeat == NoSeat ? string.Empty : nextAddress ?? string.Empty;
        }

        /// <summary>The generation of the host that sent it; the next host in line takes over at the one after.</summary>
        public int Generation { get; }

        public int HostSeat { get; }

        /// <summary>Where the host itself is, for a player who lost the connection and comes back.</summary>
        public string HostAddress { get; }

        /// <summary>The next host in line's seat, or <see cref="NoSeat"/> when no other person is there to take over.</summary>
        public int NextSeat { get; }

        public string NextAddress { get; }

        public bool HasNext => NextSeat != NoSeat && NextAddress.Length > 0;

        public void WriteTo(WireWriter w)
        {
            w.UInt16((ushort)Generation);
            w.Byte((byte)HostSeat);
            w.String(HostAddress);
            w.Byte(NextSeat == NoSeat ? NoSeatOnWire : (byte)NextSeat);
            w.String(NextAddress);
        }

        public static NextHostNotice Read(WireReader r)
        {
            int generation = r.UInt16();
            int hostSeat = r.Byte();
            if (hostSeat >= SeatExtensions.Count)
            {
                throw new WireFormatException("Host seat " + hostSeat + " does not exist.");
            }

            string hostAddress = r.String(Protocol.MaxIdBytes);
            byte next = r.Byte();
            if (next != NoSeatOnWire && next >= SeatExtensions.Count)
            {
                throw new WireFormatException("Seat " + next + " does not exist.");
            }

            string nextAddress = r.String(Protocol.MaxIdBytes);
            return new NextHostNotice(generation, hostSeat, hostAddress, next == NoSeatOnWire ? NoSeat : next, nextAddress);
        }

        public bool Equals(NextHostNotice other) =>
            other != null && other.Generation == Generation && other.HostSeat == HostSeat && other.HostAddress == HostAddress
            && other.NextSeat == NextSeat && other.NextAddress == NextAddress;

        public override bool Equals(object obj) => Equals(obj as NextHostNotice);

        public override int GetHashCode() => (Generation * 31) + (HostSeat * 7) + NextSeat;
    }

    /// <summary>
    /// What the next host in line alone is sent (ADR 030): who each person at the table is (its
    /// identity in this room, so the new host seats everyone again, the lost host included) and, for
    /// this round, the hands the bots were dealt, so that the new host can work out what they still
    /// hold from the cards played since, which are public.
    /// <para>
    /// The bots' cards are the one exception, approved by the owner on 2 Oct 2026, to "a seat's hand
    /// leaves the server only to that seat" (AGENTS.md): only bot seats, never a person's, and only to
    /// the next host in line. They are never shown and never logged; nothing here prints them.
    /// </para>
    /// </summary>
    public sealed class SuccessionPackage
    {
        private const byte Person = 1;
        private const byte Bot = 2;

        private readonly string[] _identities = new string[SeatExtensions.Count];
        private readonly CardSet[] _dealt = new CardSet[SeatExtensions.Count];
        private readonly bool[] _bot = new bool[SeatExtensions.Count];

        /// <param name="identities">Each person's identity in this room, by seat; null or empty for a bot seat.</param>
        /// <param name="dealtBotHands">The hand each bot seat was dealt this round, by seat; ignored for a person's seat.</param>
        public SuccessionPackage(int generation, int round, IReadOnlyList<string> identities, IReadOnlyList<CardSet> dealtBotHands)
        {
            Generation = generation;
            Round = round;
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                string identity = identities != null && s < identities.Count ? identities[s] : null;
                if (!string.IsNullOrEmpty(identity))
                {
                    _identities[s] = identity;
                    continue;
                }

                _bot[s] = true;
                _dealt[s] = dealtBotHands != null && s < dealtBotHands.Count ? dealtBotHands[s] : CardSet.Empty;
            }
        }

        /// <summary>The generation of the host that sent it: the table is taken over at the one after.</summary>
        public int Generation { get; }

        /// <summary>The round the bots' hands were dealt in.</summary>
        public int Round { get; }

        /// <summary>The person in <paramref name="seat"/>'s identity in this room; null for a bot seat.</summary>
        public string IdentityOf(Seat seat) => _identities[(int)seat];

        public bool IsBot(Seat seat) => _bot[(int)seat];

        /// <summary>The hand the bot in <paramref name="seat"/> was dealt in <see cref="Round"/>; false for a person's seat.</summary>
        public bool TryGetDealtHand(Seat seat, out CardSet hand)
        {
            hand = _dealt[(int)seat];
            return _bot[(int)seat];
        }

        public void WriteTo(WireWriter w)
        {
            w.UInt16((ushort)Generation);
            w.Byte((byte)Round);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                if (_bot[s])
                {
                    w.Byte(Bot);
                    WireCodec.Write(w, _dealt[s]);
                }
                else
                {
                    w.Byte(Person);
                    w.String(_identities[s]);
                }
            }
        }

        public static SuccessionPackage Read(WireReader r)
        {
            int generation = r.UInt16();
            int round = r.Byte();
            var identities = new string[SeatExtensions.Count];
            var dealt = new CardSet[SeatExtensions.Count];
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                byte kind = r.Byte();
                if (kind == Bot)
                {
                    dealt[s] = WireCodec.ReadCardSet(r);
                }
                else if (kind == Person)
                {
                    identities[s] = r.String(Protocol.MaxIdentityBytes);
                    if (identities[s].Length == 0)
                    {
                        throw new WireFormatException("A person's seat needs an identity.");
                    }
                }
                else
                {
                    throw new WireFormatException("Seat kind " + kind + " is unknown.");
                }
            }

            return new SuccessionPackage(generation, round, identities, dealt);
        }
    }
}
