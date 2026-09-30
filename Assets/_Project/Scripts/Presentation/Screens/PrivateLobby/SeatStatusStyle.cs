namespace Callbreak.Presentation.Screens
{
    /// <summary>How a lobby seat's status line is coloured (GDD §11 screen 04).</summary>
    public enum SeatStatusStyle
    {
        /// <summary>"Host": muted brown.</summary>
        Host,

        /// <summary>"Ready": Ready Green.</summary>
        Ready,

        /// <summary>"Not ready": muted brown.</summary>
        NotReady,

        /// <summary>An empty seat ("Waiting..."): muted brown.</summary>
        Waiting,
    }
}
