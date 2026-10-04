using System;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// <see cref="IOnlineMatchChoice"/> in memory, for the whole app: Quick Match until the player
    /// picks a stake on the Play Online screen, and that choice until it picks again. Nothing is
    /// saved: each launch starts on Quick Match.
    /// </summary>
    public sealed class OnlineMatchChoice : IOnlineMatchChoice
    {
        public int Stake { get; private set; } = StakeRules.None;

        public void Choose(int stake)
        {
            if (stake != StakeRules.None && !StakeRules.IsStake(stake))
            {
                throw new ArgumentOutOfRangeException(nameof(stake), stake, "Quick Match (0) or one of the stakes.");
            }

            Stake = stake;
        }
    }
}
