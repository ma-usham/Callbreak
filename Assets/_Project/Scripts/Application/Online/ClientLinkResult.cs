using Callbreak.Application.Rooms;

namespace Callbreak.Application.Online
{
    /// <summary>The outcome of connecting to a room's host.</summary>
    public readonly struct ClientLinkResult
    {
        public ClientLinkResult(IClientLink link, RoomError error)
        {
            Link = link;
            Error = error;
        }

        public IClientLink Link { get; }

        public RoomError Error { get; }

        public bool Succeeded => Error == RoomError.None && Link != null;
    }
}
