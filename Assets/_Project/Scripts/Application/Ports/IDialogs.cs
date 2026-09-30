using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Ports
{
    /// <summary>
    /// A modal question over any screen (GDD §23 "Popups" layer): "Leave match?", "Exit Callbreak?",
    /// "Resume your match?". Text is already localised by the caller.
    /// </summary>
    public sealed class DialogRequest
    {
        public DialogRequest(string title, string body, string confirmLabel, string cancelLabel)
        {
            Title = title;
            Body = body;
            ConfirmLabel = confirmLabel;
            CancelLabel = cancelLabel;
        }

        public string Title { get; }

        /// <summary>The line under the title; empty for a bare question.</summary>
        public string Body { get; }

        /// <summary>The primary (crimson) button.</summary>
        public string ConfirmLabel { get; }

        /// <summary>The secondary button; null or empty for a dialog with a single button.</summary>
        public string CancelLabel { get; }
    }

    /// <summary>How a dialog was answered.</summary>
    public enum DialogChoice
    {
        /// <summary>
        /// Closed without an answer: a tap outside the panel, Android back, a newer dialog taking
        /// its place, or the asker going away.
        /// </summary>
        Dismissed,

        /// <summary>The primary button.</summary>
        Confirm,

        /// <summary>The secondary button.</summary>
        Cancel,
    }

    /// <summary>Shows one dialog at a time: asking again closes the open one as dismissed.</summary>
    public interface IDialogs
    {
        bool IsOpen { get; }

        /// <summary>
        /// For a yes-or-no question: true for the primary button; false for the secondary one and
        /// for a dialog that was dismissed.
        /// </summary>
        Task<bool> ConfirmAsync(DialogRequest request, CancellationToken ct);

        /// <summary>
        /// For a dialog whose two buttons are two different actions ("Retry / Play Offline"): says
        /// which one was pressed, or that the dialog was dismissed. Cancelling
        /// <paramref name="ct"/> dismisses it.
        /// </summary>
        Task<DialogChoice> AskAsync(DialogRequest request, CancellationToken ct);

        /// <summary>Android back: dismisses the open dialog. True when one was open.</summary>
        bool HandleBack();
    }
}
