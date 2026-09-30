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
    /// A match left early counts as played and lost and can't set a best (GDD §8).
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
        };

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved.</summary>
        public bool IsReadOnly => _readOnly;

        public void RecordMatch(string matchId, int rank, int totalTenths, bool abandoned)
        {
            if (string.IsNullOrEmpty(matchId))
            {
                throw new ArgumentException("A match id is required.", nameof(matchId));
            }

            if (rank < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "Ranks start at 1.");
            }

            if (_data.recentMatchIds.Contains(matchId))
            {
                return;
            }

            _data.gamesPlayed = Increment(_data.gamesPlayed);
            if (!abandoned)
            {
                if (rank == 1)
                {
                    _data.wins = Increment(_data.wins);
                }

                if (!_data.hasBest || totalTenths > _data.bestTotalTenths)
                {
                    _data.hasBest = true;
                    _data.bestTotalTenths = totalTenths;
                }
            }

            _data.recentMatchIds.Add(matchId);
            Trim(_data.recentMatchIds);
            if (!_readOnly)
            {
                _store.Save(SaveKey, _data);
            }

            Changed?.Invoke();
        }

        private static int Increment(int value) => value == int.MaxValue ? value : value + 1;

        private static void Trim(List<string> ids)
        {
            if (ids.Count > RememberedMatches)
            {
                ids.RemoveRange(0, ids.Count - RememberedMatches);
            }
        }

        private static void Repair(StatsData data)
        {
            data.schemaVersion = StatsData.CurrentVersion;
            data.gamesPlayed = Math.Max(0, data.gamesPlayed);
            data.wins = Math.Max(0, Math.Min(data.wins, data.gamesPlayed));
            if (!data.hasBest)
            {
                data.bestTotalTenths = 0;
            }

            if (data.recentMatchIds == null)
            {
                data.recentMatchIds = new List<string>();
            }

            data.recentMatchIds.RemoveAll(string.IsNullOrEmpty);
            Trim(data.recentMatchIds);
        }
    }
}
