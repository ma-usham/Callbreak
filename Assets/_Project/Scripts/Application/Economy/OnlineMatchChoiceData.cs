using System;

namespace Callbreak.Application.Economy
{
    /// <summary>online-match.json: what the Play Online screen remembers between launches. Plain serializable data read and written by <see cref="OnlineMatchChoice"/>.</summary>
    [Serializable]
    public sealed class OnlineMatchChoiceData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;

        /// <summary>The stake the chips start on (<see cref="IOnlineMatchChoice.LastStake"/>): one of <see cref="StakeRules.Stakes"/>.</summary>
        public int lastStake = StakeRules.Stakes[0];
    }
}
