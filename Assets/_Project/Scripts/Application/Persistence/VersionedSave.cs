using System;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Persistence
{
    /// <summary>
    /// The load policy every local save file follows (GDD §29): a missing or unreadable file means
    /// defaults, and a file written by a newer app version (a <c>schemaVersion</c> above the one
    /// this build knows) is ignored and must never be overwritten, so the caller keeps its values
    /// in memory only.
    /// </summary>
    internal static class VersionedSave
    {
        /// <summary>
        /// Loads <paramref name="key"/>. Returns null when there is nothing usable, or when the file
        /// comes from a newer schema (then <paramref name="isFutureVersion"/> is true).
        /// </summary>
        public static T Load<T>(ISaveStore store, string key, int currentVersion, Func<T, int> versionOf, out bool isFutureVersion)
            where T : class
        {
            isFutureVersion = false;
            if (!store.TryLoad(key, out T loaded) || loaded == null)
            {
                return null;
            }

            if (versionOf(loaded) > currentVersion)
            {
                isFutureVersion = true;
                return null;
            }

            return loaded;
        }
    }
}
