using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Core.Commands;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// An online Quick Match table that a test plays the host of (ADR 029, 030): Play Online finds it
    /// at once, and the real Table scene plays a real <see cref="OnlineMatchSession"/> that the test
    /// feeds with the host's updates. The host (South) has dealt round 1, and this player calls first,
    /// with its turn clock running. The session settles Stake Match through the game's ledger: a stake
    /// a test takes for <see cref="MatchId"/> before the boot is paid out at the end (ADR 034).
    /// </summary>
    public sealed class ScriptedOnlineTable : IMatchmakingService
    {
        public const Seat Host = Seat.South;
        public const int ClockMs = 15000;

        /// <summary>The match the host deals: what a stake for this table is taken under.</summary>
        public const string MatchId = "quick-SCRIPT-1";

        private MatchEngine _engine;
        private uint _seq = 1;

        public IMatchSession Match => Session;

        public OnlineMatchSession Session { get; private set; }

        /// <summary>This player's view of the dealt round, or of the finished match after <see cref="End"/>.</summary>
        public PlayerView View { get; private set; }

        public Seat Local { get; private set; }

        /// <summary>How many messages the table has sent its host.</summary>
        public int Sent { get; private set; }

        public Task<MatchmakingResult> FindMatchAsync(IProgress<MatchmakingProgress> progress, CancellationToken ct)
        {
            return Task.FromResult(new MatchmakingResult(MatchmakingError.None));
        }

        /// <summary>
        /// Boots with this table to find, opens it through Play Online and gives the session the whole
        /// state the host sends a player that is ready.
        /// </summary>
        /// <param name="seed">Writes save files before the game starts (a stake already taken for <see cref="MatchId"/>, say).</param>
        /// <param name="choose">What the player does on Play Online before playing (taps a stake chip, say).</param>
        /// <param name="playButton">The play button tapped: Play Quick, or "PlayStakeButton" for the stake chosen.</param>
        public IEnumerator Open(GameFixture game, Action<ISaveStore> seed = null, Action<ScreenRouter> choose = null, string playButton = "PlayQuickButton")
        {
            yield return game.Boot(seed: seed, extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IMatchmakingService>(this);
            });

            Deal(game);
            GameFixture.Press(game.MenuRouter, "PlayOnlineButton");
            choose?.Invoke(game.MenuRouter);
            GameFixture.Press(game.MenuRouter, playButton);
            yield return game.UntilTable();
            yield return GameFixture.Until(() => Sent > 0, "the table to tell the host it is ready");
            Session.OnSync(new TableUpdate(_seq, null, View, Status(SeatConnection.Connected)));
            yield return null;
            Assert.AreEqual(SessionStatus.Running, Session.Status);
        }

        /// <summary>This player's call clock just started; the host's seat as given, everyone else here.</summary>
        public TableStatus Status(SeatConnection host)
        {
            var connections = new SeatConnection[SeatExtensions.Count];
            connections[(int)Host] = host;
            return new TableStatus(Local, ClockMs, ClockMs, TableStatus.NoClock, connections);
        }

        /// <summary>
        /// The host plays the match to its end (every seat calls the least it may and throws its
        /// lowest legal card) and sends this player the finished table, as it does a player who comes
        /// back after the end: the session records the match and settles its stake, and the table
        /// opens the match results.
        /// </summary>
        public void End()
        {
            for (int moves = 0; _engine.Phase != Phase.MatchOver; moves++)
            {
                Assert.Less(moves, 5000, "the scripted match to finish");
                Step(_engine);
            }

            View = _engine.ViewFor(Local);
            var connections = new SeatConnection[SeatExtensions.Count];
            Session.OnSync(new TableUpdate(++_seq, null, View, new TableStatus(Local, TableStatus.NoClock, 0, TableStatus.NoClock, connections)));
        }

        /// <summary>The place this player finished in (a shared place counts as that place).</summary>
        public int Place()
        {
            foreach (Standing standing in View.FinalStandings)
            {
                if (standing.Seat == Local)
                {
                    return standing.Rank;
                }
            }

            Assert.Fail("the finished match has no standing for " + Local);
            return 0;
        }

        private static void Step(MatchEngine engine)
        {
            CommandResult result;
            switch (engine.Phase)
            {
                case Phase.Calling:
                    result = engine.Execute(new PlaceCall(engine.CurrentSeat, engine.Rules.MinCall, engine.CurrentTurn));
                    break;

                case Phase.Playing:
                    PlayerView view = engine.ViewFor(engine.CurrentSeat);
                    result = engine.Execute(new PlayCard(view.Viewer, view.LegalCards().Lowest(), view.CurrentTurn));
                    break;

                case Phase.RoundSummary:
                    for (int s = 0; s < SeatExtensions.Count && engine.Phase == Phase.RoundSummary; s++)
                    {
                        if (!engine.ViewFor((Seat)s).IsReady((Seat)s))
                        {
                            Assert.IsTrue(engine.Execute(new ReadyNext((Seat)s)).IsAccepted, "seat " + s + " ready");
                        }
                    }

                    return;

                default:
                    Assert.Fail("the scripted match waits in " + engine.Phase);
                    return;
            }

            Assert.IsTrue(result.IsAccepted, engine.Phase + ": " + result);
        }

        private void Deal(GameFixture game)
        {
            var seats = new[]
            {
                new SeatProfile("Hari", "1", CosmeticsService.ClassicCrimson, false),
                new SeatProfile("Asha", "2", CosmeticsService.ClassicCrimson, false),
                new SeatProfile("Bikash", "3", CosmeticsService.ClassicCrimson, true),
                new SeatProfile("Suman", "4", CosmeticsService.ClassicCrimson, true),
            };
            _engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(7), new MatchSetup(MatchId, seats, Host));
            _engine.Start();

            // The first to call: the seat after the host, who deals.
            Local = _engine.CurrentSeat;
            Assert.AreNotEqual(Host, Local);
            View = _engine.ViewFor(Local);
            Session = new OnlineMatchSession(
                MatchMode.QuickMatch,
                View,
                Status(SeatConnection.Connected),
                payload => Sent++,
                game.Services.Resolve<IStatsService>(),
                game.Settings,
                stakes: game.Services.Resolve<IStakeLedger>());
        }
    }
}
