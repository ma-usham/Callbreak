using System;
using System.Collections.Generic;
using Callbreak.Application.Ports;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;

namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// The match events of GDD §15 "Analytics events", read off a session: match_start, bot_fill,
    /// round_end, auto_play, reconnect and match_end. Only the local seat's numbers leave the
    /// device, never a name or a card. Start it before the session, so it sees the first event.
    /// </summary>
    public sealed class MatchAnalytics : IDisposable
    {
        private readonly IMatchSession _session;
        private readonly MatchLaunchRequest _request;
        private readonly IAnalytics _analytics;
        private readonly IClock _clock;

        private double _startedAt;
        private double _lostAt;
        private bool _listening;
        private bool _reconnecting;
        private bool _ended;

        public MatchAnalytics(IMatchSession session, MatchLaunchRequest request, IAnalytics analytics, IClock clock)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public void Start()
        {
            if (_listening)
            {
                return;
            }

            _listening = true;
            _startedAt = _clock.Now;
            _session.EventReceived += OnEvent;
            _session.Resynced += OnResynced;
            _session.StatusChanged += OnStatusChanged;
        }

        public void Dispose()
        {
            if (!_listening)
            {
                return;
            }

            _listening = false;
            _session.EventReceived -= OnEvent;
            _session.Resynced -= OnResynced;
            _session.StatusChanged -= OnStatusChanged;
        }

        private void OnEvent(MatchEvent e)
        {
            Seat local = _session.LocalSeat;
            switch (e)
            {
                case MatchStarted started:
                    TrackStart(started);
                    break;

                case CallPlaced call when call.Seat == local && call.IsAuto:
                    _analytics.Track("auto_play", ("kind", "call"));
                    break;

                // A forced card is the engine's own move (the only card possible), not a missed turn.
                case CardPlayed card when card.Seat == local && card.IsAuto && !card.IsForced:
                    _analytics.Track("auto_play", ("kind", "card"));
                    break;

                case RoundScored scored:
                    SeatRoundResult mine = scored.Result[local];
                    _analytics.Track("round_end", ("round", scored.Round), ("call", mine.Call), ("won", mine.Won));
                    break;

                case MatchEnded ended:
                    TrackEnd(ended.Standings);
                    break;
            }
        }

        /// <summary>A resumed or rejoined match starts with a resync instead of events; it may already be over.</summary>
        private void OnResynced(PlayerView view)
        {
            if (view != null && view.Phase == Phase.MatchOver)
            {
                TrackEnd(view.FinalStandings);
            }
        }

        private void OnStatusChanged()
        {
            SessionStatus status = _session.Status;
            if (status == SessionStatus.Reconnecting)
            {
                if (!_reconnecting)
                {
                    _reconnecting = true;
                    _lostAt = _clock.Now;
                }

                return;
            }

            if (_reconnecting && (status == SessionStatus.Running || status == SessionStatus.Failed))
            {
                _reconnecting = false;
                _analytics.Track("reconnect", ("ok", status == SessionStatus.Running), ("secs", (int)(_clock.Now - _lostAt)));
            }

            // Online, a match that stops before its last round was left on purpose (GDD §8: it
            // counts as a loss). Offline, leaving only keeps the match for later.
            if (status == SessionStatus.Ended && !_ended && _session.Mode != MatchMode.Offline)
            {
                _ended = true;
                _analytics.Track("match_end", ("rank", SeatExtensions.Count), ("duration", Duration()), ("abandoned", true));
            }
        }

        private void TrackStart(MatchStarted started)
        {
            bool offline = _session.Mode == MatchMode.Offline;
            _analytics.Track(
                "match_start",
                ("mode", ModeName(_session.Mode)),
                ("difficulty", offline ? DifficultyName(_request.Difficulty) : "none"));
            if (offline)
            {
                return;
            }

            int bots = 0;
            foreach (SeatProfile seat in started.Seats)
            {
                if (seat.IsBot)
                {
                    bots++;
                }
            }

            if (bots > 0)
            {
                _analytics.Track("bot_fill", ("seats", bots));
            }
        }

        private void TrackEnd(IReadOnlyList<Standing> standings)
        {
            if (_ended || standings == null)
            {
                return;
            }

            _ended = true;
            int rank = SeatExtensions.Count;
            foreach (Standing standing in standings)
            {
                if (standing.Seat == _session.LocalSeat)
                {
                    rank = standing.Rank;
                }
            }

            _analytics.Track("match_end", ("rank", rank), ("duration", Duration()), ("abandoned", false));
        }

        private int Duration() => (int)(_clock.Now - _startedAt);

        private static string ModeName(MatchMode mode)
        {
            switch (mode)
            {
                case MatchMode.PrivateRoom:
                    return "private_room";
                case MatchMode.QuickMatch:
                    return "quick_match";
                default:
                    return "offline";
            }
        }

        private static string DifficultyName(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return "easy";
                case BotDifficulty.Hard:
                    return "hard";
                default:
                    return "normal";
            }
        }
    }
}
