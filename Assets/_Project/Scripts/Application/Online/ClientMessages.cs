using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Application.Online
{
    /// <summary>Builds the payloads a member sends the host (GDD §25 message catalogue, client → server).</summary>
    public static class ClientMessages
    {
        public static byte[] Hello(HelloMessage hello)
        {
            WireWriter w = Begin(ClientMessageKind.Hello);
            hello.WriteTo(w);
            return w.ToArray();
        }

        public static byte[] SetReady(bool ready)
        {
            WireWriter w = Begin(ClientMessageKind.SetReady);
            w.Bool(ready);
            return w.ToArray();
        }

        public static byte[] LeaveRoom() => Begin(ClientMessageKind.LeaveRoom).ToArray();

        /// <summary>The member's table has loaded and is listening.</summary>
        public static byte[] TableReady() => Begin(ClientMessageKind.TableReady).ToArray();

        public static byte[] PlaceCall(int call, TurnId turn)
        {
            WireWriter w = Begin(ClientMessageKind.PlaceCall);
            w.Byte((byte)call);
            WireCodec.Write(w, turn);
            return w.ToArray();
        }

        public static byte[] PlayCard(Card card, TurnId turn)
        {
            WireWriter w = Begin(ClientMessageKind.PlayCard);
            WireCodec.Write(w, card);
            WireCodec.Write(w, turn);
            return w.ToArray();
        }

        public static byte[] ReadyNext() => Begin(ClientMessageKind.ReadyNext).ToArray();

        public static byte[] Emote(int emoteId)
        {
            WireWriter w = Begin(ClientMessageKind.Emote);
            w.Byte((byte)emoteId);
            return w.ToArray();
        }

        /// <summary>The member missed something after <paramref name="lastSeq"/> and wants the full state.</summary>
        public static byte[] RequestResync(uint lastSeq)
        {
            WireWriter w = Begin(ClientMessageKind.RequestResync);
            w.UInt32(lastSeq);
            return w.ToArray();
        }

        /// <summary>"I'm back": clears Away without playing anything.</summary>
        public static byte[] Presence() => Begin(ClientMessageKind.Presence).ToArray();

        public static byte[] LeaveTable() => Begin(ClientMessageKind.LeaveTable).ToArray();

        private static WireWriter Begin(ClientMessageKind kind)
        {
            var w = new WireWriter();
            w.Byte((byte)kind);
            return w;
        }
    }
}
