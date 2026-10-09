namespace Callbreak.Application.Economy
{
    /// <summary>
    /// Coins the player won where the wallet wasn't on screen, waiting to be shown flying into the
    /// main menu's wallet (the owner, 9 Oct 2026): a Stake Match prize, paid at the table. The coins
    /// themselves are in the wallet from the moment they are paid; this only remembers how many the
    /// player hasn't seen arrive. In memory only: after a restart the main menu simply shows the
    /// balance.
    /// </summary>
    public interface IUnseenCoins
    {
        /// <summary>Coins won and not yet shown arriving; 0 when none.</summary>
        int Amount { get; }

        /// <summary>Remembers <paramref name="coins"/> won where the wallet isn't shown; nothing for 0 or less.</summary>
        void Add(int coins);

        /// <summary>They have been shown arriving.</summary>
        void Clear();
    }

    /// <inheritdoc cref="IUnseenCoins"/>
    public sealed class UnseenCoins : IUnseenCoins
    {
        public int Amount { get; private set; }

        public void Add(int coins)
        {
            if (coins > 0)
            {
                Amount = Amount > int.MaxValue - coins ? int.MaxValue : Amount + coins;
            }
        }

        public void Clear() => Amount = 0;
    }
}
