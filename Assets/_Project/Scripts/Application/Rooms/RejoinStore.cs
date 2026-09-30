using System;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Core.Match;

namespace Callbreak.Application.Rooms
{
    /// <summary>
    /// <see cref="IRejoinStore"/> over the save store, key "session" (GDD §29). The ten minutes are
    /// wall-clock time, because they have to be measured across an app kill. A ticket that is given
    /// up, runs out or is replaced by another match's settles its match as left in the player's
    /// stats (GDD §8: leaving always costs the match), once per match id like every result.
    /// </summary>
    public sealed class RejoinStore : IRejoinStore
    {
        public const string Key = "session";
        public const long LifetimeSeconds = 600;
        private const int SchemaVersion = 1;

        private readonly ISaveStore _store;
        private readonly Func<DateTime> _utcNow;
        private readonly IStatsService _stats;

        /// <param name="stats">Where a match not gone back to is recorded as left; null records nothing.</param>
        public RejoinStore(ISaveStore store, Func<DateTime> utcNow, IStatsService stats = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _stats = stats;
        }

        public bool TryGet(out string roomCode)
        {
            roomCode = null;
            if (!_store.TryLoad(Key, out RejoinTicket ticket) || ticket == null)
            {
                return false;
            }

            if (ticket.schemaVersion > SchemaVersion)
            {
                // A newer build's file: ignored, not deleted.
                return false;
            }

            if (ticket.schemaVersion != SchemaVersion || !RoomCode.IsValid(ticket.roomCode))
            {
                _store.Delete(Key);
                return false;
            }

            if (Now() - ticket.savedAtUnixSeconds > LifetimeSeconds)
            {
                // Ten minutes are far beyond the 90 s seat hold: the player did not come back in time.
                _store.Delete(Key);
                RecordLeft(ticket.matchId);
                return false;
            }

            roomCode = ticket.roomCode;
            return true;
        }

        public void Remember(string roomCode, string matchId)
        {
            if (!RoomCode.IsValid(roomCode))
            {
                throw new ArgumentException("Not a room code.", nameof(roomCode));
            }

            if (_store.TryLoad(Key, out RejoinTicket earlier) && earlier != null && earlier.schemaVersion <= SchemaVersion
                && !string.IsNullOrEmpty(earlier.matchId) && earlier.matchId != matchId)
            {
                // A match this device never went back to, now that it plays another.
                RecordLeft(earlier.matchId);
            }

            _store.Save(Key, new RejoinTicket { schemaVersion = SchemaVersion, roomCode = roomCode, matchId = matchId ?? string.Empty, savedAtUnixSeconds = Now() });
        }

        public void Clear()
        {
            if (_store.Exists(Key))
            {
                _store.Delete(Key);
            }
        }

        public void Abandon()
        {
            if (!_store.TryLoad(Key, out RejoinTicket ticket) || ticket == null)
            {
                return;
            }

            if (ticket.schemaVersion > SchemaVersion)
            {
                return;
            }

            _store.Delete(Key);
            RecordLeft(ticket.matchId);
        }

        private void RecordLeft(string matchId)
        {
            if (_stats != null && !string.IsNullOrEmpty(matchId))
            {
                _stats.RecordMatch(matchId, SeatExtensions.Count, 0, abandoned: true);
            }
        }

        private long Now() => new DateTimeOffset(_utcNow()).ToUnixTimeSeconds();
    }
}
