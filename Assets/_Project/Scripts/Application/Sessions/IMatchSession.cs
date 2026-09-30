using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;

namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// The table's view of one match, identical offline and online (GDD §17 principle 4: the UI
    /// never knows which mode it is in). Offline, <c>OfflineMatchSession</c> runs the engine and the
    /// bots on the device; online, <c>OnlineMatchSession</c> relays intents to the server and events
    /// back. Events arrive in order and carry only what the local seat may know.
    /// </summary>
    public interface IMatchSession : IDisposable
    {
        MatchMode Mode { get; }

        /// <summary>The seat this device plays; the table draws it at the bottom.</summary>
        Seat LocalSeat { get; }

        RuleSet Rules { get; }

        /// <summary>Four seats indexed by <c>(int)Seat</c>: names, avatars, deck backs, bots, connection.</summary>
        IReadOnlyList<SessionSeat> Seats { get; }

        /// <summary>The local seat's view after every event delivered so far.</summary>
        PlayerView View { get; }

        SessionStatus Status { get; }

        /// <summary>The running turn clock, if any (online only; offline play has no clock, GDD §6).</summary>
        TurnTimerState Timer { get; }

        bool CanPause { get; }

        bool IsPaused { get; }

        /// <summary>Every public event and the local seat's private ones, strictly in order.</summary>
        event Action<MatchEvent> EventReceived;

        /// <summary>The table must be rebuilt from this view at once (resume, reconnect, resync).</summary>
        event Action<PlayerView> Resynced;

        event Action SeatsChanged;

        event Action StatusChanged;

        event Action TimerChanged;

        /// <summary>A quick phrase from a seat (GDD §8 emotes).</summary>
        event Action<Seat, int> EmoteReceived;

        /// <summary>Begins delivering events; call once the table is ready to play them.</summary>
        void Start();

        /// <summary>Advances time-driven work: bot think delays, auto actions, the turn clock.</summary>
        void Tick(double now);

        Task<CommandResult> PlaceCallAsync(int call, CancellationToken ct);

        Task<CommandResult> PlayCardAsync(Card card, CancellationToken ct);

        /// <summary>"Next Round" on the round results: this seat is ready.</summary>
        Task ReadyNextAsync(CancellationToken ct);

        Task SendEmoteAsync(int emoteId, CancellationToken ct);

        /// <summary>Offline only: freezes bots and play while the match menu is open (GDD §11 screen 13).</summary>
        void Pause();

        void Resume();

        /// <summary>Leaves the match: offline it is saved for later; online a bot takes the seat and it counts as a loss.</summary>
        Task LeaveAsync(CancellationToken ct);
    }

    public enum SessionStatus
    {
        Starting,
        Running,
        Paused,
        Reconnecting,
        Ended,
        Failed,
    }

    public enum SeatConnection
    {
        Connected,
        Away,
        Reconnecting,
        BotControlled,
        Left,
    }

    /// <summary>What the table shows about a seat besides its cards.</summary>
    public sealed class SessionSeat
    {
        public SessionSeat(Seat seat, string name, int avatarId, string deckThemeId, bool isBot, bool isLocal, SeatConnection connection)
        {
            Seat = seat;
            Name = name;
            AvatarId = avatarId;
            DeckThemeId = deckThemeId;
            IsBot = isBot;
            IsLocal = isLocal;
            Connection = connection;
        }

        public Seat Seat { get; }

        public string Name { get; }

        public int AvatarId { get; }

        public string DeckThemeId { get; }

        public bool IsBot { get; }

        public bool IsLocal { get; }

        public SeatConnection Connection { get; }
    }

    /// <summary>A turn clock: which seat, when it ends (in <c>IClock</c> seconds) and how long it was.</summary>
    public readonly struct TurnTimerState
    {
        public TurnTimerState(Seat seat, double deadline, double duration, bool isRunning)
        {
            Seat = seat;
            Deadline = deadline;
            Duration = duration;
            IsRunning = isRunning;
        }

        public Seat Seat { get; }

        public double Deadline { get; }

        public double Duration { get; }

        public bool IsRunning { get; }

        public static TurnTimerState None => default;
    }
}
