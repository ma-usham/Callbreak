using System;
using System.Collections.Generic;
using System.Threading;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Sessions
{
    /// <summary>
    /// ADR 056: a new offline match is dealt from the seed, of a few fair draws, that gives the
    /// player the strongest hands over the match (by the bots' CallEstimator), with more draws the
    /// harder the bots. The hands are worked out from the seed alone, so they must be exactly the
    /// hands the engine deals.
    /// </summary>
    public sealed class FriendlyDealTests
    {
        private static RuleSet Rules => RuleSet.NepaliStandard;

        [TestCase(1UL)]
        [TestCase(0x9E3779B97F4A7C15UL)]
        [TestCase(424242UL)]
        [TestCase(7UL)]
        public void PlayerHands_AreTheHandsTheEngineDealsEveryRound(ulong seed)
        {
            CardSet[] predicted = FriendlyDeal.PlayerHands(seed, Rules, Seat.South);
            List<CardSet> dealt = DealtToSouth(seed);

            Assert.AreEqual(Rules.Rounds, predicted.Length);
            Assert.AreEqual(Rules.Rounds, dealt.Count, "a whole match was played");
            for (int round = 0; round < Rules.Rounds; round++)
            {
                Assert.AreEqual(dealt[round], predicted[round], $"round {round + 1}: the hand the engine dealt South");
            }
        }

        [Test]
        public void ChooseSeed_KeepsTheDrawThatDealsThePlayerTheStrongestHands()
        {
            ulong[] draws = { 11UL, 22UL, 33UL, 44UL, 55UL, 66UL };
            int next = 0;
            ulong chosen = FriendlyDeal.ChooseSeed(() => draws[next++], draws.Length, Rules, Seat.South);

            Assert.AreEqual(draws.Length, next, "every candidate was drawn");
            int strongest = int.MinValue;
            foreach (ulong d in draws)
            {
                strongest = Math.Max(strongest, FriendlyDeal.Strength(d, Rules, Seat.South));
            }

            CollectionAssert.Contains(draws, chosen);
            Assert.AreEqual(strongest, FriendlyDeal.Strength(chosen, Rules, Seat.South));
        }

        [Test]
        public void ChooseSeed_FromOneDraw_IsThatFairDraw()
        {
            int drawn = 0;
            Assert.AreEqual(77UL, FriendlyDeal.ChooseSeed(() => { drawn++; return 77UL; }, 1, Rules, Seat.South));
            Assert.AreEqual(1, drawn);
        }

        [Test]
        public void TheHarderTheBots_TheMoreDrawsTheMatchChoosesFrom()
        {
            int easy = FriendlyDeal.Candidates(BotDifficulty.Easy);
            int normal = FriendlyDeal.Candidates(BotDifficulty.Normal);
            int hard = FriendlyDeal.Candidates(BotDifficulty.Hard);

            Assert.Greater(easy, 1, "even against Easy bots the player is helped");
            Assert.Greater(normal, easy);
            Assert.Greater(hard, normal);
        }

        [Test]
        public void ANewOfflineMatch_IsDealtFromTheFriendliestDraw_AndAResumedOneKeepsItsSeed()
        {
            var saves = new InMemorySaveStore();
            var store = new OfflineMatchStore(saves);
            var settings = new SettingsService(saves);
            var stats = new StatsService(saves);
            var profile = new ProfileService(saves, new NameFilter());
            var cosmetics = new CosmeticsService(saves);
            var draws = new Pcg32Random(2024);
            var drawn = new List<ulong>();
            Func<ulong> seeds = () =>
            {
                ulong s = ((ulong)draws.NextInt(int.MaxValue) << 31) ^ (ulong)draws.NextInt(int.MaxValue);
                drawn.Add(s);
                return s;
            };

            var session = new OfflineMatchSession(
                new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Normal),
                store, new FirstLegalBots(), profile, cosmetics, settings, stats, BotTiming.Instant, null, seeds);
            session.Start();

            Assert.AreEqual(FriendlyDeal.Candidates(BotDifficulty.Normal), drawn.Count, "Normal bots: the match chose from its draws");
            ulong best = FriendlyDeal.ChooseSeed(new Queue<ulong>(drawn).Dequeue, drawn.Count, Rules, Seat.South);
            Assert.AreEqual(FriendlyDeal.PlayerHands(best, Rules, Seat.South)[0], session.View.Hand, "the player holds the friendliest draw's first hand");

            // Leaving saves the match; resuming it deals on from the save and draws nothing new.
            session.LeaveAsync(CancellationToken.None).Wait();
            int before = drawn.Count;
            var resumed = new OfflineMatchSession(
                new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Normal, resumeSaved: true),
                store, new FirstLegalBots(), profile, cosmetics, settings, stats, BotTiming.Instant, null, seeds);
            Assert.IsTrue(resumed.IsResumed, "the match was saved and resumed");
            Assert.AreEqual(before, drawn.Count, "a resumed match keeps its seed");
            Assert.AreEqual(session.View.Hand, resumed.View.Hand);
        }

        /// <summary>Plays a whole match with bots on every seat and returns South's hand from each deal.</summary>
        private static List<CardSet> DealtToSouth(ulong seed)
        {
            var seats = new[]
            {
                new SeatProfile("Hari", "0", "classic-crimson", true),
                new SeatProfile("Suman", "1", "classic-crimson", true),
                new SeatProfile("Asha", "2", "classic-crimson", true),
                new SeatProfile("Bikash", "3", "classic-crimson", true),
            };
            var engine = new MatchEngine(Rules, new Pcg32Random(seed), new MatchSetup("friendly", seats));
            var bots = new FirstLegalBots();
            var hands = new List<CardSet>();
            var events = new List<MatchEvent>();
            engine.Start();
            for (int guard = 0; guard < 5000 && engine.Phase != Phase.Closed; guard++)
            {
                events.Clear();
                engine.DrainEvents(events);
                foreach (MatchEvent e in events)
                {
                    if (e is HandDealt dealt && dealt.Seat == Seat.South)
                    {
                        hands.Add(dealt.Hand);
                    }
                }

                if (engine.Phase == Phase.RoundSummary)
                {
                    for (int s = 0; s < 4; s++)
                    {
                        engine.Execute(new ReadyNext((Seat)s));
                    }
                }
                else if (!engine.CurrentTurn.IsNone)
                {
                    Seat seat = engine.CurrentSeat;
                    PlayerView view = engine.ViewFor(seat);
                    if (engine.Phase == Phase.Calling)
                    {
                        engine.Execute(new PlaceCall(seat, bots.ChooseCall(view, null), engine.CurrentTurn));
                    }
                    else
                    {
                        engine.Execute(new PlayCard(seat, bots.ChooseCard(view, null), engine.CurrentTurn));
                    }
                }
            }

            events.Clear();
            engine.DrainEvents(events);
            return hands;
        }

        private sealed class FirstLegalBots : IBotFactory, IBotStrategy
        {
            public BotLevel Level => BotLevel.Normal;

            public IBotStrategy Create(BotLevel level) => this;

            public int ChooseCall(PlayerView view, IRandom rng) => 2;

            public Card ChooseCard(PlayerView view, IRandom rng) => view.LegalCards().Lowest();
        }
    }
}
