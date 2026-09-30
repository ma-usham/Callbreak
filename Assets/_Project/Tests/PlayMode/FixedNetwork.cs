using System;
using Callbreak.Application.Ports;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>A connection that never changes, so a test does not depend on the machine's.</summary>
    public sealed class FixedNetwork : INetworkStatus
    {
        public FixedNetwork(bool online)
        {
            IsOnline = online;
        }

        public event Action<bool> Changed
        {
            add { }
            remove { }
        }

        public bool IsOnline { get; }
    }
}
