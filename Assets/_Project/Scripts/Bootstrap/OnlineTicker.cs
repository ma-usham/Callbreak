using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// Gives the room transport (the relay or the peer-to-peer network), the hosted room, the room
    /// member and the Quick Match search their time every frame, in every scene: a hosted table
    /// keeps running while the host's device changes scene.
    /// </summary>
    public sealed class OnlineTicker : ITickable
    {
        private readonly ITickedRoomTransport _transport;
        private readonly RoomService _rooms;
        private readonly QuickMatchService _quickMatch;
        private readonly IClock _clock;

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

            _rooms.Tick(now);
            _quickMatch.Tick(now);
        }
    }
}
