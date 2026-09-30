namespace Callbreak.Application.Rooms
{
    /// <summary>Keeps at most one <see cref="RejoinTicket"/> (GDD §27: "App killed? ... Rejoin match").</summary>
    public interface IRejoinStore
    {
        /// <summary>
        /// The room code of a ticket still worth using; false when there is none or it is older than
        /// ten minutes. An expired ticket's match is recorded as left (GDD §8) and the ticket goes.
        /// </summary>
        bool TryGet(out string roomCode);

        /// <summary>
        /// Writes the ticket for the match being played now, or refreshes its time. A ticket of
        /// another match that is still there was never gone back to: that match is recorded as left.
        /// </summary>
        void Remember(string roomCode, string matchId);

        /// <summary>The match is over, or was left on purpose and recorded already: the ticket goes, nothing is recorded.</summary>
        void Clear();

        /// <summary>
        /// The player will not go back to the ticket's match: they declined "Rejoin match", or the
        /// host refused them because the seat hold is over. The match counts as left, a loss that
        /// sets no best score (GDD §8), and the ticket goes.
        /// </summary>
        void Abandon();
    }
}
