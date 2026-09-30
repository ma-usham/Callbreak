using System;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// The authority's end of a room's connections (GDD §24: the host device is the server in
    /// Phase 1). Messages are reliable and ordered per peer; everything is raised on the main
    /// thread. Implemented over PurrNet in Callbreak.Networking.
    /// </summary>
    public interface IHostLink : IDisposable
    {
        event Action<PeerId> PeerConnected;

        event Action<PeerId> PeerDisconnected;

        event Action<PeerId, byte[]> Received;

        void Send(PeerId peer, byte[] payload);

        /// <summary>Drops a peer (kicked, replaced by a newer connection, or flooding).</summary>
        void Disconnect(PeerId peer);
    }
}
