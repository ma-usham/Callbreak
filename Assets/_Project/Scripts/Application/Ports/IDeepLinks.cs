using System;

namespace Callbreak.Application.Ports
{
    /// <summary>
    /// Invite links (GDD §11 screens 00, 03 and 04): <c>callbreak://join/CODE</c> opens Join Room
    /// with the code filled in, whether the link started the app or arrived while it was running.
    /// </summary>
    public interface IDeepLinks
    {
        /// <summary>Raised when a join link arrives while the app is running.</summary>
        event Action JoinCodeArrived;

        /// <summary>True while a join link is waiting to be used.</summary>
        bool HasJoinCode { get; }

        /// <summary>The room code of the waiting join link, or null. Reading it uses it up.</summary>
        string TakeJoinCode();
    }
}
