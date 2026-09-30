namespace Callbreak.Application.Ports
{
    /// <summary>
    /// Short notices over any screen (GDD §23 "Toasts" layer): "A hand had no spades - redealing.",
    /// "Welcome back", "Couldn't connect". Text is already localised by the caller.
    /// </summary>
    public interface IToast
    {
        void Show(string text);
    }
}
