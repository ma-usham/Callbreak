using Callbreak.Application.Rooms;
using Callbreak.Core.Commands;
using Callbreak.Core.Match;
using Callbreak.Core.Views;

namespace Callbreak.Application.Online
{
    /// <summary>Builds the payloads the host sends a member (GDD §25 message catalogue, server → client).</summary>
    public static class HostMessages
    {
        /// <summary>The member has a seat; the lobby follows.</summary>
        public static byte[] Welcome(int seatIndex, RoomSnapshot room)
        {
            WireWriter w = Begin(HostMessageKind.Welcome);
            w.Byte((byte)seatIndex);
            room.WriteTo(w);
            return w.ToArray();
        }

        /// <summary>The member cannot join: full, already started, update required.</summary>
        public static byte[] Rejected(RoomError error)
        {
            WireWriter w = Begin(HostMessageKind.Rejected);
            w.Byte((byte)error);
            return w.ToArray();
        }

        public static byte[] RoomState(RoomSnapshot room)
        {
            WireWriter w = Begin(HostMessageKind.RoomState);
            room.WriteTo(w);
            return w.ToArray();
        }

        /// <summary>The room is gone for this member: the host left, or the member was removed.</summary>
        public static byte[] RoomClosed(RoomError reason)
        {
            WireWriter w = Begin(HostMessageKind.RoomClosed);
            w.Byte((byte)reason);
            return w.ToArray();
        }

        /// <summary>Everyone loads the table; <paramref name="view"/> is the seat's view before the deal.</summary>
        public static byte[] MatchStarting(PlayerView view, TableStatus status)
        {
            WireWriter w = Begin(HostMessageKind.MatchStarting);
            WireCodec.Write(w, view);
            status.WriteTo(w);
            return w.ToArray();
        }

        public static byte[] TableBatch(TableUpdate update)
        {
            WireWriter w = Begin(HostMessageKind.TableBatch);
            update.WriteTo(w);
            return w.ToArray();
        }

        /// <summary>The full state for a join, reconnect or resync; the client rebuilds its table from it.</summary>
        public static byte[] SyncState(TableUpdate update)
        {
            WireWriter w = Begin(HostMessageKind.SyncState);
            update.WriteTo(w);
            return w.ToArray();
        }

        /// <summary>To the sender alone: its intent for <paramref name="turn"/> was refused (the client rolls back).</summary>
        public static byte[] IntentRejected(TurnId turn, RejectReason reason)
        {
            WireWriter w = Begin(HostMessageKind.IntentRejected);
            WireCodec.Write(w, turn);
            w.Byte((byte)reason);
            return w.ToArray();
        }

        /// <summary>
        /// The host is still there: sent about every second while a Quick Match table waits (ADR 029)
        /// and during a match its players can take over (ADR 030), so a player notices a host that
        /// went quiet.
        /// </summary>
        public static byte[] Heartbeat() => Begin(HostMessageKind.Heartbeat).ToArray();

        /// <summary>Who takes the table over if the host is lost (ADR 030): to every player.</summary>
        public static byte[] NextHost(NextHostNotice notice)
        {
            WireWriter w = Begin(HostMessageKind.NextHost);
            notice.WriteTo(w);
            return w.ToArray();
        }

        /// <summary>
        /// The host leaves during the match (ADR 030): to every player, who takes the table over now
        /// (the next host in line) and where to find it. The room goes on there.
        /// </summary>
        public static byte[] HandOverNow(NextHostNotice notice)
        {
            WireWriter w = Begin(HostMessageKind.HandOverNow);
            notice.WriteTo(w);
            return w.ToArray();
        }

        /// <summary>
        /// To the next host in line alone (ADR 030): the people's identities and the bots' dealt hands
        /// (<see cref="SuccessionPackage"/>).
        /// </summary>
        public static byte[] Succession(SuccessionPackage package)
        {
            WireWriter w = Begin(HostMessageKind.Succession);
            package.WriteTo(w);
            return w.ToArray();
        }

        public static byte[] EmoteShown(Seat seat, int emoteId)
        {
            WireWriter w = Begin(HostMessageKind.EmoteShown);
            WireCodec.Write(w, seat);
            w.Byte((byte)emoteId);
            return w.ToArray();
        }

        private static WireWriter Begin(HostMessageKind kind)
        {
            var w = new WireWriter();
            w.Byte((byte)kind);
            return w;
        }
    }
}
