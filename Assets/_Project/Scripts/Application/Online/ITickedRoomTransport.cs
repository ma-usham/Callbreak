namespace Callbreak.Application.Online
{
    /// <summary>
    /// A room transport that moves its traffic and tells the room logic what arrived when it is
    /// given time: once per frame, on the main thread, with unscaled time.
    /// </summary>
    public interface ITickedRoomTransport : IRoomTransport
    {
        void Tick(float deltaTime, double now);
    }
}
