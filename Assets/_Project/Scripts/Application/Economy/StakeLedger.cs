using System;
using System.Collections.Generic;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;
using Callbreak.Core.Match;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// <see cref="IStakeLedger"/> over its own <c>stakes</c> save file, paying from and into the
    /// <see cref="IWallet"/>. Each match id is staked once and settles once: the last
    /// <see cref="RememberedMatches"/> matches are kept, so a repeated end (a resync, a reconnect, the
    /// app back at the same match after a restart) pays nothing more, and a match that began before a
    /// restart still pays when this device sees it end. The ledger is saved before the coins move, as
    /// with the ad rewards and the shop: an app killed in between can't take a stake twice or pay a
    /// prize twice. Prizes come from <see cref="StakeRules"/>. A file from a newer app version is
    /// ignored and never overwritten, and nothing is staked meanwhile: a stake this build couldn't keep
    /// could be paid again or never.
    /// </summary>
    public sealed class StakeLedger : IStakeLedger
    {
        public const string SaveKey = "stakes";

        /// <summary>How many staked matches the file keeps, oldest dropped first.</summary>
        public const int RememberedMatches = 50;

        private readonly ISaveStore _store;
        private readonly IWallet _wallet;
        private readonly StakeLedgerData _data;
        private readonly bool _readOnly;

        public StakeLedger(ISaveStore store, IWallet wallet)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            StakeLedgerData loaded = VersionedSave.Load<StakeLedgerData>(store, SaveKey, StakeLedgerData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new StakeLedgerData();
            Repair(_data);
        }

        public event Action Changed;

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved or staked.</summary>
        public bool IsReadOnly => _readOnly;

        public bool CanAfford(int stake)
        {
            if (stake == StakeRules.None)
            {
                return true;
            }

            return StakeRules.IsStake(stake) && _wallet.Coins >= stake;
        }

        /// <summary>
        /// Takes the stake of a match that began on this device. Throws for an empty match id and for an
        /// amount that isn't one of <see cref="StakeRules.Stakes"/>.
        /// </summary>
        public bool TakeStake(string matchId, int stake)
        {
            RequireMatchId(matchId);
            if (!StakeRules.IsStake(stake))
            {
                throw new ArgumentOutOfRangeException(nameof(stake), stake, "Not a stake.");
            }

            if (_readOnly || Find(matchId) != null || _wallet.Coins < stake)
            {
                return false;
            }

            // Kept before the coins are taken: the wallet tells its listeners as it changes, and a
            // match staked twice (or a stake taken with nothing to show for it) must not follow.
            var entry = new StakeLedgerEntry { matchId = matchId, stake = stake, state = StakeState.Taken };
            _data.matches.Add(entry);
            Trim(_data.matches);
            _store.Save(SaveKey, _data);
            if (!_wallet.TrySpend(stake))
            {
                _data.matches.Remove(entry);
                _store.Save(SaveKey, _data);
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Pays the prize of <paramref name="place"/>; throws for a place outside 1 to 4 or an empty match id.</summary>
        public int PayPrize(string matchId, int place)
        {
            RequireMatchId(matchId);
            if (place < 1 || place > SeatExtensions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(place), place, "A place is 1 to 4.");
            }

            StakeLedgerEntry entry = Find(matchId);
            if (entry == null || entry.state != StakeState.Taken)
            {
                return 0;
            }

            int prize = StakeRules.PrizeFor(entry.stake, place);
            entry.state = StakeState.Paid;
            entry.place = place;
            entry.prize = prize;

            // Settled before the coins move: the same end seen again pays nothing.
            Save();
            _wallet.Add(prize);
            Changed?.Invoke();
            return prize;
        }

        public void Forfeit(string matchId)
        {
            RequireMatchId(matchId);
            StakeLedgerEntry entry = Find(matchId);
            if (entry == null || entry.state != StakeState.Taken)
            {
                return;
            }

            entry.state = StakeState.Forfeited;
            Save();
            Changed?.Invoke();
        }

        public void CallOff(string matchId)
        {
            RequireMatchId(matchId);
            StakeLedgerEntry entry = Find(matchId);
            if (entry == null || entry.state != StakeState.Taken)
            {
                return;
            }

            entry.state = StakeState.Refunded;
            Save();
            _wallet.Add(entry.stake);
            Changed?.Invoke();
        }

        public bool TryGetStake(string matchId, out StakedMatch match)
        {
            StakeLedgerEntry entry = string.IsNullOrEmpty(matchId) ? null : Find(matchId);
            match = entry != null ? new StakedMatch(entry.matchId, entry.stake, entry.state, entry.place, entry.prize) : default;
            return entry != null;
        }

        private static void RequireMatchId(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                throw new ArgumentException("A match id is required.", nameof(matchId));
            }
        }

        private static void Trim(List<StakeLedgerEntry> matches)
        {
            if (matches.Count > RememberedMatches)
            {
                matches.RemoveRange(0, matches.Count - RememberedMatches);
            }
        }

        /// <summary>
        /// Brings a loaded file up to the current schema and drops what can't be a staked match: no
        /// id, an id seen before, an amount that isn't a stake, or a state this build doesn't know. A
        /// paid match keeps the prize it was paid; the others have none.
        /// </summary>
        private static void Repair(StakeLedgerData data)
        {
            data.schemaVersion = StakeLedgerData.CurrentVersion;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var kept = new List<StakeLedgerEntry>();
            if (data.matches != null)
            {
                foreach (StakeLedgerEntry entry in data.matches)
                {
                    bool known = entry != null && entry.state >= StakeState.Taken && entry.state <= StakeState.Refunded;
                    if (!known || string.IsNullOrEmpty(entry.matchId) || !StakeRules.IsStake(entry.stake) || !seen.Add(entry.matchId))
                    {
                        continue;
                    }

                    if (entry.state != StakeState.Paid)
                    {
                        entry.place = 0;
                        entry.prize = 0;
                    }

                    kept.Add(entry);
                }
            }

            data.matches = kept;
            Trim(data.matches);
        }

        private StakeLedgerEntry Find(string matchId)
        {
            for (int i = _data.matches.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_data.matches[i].matchId, matchId, StringComparison.Ordinal))
                {
                    return _data.matches[i];
                }
            }

            return null;
        }

        private void Save()
        {
            if (!_readOnly)
            {
                _store.Save(SaveKey, _data);
            }
        }
    }
}
