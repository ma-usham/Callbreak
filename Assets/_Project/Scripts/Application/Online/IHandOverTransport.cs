using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// What the in-game host hand-over needs of the room transport (ADR 030). A room has a
    /// generation: 0 as its creator opened it, one more each time a player takes it over from a lost
    /// host. Each generation is a room of its own on the network (its own channel), so the host that
    /// was lost and the one that took over never mix up their connections, and a player can be a
    /// member of one while it hosts the other. Implemented by PeerRoomTransport; a transport without
    /// it (the relay) keeps a match's host for the whole match.
    /// </summary>
    public interface IHandOverTransport : IRoomTransport
    {
        /// <summary>This device's address once it is signed in; null before.</summary>
        string LocalAddress { get; }

        /// <summary>
        /// Hosts generation <paramref name="generation"/> (1 or more) of <paramref name="roomCode"/>:
        /// this device takes the room over. A private room is <paramref name="listed"/> under its code
        /// at that generation, so lookups find this host from then on; a Quick Match table is not
        /// listed (its players know this device's address). Fails as
        /// <see cref="IRoomTransport.HostAsync"/> does; AlreadyStarted when another device lists the
        /// code at that generation or a later one.
        /// </summary>
        Task<HostLinkResult> TakeOverAsync(string roomCode, int generation, bool listed, CancellationToken ct);

        /// <summary>
        /// Connects to the host of generation <paramref name="generation"/> of
        /// <paramref name="roomCode"/> at <paramref name="hostAddress"/>, without a lookup. NotFound
        /// when that device does not host that generation of the room (any more).
        /// </summary>
        Task<ClientLinkResult> JoinAtAsync(string roomCode, int generation, string hostAddress, CancellationToken ct);
    }

    /// <summary>
    /// A host link that knows where its peers are on the network (ADR 030), so the host can tell its
    /// players where the next host in line is.
    /// </summary>
    public interface IHostAddresses
    {
        /// <summary>This device's address; null before it is signed in.</summary>
        string LocalAddress { get; }

        /// <summary>The generation of the room this link hosts (<see cref="IHandOverTransport"/>).</summary>
        int Generation { get; }

        /// <summary>The address of the device behind <paramref name="peer"/>; null for a peer this link does not know.</summary>
        string AddressOf(PeerId peer);
    }
}
