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
    }
}
