using System;

namespace Callbreak.Infrastructure.Services
{
    /// <summary>auth.json: the anonymous player id <see cref="LocalAuthService"/> created on first sign-in.</summary>
    [Serializable]
    public sealed class AuthData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;
        public string playerId = string.Empty;
    }
}
