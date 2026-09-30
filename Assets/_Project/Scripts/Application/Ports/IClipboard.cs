namespace Callbreak.Application.Ports
{
    /// <summary>The system clipboard: Paste Code on screen 03, copy the room code on screen 04.</summary>
    public interface IClipboard
    {
        string GetText();

        void SetText(string text);
    }
}
