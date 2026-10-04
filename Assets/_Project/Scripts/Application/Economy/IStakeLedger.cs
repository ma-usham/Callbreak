using System;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The coins of Stake Match (the owner, 5 Oct 2026; <see cref="StakeRules"/>), by match id, on this
    /// device: the stake is taken when a staked match begins here, and the match settles once, by what
    /// this device saw of it.
    /// <list type="bullet">
    /// <item>It ends here: this player's place is paid its prize (<see cref="PayPrize"/>). A shared place
    /// is paid as that place, so two tied 1st both win the 1st prize.</item>
    /// <item>The player leaves, or isn't back in time: the stake is lost and nothing is paid
    /// (<see cref="Forfeit"/>).</item>
    /// <item>It is called off through no fault of the player: the stake is given back
    /// (<see cref="CallOff"/>).</item>
    /// </list>
    /// A match whose start this device never saw has no stake taken, and settling it does nothing. Bots
    /// have no ledger: they pay no stake and win nothing.
    /// </summary>
    public interface IStakeLedger
    {
        /// <summary>Raised when a stake is taken or a match settles.</summary>
        event Action Changed;

        /// <summary>
        /// True when the wallet holds <paramref name="stake"/> coins now: what the Play Online screen
        /// asks before it starts a Stake Match's matchmaking. Quick Match (<see cref="StakeRules.None"/>)
        /// costs nothing; an amount that isn't a stake can't be played.
        /// </summary>
        bool CanAfford(int stake);

        /// <summary>
        /// The staked match <paramref name="matchId"/> has begun on this device: <paramref name="stake"/>
        /// is taken from the wallet, once per match id. False when nothing was taken: the match has a
        /// stake already, or the wallet can't cover it any more (coins spent since the screen asked).
        /// Then the match isn't staked: the player plays on and wins nothing.
        /// </summary>
        bool TakeStake(string matchId, int stake);

        /// <summary>
        /// The staked match ended on this device with this player in <paramref name="place"/> (1 to 4;
        /// a shared place counts as that place): its prize is paid, once. The coins paid; 0 when the
        /// match has no stake taken here or has settled already.
        /// </summary>
        int PayPrize(string matchId, int place);

        /// <summary>The player left the match, or wasn't back in time: the stake stays lost and nothing is paid for the match.</summary>
        void Forfeit(string matchId);

        /// <summary>
        /// The match was called off through no fault of the player (its host was lost and nobody could
        /// take the match over, so it isn't recorded either; ADR 032): the stake is given back, once.
        /// </summary>
        void CallOff(string matchId);

        /// <summary>What this device staked on the match and how it settled, for the results screen; false when it staked nothing on it.</summary>
        bool TryGetStake(string matchId, out StakedMatch match);
    }

    /// <summary>Where a staked match stands in the <see cref="IStakeLedger"/>.</summary>
    public enum StakeState
    {
        /// <summary>The stake is taken; the match hasn't settled on this device.</summary>
        Taken,

        /// <summary>The match ended here and this player's place was paid its prize.</summary>
        Paid,

        /// <summary>The player left, or wasn't back in time: the stake is lost.</summary>
        Forfeited,

        /// <summary>The match was called off through no fault of the player: the stake was given back.</summary>
        Refunded,
    }

    /// <summary>One staked match as the <see cref="IStakeLedger"/> has it.</summary>
    public readonly struct StakedMatch
    {
        public StakedMatch(string matchId, int stake, StakeState state, int place, int prize)
        {
            MatchId = matchId;
            Stake = stake;
            State = state;
            Place = place;
            Prize = prize;
        }

        public string MatchId { get; }

        /// <summary>The coins taken when the match began: 50 or 100.</summary>
        public int Stake { get; }

        public StakeState State { get; }

        /// <summary>The place the prize was paid for, 1 to 4; 0 unless <see cref="StakeState.Paid"/>.</summary>
        public int Place { get; }

        /// <summary>The coins paid at the end; 0 unless <see cref="StakeState.Paid"/>.</summary>
        public int Prize { get; }
    }
}
