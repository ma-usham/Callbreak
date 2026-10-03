using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// Who takes a table over if its host is lost, as the host last said (ADR 030): the room's
    /// generation (0 for the host that opened it, one more after every hand-over), the host's own
    /// seat and address, and the line of succession: every person after the host in seat order who
    /// can take over, with their addresses (the owner's backup host, 3 Oct 2026). Every player is
    /// sent it, so each knows where to go when the host goes quiet: the first in line takes the table
    /// over at the next generation at once; should it not, the k-th in line takes it over at the
    /// host's generation + k once its turn has come (<see cref="Protocol.BackupTakeOverSeconds"/>),
    /// and the others connect to whichever of them hosts.
    /// </summary>
    public sealed class NextHostNotice : IEquatable<NextHostNotice>
    {
        /// <summary>The seat number when nobody is named.</summary>
        public const int NoSeat = -1;

        /// <summary>The longest line there is: everyone at the table but the host.</summary>
        public const int MaxLine = SeatExtensions.Count - 1;

        private readonly Successor[] _line;

        /// <summary>A line of one, the next host in line; none when <paramref name="nextSeat"/> is <see cref="NoSeat"/>.</summary>
        public NextHostNotice(int generation, int hostSeat, string hostAddress, int nextSeat, string nextAddress)
            : this(generation, hostSeat, hostAddress, nextSeat >= 0 && nextSeat < SeatExtensions.Count ? new[] { new Successor(nextSeat, nextAddress) } : null)
        {
        }

        /// <param name="line">
        /// The successors in order, the next host in line first. A seat that does not exist, the host's
        /// own, one named twice or one without an address is left out.
        /// </param>
        public NextHostNotice(int generation, int hostSeat, string hostAddress, IReadOnlyList<Successor> line)
        {
            Generation = generation;
            HostSeat = hostSeat;
            HostAddress = hostAddress ?? string.Empty;
            var kept = new List<Successor>();
            for (int i = 0; line != null && i < line.Count && kept.Count < MaxLine; i++)
            {
                Successor s = line[i];
                if (s != null && s.Seat >= 0 && s.Seat < SeatExtensions.Count && s.Seat != hostSeat && s.Address.Length > 0 && !kept.Exists(k => k.Seat == s.Seat))
                {
                    kept.Add(s);
                }
            }

            _line = kept.ToArray();
        }

        /// <summary>The generation of the host that sent it; the k-th in line takes over at this one + k.</summary>
        public int Generation { get; }

        public int HostSeat { get; }

        /// <summary>Where the host itself is, for a player who lost the connection and comes back.</summary>
        public string HostAddress { get; }

        /// <summary>The line of succession, the next host in line first; empty when no other person is there to take over.</summary>
        public IReadOnlyList<Successor> Line => _line;

        /// <summary>The next host in line's seat, or <see cref="NoSeat"/> when no other person is there to take over.</summary>
        public int NextSeat => _line.Length > 0 ? _line[0].Seat : NoSeat;

        public string NextAddress => _line.Length > 0 ? _line[0].Address : string.Empty;

        public bool HasNext => _line.Length > 0;

        /// <summary>Where <paramref name="seat"/> stands in the line: 1 for the next host in line, 2 for its backup, and so on; 0 when it is not in it.</summary>
        public int PlaceOf(int seat)
        {
            for (int i = 0; i < _line.Length; i++)
            {
                if (_line[i].Seat == seat)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        public void WriteTo(WireWriter w)
        {
            w.UInt16((ushort)Generation);
            w.Byte((byte)HostSeat);
            w.String(HostAddress);
            w.Byte((byte)_line.Length);
            foreach (Successor s in _line)
            {
                w.Byte((byte)s.Seat);
                w.String(s.Address);
            }
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
            int count = r.Count(MaxLine);
            var line = new Successor[count];
            for (int i = 0; i < count; i++)
            {
                int seat = r.Byte();
                if (seat >= SeatExtensions.Count || seat == hostSeat)
                {
                    throw new WireFormatException("Seat " + seat + " cannot follow the host in seat " + hostSeat + ".");
                }

                for (int j = 0; j < i; j++)
                {
                    if (line[j].Seat == seat)
                    {
                        throw new WireFormatException("Seat " + seat + " is named twice in the line.");
                    }
                }

                string address = r.String(Protocol.MaxIdBytes);
                if (address.Length == 0)
                {
                    throw new WireFormatException("A successor needs an address.");
                }

                line[i] = new Successor(seat, address);
            }

            return new NextHostNotice(generation, hostSeat, hostAddress, line);
        }

        public bool Equals(NextHostNotice other)
        {
            if (other == null || other.Generation != Generation || other.HostSeat != HostSeat || other.HostAddress != HostAddress || other._line.Length != _line.Length)
            {
                return false;
            }

            for (int i = 0; i < _line.Length; i++)
            {
                if (!_line[i].Equals(other._line[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as NextHostNotice);

        public override int GetHashCode() => (Generation * 31) + (HostSeat * 7) + NextSeat;

        /// <summary>One person in the line of succession: its seat, and where its device is.</summary>
        public sealed class Successor : IEquatable<Successor>
        {
            public Successor(int seat, string address)
            {
                Seat = seat;
                Address = address ?? string.Empty;
            }

            public int Seat { get; }

            public string Address { get; }

            public bool Equals(Successor other) => other != null && other.Seat == Seat && other.Address == Address;

            public override bool Equals(object obj) => Equals(obj as Successor);

            public override int GetHashCode() => (Seat * 397) ^ Address.GetHashCode();
        }
    }

    /// <summary>
    /// What a successor is sent so it can take the table over (ADR 030): who each person at the
    /// table is (its identity in this room, so the new host seats everyone again, the lost host
    /// included) and, for the next host in line alone, the hands the bots were dealt this round, so
    /// that it can work out what they still hold from the cards played since, which are public.
    /// Every later successor (the owner's backup host, 3 Oct 2026) gets the identities without any
    /// cards (<see cref="WithoutCards"/>): a backup deals the round again.
    /// <para>
    /// The bots' cards are the one exception, approved by the owner on 2 Oct 2026, to "a seat's hand
    /// leaves the server only to that seat" (AGENTS.md): only bot seats, never a person's, and only to
    /// the next host in line; the exception does not widen to the later successors. They are never
    /// shown and never logged; nothing here prints them.
    /// </para>
    /// </summary>
    public sealed class SuccessionPackage
    {
        private const byte Person = 1;
        private const byte Bot = 2;

        private readonly string[] _identities = new string[SeatExtensions.Count];
        private readonly CardSet[] _dealt = new CardSet[SeatExtensions.Count];
        private readonly bool[] _bot = new bool[SeatExtensions.Count];

        /// <summary>The next host in line's package: the identities, and the hand each bot seat was dealt in <paramref name="round"/>.</summary>
        /// <param name="identities">Each person's identity in this room, by seat; null or empty for a bot seat.</param>
        /// <param name="dealtBotHands">The hand each bot seat was dealt this round, by seat; ignored for a person's seat.</param>
        public SuccessionPackage(int generation, int round, IReadOnlyList<string> identities, IReadOnlyList<CardSet> dealtBotHands)
            : this(generation, round, identities, dealtBotHands, hasCards: true)
        {
        }

        private SuccessionPackage(int generation, int round, IReadOnlyList<string> identities, IReadOnlyList<CardSet> dealtBotHands, bool hasCards)
        {
            Generation = generation;
            Round = round;
            HasCards = hasCards;
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                string identity = identities != null && s < identities.Count ? identities[s] : null;
                if (!string.IsNullOrEmpty(identity))
                {
                    _identities[s] = identity;
                    continue;
                }

                _bot[s] = true;
                _dealt[s] = hasCards && dealtBotHands != null && s < dealtBotHands.Count ? dealtBotHands[s] : CardSet.Empty;
            }
        }

        /// <summary>The generation of the host that sent it: its k-th in line takes the table over at this one + k.</summary>
        public int Generation { get; }

        /// <summary>The round the bots' hands were dealt in; 0 in a package without cards.</summary>
        public int Round { get; }

        /// <summary>The bots' dealt hands come with it: the next host in line's package. False for a later successor's.</summary>
        public bool HasCards { get; }

        /// <summary>A later successor's package (the owner's backup host): the people's identities and no cards at all.</summary>
        public static SuccessionPackage WithoutCards(int generation, IReadOnlyList<string> identities) =>
            new SuccessionPackage(generation, 0, identities, null, hasCards: false);

        /// <summary>The person in <paramref name="seat"/>'s identity in this room; null for a bot seat.</summary>
        public string IdentityOf(Seat seat) => _identities[(int)seat];

        public bool IsBot(Seat seat) => _bot[(int)seat];

        /// <summary>The hand the bot in <paramref name="seat"/> was dealt in <see cref="Round"/>; false for a person's seat, and in a package without cards.</summary>
        public bool TryGetDealtHand(Seat seat, out CardSet hand)
        {
            hand = _dealt[(int)seat];
            return _bot[(int)seat] && HasCards;
        }

        public void WriteTo(WireWriter w)
        {
            w.UInt16((ushort)Generation);
            w.Byte((byte)Round);
            w.Bool(HasCards);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                if (_bot[s])
                {
                    w.Byte(Bot);
                    if (HasCards)
                    {
                        WireCodec.Write(w, _dealt[s]);
                    }
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
            bool hasCards = r.Bool();
            var identities = new string[SeatExtensions.Count];
            var dealt = new CardSet[SeatExtensions.Count];
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                byte kind = r.Byte();
                if (kind == Bot)
                {
                    dealt[s] = hasCards ? WireCodec.ReadCardSet(r) : CardSet.Empty;
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

            return new SuccessionPackage(generation, round, identities, dealt, hasCards);
        }
    }
}
