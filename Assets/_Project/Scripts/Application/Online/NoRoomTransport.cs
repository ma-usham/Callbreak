using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Rooms;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// The transport of a build without a relay: every attempt to host or join reports that the
    /// connection could not be made, so the screens show "Couldn't connect" instead of hanging.
    /// </summary>
    public sealed class NoRoomTransport : IRoomTransport
    {
        public Task<HostLinkResult> HostAsync(string roomCode, CancellationToken ct) =>
            Task.FromResult(new HostLinkResult(null, RoomError.Unknown));

        public Task<ClientLinkResult> JoinAsync(string roomCode, CancellationToken ct) =>
            Task.FromResult(new ClientLinkResult(null, RoomError.Unknown));
    }
}
