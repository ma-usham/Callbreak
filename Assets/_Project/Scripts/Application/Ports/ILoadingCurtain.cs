using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Ports
{
    /// <summary>
    /// The full-screen curtain scene changes happen behind (GDD §21 startup sequence, §23 curtain
    /// layer): it covers the screen before a scene unloads and uncovers the next one once it is
    /// ready, and it swallows input while it covers.
    /// </summary>
    public interface ILoadingCurtain
    {
        bool IsCovering { get; }

        Task CoverAsync(CancellationToken ct);

        Task UncoverAsync(CancellationToken ct);
    }
}
