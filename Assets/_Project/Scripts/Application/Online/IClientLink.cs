using System;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// A member's end of its connection to the host. Messages are reliable and ordered;
    /// <see cref="Closed"/> fires once when the connection is lost or the host drops it.
    /// </summary>
    public interface IClientLink : IDisposable
    {
        bool IsConnected { get; }

        event Action<byte[]> Received;

        event Action Closed;

        void Send(byte[] payload);
    }
}
