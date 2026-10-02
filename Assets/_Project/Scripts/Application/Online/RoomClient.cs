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
    public sealed class RoomClient : IRoomSession
    {
        private const double JoinTimeoutSeconds = 10.0;

        private readonly HelloMessage _identity;
        private readonly RoomHost _host;
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

        /// <summary>Introduces this player to the host. None when seated; otherwise why not.</summary>
        public Task<RoomError> JoinAsync(CancellationToken ct)
        {
            FinishJoin(RoomError.Unknown);
            var join = new TaskCompletionSource<RoomError>();
            _join = join;
            _joinDeadline = double.NaN;
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

            Send(ClientMessages.Hello(_identity));
            return join.Task;
        }

        /// <summary>Replaces a dropped connection and introduces this player again (GDD §27 reconnection).</summary>
        public Task<RoomError> RejoinAsync(IClientLink link, CancellationToken ct)
        {
            Detach();
            Attach(link);
            return JoinAsync(ct);
        }

        public void Tick(double now)
        {
            _now = now;
            if (_join != null)
            {
                // The wait starts on the first tick after the hello went out.
                if (double.IsNaN(_joinDeadline))
                {
                    _joinDeadline = now + JoinTimeoutSeconds;
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
        public void Close(RoomError reason)
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
                    _match.OnRoomClosed(reason);
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
                // GDD §8, Phase 1: when the host leaves, the room is over for everyone.
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

            _match = new OnlineMatchSession(_mode, view, status, Send, _stats, _settings, OnMatchLeft);
            _match.Tick(_now);
            _faults.Raise(MatchStarting);
        }

        private void Apply(RoomSnapshot snapshot)
        {
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
