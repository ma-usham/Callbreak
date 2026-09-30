using System.Collections.Generic;
using Callbreak.Core.Events;
using Callbreak.Core.Views;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// One numbered message to one seat (GDD §25-26): the events that seat may see, in order, and
    /// its view and the table status after them. With no events it is a full state for a join,
    /// reconnect or resync. The per-seat <see cref="Seq"/> lets a client notice a gap and ask
    /// for a resync.
    /// </summary>
    public sealed class TableUpdate
    {
        private const int MaxEvents = 96;

        private static readonly MatchEvent[] NoEvents = new MatchEvent[0];

        public TableUpdate(uint seq, IReadOnlyList<MatchEvent> events, PlayerView view, TableStatus status)
        {
            Seq = seq;
            Events = events ?? NoEvents;
            View = view;
            Status = status;
        }

        public uint Seq { get; }

        public IReadOnlyList<MatchEvent> Events { get; }

        public PlayerView View { get; }

        public TableStatus Status { get; }

        public void WriteTo(WireWriter w)
        {
            w.UInt32(Seq);
            w.Byte((byte)Events.Count);
            for (int i = 0; i < Events.Count; i++)
            {
                WireCodec.Write(w, Events[i]);
            }

            WireCodec.Write(w, View);
            Status.WriteTo(w);
        }

        public static TableUpdate Read(WireReader r)
        {
            uint seq = r.UInt32();
            int count = r.Count(MaxEvents);
            var events = new MatchEvent[count];
            for (int i = 0; i < count; i++)
            {
                events[i] = WireCodec.ReadEvent(r);
            }

            PlayerView view = WireCodec.ReadPlayerView(r);
            TableStatus status = TableStatus.Read(r);
            return new TableUpdate(seq, events, view, status);
        }
    }
}
