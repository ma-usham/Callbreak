using System;
using System.Collections.Generic;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Bots;
using Callbreak.Core.Cards;
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
    /// <para>
    /// Over a link that knows its peers' addresses (<see cref="IHostAddresses"/>, ADR 030) the
    /// players can take the match over if the host is lost: during the match the host keeps up a
    /// heartbeat, names the line of succession to everyone (<see cref="NextHostNotice"/>), sends the
    /// next host in line alone what it needs to go on from the same trick, and every later successor
    /// what it needs to take over without any cards (<see cref="SuccessionPackage"/>). A room that was
    /// taken over is hosted from the seat of the player who took it over.
    /// </para>
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
        private readonly bool _botsChat;
        private readonly bool _quickTable;
        private readonly IHostAddresses _addresses;
        private readonly int _hostSeat;
        private readonly double _heartbeatSeconds;
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
        private bool _locked;
        private double _startsAt;
        private double _nextBeatAt;
        private NextHostNotice _nextHost;
        private readonly PackageSent[] _packages = new PackageSent[Seats];

        /// <param name="remote">The relay's host end; null for a room nobody else can reach (Quick Match filled with bots).</param>
        /// <param name="seedSource">Draws each match's shuffle seed (a CSPRNG in production, GDD §22).</param>
        /// <param name="timing">How the table's bots pace themselves; null gives the hosted defaults (GDD §6).</param>
        /// <param name="clocks">
        /// The table's turn clocks and round-results countdown; null runs the room as a private
        /// room, on the turn time of <paramref name="settings"/> (GDD §6).
        /// </param>
        /// <param name="botsChat">The bots that fill seats say a quick phrase now and then (ADR 026).</param>
        /// <param name="quickTable">
        /// A Quick Match table (ADR 029): nobody has a Ready button, so every member is ready from
        /// the moment they sit down; bots can take seats in the lobby (<see cref="SeatBot"/>); and the
        /// table can be locked for its start (<see cref="Lock"/>).
        /// </param>
        /// <param name="heartbeatSeconds">
        /// How often the host tells its players it is still there: while a Quick Match table waits
        /// (ADR 029), and during a match its players can take over (ADR 030).
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
            TableClocks clocks = null,
            bool botsChat = true,
            bool quickTable = false,
            double heartbeatSeconds = Protocol.HeartbeatSeconds)
            : this(null, code, settings, remote, botFactory, names, rng, deckIds, seedSource, timing, fillLevel, clocks, botsChat, quickTable, heartbeatSeconds)
        {
        }

        /// <summary>
        /// A room taken over from a lost host (ADR 030), or a new one when <paramref name="takeOver"/>
        /// is null: the match goes on at the rebuilt table, hosted from the seat of the player who took
        /// it over, and the players who brought their reports are seated at once.
        /// </summary>
        internal RoomHost(
            RoomTakeOver takeOver,
            string code,
            RoomSettings settings,
            IHostLink remote,
            IBotFactory botFactory,
            NameFilter names,
            IRandom rng,
            IReadOnlyList<string> deckIds,
            Func<ulong> seedSource,
            BotTiming timing,
            BotLevel fillLevel,
            TableClocks clocks,
            bool botsChat,
            bool quickTable,
            double heartbeatSeconds)
        {
            _heartbeatSeconds = heartbeatSeconds;
            _addresses = remote as IHostAddresses;
            _hostSeat = takeOver != null ? takeOver.HostSeat : 0;
            _botsChat = botsChat;
            _quickTable = quickTable;
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

            if (takeOver != null)
            {
                TakeOver(takeOver);
            }
        }

        /// <summary>The host player's own connection to this room.</summary>
        public IClientLink Local => _local;

        public string Code => _code;

        /// <summary>True while the room's match is being played.</summary>
        public bool InMatch => _table != null && !_table.IsOver;

        /// <summary>The running or last match, for tests and diagnostics.</summary>
        public TableHost Table => _table;

        /// <summary>The seats people took over the network: the host's own player and bots do not count.</summary>
        public int Guests
        {
            get
            {
                int guests = 0;
                for (int i = 0; i < Seats; i++)
                {
                    if (i != _hostSeat && _members[i] != null && !_members[i].IsBot)
                    {
                        guests++;
                    }
                }

                return guests;
            }
        }

        /// <summary>The seats nobody has taken, person or bot.</summary>
        public int FreeSeats
        {
            get
            {
                int free = 0;
                for (int i = 0; i < Seats; i++)
                {
                    if (_members[i] == null)
                    {
                        free++;
                    }
                }

                return free;
            }
        }

        /// <summary>True once a Quick Match table is locked for its start (<see cref="Lock"/>).</summary>
        public bool IsLocked => _locked;

        /// <summary>True once the host has left (<see cref="Close"/>): this room serves nobody any more.</summary>
        public bool IsClosed => _closed;

        /// <summary>
        /// True while the match would go on without this host should it leave (ADR 030): it is on, over
        /// a link that knows its peers' addresses, and someone is there to take it over.
        /// </summary>
        public bool CanHandOver => _addresses != null && !_closed && InMatch && Line().Count > 0;

        /// <summary>The seat of the host device's own player: 0, but in a room taken over, the seat of the player who took it over (ADR 030).</summary>
        public int HostSeat => _hostSeat;

        /// <summary>
        /// The room's generation (ADR 030): 0 as its creator opened it, one more each time a player
        /// took it over; 0 over a link that does not know about generations.
        /// </summary>
        public int Generation => _addresses != null ? _addresses.Generation : 0;

        /// <summary>Who takes the match over if this host is lost, as it last told its players (ADR 030); null before it named anyone.</summary>
        public NextHostNotice NextHost => _nextHost;

        /// <summary>True while some other person in the room is connected: a host whose players all dropped may have been taken over (ADR 030).</summary>
        public bool HasConnectedGuests
        {
            get
            {
                for (int i = 0; i < Seats; i++)
                {
                    Member m = _members[i];
                    if (i != _hostSeat && m != null && !m.IsBot && m.Connected)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

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

                if (now >= _nextBeatAt && ((_quickTable && _table == null) || (_addresses != null && InMatch)))
                {
                    Heartbeat();
                    _nextBeatAt = now + _heartbeatSeconds;
                }

                Pump();
                RefreshSuccession();
            }

            _faults.Rethrow();
        }

        /// <summary>
        /// Whether the host may start (GDD §11 screen 04): every seated guest is ready, and either
        /// all four seats are taken or the empty ones are to be filled with bots.
        /// </summary>
        public bool CanStart(bool fillWithBots)
        {
            if (_closed || InMatch || _members[_hostSeat] == null)
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
                if (i != _hostSeat && !m.Ready)
                {
                    return false;
                }
            }

            return people == Seats || fillWithBots;
        }

        /// <summary>
        /// Starts the match: every member is told to load the table, and the deal follows once they
        /// have. Each member sees every seat, the bots' included, in the deck it chose (ADR 031).
        /// </summary>
        public bool StartMatch(bool fillWithBots)
        {
            if (!CanStart(fillWithBots))
            {
                return false;
            }

            var profiles = new SeatProfile[Seats];
            var humans = new bool[Seats];
            var used = new HashSet<string>();
            var faces = new HashSet<int>();
            for (int i = 0; i < Seats; i++)
            {
                if (_members[i] != null)
                {
                    used.Add(_members[i].Name);
                    faces.Add(_members[i].AvatarId);
                }
            }

            // ADR 031: each player sees every card in the deck it chose, on its own device, so a seat's
            // deck draws nothing at anyone else's table; a person's says what that person chose, and a
            // bot's is the host's (the host's seat is always taken here).
            string deck = _members[_hostSeat].DeckThemeId;
            for (int i = 0; i < Seats; i++)
            {
                Member m = _members[i];
                if (m != null && m.IsBot)
                {
                    // A bot that sat down in a Quick Match lobby plays the seat under the name it showed there.
                    profiles[i] = new SeatProfile(m.Name, m.AvatarId.ToString(), deck, true);
                    continue;
                }

                if (m != null)
                {
                    humans[i] = true;
                    profiles[i] = new SeatProfile(m.Name, m.AvatarId.ToString(), m.DeckThemeId, false);
                    m.TableReady = false;
                    continue;
                }

                // A bot that fills an empty seat: a name and a portrait nobody at the table has (ADR 041).
                string name = SeatRoster.DrawName(_rng, used);
                used.Add(name);
                int avatar = SeatRoster.DrawAvatar(_rng, faces);
                faces.Add(avatar);
                profiles[i] = new SeatProfile(name, avatar.ToString(), deck, true);
            }

            // The match id goes to every player in every view, and no shuffle seed ever leaves the
            // host (GDD §22: whoever has one can deal its round in advance). So the id is a draw of
            // its own, and the table draws a fresh seed for each round as it deals it.
            string matchId = "room-" + _code + "-" + _seedSource().ToString("x16");
            TableClocks clocks = _clocks ?? TableClocks.PrivateRoom(_settings.TurnSeconds);
            var setup = new TableHostSetup(RuleSet.NepaliStandard, matchId, profiles, humans, clocks, _seedSource, _fillLevel, _timing, _botsChat);
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
                if (_members[i] != null && !_members[i].IsBot)
                {
                    Send(_members[i].Peer, HostMessages.MatchStarting(_table.ViewFor((Seat)i), status));
                }
            }

            Broadcast();
            Pump();
            RefreshSuccession();
            return true;
        }

        /// <summary>Host only: removes a guest (GDD §11 screen 04, long-press on a seat).</summary>
        public void Kick(int seatIndex)
        {
            if (_closed || seatIndex < 0 || seatIndex == _hostSeat || seatIndex >= Seats || _members[seatIndex] == null)
            {
                return;
            }

            Member m = _members[seatIndex];
            if (!string.IsNullOrEmpty(m.Identity))
            {
                _removed.Add(m.Identity);
            }

            if (m.Connected && !m.IsBot)
            {
                // A dropped member's old connection id may belong to someone else by now.
                Send(m.Peer, HostMessages.RoomClosed(RoomError.Kicked));
                ScheduleDrop(m.Peer);
            }

            RemoveMember(seatIndex, leftOnPurpose: true);
            Broadcast();
            Pump();
        }

        /// <summary>
        /// Quick Match (ADR 029): a bot takes the first empty seat of the lobby, under a name nobody at
        /// the table has, and every member sees it at once; at the start it plays that seat. False when
        /// no seat is empty, the match is on, or this is not a Quick Match table.
        /// </summary>
        public bool SeatBot()
        {
            if (!_quickTable || _closed || InMatch || _members[_hostSeat] == null)
            {
                return false;
            }

            int seat = -1;
            var used = new HashSet<string>();
            var faces = new HashSet<int>();
            for (int i = 0; i < Seats; i++)
            {
                if (_members[i] != null)
                {
                    used.Add(_members[i].Name);
                    faces.Add(_members[i].AvatarId);
                }
                else if (seat < 0)
                {
                    seat = i;
                }
            }

            if (seat < 0)
            {
                return false;
            }

            // In the host's deck: a bot chose none (each player draws it in its own anyway, ADR 031).
            // Under a portrait nobody at the table has yet (ADR 041).
            _members[seat] = Member.Bot(SeatRoster.DrawName(_rng, used), SeatRoster.DrawAvatar(_rng, faces), _members[_hostSeat].DeckThemeId);
            Broadcast();
            Pump();
            return true;
        }

        /// <summary>
        /// Tells every member over the network that this host is still there, so a player notices a
        /// host that went quiet (offline, or its app in the background): a Quick Match table's
        /// (ADR 029), and during a match one whose players can take over (ADR 030). <see cref="Tick"/>
        /// sends it every <c>heartbeatSeconds</c>.
        /// </summary>
        public void Heartbeat()
        {
            if (_closed || _remote == null || !(_quickTable || (_addresses != null && InMatch)))
            {
                return;
            }

            byte[] payload = HostMessages.Heartbeat();
            for (int i = 0; i < Seats; i++)
            {
                Member m = _members[i];
                if (i != _hostSeat && m != null && m.Connected && !m.IsBot)
                {
                    Send(m.Peer, payload);
                }
            }
        }

        /// <summary>
        /// Quick Match (ADR 029): the table is full and its match starts at <paramref name="startsAt"/>,
        /// by this host's clock. Nobody new joins any more, and every member is told how long it is
        /// until the start.
        /// </summary>
        public void Lock(double startsAt)
        {
            if (!_quickTable || _closed || InMatch)
            {
                return;
            }

            _locked = true;
            _startsAt = startsAt;
            Broadcast();
            Pump();
        }

        /// <summary>
        /// The host leaves (on purpose, or its app is closing). In the lobby, or where nobody can take
        /// the match over, the room is over for everyone (GDD §8, Phase 1). During a match its players
        /// can take over (ADR 030), the match goes on without this host (<see cref="HandOverNow"/>).
        /// </summary>
        public void Close()
        {
            if (_closed)
            {
                return;
            }

            if (!HandOverNow())
            {
                for (int i = 0; i < Seats; i++)
                {
                    if (i != _hostSeat && _members[i] != null && _members[i].Connected && !_members[i].IsBot)
                    {
                        Send(_members[i].Peer, HostMessages.RoomClosed(RoomError.HostLeft));
                    }
                }
            }

            Pump();
            _closed = true;
        }

        /// <summary>
        /// The host leaves during a match its players can take over (ADR 030): it never ends the game.
        /// The host's seat passes to a bot for the rest of the match, as anyone's who leaves (GDD §8),
        /// so the next host in line is sent that seat's hand with the bots'; then everyone is told to go
        /// to the next host in line now, which takes the table over at once. False, with nothing done,
        /// when there is nobody to take over.
        /// </summary>
        private bool HandOverNow()
        {
            if (_addresses == null || !InMatch || Line().Count == 0)
            {
                return false;
            }

            _table.Leave((Seat)_hostSeat, _now);
            RefreshSuccession();
            if (_nextHost == null || !_nextHost.HasNext)
            {
                return false;
            }

            byte[] payload = HostMessages.HandOverNow(_nextHost);
            for (int i = 0; i < Seats; i++)
            {
                Member m = _members[i];
                if (i != _hostSeat && m != null && m.Connected && !m.IsBot)
                {
                    Send(m.Peer, payload);
                }
            }

            return true;
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

            if (payload.Length > Protocol.MaxClientPayloadBytes
                && (payload.Length > Protocol.MaxReportBytes || payload[0] != (byte)ClientMessageKind.Report))
            {
                // No member ever sends this much; it is never read (GDD §25 "messages are tiny"). Only a
                // report, a hello with the player's view (ADR 030), is longer.
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
                if (kind == ClientMessageKind.Hello || kind == ClientMessageKind.Report)
                {
                    // A report is a hello with what the player holds (ADR 030); a room that is already
                    // running needs only the hello.
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
                            // GDD §8, Phase 1: the host leaving ends the room for everyone, but during a
                            // match its players can take over, which goes on without it (ADR 030).
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
            if (_seatOfPeer.TryGetValue(peer, out int seated))
            {
                Member member = _members[seated];
                if (member != null && member.Identity == hello.Identity && !string.IsNullOrEmpty(hello.Identity))
                {
                    // GDD §27 idempotent rejoin: the same player saying hello again on its connection
                    // (another part of its app took the connection over) is answered again.
                    member.TableReady = false;
                    Welcome(peer, seated, rejoined: true);
                }

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
            if (known == _hostSeat && !peer.IsLocal)
            {
                // The host's seat is the host device's own player, connected in process: whoever
                // presents its identity over the network is someone else, and never takes it over.
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
                Welcome(peer, known, rejoined: true);
                if (InMatch)
                {
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

            if (_locked)
            {
                // A locked Quick Match table is full and starting (ADR 029): nobody new sits down.
                Refuse(peer, RoomError.Full);
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

            // The host's seat is the host's; only the host device's own player may take it.
            if (free == _hostSeat && !peer.IsLocal)
            {
                free = -1;
                for (int i = 0; i < Seats; i++)
                {
                    if (i != _hostSeat && _members[i] == null)
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
            _members[free].Ready = _quickTable;
            _seatOfPeer[peer] = free;
            Send(peer, HostMessages.Welcome(free, Snapshot()));
            Broadcast();
        }

        /// <summary>
        /// Seats the member at <paramref name="seat"/>: the lobby, and during a match the table as that
        /// seat sees it and who takes over if the host is lost (ADR 030).
        /// </summary>
        private void Welcome(PeerId peer, int seat, bool rejoined)
        {
            Send(peer, HostMessages.Welcome(seat, Snapshot()));
            if (rejoined && InMatch)
            {
                Send(peer, HostMessages.MatchStarting(_table.ViewFor((Seat)seat), _table.StatusAt(_now)));
                if (_nextHost != null && !peer.IsLocal)
                {
                    Send(peer, HostMessages.NextHost(_nextHost));
                }
            }
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

            if (m.Connected && !m.IsBot)
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
                if (m != null && !m.IsBot && m.Connected && !m.TableReady)
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
            if (m != null && m.Connected && !m.IsBot)
            {
                if (_nextHost != null && (int)seat == _nextHost.NextSeat)
                {
                    // A new deal reaches the next host in line after the bots' hands for it (ADR 030).
                    RefreshSuccession();
                }

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
                    : new RoomSnapshot.Member(true, m.Name, m.AvatarId, m.DeckThemeId, i == _hostSeat, m.Ready, m.IsBot, m.Connected);
            }

            int startsInMs = _locked ? (int)Math.Round(Math.Max(0.0, _startsAt - _now) * 1000.0) : -1;
            return new RoomSnapshot(_code, _settings, seats, InMatch, startsInMs);
        }

        /// <summary>
        /// Names the line of succession and tells every player over the network (ADR 030; the owner's
        /// backup host, 3 Oct 2026): every person after the host in seat order who is connected and can
        /// be reached, bots and the people who left skipped. The first in line alone is sent what it
        /// needs to go on from the same trick, the bots' dealt hands included: again whenever a round is
        /// dealt, the line changes, or a seat passes to a bot. Every later successor is sent what it
        /// needs to take over without them. Nothing over a link that does not know its peers'
        /// addresses, nor outside a match.
        /// </summary>
        private void RefreshSuccession()
        {
            if (_addresses == null || _closed || !InMatch)
            {
                return;
            }

            List<NextHostNotice.Successor> line = Line();
            for (int i = 0; i < line.Count; i++)
            {
                SendPackage(line[i].Seat, withCards: i == 0);
            }

            var notice = new NextHostNotice(_addresses.Generation, _hostSeat, _addresses.LocalAddress, line);
            if (notice.Equals(_nextHost))
            {
                return;
            }

            _nextHost = notice;
            byte[] payload = HostMessages.NextHost(notice);
            for (int i = 0; i < Seats; i++)
            {
                Member m = _members[i];
                if (i != _hostSeat && m != null && m.Connected && !m.IsBot)
                {
                    Send(m.Peer, payload);
                }
            }
        }

        /// <summary>Every person after the host in seat order who is connected and can be reached, in order: the line of succession.</summary>
        private List<NextHostNotice.Successor> Line()
        {
            var line = new List<NextHostNotice.Successor>();
            var seat = (Seat)_hostSeat;
            for (int step = 1; step < Seats; step++)
            {
                seat = seat.Next(_table.Rules.Direction);
                Member m = _members[(int)seat];
                if (m == null || m.IsBot || !m.Connected || m.Peer.IsLocal || !_table.IsHuman(seat))
                {
                    continue;
                }

                string at = _addresses.AddressOf(m.Peer);
                if (!string.IsNullOrEmpty(at))
                {
                    line.Add(new NextHostNotice.Successor((int)seat, at));
                }
            }

            return line;
        }

        /// <summary>
        /// A successor's package (<see cref="SuccessionPackage"/>), unless it has this one already: the
        /// next host in line's <paramref name="withCards"/>, every later successor's without any.
        /// </summary>
        private void SendPackage(int seat, bool withCards)
        {
            Member heir = _members[seat];
            int round = withCards ? _table.Round : 0;
            int bots = 0;
            for (int s = 0; s < Seats; s++)
            {
                bots |= _table.IsHuman((Seat)s) ? 0 : 1 << s;
            }

            PackageSent last = _packages[seat];
            if (last != null && last.Peer == heir.Peer && last.WithCards == withCards && last.Round == round && last.Bots == bots)
            {
                return;
            }

            _packages[seat] = new PackageSent(heir.Peer, withCards, round, bots);
            var identities = new string[Seats];
            var dealt = new CardSet[Seats];
            for (int s = 0; s < Seats; s++)
            {
                if ((bots & (1 << s)) == 0)
                {
                    identities[s] = _members[s] != null ? _members[s].Identity : null;
                }
                else if (withCards)
                {
                    dealt[s] = _table.DealtHandOf((Seat)s);
                }
            }

            if (!withCards)
            {
                // A later successor deals the round again should it take over: no cards at all.
                Send(heir.Peer, HostMessages.Succession(SuccessionPackage.WithoutCards(_addresses.Generation, identities)));
                return;
            }

            // ADR 030 (owner-approved exception to AGENTS.md "a seat's hand leaves the server only by
            // TargetRpc to that seat"): the bot seats' dealt hands go to the next host in line alone,
            // so it can take the table over; never a person's hand, never to anyone later in the line,
            // never shown, never logged.
            Send(heir.Peer, HostMessages.Succession(new SuccessionPackage(_addresses.Generation, round, identities, dealt)));
        }

        /// <summary>
        /// Seats everyone at the rebuilt table of a room taken over (ADR 030): the people who brought
        /// their reports at once, the others when they come back (their seats held meanwhile), and this
        /// device's own player when it says hello over <see cref="Local"/>.
        /// </summary>
        private void TakeOver(RoomTakeOver takeOver)
        {
            _now = takeOver.Now;
            _locked = _quickTable;
            for (int s = 0; s < Seats; s++)
            {
                RoomTakeOver.SeatInfo seat = takeOver.Seats[s];
                if (seat == null)
                {
                    continue;
                }

                _members[s] = seat.IsBot
                    ? Member.Bot(seat.Name, seat.AvatarId, seat.DeckThemeId)
                    : new Member(default, seat.Identity, seat.Name, seat.AvatarId, seat.DeckThemeId) { Connected = false, Ready = _quickTable };
            }

            _table = takeOver.Table;
            _table.Outbound += OnTableOutbound;
            _table.Ended += OnTableEnded;
            _tableStartDeadline = _now + Protocol.TableReadyWaitSeconds;
            foreach (KeyValuePair<PeerId, int> reporter in takeOver.Reporters)
            {
                Member m = _members[reporter.Value];
                m.Peer = reporter.Key;
                m.Connected = true;
                m.TableReady = false;
                _seatOfPeer[reporter.Key] = reporter.Value;
                _guests[reporter.Key] = new Guest(_now) { SaidHello = true };
            }

            RefreshSuccession();
            foreach (KeyValuePair<PeerId, int> reporter in takeOver.Reporters)
            {
                Welcome(reporter.Key, reporter.Value, rejoined: true);
            }

            Broadcast();
            Pump();
        }

        private void Broadcast()
        {
            byte[] payload = HostMessages.RoomState(Snapshot());
            for (int i = 0; i < Seats; i++)
            {
                if (_members[i] != null && _members[i].Connected && !_members[i].IsBot)
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

            /// <summary>A bot sitting in a Quick Match lobby: no connection, always ready.</summary>
            public bool IsBot { get; private set; }

            /// <summary>The member's <see cref="RoomIdentity"/>: how a rejoin finds this seat again.</summary>
            public string Identity { get; }

            public string Name { get; }

            public int AvatarId { get; }

            public string DeckThemeId { get; }

            public bool Connected { get; set; }

            public bool Ready { get; set; }

            public bool TableReady { get; set; }

            public static Member Bot(string name, int avatarId, string deckThemeId) =>
                new Member(default, null, name, avatarId, deckThemeId) { IsBot = true, Ready = true };
        }

        /// <summary>The last package a successor's seat was sent: another goes only when something in it would change.</summary>
        private sealed class PackageSent
        {
            public PackageSent(PeerId peer, bool withCards, int round, int bots)
            {
                Peer = peer;
                WithCards = withCards;
                Round = round;
                Bots = bots;
            }

            public PeerId Peer { get; }

            public bool WithCards { get; }

            public int Round { get; }

            /// <summary>The bot seats, one bit each.</summary>
            public int Bots { get; }
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
