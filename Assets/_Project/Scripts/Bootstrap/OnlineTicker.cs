using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Networking;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// Gives the relay connection, the hosted room, the room member and the Quick Match search
    /// their time every frame, in every scene: a hosted table keeps running while the host's
    /// device changes scene.
    /// </summary>
    public sealed class OnlineTicker : ITickable
    {
        private readonly PurrRoomTransport _relay;
        private readonly RoomService _rooms;
        private readonly QuickMatchService _quickMatch;
        private readonly IClock _clock;

        public OnlineTicker(IRoomTransport transport, RoomService rooms, QuickMatchService quickMatch, IClock clock)
        {
            _relay = transport as PurrRoomTransport;
            _rooms = rooms;
            _quickMatch = quickMatch;
            _clock = clock;
        }

        public void Tick()
        {
            double now = _clock.Now;
            if (_relay != null)
            {
                _relay.Tick(Time.unscaledDeltaTime, now);
            }

            _rooms.Tick(now);
            _quickMatch.Tick(now);
        }
    }
}
