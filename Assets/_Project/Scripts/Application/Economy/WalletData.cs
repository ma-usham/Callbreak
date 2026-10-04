using System;

namespace Callbreak.Application.Economy
{
    /// <summary>wallet.json: the player's gold coins. Plain serializable data read and written by <see cref="Wallet"/>.</summary>
    [Serializable]
    public sealed class WalletData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;

        public int coins = WalletRules.StartingCoins;
    }
}
