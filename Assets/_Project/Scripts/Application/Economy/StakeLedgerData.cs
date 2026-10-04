using System;
using System.Collections.Generic;

namespace Callbreak.Application.Economy
{
    /// <summary>stakes.json: the Stake Matches this device staked on, oldest first. Plain serializable data read and written by <see cref="StakeLedger"/>.</summary>
    [Serializable]
    public sealed class StakeLedgerData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;

        /// <summary>The last <see cref="StakeLedger.RememberedMatches"/> staked matches, oldest first.</summary>
        public List<StakeLedgerEntry> matches = new List<StakeLedgerEntry>();
    }

    /// <summary>One staked match in stakes.json.</summary>
    [Serializable]
    public sealed class StakeLedgerEntry
    {
        public string matchId;

        /// <summary>The coins taken when the match began: one of <see cref="StakeRules.Stakes"/>.</summary>
        public int stake;

        public StakeState state;

        /// <summary>The place the prize was paid for, 1 to 4; 0 unless paid.</summary>
        public int place;

        /// <summary>The coins paid at the end; 0 unless paid.</summary>
        public int prize;
    }
}
