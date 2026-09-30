using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Bootstrap;
using Callbreak.Core.Match;
using Callbreak.Core.Views;
using Callbreak.Infrastructure.Services;
using Callbreak.Networking;
using PurrNet.Transports;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace Callbreak.Editor
{
    /// <summary>
    /// A second player in the same process, for checking a private room over the real relay
    /// (Tools/Unity/playcheck.sh): the running game is the host, and this guest has its own relay
    /// connection, its own identity and saves in memory, and the same room and match code as a
    /// second phone would run. Every message between the two crosses the relay server.
    /// With the host in a lobby, <see cref="Start"/> joins its room and marks the guest ready; once
    /// the host starts the match the guest plays its seat (call 1, lowest legal card, ready at each
    /// summary). <see cref="Report"/> says where both sides stand and whether they agree.
    /// </summary>
    public static class RelayCheck
    {
        private const double ActEverySeconds = 0.6;
        private const string GuestName = "Relay Guest";

        private static GameObject _guestObject;
        private static PurrRoomTransport _transport;
        private static RoomService _rooms;
        private static IRoomSession _room;
        private static IMatchSession _match;
        private static IClock _clock;
        private static CancellationTokenSource _cancel;
        private static bool _running;
        private static double _lastUpdate;
        private static double _nextAct;
        private static string _stage = "idle";
        private static string _problem = string.Empty;
        private static string _closed = string.Empty;
        private static int _events;
        private static int _resyncs;
        private static int _calls;
        private static int _plays;
        private static int _readies;
        private static int _refused;

        public static string Start()
        {
            if (!EditorApplication.isPlaying)
            {
                return "not in Play mode";
            }

            if (_running)
            {
                return "already running; " + Report();
            }

            ProjectLifetimeScope project = UnityEngine.Object.FindFirstObjectByType<ProjectLifetimeScope>();
            if (project == null || project.Container == null)
            {
                return "the game is not up";
            }

            IObjectResolver host = project.Container;
            IRoomSession hostRoom = host.Resolve<IRoomService>().Current;
            if (hostRoom == null || !hostRoom.IsHost)
            {
                return "the running game hosts no room: open one first (Play with Friends, Create Room)";
            }

            _clock = host.Resolve<IClock>();
            _cancel = new CancellationTokenSource();
            _events = _resyncs = _calls = _plays = _readies = _refused = 0;
            _problem = string.Empty;
            _closed = string.Empty;
            _room = null;
            _match = null;

            // The guest's own device: a relay connection, an identity and saves of its own.
            _guestObject = new GameObject("RelayCheckGuest", typeof(PurrTransport));
            UnityEngine.Object.DontDestroyOnLoad(_guestObject);
            _transport = new PurrRoomTransport(_guestObject.GetComponent<PurrTransport>());
            var saves = new MemorySaves();
            var names = new NameFilter();
            var profile = new ProfileService(saves, names);
            profile.TrySetDisplayName(GuestName);
            profile.SetAvatar(3);
            var auth = new LocalAuthService(saves);
            auth.SignInAsync(CancellationToken.None);
            _rooms = new RoomService(
                _transport,
                profile,
                new CosmeticsService(saves),
                auth,
                host.Resolve<IBotFactory>(),
                new StatsService(saves),
                new Callbreak.Application.Settings.SettingsService(saves),
                new AlwaysOnline(),
                names,
                () => (ulong)DateTime.UtcNow.Ticks);

            _running = true;
            _lastUpdate = _clock.Now;
            _nextAct = 0;
            _stage = "joining " + hostRoom.Code;
            EditorApplication.update += Update;
            _ = JoinAsync(hostRoom.Code);
            return "guest " + auth.PlayerId.Substring(0, 8) + " joining " + hostRoom.Code;
        }

        public static string Stop()
        {
            string report = Report();
            if (_running)
            {
                _running = false;
                EditorApplication.update -= Update;
            }

            try
            {
                _cancel?.Cancel();
                if (_room != null)
                {
                    _ = _room.LeaveAsync(CancellationToken.None);
                }

                _transport?.Dispose();
            }
            catch (Exception e)
            {
                report += " | stop: " + e.Message;
            }

            if (_guestObject != null)
            {
                UnityEngine.Object.Destroy(_guestObject);
            }

            _guestObject = null;
            _transport = null;
            _rooms = null;
            _room = null;
            _match = null;
            _stage = "stopped";
            return report;
        }

        /// <summary>Where the host and the guest stand, and whether they tell the same story.</summary>
        public static string Report()
        {
            var text = new StringBuilder();
            text.Append("stage=").Append(_stage);
            if (_room != null)
            {
                text.Append(" | guest room ").Append(_room.Code).Append(" seat ").Append(_room.LocalSeatIndex).Append(": ").Append(SeatsOf(_room));
            }

            ProjectLifetimeScope project = UnityEngine.Object.FindFirstObjectByType<ProjectLifetimeScope>();
            IRoomSession hostRoom = project != null && project.Container != null ? project.Container.Resolve<IRoomService>().Current : null;
            if (hostRoom != null)
            {
                text.Append(" | host room ").Append(hostRoom.Code).Append(": ").Append(SeatsOf(hostRoom));
            }

            IMatchSession guest = _match;
            IMatchSession hostMatch = HostMatch();
            if (guest != null)
            {
                text.Append(" | guest match ").Append(Describe(guest))
                    .Append(" events=").Append(_events).Append(" resyncs=").Append(_resyncs)
                    .Append(" calls=").Append(_calls).Append(" plays=").Append(_plays)
                    .Append(" readies=").Append(_readies).Append(" refused=").Append(_refused);
            }

            if (hostMatch != null)
            {
                text.Append(" | host match ").Append(Describe(hostMatch));
            }

            if (guest != null && hostMatch != null && guest.View != null && hostMatch.View != null)
            {
                PlayerView g = guest.View;
                PlayerView h = hostMatch.View;
                bool same = g.MatchId == h.MatchId && g.Round == h.Round && g.Phase == h.Phase && g.TrickIndex == h.TrickIndex && g.Scores.Equals(h.Scores);
                text.Append(" | agree=").Append(same);
                if (g.Hand.Count > 0 && h.Hand.Count > 0)
                {
                    // Each side holds only its own hand: the two must not share a card.
                    text.Append(" handsOverlap=").Append(!g.Hand.Intersect(h.Hand).IsEmpty);
                }
            }

            if (_closed.Length > 0)
            {
                text.Append(" | guest room closed: ").Append(_closed);
            }

            if (_problem.Length > 0)
            {
                text.Append(" | problem=").Append(_problem);
            }

            return text.ToString();
        }

        private static async Task JoinAsync(string code)
        {
            try
            {
                RoomResult result = await _rooms.JoinRoomAsync(code, _cancel.Token);
                if (!result.Succeeded)
                {
                    _stage = "join failed: " + result.Error;
                    return;
                }

                _room = result.Session;
                _room.Closed += OnClosed;
                _stage = "in lobby";
                await _room.SetReadyAsync(true, _cancel.Token);
                _stage = "ready";
            }
            catch (Exception e)
            {
                _problem = "join: " + e.GetType().Name + ": " + e.Message;
            }
        }

        private static void OnClosed(RoomError reason) => _closed = reason.ToString();

        private static void Update()
        {
            if (!EditorApplication.isPlaying)
            {
                Stop();
                return;
            }

            try
            {
                double now = _clock.Now;
                float delta = (float)Math.Max(0.0, now - _lastUpdate);
                _lastUpdate = now;
                _transport.Tick(delta, now);
                _rooms.Tick(now);

                IMatchSession match = _room != null ? _room.Match : null;
                if (match == null)
                {
                    return;
                }

                if (match != _match)
                {
                    _match = match;
                    match.EventReceived += _ => _events++;
                    match.Resynced += _ => _resyncs++;
                    match.Start();
                    _stage = "playing";
                }

                match.Tick(now);
                if (now >= _nextAct && Act(match))
                {
                    _nextAct = now + ActEverySeconds;
                }
            }
            catch (Exception e)
            {
                _problem = e.GetType().Name + ": " + e.Message;
            }
        }

        /// <summary>Plays the guest's seat as a player would through the session: true when it did something.</summary>
        private static bool Act(IMatchSession match)
        {
            PlayerView view = match.View;
            if (view == null || match.Status != SessionStatus.Running)
            {
                return false;
            }

            if (view.Phase == Phase.RoundSummary && !view.IsReady(match.LocalSeat))
            {
                _readies++;
                _ = match.ReadyNextAsync(_cancel.Token);
                return true;
            }

            if (!view.IsMyTurn)
            {
                return false;
            }

            if (view.Phase == Phase.Calling)
            {
                _calls++;
                _ = Count(match.PlaceCallAsync(1, _cancel.Token));
                return true;
            }

            if (view.Phase == Phase.Playing)
            {
                _plays++;
                _ = Count(match.PlayCardAsync(view.LegalCards().Lowest(), _cancel.Token));
                return true;
            }

            return false;
        }

        private static async Task Count(Task<Callbreak.Core.Commands.CommandResult> sending)
        {
            try
            {
                Callbreak.Core.Commands.CommandResult result = await sending;
                if (!result.IsAccepted)
                {
                    _refused++;
                }
            }
            catch (Exception e)
            {
                _problem = "send: " + e.GetType().Name + ": " + e.Message;
            }
        }

        private static IMatchSession HostMatch()
        {
            TableLifetimeScope table = UnityEngine.Object.FindFirstObjectByType<TableLifetimeScope>();
            return table != null && table.Container != null ? table.Container.Resolve<IMatchSession>() : null;
        }

        private static string Describe(IMatchSession session)
        {
            PlayerView view = session.View;
            var text = new StringBuilder();
            text.Append(session.Status).Append(" seat=").Append(session.LocalSeat);
            if (view != null)
            {
                text.Append(" round=").Append(view.Round).Append(' ').Append(view.Phase)
                    .Append(" trick=").Append(view.TrickIndex).Append(" hand=").Append(view.Hand.Count).Append(" totals=");
                for (int seat = 0; seat < 4; seat++)
                {
                    text.Append(seat == 0 ? string.Empty : "/").Append(view.TotalOf((Seat)seat));
                }
            }

            return text.ToString();
        }

        private static string SeatsOf(IRoomSession room)
        {
            var text = new StringBuilder();
            IReadOnlyList<RoomSeat> seats = room.Seats;
            for (int i = 0; i < seats.Count; i++)
            {
                RoomSeat seat = seats[i];
                text.Append(i == 0 ? string.Empty : ", ");
                text.Append(!seat.Occupied ? "-" : seat.Name + (seat.IsHost ? "(host)" : seat.IsReady ? "(ready)" : string.Empty) + (seat.IsBot ? "(bot)" : string.Empty));
            }

            return text.ToString();
        }

        private sealed class AlwaysOnline : INetworkStatus
        {
            public event Action<bool> Changed
            {
                add { }
                remove { }
            }

            public bool IsOnline => true;
        }

        /// <summary>The guest's saves: JSON in memory, so nothing of it reaches the player's files.</summary>
        private sealed class MemorySaves : ISaveStore
        {
            private readonly Dictionary<string, string> _files = new Dictionary<string, string>();

            public bool TryLoad<T>(string key, out T value) where T : class
            {
                value = _files.TryGetValue(key, out string json) ? JsonUtility.FromJson<T>(json) : null;
                return value != null;
            }

            public void Save<T>(string key, T value) where T : class => _files[key] = JsonUtility.ToJson(value);

            public void Delete(string key) => _files.Remove(key);

            public bool Exists(string key) => _files.ContainsKey(key);
        }
    }
}
