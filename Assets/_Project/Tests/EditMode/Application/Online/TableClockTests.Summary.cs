using Callbreak.Application.Online;
using Callbreak.Core.Match;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD §6 and §11 screen 09: the round results count down 10 s (Quick Match) or 15 s (a private
    /// room). A client shows the end of the round first (the last trick and the forced one after
    /// it), so the countdown starts when that playback is over, and players see all of it.
    /// </summary>
    public sealed partial class TableClockTests
    {
        private const double RoundEndPlayback = TableHost.RoundEndPlaybackSeconds;

        [TestCase(true, 10.0)]
        [TestCase(false, 15.0)]
        public void RoundResults_TheCountdownStartsOnceTheEndOfTheRoundHasBeenShown(bool quickMatch, double seconds)
        {
            RoomFixture room = quickMatch ? new RoomFixture(clocks: TableClocks.QuickMatch) : new RoomFixture(turnSeconds: 30);
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();

            PlayUntil(room, () => room.Host.Table.ViewFor(Seat.South).Phase == Phase.RoundSummary);
            double scoredAt = room.Now;
            int reported = room.Host.Table.StatusAt(room.Now).SummaryRemainingMs;

            Assert.AreEqual((RoundEndPlayback + seconds) * 1000.0, reported, StepSeconds * 1000.0 + 1.0, "the host counts the playback of the round's end first");
            WaitFor(room, () => guest.Session.Timer.IsRunning);
            Assert.That(room.Now - scoredAt, Is.InRange(RoundEndPlayback - StepSeconds, RoundEndPlayback + StepSeconds), "the countdown shows once the end has been played back");
            Assert.AreEqual(seconds, guest.Session.Timer.Duration, 0.001);
            Assert.That(guest.Session.Timer.Deadline - room.Now, Is.InRange(seconds - StepSeconds - 0.05, seconds + 0.001), "all of it");

            room.Step(guest.Session.Timer.Deadline - room.Now - 0.5);
            Assert.AreEqual(1, guest.Session.View.Round, "half a second before the end the results still show");
            room.Step(1.0);
            Assert.AreEqual(2, guest.Session.View.Round, "then the next round is dealt, for bots and people alike");
        }
    }
}
