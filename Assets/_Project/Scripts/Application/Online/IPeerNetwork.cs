using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Rooms;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// A peer-to-peer network with a directory of hosted rooms, as Epic Online Services offers one
    /// (ADR 028): the device signs in (no account needed), a host lists its room code, a guest
    /// looks the code up to learn the host's address, and the two then send each other packets,
    /// reliably and in order, on a channel. Packets are small (<see cref="MaxPacketSize"/>), so
    /// the room transport over it (PeerRoomTransport) splits the room's messages to fit. Every event is raised on
    /// the main thread. Implemented over EOS in Callbreak.Networking, and in memory for tests.
    /// </summary>
    public interface IPeerNetwork
    {
        /// <summary>Someone asks to connect on a channel (address, channel); nothing opens until it is accepted.</summary>
        event Action<string, string> ConnectionRequested;

        /// <summary>A connection is open (address, channel).</summary>
        event Action<string, string> Connected;

        /// <summary>A connection was closed by either side or lost (address, channel).</summary>
        event Action<string, string> Disconnected;

        /// <summary>The directory dropped a listing of this device's (the room code), for example after the device lost the network.</summary>
        event Action<string> ListingLost;

        /// <summary>The largest packet <see cref="Send"/> takes, in bytes.</summary>
        int MaxPacketSize { get; }

        /// <summary>This device's address once it is signed in; null before.</summary>
        string LocalAddress { get; }

        /// <summary>Signs this device in if it is not yet; false when the network cannot be reached.</summary>
        Task<bool> SignInAsync(CancellationToken ct);

        /// <summary>
        /// Lists this device as the host of <paramref name="roomCode"/>. None when listed;
        /// AlreadyStarted when another device lists the code already; Offline or Unknown when the
        /// directory cannot be reached.
        /// </summary>
        Task<RoomError> ListAsync(string roomCode, CancellationToken ct);

        /// <summary>Takes this device's listing of <paramref name="roomCode"/> down; nothing if there is none.</summary>
        void Unlist(string roomCode);

        /// <summary>Looks up the device that hosts <paramref name="roomCode"/>: NotFound when nobody lists it.</summary>
        Task<PeerLookup> LookUpAsync(string roomCode, CancellationToken ct);

        /// <summary>
        /// Allows the connection with <paramref name="address"/> on <paramref name="channel"/>: the
        /// answer to <see cref="ConnectionRequested"/>, and what a guest does before its first
        /// packet. The connection opens once both sides allow it or one has sent to the other.
        /// </summary>
        void Accept(string address, string channel);

        /// <summary>
        /// Closes the connection with <paramref name="address"/> on <paramref name="channel"/>.
        /// <see cref="Disconnected"/> is raised to this side too, once the network confirms the
        /// close; until then packets sent to that address on that channel are dropped.
        /// </summary>
        void Close(string address, string channel);

        /// <summary>Sends one packet of at most <see cref="MaxPacketSize"/> bytes, reliably and in order.</summary>
        void Send(string address, string channel, byte[] packet);

        /// <summary>Hands over the oldest packet that arrived and has not been taken yet, if any.</summary>
        bool TryReceive(out string address, out string channel, out byte[] packet);
    }

    /// <summary>The answer to <see cref="IPeerNetwork.LookUpAsync"/>.</summary>
    public readonly struct PeerLookup
    {
        public PeerLookup(string address, RoomError error)
        {
            Address = address;
            Error = error;
        }

        /// <summary>The host's address when it was found; null otherwise.</summary>
        public string Address { get; }

        /// <summary>None when found, NotFound when nobody lists the code, Offline or Unknown when the directory cannot be reached.</summary>
        public RoomError Error { get; }

        public bool Found => Error == RoomError.None && !string.IsNullOrEmpty(Address);
    }
}
