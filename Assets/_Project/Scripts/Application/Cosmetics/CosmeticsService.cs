using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Cosmetics
{
    /// <summary>
    /// <see cref="ICosmeticsService"/> over the <c>cosmetics</c> save file (GDD §13, screen 15). The
    /// three v1 decks are all owned; their ids match card-designs/full-decks/manifest.json. An
    /// unknown id is rejected; a stored id this build doesn't know (a removed seasonal deck) falls
    /// back to Classic Crimson. A file from a newer app version is ignored and never overwritten.
    /// </summary>
    public sealed class CosmeticsService : ICosmeticsService
    {
        public const string SaveKey = "cosmetics";
        public const string ClassicCrimson = "classic-crimson";
        public const string HimalayanIvory = "himalayan-ivory";
        public const string KathmanduGold = "kathmandu-gold";
        public const string DefaultDeckId = ClassicCrimson;

        private readonly ISaveStore _store;
        private readonly CosmeticsData _data;
        private readonly bool _readOnly;
        private readonly ReadOnlyCollection<string> _deckIds;

        public CosmeticsService(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _deckIds = new ReadOnlyCollection<string>(new[] { ClassicCrimson, HimalayanIvory, KathmanduGold });
            CosmeticsData loaded = VersionedSave.Load<CosmeticsData>(store, SaveKey, CosmeticsData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new CosmeticsData();
            _data.schemaVersion = CosmeticsData.CurrentVersion;
            if (!IsKnown(_data.selectedDeckId))
            {
                _data.selectedDeckId = DefaultDeckId;
            }
        }

        public event Action Changed;

        public IReadOnlyList<string> DeckIds => _deckIds;

        public string SelectedDeckId => _data.selectedDeckId;

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved.</summary>
        public bool IsReadOnly => _readOnly;

        public void SelectDeck(string deckId)
        {
            if (!IsKnown(deckId))
            {
                throw new ArgumentException($"Unknown deck id '{deckId}'.", nameof(deckId));
            }

            if (_data.selectedDeckId == deckId)
            {
                return;
            }

            _data.selectedDeckId = deckId;
            if (!_readOnly)
            {
                _store.Save(SaveKey, _data);
            }

            Changed?.Invoke();
        }

        public bool IsKnown(string deckId) => deckId != null && _deckIds.Contains(deckId);
    }
}
