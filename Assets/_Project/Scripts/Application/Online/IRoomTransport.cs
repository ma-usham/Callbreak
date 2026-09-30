using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Rooms;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// Opens the connections a private room needs (GDD §24 Phase 1: host + relay; the room code
    /// names the relay room). The only thing the room logic asks of the network; implemented with
    /// PurrNet in Callbreak.Networking and in memory for tests.
    /// </summary>
    public interface IRoomTransport
    {
        /// <summary>
        /// Starts hosting under <paramref name="roomCode"/>. AlreadyStarted means the code is taken
        /// (the caller draws another); Offline or Unknown mean the relay could not be reached.
        /// </summary>
        Task<HostLinkResult> HostAsync(string roomCode, CancellationToken ct);

        /// <summary>Connects to the host of <paramref name="roomCode"/>; NotFound when nobody hosts it.</summary>
        Task<ClientLinkResult> JoinAsync(string roomCode, CancellationToken ct);
    }
}
