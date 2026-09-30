using Callbreak.Application.Rooms;

namespace Callbreak.Application.Online
{
    /// <summary>The outcome of starting to host a room.</summary>
    public readonly struct HostLinkResult
    {
        public HostLinkResult(IHostLink link, RoomError error)
        {
            Link = link;
            Error = error;
        }

        public IHostLink Link { get; }

        public RoomError Error { get; }

        public bool Succeeded => Error == RoomError.None && Link != null;
    }
}
