using System.Collections.Generic;
using System.Threading;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Application.Tutorial;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Tutorial
{
    /// <summary>ADR 084: the guided first hand of the first practice match, step by step.</summary>
    public sealed class MatchTutorialTests
    {
        private InMemorySaveStore _saves;
        private SettingsService _settings;
        private StatsService _stats;
        private OfflineMatchStore _store;
        private ProfileService _profile;
        private CosmeticsService _cosmetics;

        [SetUp]
        public void SetUp()
        {
            _saves = new InMemorySaveStore();
            _settings = new SettingsService(_saves);
            _stats = new StatsService(_saves);
            _store = new OfflineMatchStore(_saves);
            _profile = new ProfileService(_saves, new NameFilter());
            _cosmetics = new CosmeticsService(_saves);
        }

        private OfflineMatchSession NewSession(bool tutorial = true, bool resume = false, BotTiming timing = null)
        {
            return new OfflineMatchSession(
                new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Easy, resume, tutorial: tutorial),
                _store, new RealBots(), _profile, _cosmetics, _settings, _stats, timing ?? BotTiming.Instant);
        }

        /// <summary>The player as the tutorial asks: Next on every caption, the call and cards it points at, else the lowest legal card.</summary>
        private static void Follow(OfflineMatchSession session)
        {
            MatchTutorial tutorial = session.Tutorial;
            if (tutorial != null && tutorial.WaitsForNext)
            {
                tutorial.Next();
                return;
            }

            PlayerView view = session.View;
            if (view.Phase == Phase.RoundSummary && !view.IsReady(session.LocalSeat))
            {
                session.ReadyNextAsync(CancellationToken.None).Wait();
                return;
            }

            if (!view.IsMyTurn)
            {
                return;
            }

            if (view.Phase == Phase.Calling)
            {
                int target = tutorial?.TargetCall ?? 0;
                session.PlaceCallAsync(target > 0 ? target : 2, CancellationToken.None).Wait();
                return;
            }

            Card? card = tutorial?.TargetCard;
            session.PlayCardAsync(card ?? view.LegalCards().Lowest(), CancellationToken.None).Wait();
        }

        private static void Run(OfflineMatchSession session, ref double now, System.Func<bool> until, int maxSteps = 20000)
        {
            for (int step = 0; step < maxSteps && !until(); step++)
            {
                now += 0.25;
                session.Tick(now);
                if (until())
                {
                    return;
                }

                Follow(session);
            }
        }

        [Test]
        public void PlainMatch_HasNoTutorial()
        {
            Assert.IsNull(NewSession(tutorial: false).Tutorial);
        }

        [Test]
        public void TutorialMatch_DealsTheScriptedHand_AndThePlayerDeals()
        {
            OfflineMatchSession session = NewSession();

            session.Start();

            Assert.AreEqual(TutorialDeal.First.HandOf(Seat.South), session.View.Hand);
            Assert.AreEqual(Seat.South, session.View.Dealer);
            Assert.AreEqual(TutorialStep.Welcome, session.Tutorial.Step);
        }

        [Test]
        public void Steps_FollowTheFirstHand_OneAtATime_InOrder()
        {
            OfflineMatchSession session = NewSession();
            var steps = new List<TutorialStep>();
            session.Tutorial.Changed += () =>
            {
                if (session.Tutorial.Step != TutorialStep.None)
                {
                    steps.Add(session.Tutorial.Step);
                }
            };
            double now = 0;

            session.Start();
            Run(session, ref now, () => !session.Tutorial.IsActive);

            CollectionAssert.AreEqual(
                new[]
                {
                    TutorialStep.Welcome, TutorialStep.Call, TutorialStep.Follow, TutorialStep.TrickWon,
                    TutorialStep.Trump, TutorialStep.YouWon, TutorialStep.Score, TutorialStep.Ready,
                },
                steps);
            Assert.IsTrue(_settings.TutorialDone, "finishing it remembers it");
        }

        [Test]
        public void Steps_PointAtTheSuggestedCallAndTheScriptedCards()
        {
            OfflineMatchSession session = NewSession();
            var seen = new Dictionary<TutorialStep, string>();
            session.Tutorial.Changed += () =>
            {
                MatchTutorial t = session.Tutorial;
                switch (t.Step)
                {
                    case TutorialStep.Call:
                        seen[t.Step] = t.TargetCall.ToString();
                        break;
                    case TutorialStep.Follow:
                    case TutorialStep.Trump:
                        seen[t.Step] = t.TargetCard.ToString();
                        Assert.IsTrue(session.View.LegalCards().Contains(t.TargetCard.Value), "the card pointed at can be played");
                        break;
                    case TutorialStep.TrickWon:
                    case TutorialStep.YouWon:
                        seen[t.Step] = t.TrickWinner.ToString();
                        break;
                }
            };
            double now = 0;

            session.Start();
            Run(session, ref now, () => session.View.Phase == Phase.RoundSummary);

            Assert.AreEqual("3", seen[TutorialStep.Call]);
            Assert.AreEqual(Card.Parse("4♥").ToString(), seen[TutorialStep.Follow]);
            Assert.AreEqual("West", seen[TutorialStep.TrickWon]);
            Assert.AreEqual(Card.Parse("3♠").ToString(), seen[TutorialStep.Trump]);
            Assert.AreEqual("South", seen[TutorialStep.YouWon]);
        }

        [Test]
        public void Score_IsThePlayersLineOfTheFirstHand()
        {
            OfflineMatchSession session = NewSession();
            double now = 0;

            session.Start();
            Run(session, ref now, () => session.Tutorial.Step == TutorialStep.Score);

            MatchTutorial tutorial = session.Tutorial;
            Assert.AreEqual(Seat.South, tutorial.Result.Seat);
            Assert.AreEqual(3, tutorial.Result.Call);
            Assert.AreEqual(session.View.TricksWonBy(Seat.South), tutorial.Result.Won);
            Assert.AreEqual(session.View.History[0][Seat.South], tutorial.Result);
        }

        [Test]
        public void ACaptionThatWaitsForNext_HoldsTheBots()
        {
            OfflineMatchSession session = NewSession();
            double now = 0;
            session.Start();
            Assert.IsTrue(session.Tutorial.HoldsBots, "the welcome waits for Next");

            for (int i = 0; i < 200; i++)
            {
                session.Tick(now += 1.0);
            }

            Assert.IsFalse(session.View.HasCalled(Seat.East), "no bot calls while the caption is up");

            session.Tutorial.Next();
            session.Tick(now += 1.0);   // the bot's turn is scheduled
            session.Tick(now += 1.0);
            Assert.IsTrue(session.View.HasCalled(Seat.East), "Next lets the match go on");
        }

        [Test]
        public void ACaptionThatWaitsForNext_EndsWhenThePlayerPlays()
        {
            OfflineMatchSession session = NewSession();
            double now = 0;
            session.Start();
            Run(session, ref now, () => session.Tutorial.Step == TutorialStep.YouWon);
            Assert.IsTrue(session.View.IsMyTurn, "the player leads trick 3");

            session.PlayCardAsync(session.View.LegalCards().Lowest(), CancellationToken.None).Wait();

            Assert.AreEqual(TutorialStep.None, session.Tutorial.Step);
            Assert.IsFalse(session.Tutorial.HoldsBots, "the rest of the hand plays on");
        }

        [Test]
        public void Skip_EndsTheTutorial_RemembersIt_AndTheMatchPlaysToTheEnd()
        {
            OfflineMatchSession session = NewSession();
            double now = 0;
            int changes = 0;
            session.Start();
            session.Tutorial.Changed += () => changes++;

            session.Tutorial.Skip();
            session.Tutorial.Skip();

            Assert.IsFalse(session.Tutorial.IsActive);
            Assert.AreEqual(TutorialStep.None, session.Tutorial.Step);
            Assert.IsFalse(session.Tutorial.HoldsBots);
            Assert.IsTrue(_settings.TutorialDone);
            Assert.AreEqual(1, changes);

            Run(session, ref now, () => session.Status == SessionStatus.Ended);
            Assert.AreEqual(SessionStatus.Ended, session.Status);
            Assert.AreEqual(TutorialStep.None, session.Tutorial.Step, "no step comes back after a skip");
        }

        [Test]
        public void AfterTheFirstHand_TheMatchGoesOnAsAnEasyMatchOfFiveRounds()
        {
            OfflineMatchSession session = NewSession();
            var rounds = new List<int>();
            session.EventReceived += e =>
            {
                if (e is RoundScored scored)
                {
                    rounds.Add(scored.Round);
                }
            };
            double now = 0;

            session.Start();
            Run(session, ref now, () => session.Status == SessionStatus.Ended);

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, rounds);
            Assert.AreEqual(BotDifficulty.Easy, session.Difficulty);
            Assert.AreEqual(1, _settings.CompletedMatches);
        }

        [Test]
        public void LeftDuringTheFirstHand_ResumesAsAPlainMatch_DealingWhatAnUninterruptedMatchWould()
        {
            // Two tutorial matches from the same seed: one plays straight through, one is left
            // after the guided tricks and resumed. The save holds the match generator's state.
            ulong seed = 0x5eed;
            var straight = new OfflineMatchSession(
                new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Easy, tutorial: true),
                _store, new RealBots(), _profile, _cosmetics, _settings, _stats, BotTiming.Instant, seeds: () => seed);
            var straightRound2 = new List<CardSet>();
            straight.EventReceived += e =>
            {
                if (e is HandDealt dealt && straight.View.Round == 2)
                {
                    straightRound2.Add(dealt.Hand);
                }
            };
            double now = 0;
            straight.Start();
            Run(straight, ref now, () => straight.View.Round == 2 && straight.View.Phase == Phase.Calling);

            var first = new OfflineMatchSession(
                new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Easy, tutorial: true),
                _store, new RealBots(), _profile, _cosmetics, _settings, _stats, BotTiming.Instant, seeds: () => seed);
            now = 0;
            first.Start();
            Run(first, ref now, () => first.View.CompletedTricks.Count >= 2);
            first.LeaveAsync(CancellationToken.None).Wait();

            OfflineMatchSession second = NewSession(tutorial: false, resume: true);
            second.Start();
            Assert.IsTrue(second.IsResumed);
            Assert.IsNull(second.Tutorial, "a resumed match is a plain one");
            var resumedRound2 = new List<CardSet>();
            second.EventReceived += e =>
            {
                if (e is HandDealt dealt && second.View.Round == 2)
                {
                    resumedRound2.Add(dealt.Hand);
                }
            };
            Run(second, ref now, () => second.View.Round == 2 && second.View.Phase == Phase.Calling);

            CollectionAssert.AreEqual(straightRound2, resumedRound2, "round 2 deals the same hand either way");
        }

        private sealed class RealBots : IBotFactory
        {
            public IBotStrategy Create(BotLevel level) => BotStrategies.Create(level);
        }
    }
}
