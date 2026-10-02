using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using NUnit.Framework;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// An online Quick Match table that a test plays the host of (ADR 029, 030): Play Online finds it
    /// at once, and the real Table scene plays a real <see cref="OnlineMatchSession"/> that the test
    /// feeds with the host's updates. The host (South) has dealt round 1, and this player calls first,
    /// with its turn clock running.
    /// </summary>
    public sealed class ScriptedOnlineTable : IMatchmakingService
    {
        public const Seat Host = Seat.South;
        public const int ClockMs = 15000;

        public IMatchSession Match => Session;

        public OnlineMatchSession Session { get; private set; }

        /// <summary>This player's view of the dealt round.</summary>
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
        public IEnumerator Open(GameFixture game)
        {
            yield return game.Boot(extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IMatchmakingService>(this);
            });

            Deal(game);
            GameFixture.Press(game.MenuRouter, "PlayOnlineButton");
            yield return game.UntilTable();
            yield return GameFixture.Until(() => Sent > 0, "the table to tell the host it is ready");
            Session.OnSync(new TableUpdate(1, null, View, Status(SeatConnection.Connected)));
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

        private void Deal(GameFixture game)
        {
            var seats = new[]
            {
                new SeatProfile("Hari", "1", CosmeticsService.ClassicCrimson, false),
                new SeatProfile("Asha", "2", CosmeticsService.ClassicCrimson, false),
                new SeatProfile("Bikash", "3", CosmeticsService.ClassicCrimson, true),
                new SeatProfile("Suman", "4", CosmeticsService.ClassicCrimson, true),
            };
            var engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(7), new MatchSetup("quick-SCRIPT-1", seats, Host));
            engine.Start();

            // The first to call: the seat after the host, who deals.
            Local = engine.CurrentSeat;
            Assert.AreNotEqual(Host, Local);
            View = engine.ViewFor(Local);
            Session = new OnlineMatchSession(
                MatchMode.QuickMatch,
                View,
                Status(SeatConnection.Connected),
                payload => Sent++,
                game.Services.Resolve<IStatsService>(),
                game.Settings);
        }
    }
}
