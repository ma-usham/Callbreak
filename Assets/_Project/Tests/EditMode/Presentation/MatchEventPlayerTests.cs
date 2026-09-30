using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Presentation.Table;
using Callbreak.Presentation.Table.Playback;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// GDD §23 event playback: a FIFO queue, one animation at a time, double speed past three
    /// waiting events, a tap skips anything longer than 0.3 s, and a resync clears everything.
    /// </summary>
    public sealed class MatchEventPlayerTests
    {
        /// <summary>An animation that lasts as long as it is told and records what happens to it.</summary>
        private sealed class FakeAnimation : IEventAnimation
        {
            public FakeAnimation(float duration)
            {
                Duration = duration;
            }

            public float Duration { get; }

            public float Elapsed { get; private set; }

            public bool Began { get; private set; }

            public bool Finished { get; private set; }

            public void Begin() => Began = true;

            public bool Tick(float deltaTime)
            {
                Elapsed += deltaTime;
                return Elapsed >= Duration;
            }

            public void Finish() => Finished = true;
        }

        /// <summary>Gives every CardPlayed an animation of a set length; everything else shows at once.</summary>
        private sealed class FakePlayback : IEventPlayback
        {
            public readonly List<string> Log = new List<string>();
            public readonly List<FakeAnimation> Animations = new List<FakeAnimation>();
            public float CardSeconds = 0.26f;
            public MatchEventPlayer Player;
            public MatchEvent EnqueueOnEnd;

            public IEventAnimation Begin(MatchEvent e)
            {
                Log.Add("begin " + Name(e));
                if (!(e is CardPlayed))
                {
                    return null;
                }

                var animation = new FakeAnimation(CardSeconds);
                Animations.Add(animation);
                return animation;
            }

            public void End(MatchEvent e)
            {
                Log.Add("end " + Name(e));
                if (EnqueueOnEnd != null)
                {
                    MatchEvent next = EnqueueOnEnd;
                    EnqueueOnEnd = null;
                    Player.Enqueue(next);
                }
            }
        }

        private static string Name(MatchEvent e)
        {
            switch (e)
            {
                case CardPlayed played: return "card " + played.Card;
                case PlayTurnStarted turn: return "turn " + turn.Seat;
                case ReadyConfirmed ready: return "ready " + ready.Seat;
                default: return e.GetType().Name;
            }
        }

        private static CardPlayed Played(string card) => new CardPlayed(Seat.East, Card.Parse(card), false, false);

        private static MatchEventPlayer NewPlayer(out FakePlayback playback, out List<string> played)
        {
            var fake = new FakePlayback();
            var player = new MatchEventPlayer(fake);
            fake.Player = player;
            var done = new List<string>();
            player.Played += e => done.Add(Name(e));
            playback = fake;
            played = done;
            return player;
        }

        [Test]
        public void Events_PlayInOrder_OneAnimationAtATime()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            player.Enqueue(Played("9♥"));
            player.Enqueue(Played("Q♥"));
            Assert.IsFalse(player.IsIdle);

            player.Tick(0.1f);
            Assert.AreEqual(1, playback.Animations.Count, "the second card waits for the first");
            Assert.IsTrue(playback.Animations[0].Began);
            Assert.AreEqual(1, player.Backlog);
            CollectionAssert.IsEmpty(played);

            player.Tick(0.2f);
            CollectionAssert.AreEqual(new[] { "card 9♥" }, played);
            Assert.AreEqual(2, playback.Animations.Count, "the next event starts in the same frame");
            Assert.AreEqual(0f, playback.Animations[1].Elapsed, "but with a clean clock");

            player.Tick(0.3f);
            CollectionAssert.AreEqual(new[] { "card 9♥", "card Q♥" }, played);
            Assert.IsTrue(player.IsIdle);
            CollectionAssert.AreEqual(new[] { "begin card 9♥", "end card 9♥", "begin card Q♥", "end card Q♥" }, playback.Log);
        }

        [Test]
        public void EventsWithoutAnimation_PlayThroughInOneTick()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            player.Enqueue(new PlayTurnStarted(Seat.East, new TurnId(1), null));
            player.Enqueue(new ReadyConfirmed(Seat.North));
            player.Enqueue(Played("9♥"));
            player.Enqueue(new ReadyConfirmed(Seat.West));

            player.Tick(0f);
            CollectionAssert.AreEqual(new[] { "turn East", "ready North" }, played);
            Assert.IsNotNull(player.Current);
            Assert.AreEqual(1, player.Backlog);

            player.Tick(1f);
            CollectionAssert.AreEqual(new[] { "turn East", "ready North", "card 9♥", "ready West" }, played);
            Assert.IsTrue(player.IsIdle);
            Assert.IsNull(player.Current);
        }

        [Test]
        public void ABacklogOfMoreThanThree_PlaysAtDoubleSpeed()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out _);
            playback.CardSeconds = 1f;
            for (int i = 0; i < 5; i++)
            {
                player.Enqueue(Played((i + 2) + "♥"));
            }

            // One playing, four waiting: double speed.
            player.Tick(0.25f);
            Assert.AreEqual(4, player.Backlog);
            Assert.AreEqual(2f, player.Speed);
            Assert.AreEqual(0.5f, playback.Animations[0].Elapsed, 1e-5f);

            // Finishing it leaves three waiting: back to normal speed.
            player.Tick(0.25f);
            Assert.AreEqual(3, player.Backlog);
            Assert.AreEqual(1f, player.Speed);
            player.Tick(0.25f);
            Assert.AreEqual(0.25f, playback.Animations[1].Elapsed, 1e-5f);
        }

        [Test]
        public void ATap_SkipsOnlyAnimationsLongerThan300Milliseconds()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            playback.CardSeconds = 0.26f;
            player.Enqueue(Played("9♥"));
            player.Tick(0.01f);
            Assert.IsFalse(player.Skip(), "a card flight is too short to skip");
            Assert.IsFalse(playback.Animations[0].Finished);
            player.Tick(1f);

            playback.CardSeconds = 1.6f;
            player.Enqueue(Played("Q♥"));
            player.Tick(0.01f);
            Assert.IsTrue(player.Skip());
            Assert.IsTrue(playback.Animations[1].Finished);
            CollectionAssert.AreEqual(new[] { "card 9♥", "card Q♥" }, played, "a skipped event still counts as played");
            Assert.AreEqual("end card Q♥", playback.Log[playback.Log.Count - 1]);
            Assert.IsFalse(player.Skip(), "nothing is playing");
        }

        [Test]
        public void Clear_DropsTheQueue_AndReportsNothing()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            playback.CardSeconds = 1f;
            player.Enqueue(Played("9♥"));
            player.Enqueue(Played("Q♥"));
            player.Enqueue(new ReadyConfirmed(Seat.North));
            player.Tick(0.1f);

            player.Clear();
            Assert.IsTrue(player.IsIdle);
            Assert.IsTrue(playback.Animations[0].Finished, "the running animation is stopped");
            CollectionAssert.IsEmpty(played);
            CollectionAssert.AreEqual(new[] { "begin card 9♥" }, playback.Log, "no End: the table is rebuilt from the view instead");

            player.Tick(1f);
            CollectionAssert.IsEmpty(played);
        }

        [Test]
        public void Abandon_DropsEverything_WithoutTouchingTheAnimation()
        {
            // The scene is unloading: the views the animation would settle are already destroyed.
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            playback.CardSeconds = 1f;
            player.Enqueue(Played("9♥"));
            player.Enqueue(Played("Q♥"));
            player.Tick(0.1f);
            float elapsed = playback.Animations[0].Elapsed;

            player.Abandon();
            Assert.IsTrue(player.IsIdle);
            Assert.IsNull(player.Current);
            Assert.AreEqual(0, player.Backlog);
            Assert.IsFalse(playback.Animations[0].Finished, "nothing is finished: there is nothing left to finish it on");
            Assert.AreEqual(elapsed, playback.Animations[0].Elapsed, "and nothing is ticked");
            CollectionAssert.AreEqual(new[] { "begin card 9♥" }, playback.Log, "no End either");
            CollectionAssert.IsEmpty(played);

            player.Tick(5f);
            Assert.AreEqual(1, playback.Animations.Count, "the dropped events never begin");
            Assert.DoesNotThrow(() => player.Abandon(), "abandoning an idle player is harmless");
        }

        [Test]
        public void EventsEnqueuedWhilePlaying_JoinTheEndOfTheQueue()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            playback.EnqueueOnEnd = new ReadyConfirmed(Seat.South);
            player.Enqueue(new ReadyConfirmed(Seat.East));
            player.Enqueue(new ReadyConfirmed(Seat.North));
            player.Tick(0f);
            CollectionAssert.AreEqual(new[] { "ready East", "ready North", "ready South" }, played);
        }

        [Test]
        public void NegativeTime_IsTreatedAsZero_AndNullEventsAreRefused()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out _);
            player.Enqueue(Played("9♥"));
            player.Tick(-5f);
            Assert.AreEqual(0f, playback.Animations[0].Elapsed);
            Assert.Throws<System.ArgumentNullException>(() => player.Enqueue(null));
            Assert.Throws<System.ArgumentNullException>(() => new MatchEventPlayer(null));
        }

        // ------------------------------------------------------------------ the input lock and the emote limit

        [Test]
        public void PlayLock_BlocksASecondCard_UntilConfirmedOrTwoSecondsPass()
        {
            var playLock = new PlayLock();
            Card ace = Card.Parse("A♥");
            Assert.IsTrue(playLock.AcceptTap(10.0));
            playLock.Begin(ace, 10.0);
            Assert.IsTrue(playLock.IsLocked);
            Assert.AreEqual(ace, playLock.Pending);
            Assert.IsFalse(playLock.AcceptTap(10.5), "a double tap cannot send two cards");
            Assert.IsFalse(playLock.TryExpire(11.9));

            Assert.IsTrue(playLock.TryExpire(12.0), "no answer after 2 s: roll back");
            Assert.IsFalse(playLock.IsLocked);
            Assert.IsFalse(playLock.TryExpire(13.0), "only once");
            Assert.IsTrue(playLock.AcceptTap(12.5));

            playLock.Begin(ace, 20.0);
            playLock.Release();
            Assert.IsFalse(playLock.IsLocked);
            Assert.IsFalse(playLock.TryExpire(30.0), "a confirmed play never rolls back");
        }

        [Test]
        public void PlayLock_DebouncesTaps_150Milliseconds()
        {
            var playLock = new PlayLock();
            Assert.IsTrue(playLock.AcceptTap(1.00));
            Assert.IsFalse(playLock.AcceptTap(1.10));
            Assert.IsTrue(playLock.AcceptTap(1.16));
        }

        [Test]
        public void Emotes_OneEveryThreeSeconds_TenPerRound()
        {
            var limiter = new EmoteLimiter();
            Assert.IsTrue(limiter.TrySend(0.0));
            Assert.IsFalse(limiter.CanSend(2.9));
            Assert.IsFalse(limiter.TrySend(2.9));
            Assert.IsTrue(limiter.TrySend(3.0));
            double now = 3.0;
            for (int i = 2; i < EmoteLimiter.MaxPerRound; i++)
            {
                now += 3.0;
                Assert.IsTrue(limiter.TrySend(now), "emote " + (i + 1));
            }

            Assert.AreEqual(10, limiter.SentThisRound);
            Assert.IsFalse(limiter.TrySend(now + 100.0), "the eleventh waits for the next round");
            limiter.ResetRound();
            Assert.IsTrue(limiter.TrySend(now + 100.0));
        }
    }
}
