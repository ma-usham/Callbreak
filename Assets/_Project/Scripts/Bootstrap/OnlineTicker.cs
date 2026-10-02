using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// Gives the room transport (the relay or the peer-to-peer network), the hosted room, the room
    /// member and the Quick Match search their time every frame, in every scene: a hosted table
    /// keeps running while the host's device changes scene. It also keeps Quick Match warmed up
    /// (signed in, the slow first directory call made) from the start, so a tap on Play Online does
    /// not spend its countdown on them (ADR 029); a warm-up that could not run is tried again later.
    /// </summary>
    public sealed class OnlineTicker : ITickable
    {
        private const double WarmUpEverySeconds = 60.0;

        private readonly ITickedRoomTransport _transport;
        private readonly RoomService _rooms;
        private readonly QuickMatchService _quickMatch;
        private readonly IClock _clock;
        private double _nextWarmUp = double.NegativeInfinity;

        public OnlineTicker(IRoomTransport transport, RoomService rooms, QuickMatchService quickMatch, IClock clock)
        {
            _transport = transport as ITickedRoomTransport;
            _rooms = rooms;
            _quickMatch = quickMatch;
            _clock = clock;
        }

        public void Tick()
        {
            double now = _clock.Now;
            if (_transport != null)
            {
                _transport.Tick(Time.unscaledDeltaTime, now);
            }

            // Does nothing once warm, while a warm-up runs, offline, or without a table directory.
            if (now >= _nextWarmUp)
            {
                _nextWarmUp = now + WarmUpEverySeconds;
                _quickMatch.WarmUp();
            }

            _rooms.Tick(now);
            _quickMatch.Tick(now);
        }
    }
}
