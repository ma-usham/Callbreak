using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Ports
{
    /// <summary>
    /// Player identity (GDD §27): a stable anonymous player id from silent sign-in at boot, used to
    /// bind seats on reconnect. Sign-in never blocks the UI.
    /// </summary>
    public interface IAuthService
    {
        bool IsSignedIn { get; }

        /// <summary>Stable across launches once signed in; empty before.</summary>
        string PlayerId { get; }

        Task<bool> SignInAsync(CancellationToken ct);
    }
}
