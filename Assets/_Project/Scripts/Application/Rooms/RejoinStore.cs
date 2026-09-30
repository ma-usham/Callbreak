using System;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Rooms
{
    /// <summary>
    /// <see cref="IRejoinStore"/> over the save store, key "session" (GDD §29). The ten minutes are
    /// wall-clock time, because they have to be measured across an app kill.
    /// </summary>
    public sealed class RejoinStore : IRejoinStore
    {
        public const string Key = "session";
        public const long LifetimeSeconds = 600;
        private const int SchemaVersion = 1;

        private readonly ISaveStore _store;
        private readonly Func<DateTime> _utcNow;

        public RejoinStore(ISaveStore store, Func<DateTime> utcNow)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
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

            bool expired = Now() - ticket.savedAtUnixSeconds > LifetimeSeconds;
            if (ticket.schemaVersion != SchemaVersion || !RoomCode.IsValid(ticket.roomCode) || expired)
            {
                _store.Delete(Key);
                return false;
            }

            roomCode = ticket.roomCode;
            return true;
        }

        public void Remember(string roomCode)
        {
            if (!RoomCode.IsValid(roomCode))
            {
                throw new ArgumentException("Not a room code.", nameof(roomCode));
            }

            _store.Save(Key, new RejoinTicket { schemaVersion = SchemaVersion, roomCode = roomCode, savedAtUnixSeconds = Now() });
        }

        public void Clear()
        {
            if (_store.Exists(Key))
            {
                _store.Delete(Key);
            }
        }

        private long Now() => new DateTimeOffset(_utcNow()).ToUnixTimeSeconds();
    }
}
