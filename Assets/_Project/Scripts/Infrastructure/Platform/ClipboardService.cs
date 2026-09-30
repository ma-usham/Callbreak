using Callbreak.Application.Ports;
using UnityEngine;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary><see cref="IClipboard"/> over the system clipboard (GDD §11 screens 03 and 04).</summary>
    public sealed class ClipboardService : IClipboard
    {
        public string GetText() => GUIUtility.systemCopyBuffer ?? string.Empty;

        public void SetText(string text) => GUIUtility.systemCopyBuffer = text ?? string.Empty;
    }
}
