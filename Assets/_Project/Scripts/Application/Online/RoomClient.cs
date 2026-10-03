using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Commands;
using Callbreak.Core.Match;
using Callbreak.Core.Views;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// One member's side of a room (GDD §11 screen 04, §27): it introduces itself, mirrors the
    /// lobby the host publishes, and, when the host starts, owns the
    /// <see cref="OnlineMatchSession"/> the table plays through. The host's own player uses the
    /// same class over an in-process link, with the extra right to start and to remove players.
    /// A screen whose handler throws never stops a message from being applied whole: the join
    /// still completes and a closed room still says so, and the exception is passed on afterwards.
    /// </summary>
    public sealed class RoomClient : IRoomSession, IHandOverRoom
    {
        private const double JoinTimeoutSeconds = 10.0;

        /// <summary>A host that took a table over answers a report once every player's is in (ADR 030): up to <see cref="Protocol.HandOverWaitSeconds"/> and the time to rebuild.</summary>
        private const double ReportTimeoutSeconds = 20.0;

        private readonly HelloMessage _identity;
        private RoomHost _host;
        private readonly MatchMode _mode;
        private readonly IStatsService _stats;
        private readonly ISettingsService _settings;
        private readonly RoomSeat[] _seats = new RoomSeat[RoomSnapshot.SeatCount];
        private readonly HandlerFaults _faults = new HandlerFaults();

        private IClientLink _link;
        private OnlineMatchSession _match;
        private TaskCompletionSource<RoomError> _join;
        private CancellationTokenRegistration _joinCancellation;
        private RoomSettings _roomSettings = new RoomSettings(string.Empty, 30);
        private string _code = string.Empty;
        private int _localSeat = -1;
        private double _now;
        private double _joinDeadline;
        private double _joinTimeout = JoinTimeoutSeconds;
        private bool _closed;

        /// <param name="host">The room's authority when this member is its host; null for a guest.</param>
        public RoomClient(IClientLink link, HelloMessage identity, RoomHost host, MatchMode mode, IStatsService stats, ISettingsService settings)
        {
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));
            _host = host;
            _mode = mode;
            _stats = stats;
            _settings = settings;
            for (int i = 0; i < _seats.Length; i++)
            {
                _seats[i] = EmptySeat(i);
            }

            Attach(link);
        }

        public event Action Changed;

        public event Action MatchStarting;

        public event Action<RoomError> Closed;

        /// <summary>The connection dropped while the room is still wanted; the room service reconnects or closes.</summary>
        public event Action LinkLost;

        public string Code => _code;

        public RoomSettings Settings => _roomSettings;

        public IReadOnlyList<RoomSeat> Seats => _seats;

        public bool IsHost => _host != null;

        public bool HandsOverWhenLeaving => _host != null && _host.CanHandOver;

        public int LocalSeatIndex => _localSeat;

        /// <summary>True while the room's match is being played.</summary>
        public bool InMatch { get; private set; }

        public bool IsClosed => _closed;

        /// <summary>The running or last match of this room; null before the first start.</summary>
        public IMatchSession Match => _match;

        /// <summary>
        /// A Quick Match table that is full and locked: when its match starts, by this member's clock
        /// (the host says it as the time from then); NaN while the table is open (ADR 029).
        /// </summary>
        public double StartsAt { get; private set; } = double.NaN;

        /// <summary>
        /// How many messages this member has had from its host, whatever they said: a count that
        /// stops moving means a host that went quiet (ADR 029).
        /// </summary>
        public int Heard { get; private set; }

        /// <summary>Who takes the match over if the host is lost, as the host last said (ADR 030); null when it never said.</summary>
        public NextHostNotice NextHost { get; private set; }

        /// <summary>
        /// The host left during the match and said so (ADR 030): the next host in line
        /// (<see cref="NextHost"/>) takes the table over now, and this player goes there. Over again
        /// once this player is connected to a host anew.
        /// </summary>
        public bool HostLeaving { get; private set; }

        /// <summary>
        /// What this player needs to take the match over (ADR 030): the next host in line's holds the
        /// bots' dealt hands, so nothing ever shows or logs it; a later successor's holds no cards (the
        /// owner's backup host). Let go of as soon as the line no longer names this player for it.
        /// </summary>
        internal SuccessionPackage Package { get; private set; }

        /// <summary>The lobby as the host last published it, with who is connected; null before the first.</summary>
        internal RoomSnapshot Room { get; private set; }

        /// <summary>This player's hello: its version, identity and looks.</summary>
        internal HelloMessage Identity => _identity;

        /// <summary>Introduces this player to the host. None when seated; otherwise why not.</summary>
        public Task<RoomError> JoinAsync(CancellationToken ct) => Introduce(ClientMessages.Hello(_identity), JoinTimeoutSeconds, ct);

        /// <summary>Sends <paramref name="hello"/> and waits for the host's answer, for <paramref name="timeout"/> seconds from the next tick at most.</summary>
        private Task<RoomError> Introduce(byte[] hello, double timeout, CancellationToken ct)
        {
            FinishJoin(RoomError.Unknown);
            var join = new TaskCompletionSource<RoomError>();
            _join = join;
            _joinDeadline = double.NaN;
            _joinTimeout = timeout;
            if (ct.CanBeCanceled)
            {
                _joinCancellation = ct.Register(() =>
                {
                    if (_join == join)
                    {
                        FinishJoin(RoomError.Unknown);
                    }
                });
            }

            Send(hello);
            return join.Task;
        }

        /// <summary>
        /// Replaces a dropped connection and introduces this player again (GDD §27 reconnection). A
        /// player that hosted a table of the match before and gives it up for this one (ADR 030) is a
        /// guest from now on.
        /// </summary>
        public Task<RoomError> RejoinAsync(IClientLink link, CancellationToken ct)
        {
            _host = null;
            HostLeaving = false;
            Detach();
            Attach(link);
            return JoinAsync(ct);
        }

        /// <summary>
        /// Goes to the host that took the match over (ADR 030) over <paramref name="link"/>, with what
        /// this player holds: its last update and that update's view. The new host answers once it has
        /// rebuilt the table. A player that hosted the match before is a guest from now on.
        /// </summary>
        internal Task<RoomError> ReportAsync(IClientLink link, CancellationToken ct)
        {
            _host = null;
            HostLeaving = false;
            Detach();
            Attach(link);
            byte[] report = _match != null
                ? ClientMessages.Report(_identity, _match.LastSeq, _match.View)
                : ClientMessages.Hello(_identity);
            return Introduce(report, ReportTimeoutSeconds, ct);
        }

        /// <summary>
        /// This player took the match over (ADR 030): it hosts the rebuilt room from now on, and says
        /// hello to it over that room's own connection, which takes its seat back at once.
        /// </summary>
        internal Task<RoomError> HostAsync(RoomHost host, CancellationToken ct)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            // What the host before said about its succession is over; this room names its own.
            NextHost = null;
            Package = null;
            HostLeaving = false;
            Detach();
            Attach(host.Local);
            return JoinAsync(ct);
        }

        /// <summary>
        /// Lets go of the connection without closing the room (ADR 030: the host was lost, or this
        /// player's own room is to be given up): the match shows it is reconnecting meanwhile. The link
        /// is the caller's to dispose.
        /// </summary>
        internal void Unlink()
        {
            Detach();
            if (_match != null)
            {
                try
                {
                    _match.OnLinkLost();
                }
                catch (Exception e)
                {
                    _faults.Keep(e);
                }
            }

            _faults.Rethrow();
        }

        public void Tick(double now)
        {
            _now = now;
            if (_join != null)
            {
                // The wait starts on the first tick after the hello went out.
                if (double.IsNaN(_joinDeadline))
                {
                    _joinDeadline = now + _joinTimeout;
                }
                else if (now >= _joinDeadline)
                {
                    FinishJoin(RoomError.Unknown);
                }
            }

            if (_match != null)
            {
                _match.Tick(now);
            }
        }

        public Task SetReadyAsync(bool ready, CancellationToken ct)
        {
            Send(ClientMessages.SetReady(ready));
            return Task.CompletedTask;
        }

        public Task<bool> StartAsync(bool fillWithBots, CancellationToken ct)
        {
            return Task.FromResult(_host != null && _host.StartMatch(fillWithBots));
        }

        public Task KickAsync(int seatIndex, CancellationToken ct)
        {
            if (_host != null)
            {
                _host.Kick(seatIndex);
            }

            return Task.CompletedTask;
        }

        public Task LeaveAsync(CancellationToken ct)
        {
            if (_closed)
            {
                return Task.CompletedTask;
            }

            if (_match != null)
            {
                try
                {
                    _match.LeaveAsync(ct);
                }
                catch (Exception e)
                {
                    _faults.Keep(e);
                }
            }

            if (!_closed)
            {
                LeaveRoom();
            }

            _faults.Rethrow();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            FinishJoin(RoomError.Unknown);
            Detach();
            if (_match != null)
            {
                _match.Dispose();
            }
        }

        /// <summary>The service gave up reconnecting, or the room is otherwise over for this member.</summary>
        public void Close(RoomError reason) => Close(reason, countsAsLeft: true);

        /// <param name="countsAsLeft">
        /// Whether a lost connection records the match as left (<see cref="OnlineMatchSession.OnRoomClosed(RoomError, bool)"/>):
        /// false when the hand-over gave up while this device was online, nobody having taken the match over.
        /// </param>
        internal void Close(RoomError reason, bool countsAsLeft)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            FinishJoin(reason);
            if (_match != null)
            {
                try
                {
                    _match.OnRoomClosed(reason, countsAsLeft);
                }
                catch (Exception e)
                {
                    _faults.Keep(e);
                }
            }

            _faults.Raise(Closed, reason);
            _faults.Rethrow();
        }

        /// <summary>Leaving the match on purpose also leaves the room.</summary>
        private void OnMatchLeft()
        {
            if (!_closed)
            {
                LeaveRoom();
            }

            _faults.Rethrow();
        }

        private void LeaveRoom()
        {
            _closed = true;
            if (_host != null)
            {
                // GDD §8, Phase 1: when the host leaves, the room is over for everyone; but a match
                // its players can take over goes on without it (ADR 030).
                try
                {
                    _host.Close();
                }
                catch (Exception e)
                {
                    _faults.Keep(e);
                }
            }
            else
            {
                Send(ClientMessages.LeaveRoom());
            }

            _faults.Raise(Closed, RoomError.None);
        }

        private void Attach(IClientLink link)
        {
            _link = link ?? throw new ArgumentNullException(nameof(link));
            _link.Received += OnReceived;
            _link.Closed += OnLinkClosed;
        }

        private void Detach()
        {
            if (_link == null)
            {
                return;
            }

            _link.Received -= OnReceived;
            _link.Closed -= OnLinkClosed;
            _link = null;
        }

        private void Send(byte[] payload)
        {
            if (_link != null && _link.IsConnected)
            {
                _link.Send(payload);
            }
        }

        private void OnLinkClosed()
        {
            if (_closed)
            {
                return;
            }

            if (_join != null)
            {
                FinishJoin(RoomError.ConnectionLost);
                return;
            }

            if (_match != null)
            {
                try
                {
                    _match.OnLinkLost();
                }
                catch (Exception e)
                {
                    _faults.Keep(e);
                }
            }

            _faults.Raise(LinkLost);
            _faults.Rethrow();
        }

        private void OnReceived(byte[] payload)
        {
            if (_closed || payload == null)
            {
                return;
            }

            Heard++;

            try
            {
                var reader = new WireReader(payload);
                var kind = (HostMessageKind)reader.Byte();
                switch (kind)
                {
                    case HostMessageKind.Welcome:
                    {
                        _localSeat = reader.Byte();
                        RoomSnapshot room = RoomSnapshot.Read(reader);
                        Apply(room);
                        FinishJoin(RoomError.None);
                        if (!room.InMatch && _match != null && _match.Status != SessionStatus.Ended && _match.Status != SessionStatus.Failed)
                        {
                            // Back in the room after its match ended, without that match's end: the
                            // seat hold ran out first, so the match cannot go on and counts as not
                            // having come back in time. Nothing is left waiting on "Reconnecting".
                            Close(RoomError.ConnectionLost);
                        }

                        break;
                    }

                    case HostMessageKind.Rejected:
                        FinishJoin(ReadError(reader));
                        break;

                    case HostMessageKind.RoomState:
                        Apply(RoomSnapshot.Read(reader));
                        break;

                    case HostMessageKind.RoomClosed:
                        Close(ReadError(reader));
                        break;

                    case HostMessageKind.MatchStarting:
                        OnMatchStarting(WireCodec.ReadPlayerView(reader), TableStatus.Read(reader));
                        break;

                    case HostMessageKind.TableBatch:
                        if (_match != null)
                        {
                            _match.OnBatch(TableUpdate.Read(reader));
                        }

                        break;

                    case HostMessageKind.SyncState:
                        if (_match != null)
                        {
                            _match.OnSync(TableUpdate.Read(reader));
                        }

                        break;

                    case HostMessageKind.IntentRejected:
                    {
                        TurnId turn = WireCodec.ReadTurnId(reader);
                        var reason = (RejectReason)reader.Byte();
                        if (_match != null)
                        {
                            _match.OnIntentRejected(turn, reason);
                        }

                        break;
                    }

                    case HostMessageKind.EmoteShown:
                    {
                        Seat seat = WireCodec.ReadSeat(reader);
                        int emote = reader.Byte();
                        if (_match != null)
                        {
                            _match.OnEmote(seat, emote);
                        }

                        break;
                    }

                    case HostMessageKind.NextHost:
                        NextHost = NextHostNotice.Read(reader);
                        KeepPackageFor(NextHost);
                        break;

                    case HostMessageKind.Succession:
                        Package = SuccessionPackage.Read(reader);
                        break;

                    case HostMessageKind.HandOverNow:
                        NextHost = NextHostNotice.Read(reader);
                        HostLeaving = NextHost.HasNext;
                        KeepPackageFor(NextHost);
                        break;
                }
            }
            catch (WireFormatException)
            {
                // A message this build cannot read is skipped; a missed table update shows up as a
                // gap in the sequence and is repaired by a resync.
            }
            catch (Exception e)
            {
                // A handler of the match threw after the match had applied the message.
                _faults.Keep(e);
            }

            _faults.Rethrow();
        }

        /// <summary>
        /// Keeps the package only where <paramref name="notice"/> puts this player for it: one with the
        /// bots' cards while it is the next host in line, one without while it is anywhere in the line,
        /// for the same host. Someone else is next, or this player is out of the line: the bots' cards,
        /// and the identities, are not this player's to keep.
        /// </summary>
        private void KeepPackageFor(NextHostNotice notice)
        {
            if (Package == null)
            {
                return;
            }

            int place = notice.PlaceOf(_localSeat);
            bool keep = Package.Generation == notice.Generation && (Package.HasCards ? place == 1 : place >= 1);
            if (!keep)
            {
                Package = null;
            }
        }

        private void OnMatchStarting(PlayerView view, TableStatus status)
        {
            InMatch = true;
            if (_match != null && _match.MatchId == view.MatchId
                && _match.Status != SessionStatus.Ended && _match.Status != SessionStatus.Failed)
            {
                // Back in a match this member was already playing.
                _match.OnRejoined();
                return;
            }

            if (_match != null)
            {
                _match.Dispose();
            }

            // ADR 031: this player sees every card in the deck it chose, the one its hello named.
            _match = new OnlineMatchSession(_mode, view, status, Send, _stats, _settings, OnMatchLeft, _identity.DeckThemeId);
            _match.Tick(_now);
            _faults.Raise(MatchStarting);
        }

        private void Apply(RoomSnapshot snapshot)
        {
            Room = snapshot;
            _code = snapshot.Code;
            _roomSettings = snapshot.Settings;
            InMatch = snapshot.InMatch;
            StartsAt = snapshot.StartsInMs >= 0 ? _now + (snapshot.StartsInMs / 1000.0) : double.NaN;
            for (int i = 0; i < _seats.Length; i++)
            {
                RoomSnapshot.Member m = snapshot.Seats[i];
                _seats[i] = new RoomSeat(i, m.Occupied, m.Name, m.AvatarId, m.DeckThemeId, m.IsHost, m.IsReady, m.IsBot, i == _localSeat);
            }

            _faults.Raise(Changed);
        }

        private void FinishJoin(RoomError result)
        {
            TaskCompletionSource<RoomError> join = _join;
            if (join == null)
            {
                return;
            }

            _join = null;
            _joinCancellation.Dispose();
            _joinCancellation = default;
            join.TrySetResult(result);
        }

        private static RoomError ReadError(WireReader reader)
        {
            byte value = reader.Byte();
            return value <= (byte)RoomError.ConnectionLost ? (RoomError)value : RoomError.Unknown;
        }

        private static RoomSeat EmptySeat(int index) =>
            new RoomSeat(index, false, string.Empty, 0, string.Empty, index == 0, false, false, false);
    }
}
