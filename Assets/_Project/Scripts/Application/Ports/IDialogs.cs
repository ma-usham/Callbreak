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

    /// <summary>
    /// Shows one dialog at a time. The task completes with true for the primary button and false
    /// for the secondary one, a tap outside the panel, Android back, or when a newer dialog
    /// replaces it.
    /// </summary>
    public interface IDialogs
    {
        bool IsOpen { get; }

        Task<bool> ConfirmAsync(DialogRequest request, CancellationToken ct);

        /// <summary>Android back: closes the open dialog as "cancel". True when one was open.</summary>
        bool HandleBack();
    }
}
