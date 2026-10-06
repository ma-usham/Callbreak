namespace Callbreak.Application.Profile
{
    /// <summary>Outcome of validating a display name (GDD §11 screen 14: 3-16 letters, digits or spaces, filtered).</summary>
    public enum NameCheck
    {
        Ok,
        Unchanged,
        TooShort,
        TooLong,
        InvalidCharacters,
        NotAllowed,

        /// <summary>A valid new name, but the last one was saved less than 7 days ago (ADR 041).</summary>
        TooSoon,
    }
}
