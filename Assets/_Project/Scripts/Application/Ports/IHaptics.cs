namespace Callbreak.Application.Ports
{
    public enum HapticPattern
    {
        /// <summary>Your turn starts; you win a trick.</summary>
        Light,

        /// <summary>Double light: an illegal tap; the timer reaches 5 s.</summary>
        Warning,

        /// <summary>Your call is met at round end.</summary>
        Success,
    }

    /// <summary>Vibration (GDD §14). Implementations respect the Vibration setting themselves.</summary>
    public interface IHaptics
    {
        void Play(HapticPattern pattern);
    }
}
