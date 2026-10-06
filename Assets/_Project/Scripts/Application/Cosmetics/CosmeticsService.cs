using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Callbreak.Application.Economy;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Cosmetics
{
    /// <summary>
    /// The player's cosmetics over the <c>cosmetics</c> save file (GDD §13, screen 15; the shop of
    /// 4 Oct 2026, mockups 15, 20 and 21): what the player owns and has equipped of each
    /// <see cref="CosmeticKind"/> in the <see cref="CosmeticCatalog"/>, and buying with coins from the
    /// <see cref="IWallet"/>. It is both <see cref="ICosmeticsService"/> and <see cref="ICosmeticsShop"/>,
    /// so the equipped deck is the selected deck, and <see cref="Changed"/> is raised for any change.
    /// An unknown id is rejected; a stored id this build doesn't know (a removed seasonal item), or an
    /// equipped one no longer owned, falls back to the default. A purchase this build doesn't know
    /// stays in the file. A version 1 file keeps its deck while it is still owned (every deck was
    /// free until 6 Oct 2026; now only Classic Crimson is) and starts the rest at the defaults. A file
    /// from a newer app version is ignored and never overwritten: equipping then lives in memory for
    /// this session, and nothing is sold, because this build couldn't keep the purchase.
    /// </summary>
    public sealed class CosmeticsService : ICosmeticsService, ICosmeticsShop
    {
        public const string SaveKey = "cosmetics";
        public const string ClassicCrimson = CosmeticCatalog.Decks.ClassicCrimson;
        public const string HimalayanIvory = CosmeticCatalog.Decks.HimalayanIvory;
        public const string KathmanduGold = CosmeticCatalog.Decks.KathmanduGold;
        public const string DefaultDeckId = CosmeticCatalog.Decks.Default;

        private const int FirstShopVersion = 2;

        private readonly ISaveStore _store;
        private readonly IWallet _wallet;
        private readonly CosmeticsData _data;
        private readonly bool _readOnly;
        private readonly ReadOnlyCollection<string> _deckIds;

        /// <param name="wallet">
        /// What purchases are paid from. Null for code that needs only the deck (tools, tests): a
        /// purchase that costs coins then throws <see cref="InvalidOperationException"/>.
        /// </param>
        public CosmeticsService(ISaveStore store, IWallet wallet = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _wallet = wallet;
            _deckIds = new ReadOnlyCollection<string>(CosmeticCatalog.Of(CosmeticKind.Deck).Select(item => item.Id).ToArray());
            CosmeticsData loaded = VersionedSave.Load<CosmeticsData>(store, SaveKey, CosmeticsData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new CosmeticsData();
            Repair(_data);
        }

        public event Action Changed;

        public IReadOnlyList<string> DeckIds => _deckIds;

        public string SelectedDeckId => _data.selectedDeckId;

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved.</summary>
        public bool IsReadOnly => _readOnly;

        /// <summary>Equips a deck: the same as <see cref="Equip"/> for <see cref="CosmeticKind.Deck"/>.</summary>
        public void SelectDeck(string deckId) => Equip(CosmeticKind.Deck, deckId);

        public bool IsKnown(string deckId) => CosmeticCatalog.Find(CosmeticKind.Deck, deckId) != null;

        public IReadOnlyList<CosmeticItem> Catalog(CosmeticKind kind) => CosmeticCatalog.Of(kind);

        public bool IsOwned(CosmeticKind kind, string id) => IsOwned(_data, CosmeticCatalog.Find(kind, id));

        public string Equipped(CosmeticKind kind) => EquippedIn(_data, kind);

        /// <summary>
        /// Equips an owned item from the next match on. Throws <see cref="ArgumentException"/> for an
        /// id not in the kind's catalogue and <see cref="InvalidOperationException"/> for one not owned.
        /// </summary>
        public void Equip(CosmeticKind kind, string id)
        {
            CosmeticItem item = CosmeticCatalog.Find(kind, id);
            if (item == null)
            {
                throw new ArgumentException($"Unknown {kind} id '{id}'.", nameof(id));
            }

            if (!IsOwned(_data, item))
            {
                throw new InvalidOperationException($"The {kind} '{id}' isn't owned.");
            }

            if (Equipped(kind) == id)
            {
                return;
            }

            SetEquipped(_data, kind, id);
            Commit();
        }

        /// <summary>
        /// Buys an item with coins from the wallet: owned, not equipped. The purchase is saved before
        /// the coins are taken, so a wallet listener that throws (its exception reaches the caller)
        /// can't take the coins without the item. While the file comes from a newer app version
        /// nothing is sold (<see cref="PurchaseResult.UnknownItem"/>) and no coins are taken.
        /// </summary>
        public PurchaseResult Buy(CosmeticKind kind, string id)
        {
            CosmeticItem item = CosmeticCatalog.Find(kind, id);
            if (item == null)
            {
                return PurchaseResult.UnknownItem;
            }

            if (IsOwned(_data, item))
            {
                return PurchaseResult.AlreadyOwned;
            }

            if (_readOnly)
            {
                return PurchaseResult.UnknownItem;
            }

            if (_wallet == null)
            {
                throw new InvalidOperationException("This CosmeticsService has no wallet to pay from.");
            }

            if (_wallet.Coins < item.Price)
            {
                return PurchaseResult.NotEnoughCoins;
            }

            // The purchase is saved before the coins are taken: the wallet tells its listeners as it
            // changes, and one that throws must not leave the coins gone and the item not owned.
            List<string> bought = Bought(_data, kind);
            bought.Add(id);
            _store.Save(SaveKey, _data);
            if (!_wallet.TrySpend(item.Price))
            {
                bought.Remove(id);
                _store.Save(SaveKey, _data);
                return PurchaseResult.NotEnoughCoins;
            }

            Changed?.Invoke();
            return PurchaseResult.Bought;
        }

        private void Commit()
        {
            if (!_readOnly)
            {
                _store.Save(SaveKey, _data);
            }

            Changed?.Invoke();
        }

        private static bool IsOwned(CosmeticsData data, CosmeticItem item)
        {
            return item != null && (item.Price <= 0 || Bought(data, item.Kind).Contains(item.Id));
        }

        private static List<string> Bought(CosmeticsData data, CosmeticKind kind)
        {
            switch (kind)
            {
                case CosmeticKind.Deck:
                    return data.boughtDeckIds;
                case CosmeticKind.Background:
                    return data.boughtBackgroundIds;
                case CosmeticKind.Table:
                    return data.boughtTableIds;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cosmetic kind.");
            }
        }

        private static string EquippedIn(CosmeticsData data, CosmeticKind kind)
        {
            switch (kind)
            {
                case CosmeticKind.Deck:
                    return data.selectedDeckId;
                case CosmeticKind.Background:
                    return data.equippedBackgroundId;
                case CosmeticKind.Table:
                    return data.equippedTableId;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cosmetic kind.");
            }
        }

        private static void SetEquipped(CosmeticsData data, CosmeticKind kind, string id)
        {
            switch (kind)
            {
                case CosmeticKind.Deck:
                    data.selectedDeckId = id;
                    break;
                case CosmeticKind.Background:
                    data.equippedBackgroundId = id;
                    break;
                case CosmeticKind.Table:
                    data.equippedTableId = id;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cosmetic kind.");
            }
        }

        /// <summary>Brings a loaded file up to the current schema and replaces anything unusable with its default.</summary>
        private static void Repair(CosmeticsData data)
        {
            if (data.schemaVersion < FirstShopVersion)
            {
                // Version 1 knew only the deck, when every deck was free: it stays if it is still owned
                // (checked below with the rest), and the rest starts at the defaults.
                data.equippedBackgroundId = CosmeticCatalog.Backgrounds.Default;
                data.equippedTableId = CosmeticCatalog.Tables.Default;
                data.boughtDeckIds = new List<string>();
                data.boughtBackgroundIds = new List<string>();
                data.boughtTableIds = new List<string>();
            }

            data.schemaVersion = CosmeticsData.CurrentVersion;
            data.boughtDeckIds = Clean(data.boughtDeckIds);
            data.boughtBackgroundIds = Clean(data.boughtBackgroundIds);
            data.boughtTableIds = Clean(data.boughtTableIds);
            foreach (CosmeticKind kind in new[] { CosmeticKind.Deck, CosmeticKind.Background, CosmeticKind.Table })
            {
                if (!IsOwned(data, CosmeticCatalog.Find(kind, EquippedIn(data, kind))))
                {
                    SetEquipped(data, kind, CosmeticCatalog.DefaultOf(kind));
                }
            }
        }

        /// <summary>The purchases as read, without empty entries; ids this build doesn't know are kept for a later one.</summary>
        private static List<string> Clean(List<string> ids)
        {
            var clean = new List<string>();
            if (ids != null)
            {
                foreach (string id in ids)
                {
                    if (!string.IsNullOrEmpty(id) && !clean.Contains(id))
                    {
                        clean.Add(id);
                    }
                }
            }

            return clean;
        }
    }
}
