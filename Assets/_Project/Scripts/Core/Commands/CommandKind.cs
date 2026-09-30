namespace Callbreak.Core.Commands
{
    /// <summary>Which command a <see cref="RecordedCommand"/> holds.</summary>
    public enum CommandKind : byte
    {
        /// <summary>A <see cref="PlaceCall"/>.</summary>
        PlaceCall = 1,

        /// <summary>A <see cref="PlayCard"/>.</summary>
        PlayCard = 2,

        /// <summary>A <see cref="ReadyNext"/>.</summary>
        ReadyNext = 3,
    }
}
