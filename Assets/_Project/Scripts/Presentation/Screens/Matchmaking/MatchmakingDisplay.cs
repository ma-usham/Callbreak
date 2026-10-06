using System;
using System.Collections.Generic;
using System.Globalization;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// What the Quick Match screen (GDD §11 screen 05) shows for a search report, worked out apart
    /// from the view so that it needs no engine (ADR 029):
    /// <list type="bullet">
    /// <item>Every player sees themselves in the first seat circle, and the others after them in the
    /// table's seat order (the host's seat first): on the host's screen You, B, -, -; on B's screen
    /// You (B), Host, -, -. Bots are players like any other (ADR 025).</item>
    /// <item>The pill shows the player's own search countdown, 00:20 down to 00:00; once the table is
    /// found, the start countdown every player at the table shares, 00:03 down to 00:01. Both are
    /// rounded up. A search without a countdown keeps the honest elapsed time.</item>
    /// <item>When the shared start countdown takes over from an own countdown that is further away
    /// (00:09, then 00:03), the pill counts down quickly instead of jumping, except with Reduce
    /// motion.</item>
    /// <item>Nobody leaves a found table during its start countdown: Cancel and the back arrow are
    /// greyed and Android back does nothing until the search resumes.</item>
    /// </list>
    /// </summary>
    public static class MatchmakingDisplay
    {
        /// <summary>The pill's longest time, 99:59.</summary>
        private const int MaxClockSeconds = 99 * 60 + 59;

        /// <summary>Which time the pill shows.</summary>
        public enum ClockKind
        {
            /// <summary>The time since the search began, counting up: a search that reports no countdown.</summary>
            Elapsed,

            /// <summary>The player's own countdown while the search runs.</summary>
            Search,

            /// <summary>The table's start countdown, the same on every screen at the table.</summary>
            Start,
        }

        /// <summary>
        /// The screen for <paramref name="progress"/> (null before the first report: only the player,
        /// everyone else still searching) with <paramref name="seatCount"/> seat circles. A filled
        /// circle shows its player's avatar (ADR 041): <paramref name="viewerAvatar"/> in the first.
        /// </summary>
        public static MatchmakingScreenState StateOf(MatchmakingProgress progress, ILocalization text, int seatCount, int viewerAvatar = 0)
        {
            IReadOnlyList<RoomSeat> order = InViewerOrder(progress?.Seats);
            var seats = new MatchmakingScreenState.Seat[Math.Max(0, seatCount)];
            string ready = text.Get("ui.matchmaking.seat_ready");
            string searching = text.Get("ui.matchmaking.seat_searching");
            int connected = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                RoomSeat seat = i < order.Count ? order[i] : null;

                // The first circle is the player, from before the first report on.
                bool viewer = i == 0;
                if (viewer || (seat != null && seat.Occupied))
                {
                    connected++;
                    seats[i] = new MatchmakingScreenState.Seat(true, viewer ? text.Get("ui.common.you") : seat.Name, ready, viewer ? viewerAvatar : seat.AvatarId);
                }
                else
                {
                    seats[i] = new MatchmakingScreenState.Seat(false, string.Empty, searching);
                }
            }

            // A found table is locked: nobody leaves during its start countdown. A search that
            // resumes (a seat emptied again) can be left again.
            bool found = progress != null && progress.TableFound;
            return new MatchmakingScreenState(
                seats,
                text.Get(found ? "ui.matchmaking.table_found" : "ui.matchmaking.finding"),
                text.Format("ui.matchmaking.connected", connected),
                ClockOf(progress),
                ClockSeconds(progress),
                canLeave: !found);
        }

        /// <summary>
        /// The seats in the order the screen draws them: the one with IsLocal first, then all the
        /// others in the table's order. Without a local seat the table's order stays as it is.
        /// </summary>
        public static IReadOnlyList<RoomSeat> InViewerOrder(IReadOnlyList<RoomSeat> seats)
        {
            if (seats == null)
            {
                return Array.Empty<RoomSeat>();
            }

            int local = -1;
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i] != null && seats[i].IsLocal)
                {
                    local = i;
                    break;
                }
            }

            if (local <= 0)
            {
                return seats;
            }

            var ordered = new RoomSeat[seats.Count];
            ordered[0] = seats[local];
            int next = 1;
            for (int i = 0; i < seats.Count; i++)
            {
                if (i != local)
                {
                    ordered[next++] = seats[i];
                }
            }

            return ordered;
        }

        /// <summary>
        /// The start countdown once the table is found, else the player's own countdown, else (no
        /// countdown reported: NaN) the elapsed time.
        /// </summary>
        public static ClockKind ClockOf(MatchmakingProgress progress)
        {
            if (progress == null)
            {
                return ClockKind.Elapsed;
            }

            if (progress.TableFound && IsTime(progress.StartsInSeconds))
            {
                return ClockKind.Start;
            }

            return IsTime(progress.SecondsLeft) ? ClockKind.Search : ClockKind.Elapsed;
        }

        /// <summary>
        /// The whole seconds in the pill. A countdown is rounded up, so 00:01 stays until the last
        /// moment and a fresh 20 s search reads 00:20; the elapsed time is rounded down.
        /// </summary>
        public static int ClockSeconds(MatchmakingProgress progress)
        {
            switch (ClockOf(progress))
            {
                case ClockKind.Start:
                    return Clamp(Math.Ceiling(progress.StartsInSeconds));
                case ClockKind.Search:
                    return Clamp(Math.Ceiling(progress.SecondsLeft));
                default:
                    return progress != null && IsTime(progress.ElapsedSeconds) ? Clamp(Math.Floor(progress.ElapsedSeconds)) : 0;
            }
        }

        /// <summary>"00:20": minutes and seconds, two digits each.</summary>
        public static string FormatClock(int seconds)
        {
            int s = Math.Min(Math.Max(seconds, 0), MaxClockSeconds);
            return (s / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Whether the pill counts down quickly from <paramref name="shown"/> to <paramref name="target"/>
        /// instead of changing at once: only for the start countdown, only downwards by more than a
        /// second (the moment it takes over from an own countdown further away), never with Reduce motion.
        /// </summary>
        public static bool RollsDown(int shown, int target, ClockKind clock, bool reduceMotion)
        {
            return clock == ClockKind.Start && !reduceMotion && shown - target > 1;
        }

        /// <summary>
        /// The number a quick count from <paramref name="from"/> down to <paramref name="to"/> shows when
        /// <paramref name="progress"/> (0-1) of it has passed: every number in between, each for the
        /// same time, <paramref name="to"/> at the end.
        /// </summary>
        public static int RollStep(int from, int to, float progress)
        {
            if (progress <= 0f)
            {
                return from;
            }

            if (progress >= 1f || from <= to)
            {
                return to;
            }

            return Math.Min(from, to + (int)Math.Ceiling((from - to) * (1.0 - progress) - 1e-9));
        }

        private static bool IsTime(double seconds) => !double.IsNaN(seconds) && !double.IsInfinity(seconds);

        private static int Clamp(double seconds) => (int)Math.Min(Math.Max(seconds, 0.0), MaxClockSeconds);
    }
}
