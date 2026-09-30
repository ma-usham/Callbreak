namespace Callbreak.Application.Ports
{
    /// <summary>The native share sheet (GDD §11 screen 04 "Invite Friends"); falls back to the clipboard.</summary>
    public interface IShareService
    {
        /// <summary>Returns false when no share sheet is available and the text was copied instead.</summary>
        bool ShareText(string title, string text);
    }
}
