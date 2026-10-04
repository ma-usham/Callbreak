using System;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// <see cref="IWallet"/> over its own <c>wallet</c> save file. A new player (no file) has
    /// <see cref="WalletRules.StartingCoins"/>. Amounts are positive (anything else throws); the
    /// balance never goes below 0 and stops at the largest int rather than wrap. Every change saves
    /// and raises <see cref="Changed"/>. A balance read from disk is repaired rather than trusted. A
    /// file from a newer app version is ignored and never overwritten: the coins then live in memory
    /// for this session.
    /// </summary>
    public sealed class Wallet : IWallet
    {
        public const string SaveKey = "wallet";

        private readonly ISaveStore _store;
        private readonly WalletData _data;
        private readonly bool _readOnly;

        public Wallet(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            WalletData loaded = VersionedSave.Load<WalletData>(store, SaveKey, WalletData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new WalletData();
            _data.schemaVersion = WalletData.CurrentVersion;
            _data.coins = Math.Max(0, _data.coins);
        }

        public event Action Changed;

        public int Coins => _data.coins;

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved.</summary>
        public bool IsReadOnly => _readOnly;

        public bool TrySpend(int amount)
        {
            RequirePositive(amount);
            if (_data.coins < amount)
            {
                return false;
            }

            _data.coins -= amount;
            Commit();
            return true;
        }

        public void Add(int amount)
        {
            RequirePositive(amount);
            _data.coins = _data.coins > int.MaxValue - amount ? int.MaxValue : _data.coins + amount;
            Commit();
        }

        private static void RequirePositive(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "An amount of coins is positive.");
            }
        }

        private void Commit()
        {
            if (!_readOnly)
            {
                _store.Save(SaveKey, _data);
            }

            Changed?.Invoke();
        }
    }
}
