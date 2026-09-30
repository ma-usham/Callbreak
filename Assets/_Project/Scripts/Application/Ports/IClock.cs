namespace Callbreak.Application.Ports
{
    /// <summary>
    /// Monotonic time in seconds. Sessions, timers and bots read time only through this, so tests
    /// drive it by hand (GDD §22 "no clock inside" the engine; §30 no wall-clock in tests).
    /// </summary>
    public interface IClock
    {
        double Now { get; }
    }
}
