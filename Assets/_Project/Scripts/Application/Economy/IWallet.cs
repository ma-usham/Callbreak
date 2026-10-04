using System;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The player's gold coins (the owner, 4 Oct 2026: the shop's only currency; mockups 21-23). Every
    /// player starts with <see cref="StartingCoins"/>; coins are earned by watching ads
    /// (<see cref="IAdRewards"/>) and spent in the shop. No real-money purchases.
    /// </summary>
    public interface IWallet
    {
        event Action Changed;

        int Coins { get; }

        /// <summary>Takes <paramref name="amount"/> coins if there are enough; false (and nothing taken) if not.</summary>
        bool TrySpend(int amount);

        void Add(int amount);
    }

    public static class WalletRules
    {
        /// <summary>What a new player starts with (the owner, 4 Oct 2026; a placeholder to tune).</summary>
        public const int StartingCoins = 500;
    }
}
