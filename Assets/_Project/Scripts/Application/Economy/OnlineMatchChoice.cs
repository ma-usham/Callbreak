using System;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// <see cref="IOnlineMatchChoice"/> for the whole app. <see cref="Stake"/>, the match the next
    /// search looks for, lives in memory: Quick Match until the player plays a stake, and each launch
    /// starts on Quick Match. <see cref="LastStake"/>, the stake the Play Online chips start on, is
    /// kept in its own <c>online-match</c> save file, so the chips remember it after a Quick Match
    /// and after a restart (the owner, 5 Oct 2026). A new player, or a saved amount that is no longer
    /// a stake, starts on the smallest stake. A file from a newer app version is ignored and never
    /// overwritten: the chips then remember only for this session.
    /// </summary>
    public sealed class OnlineMatchChoice : IOnlineMatchChoice
    {
        public const string SaveKey = "online-match";

        private readonly ISaveStore _store;
        private readonly OnlineMatchChoiceData _data;
        private readonly bool _readOnly;

        public OnlineMatchChoice(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            OnlineMatchChoiceData loaded = VersionedSave.Load<OnlineMatchChoiceData>(store, SaveKey, OnlineMatchChoiceData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new OnlineMatchChoiceData();
            _data.schemaVersion = OnlineMatchChoiceData.CurrentVersion;
            if (!StakeRules.IsStake(_data.lastStake))
            {
                _data.lastStake = StakeRules.Stakes[0];
            }
        }

        public int Stake { get; private set; } = StakeRules.None;

        public int LastStake => _data.lastStake;

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved.</summary>
        public bool IsReadOnly => _readOnly;

        public void Choose(int stake)
        {
            if (stake != StakeRules.None && !StakeRules.IsStake(stake))
            {
                throw new ArgumentOutOfRangeException(nameof(stake), stake, "Quick Match (0) or one of the stakes.");
            }

            Stake = stake;
            if (stake != StakeRules.None)
            {
                Remember(stake);
            }
        }

        public void RememberStake(int stake)
        {
            if (!StakeRules.IsStake(stake))
            {
                throw new ArgumentOutOfRangeException(nameof(stake), stake, "One of the stakes.");
            }

            Remember(stake);
        }

        /// <summary>Saves the chips' stake when it changes: a tap on the chip already chosen writes nothing.</summary>
        private void Remember(int stake)
        {
            if (_data.lastStake == stake)
            {
                return;
            }

            _data.lastStake = stake;
            if (!_readOnly)
            {
                _store.Save(SaveKey, _data);
            }
        }
    }
}
