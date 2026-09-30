using Callbreak.Application.Ports;
using UnityEngine;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary><see cref="IClock"/> as real time since startup: monotonic, unaffected by time scale or pauses.</summary>
    public sealed class UnityClock : IClock
    {
        public double Now => Time.realtimeSinceStartupAsDouble;
    }
}
