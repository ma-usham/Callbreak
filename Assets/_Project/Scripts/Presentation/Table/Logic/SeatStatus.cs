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

        /// <summary>The call was placed by the timer or a stand-in ("Bid 3 · auto").</summary>
        public bool IsAutoCall { get; }

        /// <summary>All four have called and tricks are being played.</summary>
        public bool IsPlaying { get; }

        /// <summary>Tricks won this round.</summary>
        public int Won { get; }

        public bool HasCalled => Call != CallRules.NotCalled;
    }

    /// <summary>
    /// The status pill's copy: Waiting → Calling… → Bid 3 → Bid 3 · Won 1, with a tick once the call
    /// is met and "· auto" for a call the timer placed. The line is about the seat, not about who
    /// is playing it: it stays when the player is away, reconnecting or gone and a bot plays on,
    /// because what a seat called and has won decides how the others play. How the player is
    /// connected has its own short chip beside the pill (GDD §8 "What others see"), which is also
    /// where a fill-in bot carries its BOT tag (GDD §9).
    /// </summary>
    public static class SeatStatus
    {
        public const string WaitingKey = "ui.seat.waiting";
        public const string CallingKey = "ui.seat.calling";
        public const string ChoosingBidKey = "ui.seat.choosing_bid";
        public const string BidKey = "ui.seat.bid";
        public const string BidAutoKey = "ui.seat.bid_auto";
        public const string BidWonKey = "ui.seat.bid_won";
        public const string BidWonMetKey = "ui.seat.bid_won_met";
        public const string AwayKey = "ui.seat.away";
        public const string ReconnectingKey = "ui.seat.reconnecting";
        public const string BotPlayingKey = "ui.seat.bot_playing";

        /// <summary>"BOT", as the Quick Match search marks a seat a bot filled.</summary>
        public const string BotTagKey = "ui.matchmaking.seat_bot";

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

            if (!seat.IsPlaying)
            {
                return localization.Format(seat.IsAutoCall ? BidAutoKey : BidKey, seat.Call);
            }

            return localization.Format(HasMetItsCall(seat) ? BidWonMetKey : BidWonKey, seat.Call, seat.Won);
        }

        /// <summary>
        /// The pill turns gold: the seat has won at least what it called (GDD screen 08, callout 3).
        /// Only for a player who is at the table: the line of a seat that is away keeps its tick,
        /// but the pill stays plain.
        /// </summary>
        public static bool IsMet(in SeatStatusInfo seat)
        {
            return seat.Connection == SeatConnection.Connected && HasMetItsCall(seat);
        }

        /// <summary>
        /// The chip beside the pill (GDD §8 "What others see"): "Away" after two timeouts,
        /// "Reconnecting…" while the seat is held, "Bot playing" once a bot has taken it for good;
        /// for a player who is there, "BOT" when <paramref name="isFillInBot"/> (a bot that fills a
        /// seat of an online table), and otherwise null: no chip.
        /// </summary>
        public static string Chip(SeatConnection connection, bool isFillInBot, ILocalization localization)
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
                case SeatConnection.BotControlled:
                case SeatConnection.Left:
                    return localization.Get(BotPlayingKey);
                default:
                    return isFillInBot ? localization.Get(BotTagKey) : null;
            }
        }

        /// <summary>The chip shows a spinner: the seat's player is reconnecting (GDD §8).</summary>
        public static bool ChipIsBusy(SeatConnection connection) => connection == SeatConnection.Reconnecting;

        private static bool HasMetItsCall(in SeatStatusInfo seat)
        {
            return seat.HasCalled && seat.IsPlaying && ScoreCalculator.IsMet(seat.Call, seat.Won);
        }
    }
}
