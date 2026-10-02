using System;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;

namespace Callbreak.Presentation.Table
{
    /// <summary>What a seat's status pill has to tell (GDD §11 screens 07 and 08).</summary>
    public readonly struct SeatStatusInfo
    {
        public SeatStatusInfo(bool isLocal, SeatConnection connection, bool isCalling, int call, bool isAutoCall, bool isPlaying, int won)
        {
            IsLocal = isLocal;
            Connection = connection;
            IsCalling = isCalling;
            Call = call;
            IsAutoCall = isAutoCall;
            IsPlaying = isPlaying;
            Won = won;
        }

        /// <summary>The seat at the bottom of the screen.</summary>
        public bool IsLocal { get; }

        public SeatConnection Connection { get; }

        /// <summary>Its call turn is open.</summary>
        public bool IsCalling { get; }

        /// <summary>The call, or <see cref="CallRules.NotCalled"/>.</summary>
        public int Call { get; }

        /// <summary>The call was placed by the timer or a stand-in (the pill no longer says so: it reads "won-bid").</summary>
        public bool IsAutoCall { get; }

        /// <summary>All four have called and tricks are being played.</summary>
        public bool IsPlaying { get; }

        /// <summary>Tricks won this round.</summary>
        public int Won { get; }

        public bool HasCalled => Call != CallRules.NotCalled;
    }

    /// <summary>
    /// The status pill's copy: Waiting → Calling… (Choosing bid on the local seat), then from the
    /// call on only "won-bid": "0-3" once 3 is called, "1-3" after the first trick won, and so on
    /// (the owner, 3 Oct 2026, in place of "Bid 3 · Won 1"). A met call turns the pill gold
    /// (<see cref="IsMet"/>). The line and its gold are about the seat, not about who is playing
    /// it: they stay when the player is away, reconnecting or gone and a bot plays on, because what
    /// a seat called and has won decides how the others play. How the player is connected has its
    /// own short chip beside the pill (GDD §8 "What others see"). A bot that fills a seat has no
    /// tag there (ADR 025), and neither has the seat of a player who left or was not back in time.
    /// </summary>
    public static class SeatStatus
    {
        public const string WaitingKey = "ui.seat.waiting";
        public const string CallingKey = "ui.seat.calling";
        public const string ChoosingBidKey = "ui.seat.choosing_bid";
        public const string WonBidKey = "ui.seat.won_bid";
        public const string AwayKey = "ui.seat.away";
        public const string ReconnectingKey = "ui.seat.reconnecting";

        public static string Text(in SeatStatusInfo seat, ILocalization localization)
        {
            if (localization == null)
            {
                throw new ArgumentNullException(nameof(localization));
            }

            if (!seat.HasCalled)
            {
                if (!seat.IsCalling)
                {
                    return localization.Get(WaitingKey);
                }

                // The local seat is looking at the call panel; others see it "Calling…".
                return localization.Get(seat.IsLocal ? ChoosingBidKey : CallingKey);
            }

            // Tricks won, then the call (nothing is won before play starts).
            return localization.Format(WonBidKey, seat.IsPlaying ? seat.Won : 0, seat.Call);
        }

        /// <summary>
        /// The pill turns gold: the seat has won at least what it called (GDD screen 08, callout 3),
        /// whoever plays it. A seat whose player is away, reconnecting or gone turns gold too (the
        /// owner, 3 Oct 2026, in place of "only for a player who is at the table").
        /// </summary>
        public static bool IsMet(in SeatStatusInfo seat)
        {
            return HasMetItsCall(seat);
        }

        /// <summary>
        /// The chip beside the pill (GDD §8 "What others see"): "Away" after two timeouts and
        /// "Reconnecting…" while the seat is held; null for a player who is there: no chip. A bot
        /// that fills a seat from the start carries no tag (ADR 025; GDD §9 gave it "BOT" online),
        /// and neither does the seat of a player who left on purpose, a host who handed the table
        /// over included, or of one who was not back in time: a bot plays it on silently (the
        /// owner, 3 Oct 2026, in place of "Bot playing"). Pass the connection through
        /// <see cref="Shown"/> first.
        /// </summary>
        public static string Chip(SeatConnection connection, ILocalization localization)
        {
            if (localization == null)
            {
                throw new ArgumentNullException(nameof(localization));
            }

            switch (connection)
            {
                case SeatConnection.Away:
                    return localization.Get(AwayKey);
                case SeatConnection.Reconnecting:
                    return localization.Get(ReconnectingKey);
                default:
                    return null;
            }
        }

        /// <summary>
        /// How a seat's connection shows to the others (the owner, 3 Oct 2026: "Reconnecting…" only
        /// where its player can come back). In a private room anyone whose app was killed can come
        /// back by opening it again ("Rejoin your match?") while the seat is held. In Quick Match
        /// nobody can: the app keeps no rejoin ticket there, and a killed app looks the same as one
        /// in the background or out of signal. So a dropped seat in Quick Match shows as if its
        /// player were there: no chip, a bot plays it on as for a player who left, and a player
        /// whose app was only in the background takes it back quietly, without a "Welcome back".
        /// </summary>
        public static SeatConnection Shown(SeatConnection connection, MatchMode mode)
        {
            return mode == MatchMode.QuickMatch && connection == SeatConnection.Reconnecting ? SeatConnection.Connected : connection;
        }

        /// <summary>The chip shows a spinner: the seat's player is reconnecting (GDD §8).</summary>
        public static bool ChipIsBusy(SeatConnection connection) => connection == SeatConnection.Reconnecting;

        private static bool HasMetItsCall(in SeatStatusInfo seat)
        {
            return seat.HasCalled && seat.IsPlaying && ScoreCalculator.IsMet(seat.Call, seat.Won);
        }
    }
}
