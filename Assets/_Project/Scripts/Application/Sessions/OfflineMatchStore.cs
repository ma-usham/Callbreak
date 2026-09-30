using System;
using Callbreak.Application.Ports;
using Callbreak.Core.Views;

namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// The unfinished offline match (GDD §29 offline_match.json): the engine snapshot plus what the
    /// session needs to continue it. Deleted when the match ends.
    /// </summary>
    [Serializable]
    public sealed class OfflineMatchSave
    {
        public int schemaVersion = 1;
        public MatchSnapshot match;
        public int difficulty;
        public ulong seed;
        public ulong rngState;
        public ulong rngIncrement;
    }

    /// <summary>Keeps at most one unfinished offline match (GDD §8 "Resume your match?").</summary>
    public interface IOfflineMatchStore
    {
        bool HasSave { get; }

        bool TryLoad(out OfflineMatchSave save);

        void Save(OfflineMatchSave save);

        void Clear();
    }

    /// <summary><see cref="IOfflineMatchStore"/> over the save store, key "offline_match".</summary>
    public sealed class OfflineMatchStore : IOfflineMatchStore
    {
        public const string Key = "offline_match";
        private const int SchemaVersion = 1;

        private readonly ISaveStore _store;

        public OfflineMatchStore(ISaveStore store)
        {
            _store = store;
        }

        public bool HasSave => _store.Exists(Key);

        public bool TryLoad(out OfflineMatchSave save)
        {
            if (_store.TryLoad(Key, out save) && save != null && save.schemaVersion == SchemaVersion && save.match != null)
            {
                return true;
            }

            save = null;
            return false;
        }

        public void Save(OfflineMatchSave save)
        {
            save.schemaVersion = SchemaVersion;
            _store.Save(Key, save);
        }

        public void Clear()
        {
            if (_store.Exists(Key))
            {
                _store.Delete(Key);
            }
        }
    }
}
