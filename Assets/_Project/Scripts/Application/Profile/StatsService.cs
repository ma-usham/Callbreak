using System;
using System.Collections.Generic;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Profile
{
    /// <summary>
    /// <see cref="IStatsService"/> over the <c>stats</c> save file (GDD §10, §15). Each match id is
    /// recorded once: the last <see cref="RememberedMatches"/> ids are kept, so a repeated
    /// MatchEnded (reconnect, resync, app restart) changes nothing. Games played counts every
    /// recorded match; a win is rank 1, shared or not; the best score is the highest final total.
    /// A match left early counts as played and lost and can't set a best (GDD §8). What the GDD
    /// tracks for later is counted too: the rounds of finished matches, the calls met in them and
    /// the calls added up, and the current and best run of wins, which any other result ends.
    /// </summary>
    public sealed class StatsService : IStatsService
    {
        public const string SaveKey = "stats";
        public const int RememberedMatches = 50;

        private readonly ISaveStore _store;
        private readonly StatsData _data;
        private readonly bool _readOnly;

        public StatsService(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            StatsData loaded = VersionedSave.Load<StatsData>(store, SaveKey, StatsData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new StatsData();
            Repair(_data);
        }

        public event Action Changed;

        /// <summary>A snapshot; changing it changes nothing here.</summary>
        public PlayerStats Current => new PlayerStats
        {
            gamesPlayed = _data.gamesPlayed,
            wins = _data.wins,
            hasBest = _data.hasBest,
            bestTotalTenths = _data.bestTotalTenths,
            roundsPlayed = _data.roundsPlayed,
            roundsMet = _data.roundsMet,
            callsTotal = _data.callsTotal,
            winStreak = _data.winStreak,
            bestWinStreak = _data.bestWinStreak,
        };

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved.</summary>
        public bool IsReadOnly => _readOnly;

        public void RecordMatch(string matchId, int rank, int totalTenths, bool abandoned, int roundsPlayed = 0, int roundsMet = 0, int callsTotal = 0)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                throw new ArgumentException("A match id is required.", nameof(matchId));
            }

            if (rank < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "Ranks start at 1.");
            }

            if (roundsPlayed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roundsPlayed), roundsPlayed, "A match has no fewer than 0 rounds.");
            }

            if (roundsMet < 0 || roundsMet > roundsPlayed)
            {
                throw new ArgumentOutOfRangeException(nameof(roundsMet), roundsMet, $"Calls are met in 0 to {roundsPlayed} of {roundsPlayed} rounds.");
            }

            if (callsTotal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(callsTotal), callsTotal, "Calls add up to 0 or more.");
            }

            if (_data.recentMatchIds.Contains(matchId))
            {
                return;
            }

            bool won = !abandoned && rank == 1;
            _data.gamesPlayed = Increment(_data.gamesPlayed);
            if (!abandoned)
            {
                if (won)
                {
                    _data.wins = Increment(_data.wins);
                }

                if (!_data.hasBest || totalTenths > _data.bestTotalTenths)
                {
                    _data.hasBest = true;
                    _data.bestTotalTenths = totalTenths;
                }

                _data.roundsPlayed = Add(_data.roundsPlayed, roundsPlayed);
                _data.roundsMet = Add(_data.roundsMet, roundsMet);
                _data.callsTotal = Add(_data.callsTotal, callsTotal);
            }

            // A run of wins ends with anything else: a lower place, or a match left early.
            _data.winStreak = won ? Increment(_data.winStreak) : 0;
            _data.bestWinStreak = Math.Max(_data.bestWinStreak, _data.winStreak);

            _data.recentMatchIds.Add(matchId);
            Trim(_data.recentMatchIds);
            if (!_readOnly)
            {
                _store.Save(SaveKey, _data);
            }

            Changed?.Invoke();
        }

        private static int Increment(int value) => Add(value, 1);

        /// <summary>Counters stop at the largest number rather than wrap around.</summary>
        private static int Add(int value, int amount) => value > int.MaxValue - amount ? int.MaxValue : value + amount;

        private static void Trim(List<string> ids)
        {
            if (ids.Count > RememberedMatches)
            {
                ids.RemoveRange(0, ids.Count - RememberedMatches);
            }
        }

        /// <summary>Brings a loaded file up to the current schema and makes its counters agree with each other.</summary>
        private static void Repair(StatsData data)
        {
            data.schemaVersion = StatsData.CurrentVersion;
            data.gamesPlayed = Math.Max(0, data.gamesPlayed);
            data.wins = Math.Max(0, Math.Min(data.wins, data.gamesPlayed));
            if (!data.hasBest)
            {
                data.bestTotalTenths = 0;
            }

            data.roundsPlayed = Math.Max(0, data.roundsPlayed);
            data.roundsMet = Math.Max(0, Math.Min(data.roundsMet, data.roundsPlayed));
            data.callsTotal = Math.Max(0, data.callsTotal);
            data.winStreak = Math.Max(0, Math.Min(data.winStreak, data.wins));
            data.bestWinStreak = Math.Max(data.winStreak, Math.Min(data.bestWinStreak, data.wins));

            if (data.recentMatchIds == null)
            {
                data.recentMatchIds = new List<string>();
            }

            data.recentMatchIds.RemoveAll(string.IsNullOrEmpty);
            Trim(data.recentMatchIds);
        }
    }
}
