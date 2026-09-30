using System;
using System.Collections.Generic;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Bots;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// The authority of one room (GDD §11 screen 04, §27): who is in it, who is ready, and, once
    /// the host starts, the <see cref="TableHost"/> that runs the match. Members talk to it only
    /// through messages; the host device's own player is just another member, connected in
    /// process through <see cref="Local"/>, so the host's screens run the same code as a guest's.
    /// </summary>
    public sealed class RoomHost : IDisposable
    {
        private const int Seats = RoomSnapshot.SeatCount;
        private const double HelloWaitSeconds = 10.0;
        private const double DropDelaySeconds = 1.0;
        private const int DropsBeforeKick = 100;

        private readonly string _code;
        private readonly RoomSettings _settings;
        private readonly IHostLink _remote;
        private readonly IBotFactory _botFactory;
        private readonly NameFilter _names;
        private readonly IRandom _rng;
        private readonly IReadOnlyList<string> _deckIds;
        private readonly Func<ulong> _seedSource;
        private readonly BotTiming _timing;
        private readonly BotLevel _fillLevel;
        private readonly TableClocks _clocks;
        private readonly LocalLink _local;
        private readonly Member[] _members = new Member[Seats];
        private readonly Dictionary<PeerId, int> _seatOfPeer = new Dictionary<PeerId, int>();
        private readonly Dictionary<PeerId, Guest> _guests = new Dictionary<PeerId, Guest>();
        private readonly HashSet<string> _removed = new HashSet<string>();
        private readonly Dictionary<string, FinishedSeat> _finished = new Dictionary<string, FinishedSeat>();
        private readonly Queue<Inbound> _inbound = new Queue<Inbound>();
        private readonly Queue<byte[]> _localOutbound = new Queue<byte[]>();
        private readonly List<PeerId> _scratch = new List<PeerId>();
        private readonly HandlerFaults _faults = new HandlerFaults();

        private TableHost _table;
        private double _now;
        private double _tableStartDeadline;
        private bool _pumping;
        private bool _closed;

        /// <param name="remote">The relay's host end; null for a room nobody else can reach (Quick Match filled with bots).</param>
        /// <param name="seedSource">Draws each match's shuffle seed (a CSPRNG in production, GDD §22).</param>
        /// <param name="timing">How the table's bots pace themselves; null gives the hosted defaults (GDD §6).</param>
        /// <param name="clocks">
        /// The table's turn clocks and round-results countdown; null runs the room as a private
        /// room, on the turn time of <paramref name="settings"/> (GDD §6).
        /// </param>
        public RoomHost(
            string code,
            RoomSettings settings,
            IHostLink remote,
            IBotFactory botFactory,
            NameFilter names,
            IRandom rng,
            IReadOnlyList<string> deckIds,
            Func<ulong> seedSource,
            BotTiming timing = null,
            BotLevel fillLevel = BotLevel.Normal,
            TableClocks clocks = null)
        {
            _code = code ?? string.Empty;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _remote = remote;
            _botFactory = botFactory ?? throw new ArgumentNullException(nameof(botFactory));
            _names = names ?? throw new ArgumentNullException(nameof(names));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _deckIds = deckIds ?? new string[0];
            _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
            _timing = timing;
            _fillLevel = fillLevel;
            _clocks = clocks;
            _local = new LocalLink(this);
            if (_remote != null)
            {
                _remote.PeerConnected += OnPeerConnected;
                _remote.PeerDisconnected += OnPeerDisconnected;
                _remote.Received += OnRemoteReceived;
            }
        }

        /// <summary>The host player's own connection to this room.</summary>
        public IClientLink Local => _local;

        public string Code => _code;

        /// <summary>True while the room's match is being played.</summary>
        public bool InMatch => _table != null && !_table.IsOver;

        /// <summary>The running or last match, for tests and diagnostics.</summary>
        public TableHost Table => _table;

        /// <summary>
        /// Drives the table's time and hands out what is queued. Throws, after all of that, the first
        /// exception a handler of the host's own player (or the transport) threw since the last tick.
        /// </summary>
        public void Tick(double now)
        {
            _now = now;
            if (!_closed)
            {
                DropSilentGuests(now);
                if (_table != null)
                {
                    if (!_table.IsStarted && (EveryTableReady() || now >= _tableStartDeadline))
                    {
                        _table.Start(now);
                    }

                    _table.Tick(now);
                }

                Pump();
            }

            _faults.Rethrow();
        }

        /// <summary>
        /// Whether the host may start (GDD §11 screen 04): every seated guest is ready, and either
        /// all four seats are taken or the empty ones are to be filled with bots.
        /// </summary>
        public bool CanStart(bool fillWithBots)
        {
            if (_closed || InMatch || _members[0] == null)
            {
                return false;
            }

            int people = 0;
            for (int i = 0; i < Seats; i++)
            {
                Member m = _members[i];
                if (m == null)
                {
                    continue;
                }

                people++;
                if (i > 0 && !m.Ready)
                {
                    return false;
                }
            }

            return people == Seats || fillWithBots;
        }

        /// <summary>Starts the match: every member is told to load the table, and the deal follows once they have.</summary>
        public bool StartMatch(bool fillWithBots)
        {
            if (!CanStart(fillWithBots))
            {
                return false;
            }

            var profiles = new SeatProfile[Seats];
            var humans = new bool[Seats];
            var used = new HashSet<string>();
            for (int i = 0; i < Seats; i++)
            {
                if (_members[i] != null)
                {
                    used.Add(_members[i].Name);
                }
            }

            for (int i = 0; i < Seats; i++)
            {
                Member m = _members[i];
                if (m != null)
                {
                    humans[i] = true;
                    profiles[i] = new SeatProfile(m.Name, m.AvatarId.ToString(), m.DeckThemeId, false);
                    m.TableReady = false;
                    continue;
                }

                string name = SeatRoster.DrawName(_rng, used);
                used.Add(name);
                string deck = _deckIds.Count > 0 ? _deckIds[_rng.NextInt(_deckIds.Count)] : string.Empty;
                profiles[i] = new SeatProfile(name, _rng.NextInt(SeatRoster.AvatarCount).ToString(), deck, true);
            }

            // The match id goes to every player in every view, and no shuffle seed ever leaves the
            // host (GDD §22: whoever has one can deal its round in advance). So the id is a draw of
            // its own, and the table draws a fresh seed for each round as it deals it.
            string matchId = "room-" + _code + "-" + _seedSource().ToString("x16");
            TableClocks clocks = _clocks ?? TableClocks.PrivateRoom(_settings.TurnSeconds);
            var setup = new TableHostSetup(RuleSet.NepaliStandard, matchId, profiles, humans, clocks, _seedSource, _fillLevel, _timing);
            if (_table != null)
            {
                _table.Outbound -= OnTableOutbound;
                _table.Ended -= OnTableEnded;
            }

            _table = new TableHost(setup, _botFactory);
            _table.Outbound += OnTableOutbound;
            _table.Ended += OnTableEnded;
            _tableStartDeadline = _now + Protocol.TableReadyWaitSeconds;

            TableStatus status = _table.StatusAt(_now);
            for (int i = 0; i < Seats; i++)
            {
                if (_members[i] != null)
                {
                    Send(_members[i].Peer, HostMessages.MatchStarting(_table.ViewFor((Seat)i), status));
                }
            }

            Broadcast();
            Pump();
            return true;
        }

        /// <summary>Host only: removes a guest (GDD §11 screen 04, long-press on a seat).</summary>
        public void Kick(int seatIndex)
        {
            if (_closed || seatIndex <= 0 || seatIndex >= Seats || _members[seatIndex] == null)
            {
                return;
            }

            Member m = _members[seatIndex];
            if (!string.IsNullOrEmpty(m.Identity))
            {
                _removed.Add(m.Identity);
            }

            if (m.Connected)
            {
                // A dropped member's old connection id may belong to someone else by now.
                Send(m.Peer, HostMessages.RoomClosed(RoomError.Kicked));
                ScheduleDrop(m.Peer);
            }

            RemoveMember(seatIndex, leftOnPurpose: true);
            Broadcast();
            Pump();
        }

        /// <summary>The host leaves: the room is over for everyone (GDD §8, Phase 1).</summary>
        public void Close()
        {
            if (_closed)
            {
                return;
            }

            for (int i = 1; i < Seats; i++)
            {
                if (_members[i] != null && _members[i].Connected)
                {
                    Send(_members[i].Peer, HostMessages.RoomClosed(RoomError.HostLeft));
                }
            }

            Pump();
            _closed = true;
        }

        /// <summary>Closes the room and lets go of the link; throws last a fault no tick has passed on yet.</summary>
        public void Dispose()
        {
            Close();
            if (_remote != null)
            {
                _remote.PeerConnected -= OnPeerConnected;
                _remote.PeerDisconnected -= OnPeerDisconnected;
                _remote.Received -= OnRemoteReceived;
            }

            if (_table != null)
            {
                _table.Outbound -= OnTableOutbound;
                _table.Ended -= OnTableEnded;
            }

            try
            {
                _local.Close();
            }
            catch (Exception e)
            {
                _faults.Keep(e);
            }

            _faults.Rethrow();
        }

        private void OnPeerConnected(PeerId peer)
        {
            if (!_closed && !_guests.ContainsKey(peer))
            {
                _guests[peer] = new Guest(_now);
            }
        }

        private void OnPeerDisconnected(PeerId peer)
        {
            _guests.Remove(peer);
            if (_closed || !_seatOfPeer.TryGetValue(peer, out int seat))
            {
                return;
            }

            _seatOfPeer.Remove(peer);
            Member m = _members[seat];
            if (InMatch && _table.IsHuman((Seat)seat))
            {
                // GDD §8: the seat is held; a bot covers the turns until the player is back.
                m.Connected = false;
                _table.SetConnected((Seat)seat, false, _now);
            }
            else
            {
                _members[seat] = null;
            }

            Broadcast();
            Pump();
        }

        private void OnRemoteReceived(PeerId peer, byte[] payload)
        {
            if (_closed || payload == null)
            {
                return;
            }

            if (!_guests.TryGetValue(peer, out Guest guest))
            {
                guest = new Guest(_now);
                _guests[peer] = guest;
            }

            // GDD §25: a token bucket per connection; the excess is dropped, floods are kicked.
            if (!guest.TakeToken(_now))
            {
                if (guest.Drops > DropsBeforeKick)
                {
                    DropPeer(peer);
                }

                return;
            }

            if (payload.Length > Protocol.MaxClientPayloadBytes)
            {
                // No member ever sends this much; it is never read (GDD §25 "messages are tiny").
                return;
            }

            _inbound.Enqueue(new Inbound(peer, payload));
            Pump();
        }

        private void OnLocalSent(byte[] payload)
        {
            if (_closed || payload == null)
            {
                return;
            }

            _inbound.Enqueue(new Inbound(PeerId.Local, payload));
            Pump();
        }

        /// <summary>
        /// Handles queued messages one at a time and only then hands the host's own player its
        /// replies, so nothing re-enters the room while it is in the middle of a change. The host
        /// player's screens run inside those deliveries: one that throws never stops the room from
        /// finishing everything queued (the table has already flushed to every seat and set its
        /// clock). The first exception is kept and thrown from the next <see cref="Tick"/>, once
        /// that frame's work is done, so the app's ticker logs it.
        /// </summary>
        private void Pump()
        {
            if (_pumping)
            {
                return;
            }

            _pumping = true;
            try
            {
                while (_inbound.Count > 0 || _localOutbound.Count > 0)
                {
                    while (_inbound.Count > 0)
                    {
                        Inbound message = _inbound.Dequeue();
                        try
                        {
                            Process(message.Peer, message.Payload);
                        }
                        catch (Exception e)
                        {
                            _faults.Keep(e);
                        }
                    }

                    while (_localOutbound.Count > 0)
                    {
                        try
                        {
                            _local.Deliver(_localOutbound.Dequeue());
                        }
                        catch (Exception e)
                        {
                            _faults.Keep(e);
                        }
                    }
                }
            }
            finally
            {
                _pumping = false;
            }
        }

        private void Process(PeerId peer, byte[] payload)
        {
            try
            {
                var reader = new WireReader(payload);
                var kind = (ClientMessageKind)reader.Byte();
                if (kind == ClientMessageKind.Hello)
                {
                    ReadHello(peer, reader);
                    return;
                }

                if (!_seatOfPeer.TryGetValue(peer, out int seat))
                {
                    return;
                }

                Member member = _members[seat];
                switch (kind)
                {
                    case ClientMessageKind.SetReady:
                    {
                        bool ready = reader.Bool();
                        if (!InMatch && member.Ready != ready)
                        {
                            member.Ready = ready;
                            Broadcast();
                        }

                        break;
                    }

                    case ClientMessageKind.LeaveRoom:
                    case ClientMessageKind.LeaveTable:
                        if (peer.IsLocal)
                        {
                            // GDD §8, Phase 1: the host leaving ends the room for everyone.
                            Close();
                            break;
                        }

                        RemoveMember(seat, leftOnPurpose: true);
                        ScheduleDrop(peer);
                        Broadcast();
                        break;

                    case ClientMessageKind.TableReady:
                        // Answered with the whole state; the deal waits until every member has said this.
                        member.TableReady = true;
                        if (_table != null)
                        {
                            _table.Handle((Seat)seat, kind, reader, _now);
                        }

                        break;

                    default:
                        if (_table != null)
                        {
                            _table.Handle((Seat)seat, kind, reader, _now);
                        }

                        break;
                }
            }
            catch (WireFormatException)
            {
                // A malformed message is dropped; it never reaches the table.
            }
        }

        private void ReadHello(PeerId peer, WireReader reader)
        {
            _guests.TryGetValue(peer, out Guest guest);
            if (guest != null && guest.IsLeaving)
            {
                // Removed, replaced, refused or gone on purpose: nothing it says counts any more.
                return;
            }

            // GDD §25 versioning: the version comes first, and another build's hello may be laid
            // out differently, so it is answered before anything after the version is read.
            ushort version = reader.UInt16();
            if (version != Protocol.Version)
            {
                if (guest != null)
                {
                    guest.SaidHello = true;
                }

                Refuse(peer, RoomError.UpdateRequired);
                return;
            }

            HelloMessage hello = HelloMessage.ReadAfterVersion(reader, version);
            if (guest != null)
            {
                guest.SaidHello = true;
            }

            OnHello(peer, hello);
        }

        private void OnHello(PeerId peer, HelloMessage hello)
        {
            if (_seatOfPeer.ContainsKey(peer))
            {
                return;
            }

            if (_removed.Contains(hello.Identity))
            {
                // GDD §11 screen 04: the host removed this player; that holds for as long as the room.
                Refuse(peer, RoomError.Kicked);
                return;
            }

            if (_finished.TryGetValue(hello.Identity, out FinishedSeat finished))
            {
                // Back after the match ended but within the seat hold: its end first, then the room
                // as it is now (seated in the lobby, or refused if the next match is on).
                if (_now <= finished.Until)
                {
                    Send(peer, finished.End);
                }
                else
                {
                    _finished.Remove(hello.Identity);
                }
            }

            // GDD §27 idempotent rejoin: the same player connecting again takes its seat back.
            int known = FindByIdentity(hello.Identity);
            if (known == 0 && !peer.IsLocal)
            {
                // Seat 0 is the host device's own player, connected in process: whoever presents
                // its identity over the network is someone else, and never takes it over.
                Refuse(peer, RoomError.Unknown);
                return;
            }

            if (known >= 0 && InMatch && !_table.IsHuman((Seat)known))
            {
                // GDD §8: not back within the hold, the seat is a bot for the rest of the match.
                Refuse(peer, RoomError.AlreadyStarted);
                return;
            }

            if (known >= 0)
            {
                Member m = _members[known];
                if (m.Connected && m.Peer != peer)
                {
                    // GDD §27: a second connection of the same player replaces the first.
                    Send(m.Peer, HostMessages.RoomClosed(RoomError.Replaced));
                    ScheduleDrop(m.Peer);
                    ForgetPeer(m.Peer, known);
                }

                // A member that dropped has no connection of its own any more: the relay may already
                // have given its old id to someone else, whose mapping stays as it is.
                m.Peer = peer;
                m.Connected = true;
                m.TableReady = false;
                _seatOfPeer[peer] = known;
                Send(peer, HostMessages.Welcome(known, Snapshot()));
                if (InMatch)
                {
                    Send(peer, HostMessages.MatchStarting(_table.ViewFor((Seat)known), _table.StatusAt(_now)));
                    _table.SetConnected((Seat)known, true, _now);
                }

                Broadcast();
                return;
            }

            if (InMatch)
            {
                Refuse(peer, RoomError.AlreadyStarted);
                return;
            }

            int free = -1;
            for (int i = 0; i < Seats; i++)
            {
                if (_members[i] == null)
                {
                    free = i;
                    break;
                }
            }

            // Seat 0 is the host's; only the host device's own player may take it.
            if (free == 0 && !peer.IsLocal)
            {
                free = -1;
                for (int i = 1; i < Seats; i++)
                {
                    if (_members[i] == null)
                    {
                        free = i;
                        break;
                    }
                }
            }

            if (free < 0)
            {
                Refuse(peer, RoomError.Full);
                return;
            }

            _members[free] = new Member(peer, hello.Identity, CleanName(hello.Name, free), CleanAvatar(hello.AvatarId), CleanDeck(hello.DeckThemeId));
            _seatOfPeer[peer] = free;
            Send(peer, HostMessages.Welcome(free, Snapshot()));
            Broadcast();
        }

        /// <summary>GDD §11 screen 14: names are checked again where they are shown to others.</summary>
        private string CleanName(string raw, int seat)
        {
            string name = ProfileService.NormalizeName(raw);
            int length = ProfileService.CountCodePoints(name);
            if (length < 3 || length > 16 || !ProfileService.HasOnlyNameCharacters(name) || !_names.IsAllowed(name))
            {
                return "Player " + (seat + 1);
            }

            return name;
        }

        /// <summary>One of the avatar presets (GDD §11 screen 14); anything else is the default silhouette.</summary>
        private static int CleanAvatar(int avatar) => avatar >= 0 && avatar < SeatRoster.AvatarCount ? avatar : 0;

        /// <summary>One of the decks this build has (GDD §13); anything else is the default deck.</summary>
        private string CleanDeck(string deck)
        {
            for (int i = 0; i < _deckIds.Count; i++)
            {
                if (_deckIds[i] == deck)
                {
                    return deck;
                }
            }

            return CosmeticsService.DefaultDeckId;
        }

        private void RemoveMember(int seat, bool leftOnPurpose)
        {
            Member m = _members[seat];
            if (m == null)
            {
                return;
            }

            if (m.Connected)
            {
                ForgetPeer(m.Peer, seat);
            }

            _members[seat] = null;
            if (leftOnPurpose && _table != null && !_table.IsOver)
            {
                _table.Leave((Seat)seat, _now);
            }
        }

        /// <summary>Unmaps <paramref name="peer"/> only while it still stands for <paramref name="seat"/>.</summary>
        private void ForgetPeer(PeerId peer, int seat)
        {
            if (_seatOfPeer.TryGetValue(peer, out int mapped) && mapped == seat)
            {
                _seatOfPeer.Remove(peer);
            }
        }

        private void Refuse(PeerId peer, RoomError error)
        {
            Send(peer, HostMessages.Rejected(error));
            ScheduleDrop(peer);
        }

        /// <summary>Lets the last message reach the peer before its connection is closed.</summary>
        private void ScheduleDrop(PeerId peer)
        {
            if (peer.IsLocal)
            {
                return;
            }

            if (!_guests.TryGetValue(peer, out Guest guest))
            {
                guest = new Guest(_now);
                _guests[peer] = guest;
            }

            guest.DropAt = _now + DropDelaySeconds;
        }

        private void DropSilentGuests(double now)
        {
            if (_remote == null || _guests.Count == 0)
            {
                return;
            }

            _scratch.Clear();
            foreach (KeyValuePair<PeerId, Guest> pair in _guests)
            {
                Guest guest = pair.Value;
                bool silent = !guest.SaidHello && now >= guest.ConnectedAt + HelloWaitSeconds;
                if (silent || now >= guest.DropAt)
                {
                    _scratch.Add(pair.Key);
                }
            }

            foreach (PeerId peer in _scratch)
            {
                _guests.Remove(peer);
                DropPeer(peer);
            }
        }

        private bool EveryTableReady()
        {
            for (int i = 0; i < Seats; i++)
            {
                Member m = _members[i];
                if (m != null && m.Connected && !m.TableReady)
                {
                    return false;
                }
            }

            return true;
        }

        private int FindByIdentity(string identity)
        {
            if (string.IsNullOrEmpty(identity))
            {
                return -1;
            }

            for (int i = 0; i < Seats; i++)
            {
                if (_members[i] != null && _members[i].Identity == identity)
                {
                    return i;
                }
            }

            return -1;
        }

        private void OnTableOutbound(Seat seat, byte[] payload)
        {
            Member m = _members[(int)seat];
            if (m != null && m.Connected)
            {
                Send(m.Peer, payload);
            }
        }

        /// <summary>
        /// GDD §11 screen 10: after the match the room is a lobby again, with the ready flags reset.
        /// A player who is reconnecting leaves the lobby, but the seat hold still counts for them
        /// (GDD §8 "back within 90 s: full state restored"): when they are back in time they get the
        /// end of their match, recorded like everyone else's, and then the room as it is by then.
        /// </summary>
        private void OnTableEnded()
        {
            TableStatus status = _table.StatusAt(_now);
            for (int i = 0; i < Seats; i++)
            {
                Member m = _members[i];
                if (m == null)
                {
                    continue;
                }

                if (!m.Connected)
                {
                    if (_table.IsHuman((Seat)i) && !string.IsNullOrEmpty(m.Identity))
                    {
                        byte[] end = HostMessages.SyncState(new TableUpdate(0, null, _table.ViewFor((Seat)i), PresentAt(status, (Seat)i)));
                        _finished[m.Identity] = new FinishedSeat(end, _now + Protocol.SeatHoldSeconds);
                    }

                    _members[i] = null;
                    continue;
                }

                m.Ready = false;
                m.TableReady = false;
            }

            Broadcast();
        }

        /// <summary><paramref name="status"/> with <paramref name="seat"/> shown present: the player it goes to is back.</summary>
        private static TableStatus PresentAt(TableStatus status, Seat seat)
        {
            var connections = new SeatConnection[Seats];
            for (int s = 0; s < Seats; s++)
            {
                connections[s] = s == (int)seat ? SeatConnection.Connected : status.ConnectionOf((Seat)s);
            }

            return new TableStatus(status.TimerSeat, TableStatus.NoClock, 0, TableStatus.NoClock, connections);
        }

        private RoomSnapshot Snapshot()
        {
            var seats = new RoomSnapshot.Member[Seats];
            for (int i = 0; i < Seats; i++)
            {
                Member m = _members[i];
                seats[i] = m == null
                    ? RoomSnapshot.Member.Empty
                    : new RoomSnapshot.Member(true, m.Name, m.AvatarId, m.DeckThemeId, i == 0, m.Ready, false, m.Connected);
            }

            return new RoomSnapshot(_code, _settings, seats, InMatch);
        }

        private void Broadcast()
        {
            byte[] payload = HostMessages.RoomState(Snapshot());
            for (int i = 0; i < Seats; i++)
            {
                if (_members[i] != null && _members[i].Connected)
                {
                    Send(_members[i].Peer, payload);
                }
            }
        }

        /// <summary>A transport that fails for one peer never stops the others' updates (the exception is passed on after the pump).</summary>
        private void Send(PeerId peer, byte[] payload)
        {
            if (peer.IsLocal)
            {
                _localOutbound.Enqueue(payload);
                return;
            }

            if (_remote == null)
            {
                return;
            }

            try
            {
                _remote.Send(peer, payload);
            }
            catch (Exception e)
            {
                _faults.Keep(e);
            }
        }

        private void DropPeer(PeerId peer)
        {
            try
            {
                _remote.Disconnect(peer);
            }
            catch (Exception e)
            {
                _faults.Keep(e);
            }
        }

        private readonly struct Inbound
        {
            public Inbound(PeerId peer, byte[] payload)
            {
                Peer = peer;
                Payload = payload;
            }

            public PeerId Peer { get; }

            public byte[] Payload { get; }
        }

        private sealed class Member
        {
            public Member(PeerId peer, string identity, string name, int avatarId, string deckThemeId)
            {
                Peer = peer;
                Identity = identity;
                Name = name;
                AvatarId = avatarId;
                DeckThemeId = deckThemeId;
                Connected = true;
            }

            public PeerId Peer { get; set; }

            /// <summary>The member's <see cref="RoomIdentity"/>: how a rejoin finds this seat again.</summary>
            public string Identity { get; }

            public string Name { get; }

            public int AvatarId { get; }

            public string DeckThemeId { get; }

            public bool Connected { get; set; }

            public bool Ready { get; set; }

            public bool TableReady { get; set; }
        }

        /// <summary>The end of a match, kept for a player who was reconnecting when it came, until their seat hold is over.</summary>
        private sealed class FinishedSeat
        {
            public FinishedSeat(byte[] end, double until)
            {
                End = end;
                Until = until;
            }

            /// <summary>The SyncState of the finished table for that player's seat.</summary>
            public byte[] End { get; }

            public double Until { get; }
        }

        /// <summary>A remote connection: its flood allowance, and whether it has introduced itself.</summary>
        private sealed class Guest
        {
            private double _tokens = Protocol.IntentsPerSecond;
            private double _refilledAt;

            public Guest(double now)
            {
                ConnectedAt = now;
                _refilledAt = now;
            }

            public double ConnectedAt { get; }

            public bool SaidHello { get; set; }

            public double DropAt { get; set; } = double.PositiveInfinity;

            /// <summary>The connection is to be closed: it was refused, removed, replaced or left.</summary>
            public bool IsLeaving => !double.IsPositiveInfinity(DropAt);

            public int Drops { get; private set; }

            public bool TakeToken(double now)
            {
                _tokens = Math.Min(Protocol.IntentsPerSecond, _tokens + ((now - _refilledAt) * Protocol.IntentsPerSecond));
                _refilledAt = now;
                if (_tokens < 1.0)
                {
                    Drops++;
                    return false;
                }

                _tokens -= 1.0;
                return true;
            }
        }

        /// <summary>The host player's in-process connection: no network, the same messages.</summary>
        private sealed class LocalLink : IClientLink
        {
            private readonly RoomHost _host;
            private bool _open = true;

            public LocalLink(RoomHost host)
            {
                _host = host;
            }

            public event Action<byte[]> Received;

            public event Action Closed;

            public bool IsConnected => _open;

            public void Send(byte[] payload)
            {
                if (_open)
                {
                    _host.OnLocalSent(payload);
                }
            }

            public void Deliver(byte[] payload)
            {
                if (_open)
                {
                    Received?.Invoke(payload);
                }
            }

            public void Close()
            {
                if (!_open)
                {
                    return;
                }

                _open = false;
                Closed?.Invoke();
            }

            public void Dispose()
            {
                _open = false;
            }
        }
    }
}
