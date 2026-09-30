using System;

namespace Callbreak.Application.Ports
{
    /// <summary>
    /// Whether the device has a connection at all (GDD §11 screen 00: with no internet the online
    /// buttons are muted and a toast points to Play Offline).
    /// </summary>
    public interface INetworkStatus
    {
        bool IsOnline { get; }

        event Action<bool> Changed;
    }
}
