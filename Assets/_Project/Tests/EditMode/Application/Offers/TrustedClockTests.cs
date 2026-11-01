using System;
using Callbreak.Application.Offers;
using Callbreak.Application.Ports;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The clock festival offers go by (ADR 067): the device's own time until a server has told the
    /// time, then the server's time moved on by the game's monotonic clock, so a phone set to another
    /// date can't bring an ended offer back while the server's time is known. The server's time comes
    /// from an HTTP Date header; one that can't be read changes nothing.
    /// </summary>
    public sealed class TrustedClockTests
    {
        private static readonly DateTime DeviceTime = new DateTime(2026, 12, 25, 9, 0, 0, DateTimeKind.Utc);

        private ManualClock _monotonic;
        private TrustedClock _clock;

        [SetUp]
        public void SetUp()
        {
            _monotonic = new ManualClock { Now = 100 };
            _clock = new TrustedClock(_monotonic, () => DeviceTime);
        }

        [Test]
        public void BeforeAServerHasSpoken_ItIsTheDevicesTime()
        {
            Assert.IsFalse(_clock.IsTrusted);
            Assert.AreEqual(DeviceTime, _clock.UtcNow);
            Assert.AreEqual(DateTimeKind.Utc, _clock.UtcNow.Kind);
        }

        [Test]
        public void AServersTime_MovesOnWithTheMonotonicClock_WhateverTheDeviceSays()
        {
            var server = new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc);

            _clock.Sync(server);
            _monotonic.Now += 90.5;

            Assert.IsTrue(_clock.IsTrusted);
            Assert.AreEqual(server.AddSeconds(90.5), _clock.UtcNow);
            Assert.AreEqual(DateTimeKind.Utc, _clock.UtcNow.Kind);
        }

        [Test]
        public void ALaterSync_TakesThePlaceOfTheFirst()
        {
            _clock.Sync(new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc));
            _monotonic.Now += 3600;
            var later = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);

            _clock.Sync(later);

            Assert.AreEqual(later, _clock.UtcNow);
        }

        [Test]
        public void ALocalTime_IsTurnedIntoUtc()
        {
            var local = new DateTime(2026, 11, 1, 12, 15, 0, DateTimeKind.Local);

            _clock.Sync(local);

            Assert.AreEqual(local.ToUniversalTime(), _clock.UtcNow);
        }

        [Test]
        public void AnHttpDateHeader_SetsTheTime()
        {
            Assert.IsTrue(_clock.TrySyncFromHttpDate("Sun, 01 Nov 2026 06:30:00 GMT"));

            Assert.IsTrue(_clock.IsTrusted);
            Assert.AreEqual(new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc), _clock.UtcNow);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("yesterday")]
        [TestCase("2026-11-01T06:30:00Z")]
        public void AnHttpDateHeaderThatCantBeRead_ChangesNothing(string header)
        {
            Assert.IsFalse(_clock.TrySyncFromHttpDate(header));

            Assert.IsFalse(_clock.IsTrusted);
            Assert.AreEqual(DeviceTime, _clock.UtcNow);
        }

        [Test]
        public void ADeviceTimeWithoutAKind_IsTakenAsUtc_AndALocalOneTurnedIntoUtc()
        {
            var unspecified = new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Unspecified);
            var local = new DateTime(2026, 11, 1, 12, 15, 0, DateTimeKind.Local);

            Assert.AreEqual(DateTime.SpecifyKind(unspecified, DateTimeKind.Utc), new TrustedClock(_monotonic, () => unspecified).UtcNow);
            Assert.AreEqual(local.ToUniversalTime(), new TrustedClock(_monotonic, () => local).UtcNow);
        }

        private sealed class ManualClock : IClock
        {
            public double Now { get; set; }
        }
    }
}
