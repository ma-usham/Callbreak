using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using VContainer.Unity;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// Gives the hosted room, the room member and the Quick Match search their time every frame, in
    /// every scene: a hosted table keeps running while the host's device changes scene.
    /// </summary>
    public sealed class OnlineTicker : ITickable
    {
        private readonly RoomService _rooms;
        private readonly QuickMatchService _quickMatch;
        private readonly IClock _clock;

        public OnlineTicker(RoomService rooms, QuickMatchService quickMatch, IClock clock)
        {
            _rooms = rooms;
            _quickMatch = quickMatch;
            _clock = clock;
        }

        public void Tick()
        {
            double now = _clock.Now;
            _rooms.Tick(now);
            _quickMatch.Tick(now);
        }
    }
}
