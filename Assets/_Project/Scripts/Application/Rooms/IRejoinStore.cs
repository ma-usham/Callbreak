namespace Callbreak.Application.Rooms
{
    /// <summary>Keeps at most one <see cref="RejoinTicket"/> (GDD §27: "App killed? ... Rejoin match").</summary>
    public interface IRejoinStore
    {
        /// <summary>The room code of a ticket still worth using; false when there is none or it is older than ten minutes.</summary>
        bool TryGet(out string roomCode);

        /// <summary>Writes the ticket for the match being played now, or refreshes its time.</summary>
        void Remember(string roomCode);

        void Clear();
    }
}
