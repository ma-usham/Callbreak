using System;

namespace Callbreak.Application.Ports
{
    /// <summary>
    /// The wall clock in UTC, for what goes by dates, as festival offers do (ADR 067): a server's time
    /// once one has answered (<see cref="Offers.TrustedClock"/>), the device's until then. A test brings
    /// its own.
    /// </summary>
    public interface IWallClock
    {
        DateTime UtcNow { get; }
    }
}
