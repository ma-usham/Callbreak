using System;
using System.Collections.Generic;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// Stake Match (the owner, 5 Oct 2026; mockups 23 and 24): an online match where every person puts in
    /// a stake of 50 or 100 coins and wins a prize by final place. Quick Match has no stake.
    /// <list type="bullet">
    /// <item>Prizes by place (1st to 4th): stake 50 pays 100, 40, 30, 20; stake 100 pays 200, 80, 60, 40.</item>
    /// <item>Bots fill empty seats as in Quick Match; a bot pays no stake and wins nothing.</item>
    /// <item>Players who share a place after every tie-break (Core <c>Standings</c>) each get that place's
    /// prize: two tied 1st both win the 1st prize.</item>
    /// <item>A player who leaves, or isn't back in time after dropping, loses the stake and wins nothing.</item>
    /// </list>
    /// </summary>
    public static class StakeRules
    {
        /// <summary>The stake of a Quick Match: none.</summary>
        public const int None = 0;

        private static readonly int[] StakesInOrder = { 50, 100 };
        private static readonly int[] Prizes50 = { 100, 40, 30, 20 };
        private static readonly int[] Prizes100 = { 200, 80, 60, 40 };

        /// <summary>The stakes a player can choose, smallest first.</summary>
        public static IReadOnlyList<int> Stakes => StakesInOrder;

        public static bool IsStake(int stake) => stake == 50 || stake == 100;

        /// <summary>The prizes of a stake by place, 1st first.</summary>
        public static IReadOnlyList<int> PrizesFor(int stake)
        {
            switch (stake)
            {
                case 50:
                    return Prizes50;
                case 100:
                    return Prizes100;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stake), stake, "Not a stake.");
            }
        }

        /// <summary>The prize for finishing in <paramref name="place"/> (1 to 4; a shared place counts as that place).</summary>
        public static int PrizeFor(int stake, int place)
        {
            IReadOnlyList<int> prizes = PrizesFor(stake);
            if (place < 1 || place > prizes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(place), place, "A place is 1 to 4.");
            }

            return prizes[place - 1];
        }
    }

    /// <summary>
    /// The online match the player picked on the Play Online screen (mockups 23 and 24), read by
    /// matchmaking: Quick Match (<see cref="StakeRules.None"/>) or a Stake Match of one of
    /// <see cref="StakeRules.Stakes"/>. Players are only matched with players of the same stake.
    /// </summary>
    public interface IOnlineMatchChoice
    {
        int Stake { get; }

        /// <summary>Picks Quick Match (0) or a stake; throws for any other amount.</summary>
        void Choose(int stake);
    }
}
