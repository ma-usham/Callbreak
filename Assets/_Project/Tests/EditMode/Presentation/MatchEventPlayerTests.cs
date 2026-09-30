using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Presentation.Table;
using Callbreak.Presentation.Table.Playback;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// GDD §23 event playback: a FIFO queue, one animation at a time, double speed for a table
    /// that is more than three events behind (one move's own events are not a backlog), the pause
    /// before a card the engine played by itself, a tap skips anything longer than 0.3 s, a resync
    /// clears everything, and an event whose playback throws ends in a rebuild, not in a stall.
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

            public bool FailOnTick { get; set; }

            public void Begin() => Began = true;

            public bool Tick(float deltaTime)
            {
                if (FailOnTick)
                {
                    throw new InvalidOperationException("the animation failed");
                }

                Elapsed += deltaTime;
                return Elapsed >= Duration;
            }

            public void Finish() => Finished = true;
        }

        /// <summary>
        /// Gives every CardPlayed an animation of a set length, and every TrickCompleted one when
        /// <see cref="TrickSeconds"/> is set; everything else shows at once. It notes when each
        /// event began and ended on the test's clock, and can be told to fail.
        /// </summary>
        private sealed class FakePlayback : IEventPlayback
        {
            public readonly List<string> Log = new List<string>();
            public readonly List<FakeAnimation> Animations = new List<FakeAnimation>();
            public readonly Dictionary<MatchEvent, float> BeganAt = new Dictionary<MatchEvent, float>();
            public readonly Dictionary<MatchEvent, float> EndedAt = new Dictionary<MatchEvent, float>();
            public float CardSeconds = 0.26f;
            public float TrickSeconds;
            public float Now;
            public MatchEventPlayer Player;
            public MatchEvent EnqueueOnEnd;
            public MatchEvent FailOnBegin;
            public MatchEvent FailOnEnd;

            public IEventAnimation Begin(MatchEvent e)
            {
                Log.Add("begin " + Name(e));
                BeganAt[e] = Now;
                if (e == FailOnBegin)
                {
                    throw new InvalidOperationException("begin failed");
                }

                float seconds = e is CardPlayed ? CardSeconds : e is TrickCompleted ? TrickSeconds : 0f;
                if (seconds <= 0f)
                {
                    return null;
                }

                var animation = new FakeAnimation(seconds);
                Animations.Add(animation);
                return animation;
            }

            public void End(MatchEvent e)
            {
                Log.Add("end " + Name(e));
                EndedAt[e] = Now;
                if (e == FailOnEnd)
                {
                    throw new InvalidOperationException("end failed");
                }

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

        private static PlayTurnStarted Turn(Seat seat, int id) => new PlayTurnStarted(seat, new TurnId(id), null);

        /// <summary>
        /// What the real engine emits for the last card a seat chooses in a round, the fourth card
        /// of the twelfth trick: that card, the trick, the four cards of the last trick (which the
        /// engine plays itself), that trick, the round's score and the three bots' "ready".
        /// </summary>
        private static List<MatchEvent> LastMoveOfARound()
        {
            var engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(7UL), TestSetups.With(false, true, true, true));
            var driver = new MatchDriver(engine, randomPolicy: false);
            driver.Start();
            for (int guard = 0; guard < 200; guard++)
            {
                if (engine.Phase == Phase.Playing && engine.ViewFor(Seat.South).TrickIndex == 11 && engine.ViewFor(Seat.South).CurrentTrick.Count == 3)
                {
                    break;
                }

                Assert.IsTrue(driver.Step(), "the round ended before its twelfth trick");
            }

            int before = driver.Events.Count;
            driver.Step();
            return driver.Events.GetRange(before, driver.Events.Count - before);
        }

        /// <summary>Plays everything queued in small frames, keeping the playback's clock; returns the seconds it took.</summary>
        private static float PlayOut(MatchEventPlayer player, FakePlayback playback, Action eachFrame = null)
        {
            const float frame = 0.01f;
            float now = 0f;
            for (int i = 0; i < 5000 && !player.IsIdle; i++)
            {
                playback.Now = now;
                player.Tick(frame);
                eachFrame?.Invoke();
                now += frame;
            }

            Assert.IsTrue(player.IsIdle, "the queue did not play out");
            return now;
        }

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
        public void TheEndOfARound_ArrivesAllAtOnce_AndStillPlaysAtNormalSpeed()
        {
            List<MatchEvent> burst = LastMoveOfARound();
            Assert.AreEqual(11, burst.Count, "one command: card, trick, four forced cards, trick, score, three bots ready");
            Assert.IsInstanceOf<CardPlayed>(burst[0]);
            Assert.IsFalse(((CardPlayed)burst[0]).IsForced, "the fourth card of trick 12 was chosen");
            Assert.IsInstanceOf<TrickCompleted>(burst[1]);
            for (int i = 2; i <= 5; i++)
            {
                Assert.IsTrue(burst[i] is CardPlayed forced && forced.IsForced, "the last trick plays itself (GDD 6)");
            }

            Assert.IsInstanceOf<TrickCompleted>(burst[6]);
            Assert.IsInstanceOf<RoundScored>(burst[7]);

            // GDD 6 and 14: a card flies for 0.26 s; a finished trick stays 0.9 s and is swept in 0.35 s.
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            playback.CardSeconds = 0.26f;
            playback.TrickSeconds = 0.9f + 0.35f;
            foreach (MatchEvent e in burst)
            {
                player.Enqueue(e);
            }

            float slowest = float.MaxValue, fastest = 0f;
            float total = PlayOut(player, playback, () =>
            {
                slowest = Math.Min(slowest, player.Speed);
                fastest = Math.Max(fastest, player.Speed);
            });

            Assert.AreEqual(1f, fastest, "nothing of a round's own ending is a backlog: no double speed");
            Assert.AreEqual(1f, slowest);
            Assert.AreEqual(11, played.Count);

            const float frames = 0.035f;
            Assert.AreEqual(0.9f + 0.35f, playback.EndedAt[burst[1]] - playback.BeganAt[burst[1]], frames, "the twelfth trick is shown for its 0.9 s and swept in 0.35 s");
            Assert.AreEqual(0.9f + 0.35f, playback.EndedAt[burst[6]] - playback.BeganAt[burst[6]], frames, "so is the last trick");
            for (int i = 2; i <= 5; i++)
            {
                Assert.AreEqual(0.35f, playback.BeganAt[burst[i]] - playback.EndedAt[burst[i - 1]], frames, "a forced card waits the 0.35 s of a forced move (GDD 9), card " + (i - 1));
                Assert.AreEqual(0.26f, playback.EndedAt[burst[i]] - playback.BeganAt[burst[i]], frames, "and flies at normal speed");
            }

            Assert.AreEqual(playback.EndedAt[burst[6]], playback.BeganAt[burst[7]], 0.0001f, "the round is scored once the last trick has been swept");
            Assert.AreEqual(0.26f + 1.25f + (4 * (0.35f + 0.26f)) + 1.25f, total, 0.15f, "the whole ending takes its 5.2 s");
        }

        [Test]
        public void OneMovesOwnEvents_AreNotABacklog_HoweverManyTheyAre()
        {
            // The fourth card of a trick brings the trick and the next turn with it: nothing to catch up on.
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out _);
            playback.CardSeconds = 1f;
            player.Enqueue(Played("9♥"));
            for (int i = 0; i < 6; i++)
            {
                player.Enqueue(new ReadyConfirmed(Seat.North));
            }

            player.Tick(0.25f);
            Assert.AreEqual(6, player.Backlog);
            Assert.AreEqual(1f, player.Speed, "six events wait, all of them part of the move on screen");
            Assert.AreEqual(0.25f, playback.Animations[0].Elapsed, 1e-5f);
        }

        [Test]
        public void ALateTable_CatchesUpAtDoubleSpeed_UntilItIsThreeEventsBehind()
        {
            // Three moves arrived while the first was still to be shown (the app was in the background).
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out _);
            playback.CardSeconds = 1f;
            player.Enqueue(Played("9♥"));
            player.Enqueue(Turn(Seat.North, 2));
            player.Enqueue(Played("Q♥"));
            player.Enqueue(Turn(Seat.West, 3));
            player.Enqueue(Played("10♥"));
            player.Enqueue(Turn(Seat.South, 4));

            // 9♥ is on screen; Q♥, its turn, 10♥ and its turn are what the table is behind by.
            player.Tick(0.25f);
            Assert.AreEqual(2f, player.Speed);
            Assert.AreEqual(0.5f, playback.Animations[0].Elapsed, 1e-5f);

            // Q♥ on screen: only 10♥ and its turn are later moves. Normal speed again.
            player.Tick(0.25f);
            Assert.AreEqual(2, playback.Animations.Count);
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
        public void ATap_CutsShortThePauseBeforeAForcedCard()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            player.Enqueue(new CardPlayed(Seat.East, Card.Parse("9♥"), true, true));
            player.Tick(0.1f);
            Assert.IsNotNull(player.Current, "the forced card's turn has come");
            Assert.IsFalse(player.IsIdle);
            CollectionAssert.IsEmpty(playback.Log, "but it waits 0.35 s before it is played");

            Assert.IsTrue(player.Skip(), "0.35 s is longer than 0.3 s: a tap ends the wait");
            player.Tick(0f);
            CollectionAssert.AreEqual(new[] { "begin card 9♥" }, playback.Log);
            Assert.IsFalse(player.Skip(), "the flight itself is too short to skip");
            player.Tick(0.3f);
            CollectionAssert.AreEqual(new[] { "card 9♥" }, played);
        }

        [Test]
        public void AForcedCard_InALateTable_WaitsHalfAsLong()
        {
            // Double speed shortens the pause like everything else.
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out _);
            player.Enqueue(new CardPlayed(Seat.East, Card.Parse("9♥"), true, true));
            for (int i = 0; i < 4; i++)
            {
                player.Enqueue(Played((i + 2) + "♠"));
            }

            player.Tick(0.1f);
            Assert.AreEqual(2f, player.Speed);
            CollectionAssert.IsEmpty(playback.Log);
            player.Tick(0.1f);
            CollectionAssert.AreEqual(new[] { "begin card 9♥" }, playback.Log, "0.2 s at double speed is more than the 0.35 s");
            Assert.AreEqual(0f, playback.Animations[0].Elapsed, "and the card starts on a clean clock");
        }

        // ------------------------------------------------------------------ a playback that throws

        [Test]
        public void AnEventThatFailsToBegin_IsReported_AndTheQueueIsDropped_NotStalled()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            var failing = new ReadyConfirmed(Seat.North);
            playback.FailOnBegin = failing;
            var faults = new List<MatchEvent>();
            Exception reported = null;
            player.Faulted += (e, error) =>
            {
                faults.Add(e);
                reported = error;
            };
            player.Enqueue(Played("9♥"));
            player.Enqueue(failing);
            player.Enqueue(Played("Q♥"));

            Assert.DoesNotThrow(() => player.Tick(1f));
            CollectionAssert.AreEqual(new[] { "card 9♥" }, played, "what played before the failure stays played");
            Assert.AreEqual(1, faults.Count);
            Assert.AreSame(failing, faults[0], "the listener is told which event failed");
            Assert.IsInstanceOf<InvalidOperationException>(reported);
            Assert.IsTrue(player.IsIdle, "the failed event and everything behind it are dropped: the table is rebuilt from the session's view");
            Assert.IsNull(player.Current);
            Assert.AreEqual(0, player.Backlog);

            // Not stalled: later ticks do nothing, and the next event plays as usual.
            player.Tick(1f);
            player.Tick(1f);
            Assert.AreEqual(1, faults.Count, "the failure is reported once");
            player.Enqueue(Played("K♥"));
            player.Tick(1f);
            CollectionAssert.AreEqual(new[] { "card 9♥", "card K♥" }, played);
            Assert.IsTrue(player.IsIdle);
        }

        [Test]
        public void AnAnimationThatThrows_IsStopped_AndReported()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            var faults = new List<MatchEvent>();
            player.Faulted += (e, error) => faults.Add(e);
            playback.CardSeconds = 1f;
            CardPlayed card = Played("9♥");
            player.Enqueue(card);
            player.Enqueue(Played("Q♥"));
            player.Tick(0.1f);

            playback.Animations[0].FailOnTick = true;
            Assert.DoesNotThrow(() => player.Tick(0.1f));
            Assert.AreEqual(1, faults.Count);
            Assert.AreSame(card, faults[0]);
            Assert.IsTrue(playback.Animations[0].Finished, "what the animation was moving is settled");
            Assert.IsTrue(player.IsIdle);
            CollectionAssert.IsEmpty(played);
            Assert.AreEqual(1, playback.Animations.Count, "the waiting card never begins");
        }

        [Test]
        public void AnEventThatFailsToEnd_IsReported_WhetherItPlayedOutOrWasSkipped()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            var faults = new List<MatchEvent>();
            player.Faulted += (e, error) => faults.Add(e);
            playback.CardSeconds = 1f;
            CardPlayed first = Played("9♥"), second = Played("Q♥");
            playback.FailOnEnd = first;
            player.Enqueue(first);
            player.Enqueue(new ReadyConfirmed(Seat.North));
            Assert.DoesNotThrow(() => player.Tick(2f));
            Assert.AreEqual(1, faults.Count);
            Assert.AreSame(first, faults[0]);
            Assert.IsTrue(player.IsIdle);
            CollectionAssert.IsEmpty(played);

            playback.FailOnEnd = second;
            player.Enqueue(second);
            player.Tick(0.1f);
            Assert.DoesNotThrow(() => player.Skip());
            Assert.AreEqual(2, faults.Count);
            Assert.AreSame(second, faults[1]);
            Assert.IsTrue(player.IsIdle);
        }

        [Test]
        public void WithNobodyListening_TheFailureIsThrownOn_AndThePlayerStillDoesNotStall()
        {
            MatchEventPlayer player = NewPlayer(out FakePlayback playback, out List<string> played);
            var failing = new ReadyConfirmed(Seat.North);
            playback.FailOnBegin = failing;
            player.Enqueue(failing);
            player.Enqueue(Played("Q♥"));
            Assert.Throws<InvalidOperationException>(() => player.Tick(0f), "an error nobody handles is never swallowed");
            Assert.IsTrue(player.IsIdle);

            player.Enqueue(Played("K♥"));
            player.Tick(1f);
            CollectionAssert.AreEqual(new[] { "card K♥" }, played);
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
