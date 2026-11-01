using System;
using System.Globalization;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Offers
{
    /// <summary>
    /// The wall clock festival offers go by (ADR 067). A phone's own time can be set to any date, which
    /// would bring an ended offer back, so once a server has told the time (an HTTP Date header) the
    /// clock is that time moved on by the game's monotonic <see cref="IClock"/>, whatever the device
    /// says. Until then, and when no server answers, it is the device's time. The caller asks again
    /// when the game comes back to the foreground, since the monotonic clock may not count the time
    /// the phone slept.
    /// </summary>
    public sealed class TrustedClock : IWallClock
    {
        private readonly IClock _monotonic;
        private readonly Func<DateTime> _deviceUtcNow;
        private DateTime _serverUtc;
        private double _syncedAt;

        /// <param name="deviceUtcNow">The device's wall clock; a time without a kind is taken as UTC.</param>
        public TrustedClock(IClock monotonic, Func<DateTime> deviceUtcNow)
        {
            _monotonic = monotonic ?? throw new ArgumentNullException(nameof(monotonic));
            _deviceUtcNow = deviceUtcNow ?? throw new ArgumentNullException(nameof(deviceUtcNow));
        }

        /// <summary>A server has told the time.</summary>
        public bool IsTrusted { get; private set; }

        public DateTime UtcNow => IsTrusted ? _serverUtc.AddSeconds(_monotonic.Now - _syncedAt) : Utc(_deviceUtcNow());

        /// <summary>The time a server gave, as of now on the monotonic clock.</summary>
        public void Sync(DateTime serverTime)
        {
            _serverUtc = Utc(serverTime);
            _syncedAt = _monotonic.Now;
            IsTrusted = true;
        }

        /// <summary>Syncs to an HTTP Date header ("Sun, 01 Nov 2026 06:30:00 GMT"); false, changing nothing, when it can't be read.</summary>
        public bool TrySyncFromHttpDate(string header)
        {
            if (string.IsNullOrWhiteSpace(header)
                || !DateTime.TryParseExact(header.Trim(), "r", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime server))
            {
                return false;
            }

            Sync(DateTime.SpecifyKind(server, DateTimeKind.Utc));
            return true;
        }

        private static DateTime Utc(DateTime time) =>
            time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : DateTime.SpecifyKind(time, DateTimeKind.Utc);
    }
}
