using System;
using Callbreak.Application.Ports;
using UnityEngine;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// <see cref="INetworkStatus"/> from <c>UnityEngine.Application.internetReachability</c> (GDD §11
    /// screen 00: offline mutes the online buttons). It only says whether a network is there, not
    /// whether a server answers. The bootstrap calls <see cref="Tick"/> every frame; the platform is
    /// asked at most once per <see cref="PollIntervalSeconds"/> (it is a JNI call on Android), and
    /// <see cref="Changed"/> fires on the frame a change is seen.
    /// </summary>
    public sealed class NetworkStatus : INetworkStatus
    {
        public const double PollIntervalSeconds = 1.0;

        private readonly IClock _clock;
        private double _nextPoll;

        public NetworkStatus(IClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            IsOnline = Read();
            _nextPoll = clock.Now + PollIntervalSeconds;
        }

        public event Action<bool> Changed;

        public bool IsOnline { get; private set; }

        public void Tick()
        {
            double now = _clock.Now;
            if (now < _nextPoll)
            {
                return;
            }

            _nextPoll = now + PollIntervalSeconds;
            Poll();
        }

        /// <summary>Checks now, regardless of the interval (e.g. when the app returns to the foreground).</summary>
        public void Poll()
        {
            bool online = Read();
            if (online == IsOnline)
            {
                return;
            }

            IsOnline = online;
            Changed?.Invoke(online);
        }

        private static bool Read() => UnityEngine.Application.internetReachability != NetworkReachability.NotReachable;
    }
}
