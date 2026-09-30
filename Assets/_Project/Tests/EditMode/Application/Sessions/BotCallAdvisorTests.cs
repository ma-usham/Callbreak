using System;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Randomness;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Sessions
{
    /// <summary>GDD §11 screen 07: the beginner assist marks "the estimator's suggested call".</summary>
    public sealed class BotCallAdvisorTests
    {
        [Test]
        public void Suggest_AsksTheHardEstimator_WithThePlayersOwnView()
        {
            var bots = new Bots { Call = 4 };
            var advisor = new BotCallAdvisor(bots);
            PlayerView view = DealtView();

            int call = advisor.Suggest(view);

            Assert.AreEqual(4, call);
            Assert.AreEqual(BotLevel.Hard, bots.Asked, "the strongest estimate");
            Assert.AreSame(view, bots.Seen, "it sees what the player sees and nothing else");
        }

        [Test]
        public void Suggest_TheSameHandTwice_GivesTheSameCall()
        {
            var advisor = new BotCallAdvisor(new Bots { FromRandom = true });
            PlayerView view = DealtView();

            int first = advisor.Suggest(view);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(first, advisor.Suggest(view), "the dot does not move between two looks");
            }
        }

        [TestCase(0)]
        [TestCase(99)]
        [TestCase(-3)]
        public void Suggest_AnEstimateOutsideTheRules_IsBroughtIntoTheCallRange(int estimate)
        {
            var advisor = new BotCallAdvisor(new Bots { Call = estimate });
            PlayerView view = DealtView();

            int call = advisor.Suggest(view);

            Assert.GreaterOrEqual(call, view.Rules.MinCall);
            Assert.LessOrEqual(call, view.Rules.MaxCall);
        }

        [Test]
        public void NoFactoryOrNoView_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new BotCallAdvisor(null));
            Assert.Throws<ArgumentNullException>(() => new BotCallAdvisor(new Bots()).Suggest(null));
        }

        /// <summary>The local seat's view of a freshly dealt offline round.</summary>
        private static PlayerView DealtView()
        {
            var saves = new InMemorySaveStore();
            var session = new OfflineMatchSession(
                new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Normal),
                new OfflineMatchStore(saves),
                new Bots { Call = 2 },
                new ProfileService(saves, new NameFilter()),
                new CosmeticsService(saves),
                new SettingsService(saves),
                new StatsService(saves),
                BotTiming.Instant);
            session.Start();
            Assert.AreEqual(13, session.View.Hand.Count);
            return session.View;
        }

        private sealed class Bots : IBotFactory, IBotStrategy
        {
            public int Call = 2;
            public bool FromRandom;

            public BotLevel Asked { get; private set; } = BotLevel.Easy;

            public PlayerView Seen { get; private set; }

            public BotLevel Level => BotLevel.Hard;

            public IBotStrategy Create(BotLevel level)
            {
                Asked = level;
                return this;
            }

            public int ChooseCall(PlayerView view, IRandom rng)
            {
                Seen = view;
                return FromRandom ? 1 + rng.NextInt(8) : Call;
            }

            public Card ChooseCard(PlayerView view, IRandom rng) => view.LegalCards().Lowest();
        }
    }
}
