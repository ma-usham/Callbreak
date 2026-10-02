using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// What Quick Match needs of the room transport besides rooms found by their code (ADR 029):
    /// signing in before the table directory (<see cref="IQuickTableDirectory"/>) is searched,
    /// opening a table that only that directory leads to, and joining a table's host at the
    /// address the directory gave, without looking a code up; and, once a table's match is on,
    /// keeping it going when its host is lost (<see cref="IHandOverTransport"/>, ADR 030).
    /// Implemented by PeerRoomTransport; a transport without it (the relay, none) plays Quick Match
    /// at a table of the device's own.
    /// </summary>
    public interface IQuickMatchTransport : IHandOverTransport
    {
        /// <summary>
        /// The directory dropped a listing this transport does not keep itself, such as a Quick Match
        /// table's (the room code), for example after the device lost the network for a moment.
        /// Raised in order with everything else the transport reports; listing it again is up to
        /// whoever listed it.
        /// </summary>
        event Action<string> ListingLost;

        /// <summary>Signs this device in if it is not yet; false when the network cannot be reached.</summary>
        Task<bool> SignInAsync(CancellationToken ct);

        /// <summary>
        /// Hosts <paramref name="roomCode"/> without listing it anywhere: only a guest that knows this
        /// device's address joins. Fails as <see cref="IRoomTransport.HostAsync"/> does, but never
        /// with AlreadyStarted.
        /// </summary>
        Task<HostLinkResult> OpenAsync(string roomCode, CancellationToken ct);

        /// <summary>
        /// Connects to the host at <paramref name="hostAddress"/> on <paramref name="roomCode"/>'s
        /// channel, without a lookup. NotFound when that device turns the connection down (it hosts
        /// no such room any more); otherwise as <see cref="IRoomTransport.JoinAsync"/>.
        /// </summary>
        Task<ClientLinkResult> JoinAtAsync(string roomCode, string hostAddress, CancellationToken ct);
    }
}
