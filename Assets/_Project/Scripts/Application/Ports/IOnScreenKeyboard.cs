namespace Callbreak.Application.Ports
{
    /// <summary>
    /// The device's on-screen keyboard: how much of the bottom of the screen it covers, so a panel
    /// can keep its text field above it (a phone held sideways has a keyboard over half the screen).
    /// </summary>
    public interface IOnScreenKeyboard
    {
        /// <summary>The keyboard's height in screen pixels; 0 while it is down, and on a device without one.</summary>
        float Height { get; }
    }
}
