using Callbreak.Application.Sessions;
using Callbreak.Core.Match;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// What the host says about the table besides the cards (GDD §8, §25): the running turn clock,
    /// the round summary's countdown and each seat's presence. Times are "milliseconds from now",
    /// because only the host's clock counts and clients merely display it.
    /// </summary>
    public sealed class TableStatus
    {
        public const int NoClock = -1;

        private readonly SeatConnection[] _connections;

        public TableStatus(Seat timerSeat, int timerRemainingMs, int timerDurationMs, int summaryRemainingMs, SeatConnection[] connections)
        {
            TimerSeat = timerSeat;
            TimerRemainingMs = timerRemainingMs;
            TimerDurationMs = timerDurationMs;
            SummaryRemainingMs = summaryRemainingMs;
            _connections = new SeatConnection[SeatExtensions.Count];
            for (int i = 0; i < _connections.Length; i++)
            {
                _connections[i] = connections != null && i < connections.Length ? connections[i] : SeatConnection.Connected;
            }
        }

        public Seat TimerSeat { get; }

        /// <summary>
        /// Milliseconds left on the turn clock, or <see cref="NoClock"/> when none is running. More
        /// than <see cref="TimerDurationMs"/> while the table still shows a deal or a finished trick:
        /// the clock starts when that hold ends, and a client shows it from then on.
        /// </summary>
        public int TimerRemainingMs { get; }

        /// <summary>
        /// The whole length in milliseconds of the clock that is running: the turn clock's (a call
        /// turn and a play turn can differ), or in the round summary the countdown's. 0 when
        /// nothing runs.
        /// </summary>
        public int TimerDurationMs { get; }

        /// <summary>
        /// Milliseconds until the next round starts by itself, or <see cref="NoClock"/>. Longer than
        /// the countdown until the end of the round has been played back on the table.
        /// </summary>
        public int SummaryRemainingMs { get; }

        public bool HasTimer => TimerRemainingMs >= 0;

        public SeatConnection ConnectionOf(Seat seat) => _connections[(int)seat];

        public void WriteTo(WireWriter w)
        {
            WireCodec.Write(w, TimerSeat);
            w.Int32(TimerRemainingMs);
            w.Int32(TimerDurationMs);
            w.Int32(SummaryRemainingMs);
            for (int i = 0; i < _connections.Length; i++)
            {
                w.Byte((byte)_connections[i]);
            }
        }

        public static TableStatus Read(WireReader r)
        {
            Seat timerSeat = WireCodec.ReadSeat(r);
            int remaining = r.Int32();
            int duration = r.Int32();
            int summary = r.Int32();
            var connections = new SeatConnection[SeatExtensions.Count];
            for (int i = 0; i < connections.Length; i++)
            {
                byte value = r.Byte();
                if (value > (byte)SeatConnection.Left)
                {
                    throw new WireFormatException("Seat presence " + value + " is unknown.");
                }

                connections[i] = (SeatConnection)value;
            }

            return new TableStatus(timerSeat, remaining, duration, summary, connections);
        }
    }
}
