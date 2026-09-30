using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Randomness;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// Private rooms by code (GDD §11 screens 02-04, §27): creating one makes this device the host
    /// (Phase 1: host and relay) with its own player seated first; joining one connects to that
    /// host. One room at a time. While a match is on, a dropped connection is retried with
    /// growing pauses for as long as the host holds the seat; after that the room is closed.
    /// </summary>
    public sealed class RoomService : IRoomService, IDisposable
    {
        /// <summary>GDD §25: join attempts per device are limited, which stops code guessing.</summary>
        public const int JoinAttemptsPerMinute = 6;

        private const double ClosingGraceSeconds = 0.5;
        private const double TicketRefreshSeconds = 60.0;
        private const int CodeAttempts = 3;

        private static readonly double[] RetryPauses = { 1, 2, 4, 8 };

        private readonly IRoomTransport _transport;
        private readonly IProfileService _profile;
        private readonly ICosmeticsService _cosmetics;
        private readonly IAuthService _auth;
        private readonly IBotFactory _bots;
        private readonly IStatsService _stats;
        private readonly ISettingsService _settings;
        private readonly INetworkStatus _network;
        private readonly NameFilter _names;
        private readonly Func<ulong> _seedSource;
        private readonly IRandom _rng;
        private readonly BotTiming _timing;
        private readonly IRejoinStore _tickets;
        private readonly IAnalytics _analytics;
        private readonly List<double> _joinAttempts = new List<double>();
        private readonly List<Closing> _closing = new List<Closing>();
        private readonly string _fallbackPlayerId;

        private RoomHost _host;
        private IHostLink _hostLink;
        private RoomClient _client;
        private RoomClient _joining;
        private IClientLink _clientLink;
        private double _now;
        private bool _busy;
        private bool _reconnecting;
        private double _reconnectUntil;
        private double _nextRetryAt;
        private int _retries;
        private Task<ClientLinkResult> _retry;
        private Task<RoomError> _rejoin;
        private CancellationTokenSource _retryCancel;
        private bool _ticketHeld;
        private double _nextTicketAt;

        /// <param name="seedSource">Draws shuffle seeds and the room code's randomness (a CSPRNG in production).</param>
        /// <param name="tickets">Where a guest's running match is remembered for "Rejoin match"; null keeps none.</param>
        /// <param name="analytics">Receives room_created and room_joined (GDD §15); null reports nothing.</param>
        public RoomService(
            IRoomTransport transport,
            IProfileService profile,
            ICosmeticsService cosmetics,
            IAuthService auth,
            IBotFactory bots,
            IStatsService stats,
            ISettingsService settings,
            INetworkStatus network,
            NameFilter names,
            Func<ulong> seedSource,
            BotTiming timing = null,
            IRejoinStore tickets = null,
            IAnalytics analytics = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _profile = profile;
            _cosmetics = cosmetics;
            _auth = auth;
            _bots = bots;
            _stats = stats;
            _settings = settings;
            _network = network;
            _names = names;
            _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
            _rng = new Pcg32Random(_seedSource());
            _timing = timing;
            _tickets = tickets;
            _analytics = analytics;
            _fallbackPlayerId = "guest-" + _seedSource().ToString("x16");
        }

        public IRoomSession Current => _client != null && !_client.IsClosed ? _client : null;

        /// <summary>The member's connection to the host, for tests that drop it.</summary>
        internal IClientLink ClientLink => _clientLink;

        public async Task<RoomResult> CreateRoomAsync(RoomSettings settings, CancellationToken ct)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (_busy)
            {
                return new RoomResult(null, RoomError.RateLimited);
            }

            if (!_network.IsOnline)
            {
                return new RoomResult(null, RoomError.Offline);
            }

            _busy = true;
            try
            {
                LeaveCurrent();
                // A code someone else is hosting right now comes back as AlreadyStarted: draw another.
                string code = RoomCode.Generate(_rng.NextInt);
                HostLinkResult hosted = await _transport.HostAsync(code, ct);
                for (int attempt = 0; attempt < CodeAttempts && hosted.Error == RoomError.AlreadyStarted; attempt++)
                {
                    code = RoomCode.Generate(_rng.NextInt);
                    hosted = await _transport.HostAsync(code, ct);
                }

                if (!hosted.Succeeded)
                {
                    return new RoomResult(null, hosted.Error == RoomError.None ? RoomError.Unknown : hosted.Error);
                }

                var host = new RoomHost(code, settings, hosted.Link, _bots, _names, _rng, _cosmetics.DeckIds, _seedSource, _timing);
                host.Tick(_now);
                var client = new RoomClient(host.Local, Identity(), host, MatchMode.PrivateRoom, _stats, _settings);
                client.Tick(_now);
                RoomError joined = await client.JoinAsync(ct);
                if (joined != RoomError.None)
                {
                    client.Dispose();
                    host.Dispose();
                    hosted.Link.Dispose();
                    return new RoomResult(null, joined);
                }

                _host = host;
                _hostLink = hosted.Link;
                Adopt(client, host.Local);
                _analytics?.Track("room_created", ("turn_seconds", settings.TurnSeconds));
                return new RoomResult(client, RoomError.None);
            }
            finally
            {
                _busy = false;
            }
        }

        public async Task<RoomResult> JoinRoomAsync(string code, CancellationToken ct)
        {
            string normalized = RoomCode.Normalize(code);
            if (!RoomCode.IsValid(normalized))
            {
                return new RoomResult(null, RoomError.NotFound);
            }

            if (_busy || !AllowJoinAttempt())
            {
                return new RoomResult(null, RoomError.RateLimited);
            }

            if (!_network.IsOnline)
            {
                return new RoomResult(null, RoomError.Offline);
            }

            _busy = true;
            try
            {
                LeaveCurrent();
                ClientLinkResult linked = await _transport.JoinAsync(normalized, ct);
                if (!linked.Succeeded)
                {
                    return new RoomResult(null, linked.Error == RoomError.None ? RoomError.Unknown : linked.Error);
                }

                var client = new RoomClient(linked.Link, Identity(), null, MatchMode.PrivateRoom, _stats, _settings);
                client.Tick(_now);
                _joining = client;
                RoomError joined;
                try
                {
                    joined = await client.JoinAsync(ct);
                }
                finally
                {
                    _joining = null;
                }

                if (joined != RoomError.None)
                {
                    client.Dispose();
                    linked.Link.Dispose();
                    return new RoomResult(null, joined);
                }

                Adopt(client, linked.Link);
                _analytics?.Track("room_joined");
                return new RoomResult(client, RoomError.None);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Drives the hosted room, the member and any reconnect. Call every frame.</summary>
        public void Tick(double now)
        {
            _now = now;
            if (_host != null)
            {
                _host.Tick(now);
            }

            if (_joining != null)
            {
                _joining.Tick(now);
            }

            if (_client != null)
            {
                _client.Tick(now);
            }

            if (_reconnecting)
            {
                Reconnect(now);
            }

            TrackRejoinTicket(now);
            for (int i = _closing.Count - 1; i >= 0; i--)
            {
                if (now >= _closing[i].At)
                {
                    _closing[i].Dispose();
                    _closing.RemoveAt(i);
                }
            }
        }

        public void Dispose()
        {
            LeaveCurrent();
            foreach (Closing closing in _closing)
            {
                closing.Dispose();
            }

            _closing.Clear();
        }

        private HelloMessage Identity()
        {
            string playerId = _auth != null && !string.IsNullOrEmpty(_auth.PlayerId) ? _auth.PlayerId : _fallbackPlayerId;
            return new HelloMessage(Protocol.Version, playerId, _profile.DisplayName, _profile.AvatarId, _cosmetics.SelectedDeckId);
        }

        private bool AllowJoinAttempt()
        {
            _joinAttempts.RemoveAll(at => _now - at >= 60.0);
            if (_joinAttempts.Count >= JoinAttemptsPerMinute)
            {
                return false;
            }

            _joinAttempts.Add(_now);
            return true;
        }

        private void Adopt(RoomClient client, IClientLink link)
        {
            _client = client;
            _clientLink = link;
            client.Closed += OnClosed;
            client.LinkLost += OnLinkLost;
        }

        private void LeaveCurrent()
        {
            if (_client != null && !_client.IsClosed)
            {
                _client.LeaveAsync(CancellationToken.None);
            }

            Release();
            DropRejoinTicket();
        }

        /// <summary>
        /// GDD §27 and §29: while this device plays a match as a guest it keeps a fresh ticket, so
        /// the next launch can offer "Rejoin match" after an app kill. The host keeps none: its
        /// room dies with its app (Phase 1).
        /// </summary>
        private void TrackRejoinTicket(double now)
        {
            if (_tickets == null)
            {
                return;
            }

            RoomClient client = _client;
            if (client == null || client.IsClosed || client.IsHost || !client.InMatch)
            {
                DropRejoinTicket();
                return;
            }

            if (!_ticketHeld || now >= _nextTicketAt)
            {
                _tickets.Remember(client.Code);
                _ticketHeld = true;
                _nextTicketAt = now + TicketRefreshSeconds;
            }
        }

        private void DropRejoinTicket()
        {
            if (_ticketHeld)
            {
                _ticketHeld = false;
                _tickets.Clear();
            }
        }

        private void OnClosed(RoomError reason) => Release();

        /// <summary>Lets the last messages out, then closes the connections of the room that ended.</summary>
        private void Release()
        {
            StopReconnecting();
            if (_client == null && _host == null)
            {
                return;
            }

            var closing = new Closing(_now + ClosingGraceSeconds, _client, _clientLink, _host, _hostLink);
            if (_client != null)
            {
                _client.Closed -= OnClosed;
                _client.LinkLost -= OnLinkLost;
            }

            _client = null;
            _clientLink = null;
            _host = null;
            _hostLink = null;
            _closing.Add(closing);
        }

        /// <summary>
        /// GDD §8 and §27: in a match the seat is held for a while, so try again with growing
        /// pauses until it would be given up. In the lobby a lost connection simply ends the room.
        /// </summary>
        private void OnLinkLost()
        {
            if (_client == null || _client.IsClosed)
            {
                return;
            }

            if (_client.IsHost || !_client.InMatch)
            {
                _client.Close(RoomError.ConnectionLost);
                return;
            }

            _reconnecting = true;
            _retries = 0;
            _reconnectUntil = _now + Protocol.SeatHoldSeconds;
            _nextRetryAt = _now + RetryPauses[0];
            _retry = null;
            _rejoin = null;
        }

        private void Reconnect(double now)
        {
            RoomClient client = _client;
            if (client == null || client.IsClosed)
            {
                StopReconnecting();
                return;
            }

            if (_rejoin != null)
            {
                if (!_rejoin.IsCompleted)
                {
                    return;
                }

                RoomError result = _rejoin.Status == TaskStatus.RanToCompletion ? _rejoin.Result : RoomError.Unknown;
                _rejoin = null;
                if (result == RoomError.None)
                {
                    StopReconnecting();
                    return;
                }

                if (result == RoomError.AlreadyStarted || result == RoomError.Full || result == RoomError.UpdateRequired)
                {
                    // The host no longer knows this player: the seat was given up.
                    StopReconnecting();
                    client.Close(RoomError.ConnectionLost);
                    return;
                }
            }

            if (_retry != null)
            {
                if (!_retry.IsCompleted)
                {
                    return;
                }

                bool linked = _retry.Status == TaskStatus.RanToCompletion && _retry.Result.Succeeded;
                IClientLink link = linked ? _retry.Result.Link : null;
                _retry = null;
                if (linked)
                {
                    if (_clientLink != null)
                    {
                        _clientLink.Dispose();
                    }

                    _clientLink = link;
                    _rejoin = client.RejoinAsync(link, _retryCancel.Token);
                    return;
                }
            }

            if (now >= _reconnectUntil)
            {
                StopReconnecting();
                client.Close(RoomError.ConnectionLost);
                return;
            }

            if (now >= _nextRetryAt)
            {
                _nextRetryAt = now + RetryPauses[Math.Min(++_retries, RetryPauses.Length - 1)];
                if (_network.IsOnline)
                {
                    if (_retryCancel == null)
                    {
                        _retryCancel = new CancellationTokenSource();
                    }

                    _retry = _transport.JoinAsync(client.Code, _retryCancel.Token);
                }
            }
        }

        private void StopReconnecting()
        {
            _reconnecting = false;
            _retry = null;
            _rejoin = null;
            if (_retryCancel != null)
            {
                _retryCancel.Cancel();
                _retryCancel.Dispose();
                _retryCancel = null;
            }
        }

        private sealed class Closing
        {
            private readonly RoomClient _client;
            private readonly IClientLink _clientLink;
            private readonly RoomHost _host;
            private readonly IHostLink _hostLink;

            public Closing(double at, RoomClient client, IClientLink clientLink, RoomHost host, IHostLink hostLink)
            {
                At = at;
                _client = client;
                _clientLink = clientLink;
                _host = host;
                _hostLink = hostLink;
            }

            public double At { get; }

            public void Dispose()
            {
                if (_client != null)
                {
                    _client.Dispose();
                }

                if (_host != null)
                {
                    _host.Dispose();
                }

                if (_clientLink != null)
                {
                    _clientLink.Dispose();
                }

                if (_hostLink != null)
                {
                    _hostLink.Dispose();
                }
            }
        }
    }
}
