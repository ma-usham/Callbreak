using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Profile;
using Callbreak.Application.Reviews;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// Google Play's review card (the owner, 3 Nov 2026: "lets implement the google plays rate review in
    /// our game"; ADR 080). The game asks the store for it only after a win, in this launch, from the
    /// player's third finished match on; once for that win; at most once every 30 days; and three
    /// times in all, remembered across launches. A lower place or a match left early since the win
    /// asks nothing. Whether the card then shows is Google Play's to decide.
    /// </summary>
    public sealed class ReviewPromptTests
    {
        private static readonly DateTime Start = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc);

        private InMemorySaveStore _saves;
        private StatsService _stats;
        private StandInStoreReview _store;
        private DateTime _now;
        private int _matches;
        private ReviewPrompt _prompt;
        private SynchronizationContext _context;

        [SetUp]
        public void SetUp()
        {
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _saves = new InMemorySaveStore();
            _stats = new StatsService(_saves);
            _store = new StandInStoreReview();
            _now = Start;
            _matches = 0;
            _prompt = NewPrompt();
        }

        [TearDown]
        public void TearDown()
        {
            _prompt.Dispose();
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        [Test]
        public void AWin_InTheThirdMatch_AsksTheStoreOnce()
        {
            Finish(2);
            Finish(3);
            Finish(1);

            Assert.That(_prompt.IsDue, Is.True);
            Assert.That(Ask(), Is.True);
            Assert.That(_store.Requests, Is.EqualTo(1));
        }

        [Test]
        public void AWin_BeforeTheThirdMatch_AsksNothing()
        {
            Finish(1);
            Finish(1);

            Assert.That(_prompt.IsDue, Is.False);
            Assert.That(Ask(), Is.False);
            Assert.That(_store.Requests, Is.Zero);
        }

        [Test]
        public void ALowerPlace_AfterWins_AsksNothing()
        {
            Finish(1);
            Finish(1);
            Finish(2);

            Assert.That(Ask(), Is.False);
            Assert.That(_store.Requests, Is.Zero);
        }

        [Test]
        public void AMatchLeftEarly_AfterAWin_AsksNothing()
        {
            Finish(2);
            Finish(2);
            Finish(1);
            Leave();

            Assert.That(Ask(), Is.False);
            Assert.That(_store.Requests, Is.Zero);
        }

        [Test]
        public void OneWin_AsksOnce()
        {
            Finish(2);
            Finish(2);
            Finish(1);

            Assert.That(Ask(), Is.True);
            Assert.That(_prompt.IsDue, Is.False);
            Assert.That(Ask(), Is.False);
            Assert.That(_store.Requests, Is.EqualTo(1));
        }

        [Test]
        public void WithinThirtyDaysOfAnAsk_AWinAsksNothing_AndAfterThemItDoes()
        {
            Finish(2);
            Finish(2);
            Finish(1);
            Assert.That(Ask(), Is.True);

            _now = Start + ReviewPrompt.MinInterval - TimeSpan.FromMinutes(1);
            Finish(1);
            Assert.That(Ask(), Is.False);

            _now = Start + ReviewPrompt.MinInterval;
            Finish(1);
            Assert.That(Ask(), Is.True);
            Assert.That(_store.Requests, Is.EqualTo(2));
        }

        [Test]
        public void AfterThreeAsks_ItNeverAsksAgain()
        {
            Finish(2);
            Finish(2);
            for (int ask = 0; ask < ReviewPrompt.MaxAsks; ask++)
            {
                _now = Start + TimeSpan.FromTicks(ReviewPrompt.MinInterval.Ticks * ask);
                Finish(1);
                Assert.That(Ask(), Is.True, "ask " + (ask + 1));
            }

            _now = Start + TimeSpan.FromDays(400);
            Finish(1);

            Assert.That(Ask(), Is.False);
            Assert.That(_store.Requests, Is.EqualTo(ReviewPrompt.MaxAsks));
        }

        [Test]
        public void TheAsks_AreRemembered_AcrossLaunches()
        {
            Finish(2);
            Finish(2);
            Finish(1);
            Assert.That(Ask(), Is.True);

            Relaunch();
            _now = Start + TimeSpan.FromDays(10);
            Finish(1);

            Assert.That(Ask(), Is.False);
            Assert.That(_saves.Peek<ReviewPromptData>(ReviewPrompt.SaveKey).asks, Is.EqualTo(1));
        }

        [Test]
        public void AWinFromAnEarlierLaunch_AsksNothing()
        {
            Finish(2);
            Finish(2);
            Finish(1);

            Relaunch();

            Assert.That(_prompt.IsDue, Is.False);
            Assert.That(Ask(), Is.False);
            Assert.That(_store.Requests, Is.Zero);
        }

        [Test]
        public void ACancelledAsk_AsksTheStoreNothing_AndRecordsNoAsk()
        {
            Finish(2);
            Finish(2);
            Finish(1);
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Task<bool> ask = _prompt.AskIfDueAsync(cancelled.Token);

                Assert.That(ask.IsCanceled, Is.True);
            }

            Assert.That(_store.Requests, Is.Zero);
            Assert.That(_prompt.IsDue, Is.True);
        }

        [Test]
        public void TheStandIn_CountsEachRequest_AndCompletesAtOnce()
        {
            Task first = _store.RequestAsync(CancellationToken.None);
            Task second = _store.RequestAsync(CancellationToken.None);

            Assert.That(first.IsCompleted && second.IsCompleted, Is.True);
            Assert.That(_store.Requests, Is.EqualTo(2));
        }

        private ReviewPrompt NewPrompt() => new ReviewPrompt(_saves, _stats, _store, () => _now);

        /// <summary>A new launch: the statistics and the prompt are read again from the saves.</summary>
        private void Relaunch()
        {
            _prompt.Dispose();
            _stats = new StatsService(_saves);
            _prompt = NewPrompt();
        }

        private void Finish(int rank)
        {
            _matches++;
            _stats.RecordMatch("match-" + _matches, rank, 100, abandoned: false);
        }

        private void Leave()
        {
            _matches++;
            _stats.RecordMatch("match-" + _matches, 4, 0, abandoned: true);
        }

        private bool Ask()
        {
            Task<bool> ask = _prompt.AskIfDueAsync(CancellationToken.None);
            Assert.That(ask.IsCompleted, Is.True);
            return ask.Result;
        }
    }
}
