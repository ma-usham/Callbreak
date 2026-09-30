using System.Collections.Generic;
using Callbreak.Presentation.Table.Playback;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// The building blocks of the table's event animations: a beat, several animations together,
    /// several in order, and a callback step. They are driven by the event player's clock, so a
    /// skip (Finish) must leave every part finished and every callback run exactly once.
    /// </summary>
    public sealed class TimedAnimationsTests
    {
        /// <summary>Records its life so the order of calls can be checked.</summary>
        private sealed class Probe : IEventAnimation
        {
            private readonly List<string> _log;
            private readonly string _name;
            private float _elapsed;

            public Probe(string name, float duration, List<string> log)
            {
                _name = name;
                _log = log;
                Duration = duration;
            }

            public float Duration { get; }

            public bool Finished { get; private set; }

            public void Begin() => _log.Add(_name + " begin");

            public bool Tick(float deltaTime)
            {
                _elapsed += deltaTime;
                if (_elapsed >= Duration && !Finished)
                {
                    Finished = true;
                    _log.Add(_name + " done");
                }

                return Finished;
            }

            public void Finish()
            {
                if (!Finished)
                {
                    Finished = true;
                    _log.Add(_name + " finish");
                }
            }
        }

        [Test]
        public void ABeat_LastsItsSeconds()
        {
            var beat = new Beat(0.4f);
            Assert.AreEqual(0.4f, beat.Duration);
            beat.Begin();
            Assert.IsFalse(beat.Tick(0.2f));
            Assert.IsFalse(beat.Tick(0.19f));
            Assert.IsTrue(beat.Tick(0.02f));
        }

        [Test]
        public void ABeat_IsNeverNegative_AndASkipEndsIt()
        {
            Assert.AreEqual(0f, new Beat(-1f).Duration);
            Assert.IsTrue(new Beat(-1f).Tick(0f), "a beat of no length is over at once");

            var beat = new Beat(5f);
            beat.Begin();
            Assert.IsFalse(beat.Tick(0.1f));
            beat.Finish();
            Assert.IsTrue(beat.Tick(0f));
        }

        [Test]
        public void Together_LastsAsLongAsItsLongestPart()
        {
            var log = new List<string>();
            var quick = new Probe("quick", 0.1f, log);
            var slow = new Probe("slow", 0.3f, log);
            Together together = new Together().Add(quick).Add(null).Add(slow);
            Assert.AreEqual(2, together.Count, "a missing part is left out");
            Assert.AreEqual(0.3f, together.Duration);

            together.Begin();
            Assert.IsFalse(together.Tick(0.15f));
            Assert.IsTrue(quick.Finished);
            Assert.IsFalse(slow.Finished);
            Assert.IsTrue(together.Tick(0.15f));
            Assert.AreEqual(new[] { "quick begin", "slow begin", "quick done", "slow done" }, log);
        }

        [Test]
        public void Together_SkippedMidway_FinishesOnlyWhatIsStillRunning()
        {
            var log = new List<string>();
            var quick = new Probe("quick", 0.1f, log);
            var slow = new Probe("slow", 0.3f, log);
            Together together = new Together().Add(quick).Add(slow);
            together.Begin();
            together.Tick(0.15f);
            together.Finish();
            Assert.AreEqual(new[] { "quick begin", "slow begin", "quick done", "slow finish" }, log);
            Assert.IsTrue(together.Tick(0f));
        }

        [Test]
        public void AnEmptyTogether_IsOverAtOnce()
        {
            var together = new Together();
            Assert.AreEqual(0f, together.Duration);
            together.Begin();
            Assert.IsTrue(together.Tick(0f));
        }

        [Test]
        public void InOrder_RunsItsStepsOneAfterAnother()
        {
            var log = new List<string>();
            InOrder sequence = new InOrder()
                .Then(new Probe("hold", 0.9f, log))
                .Then(new Instant(() => log.Add("sound")))
                .Then(null)
                .Then(new Probe("sweep", 0.35f, log));
            Assert.AreEqual(1.25f, sequence.Duration, 1e-5f);

            sequence.Begin();
            Assert.IsFalse(sequence.Tick(0.5f));
            Assert.AreEqual(new[] { "hold begin" }, log, "the sweep has not begun while the trick is held");

            // The hold ends, the sound plays and the sweep begins in the same frame, on a clean clock.
            Assert.IsFalse(sequence.Tick(0.5f));
            Assert.AreEqual(new[] { "hold begin", "hold done", "sound", "sweep begin" }, log);
            Assert.IsFalse(sequence.Tick(0.3f));
            Assert.IsTrue(sequence.Tick(0.1f));
            Assert.AreEqual("sweep done", log[log.Count - 1]);
        }

        [Test]
        public void InOrder_Skipped_FinishesEveryStepAndRunsEveryCallbackOnce()
        {
            var log = new List<string>();
            int sounds = 0;
            InOrder sequence = new InOrder()
                .Then(new Probe("hold", 0.9f, log))
                .Then(new Instant(() => sounds++))
                .Then(new Probe("sweep", 0.35f, log));
            sequence.Begin();
            sequence.Tick(0.1f);
            sequence.Finish();
            Assert.AreEqual(new[] { "hold begin", "hold finish", "sweep finish" }, log);
            Assert.AreEqual(1, sounds);
            Assert.IsTrue(sequence.Tick(0f));
            Assert.AreEqual(1, sounds, "a skipped callback does not run again");
        }

        [Test]
        public void InOrder_TickedWithoutBegin_StartsItself()
        {
            var log = new List<string>();
            InOrder sequence = new InOrder().Then(new Probe("only", 0.1f, log));
            Assert.IsTrue(sequence.Tick(0.2f));
            Assert.AreEqual(new[] { "only begin", "only done" }, log);
        }

        [Test]
        public void AnInstant_RunsOnce_HoweverItIsReached()
        {
            int runs = 0;
            var begun = new Instant(() => runs++);
            begun.Begin();
            Assert.IsTrue(begun.Tick(0f));
            begun.Finish();
            Assert.AreEqual(1, runs);

            var skipped = new Instant(() => runs++);
            skipped.Finish();
            Assert.AreEqual(2, runs);
            Assert.AreEqual(0f, skipped.Duration);

            Assert.DoesNotThrow(() => new Instant(null).Begin());
        }
    }
}
