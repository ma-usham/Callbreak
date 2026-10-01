namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Something over the menu that hears Android back (Escape) before the dialogs and the screen
    /// stack do: the name question at the start (ADR 022) keeps the press for itself while it is up.
    /// </summary>
    public interface IBackHandler
    {
        /// <summary>True when the press was handled (or swallowed) and nothing else may act on it.</summary>
        bool HandleBack();
    }
}
