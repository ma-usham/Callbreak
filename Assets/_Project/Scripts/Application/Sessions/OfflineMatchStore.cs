using System;
using Callbreak.Application.Ports;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
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
        /// <summary>
        /// True when there is a save <see cref="TryLoad"/> accepts, so "Resume" is only ever
        /// offered for a match that can really be continued.
        /// </summary>
        bool HasSave { get; }

        /// <summary>
        /// The saved match. False, with no save, when there is none or when what is there cannot
        /// be continued: written under another schema, holding no match, or holding one the rules
        /// engine refuses (GDD §22 "a snapshot that fails to load is discarded, never fixed up").
        /// </summary>
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

        /// <summary>The rules every offline match is played under (GDD §11 screen 06).</summary>
        internal static RuleSet Rules => RuleSet.NepaliStandard;

        public bool HasSave => TryLoad(out _);

        public bool TryLoad(out OfflineMatchSave save)
        {
            if (_store.TryLoad(Key, out save) && save != null && save.schemaVersion == SchemaVersion && TryRestore(save, out _, out _))
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

        /// <summary>
        /// Rebuilds the saved match: its engine, and the generator that engine deals the rest of
        /// the match from (the one whose state must be saved next time). False when the match
        /// cannot be continued: no snapshot, a generator state that is not one, a snapshot the
        /// engine refuses, or a match with nothing left to play.
        /// </summary>
        internal static bool TryRestore(OfflineMatchSave save, out MatchEngine engine, out Pcg32Random rng)
        {
            engine = null;
            rng = null;

            // A PCG increment is always odd; anything else was never written by a session.
            if (save == null || save.match == null || (save.rngIncrement & 1UL) == 0UL)
            {
                return false;
            }

            Pcg32Random restoredRng = Pcg32Random.FromState(save.rngState, save.rngIncrement);
            if (!MatchEngine.TryRestore(save.match, Rules, restoredRng, out MatchEngine restored, out _))
            {
                return false;
            }

            if (restored.Phase != Phase.Calling && restored.Phase != Phase.Playing && restored.Phase != Phase.RoundSummary)
            {
                return false;
            }

            engine = restored;
            rng = restoredRng;
            return true;
        }
    }
}
