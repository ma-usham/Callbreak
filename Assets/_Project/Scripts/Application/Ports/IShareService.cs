namespace Callbreak.Application.Ports
{
    /// <summary>The device's share sheet (GDD §11 screen 04 "Invite Friends").</summary>
    public interface IShareService
    {
        /// <summary>
        /// Opens the share sheet with <paramref name="text"/>. False when there is none on this
        /// platform (the Editor, a desktop, iOS until its plugin exists) or the system refused;
        /// the caller then offers the text another way.
        /// </summary>
        bool ShareText(string title, string text);
    }
}
