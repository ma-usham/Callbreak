using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;

namespace Callbreak.Infrastructure.Services
{
    /// <summary>
    /// <see cref="IAuthService"/> without a backend: a random anonymous player id (a GUID) created
    /// on the first sign-in and kept in the <c>auth</c> save file, so it is stable across launches
    /// on this device. It stands in for UGS anonymous sign-in (GDD §16, §27) until the project is
    /// linked to a Unity Cloud project; callers only see the port. Signing in never blocks.
    /// </summary>
    public sealed class LocalAuthService : IAuthService
    {
        public const string SaveKey = "auth";

        private readonly ISaveStore _store;

        public LocalAuthService(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool IsSignedIn { get; private set; }

        public string PlayerId { get; private set; } = string.Empty;

        public Task<bool> SignInAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(ct);
            }

            if (IsSignedIn)
            {
                return Task.FromResult(true);
            }

            string id = null;
            bool keepFile = false;
            if (_store.TryLoad(SaveKey, out AuthData data) && data != null && IsValidId(data.playerId))
            {
                id = data.playerId;
            }
            else if (data != null && data.schemaVersion > AuthData.CurrentVersion)
            {
                keepFile = true; // written by a newer app version: never overwrite it
            }

            if (id == null)
            {
                id = Guid.NewGuid().ToString("N");
                if (!keepFile)
                {
                    _store.Save(SaveKey, new AuthData { playerId = id });
                }
            }

            PlayerId = id;
            IsSignedIn = true;
            return Task.FromResult(true);
        }

        private static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && Guid.TryParseExact(id, "N", out _);
    }
}
