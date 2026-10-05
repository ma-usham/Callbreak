using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Flow;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Commands;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The ads between matches (the owner, 5 Oct 2026) in the real Boot, Menu and Table scenes, with a
    /// scripted online table (<see cref="ScriptedOnlineTable"/>, or a scripted private room) and a
    /// scripted ad network under the game's own ads (<see cref="PreloadedInterstitialAds"/>,
    /// <see cref="BetweenMatchAds"/>): its ads load at once and stay up until the test closes them.
    /// Leaving a finished Quick Match's results for the main menu shows the ad first, and the menu
    /// opens only once it has gone; Play Again shows it before the search starts. Leaving a private
    /// room's results for the main menu leaves the room before the ad, and the menu finds no room to
    /// leave again. After the player's very first match nothing shows; buying No Ads lets the loaded ad
    /// go, and then nothing shows or loads. Saves go to a temporary folder.
    /// </summary>
    public sealed class BetweenMatchAdsFlowTests
    {
        private GameFixture _game;
        private ScriptedOnlineTable _table;
        private List<string> _events;
        private ScriptedAdNetwork _network;

        /// <summary>The game's ads between matches, over the scripted network.</summary>
        private PreloadedInterstitialAds Ads => (PreloadedInterstitialAds)_game.Services.Resolve<IInterstitialAds>();

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _table = new ScriptedOnlineTable();
            _events = new List<string>();
            _network = new ScriptedAdNetwork(_events);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator LeavingTheResultsForTheMainMenu_ShowsTheAdFirst_ThenTheMenu()
        {
            yield return Open(PlayedBefore);
            Assert.IsTrue(Ads.IsAvailable, "an ad is loaded before the match ends");
            yield return EndMatch();

            GameFixture.Press(_game.TableRouter, "MainMenuButton");
            yield return Frames(5);

            CollectionAssert.AreEqual(new[] { ScriptedAdNetwork.Shown }, _events, "the ad shows as the results are left");
            Assert.AreEqual(AppState.InMatch, _game.Flow.State, "the menu waits for the ad to go");
            Assert.AreEqual(ScreenId.MatchResults, _game.TableRouter.Current);

            _network.Showing.Close();
            yield return _game.UntilMenu();
            Assert.AreEqual(ScreenId.MainMenu, _game.MenuRouter.Current, "then the main menu");
            Assert.AreEqual(1, _network.Shows);
        }

        [UnityTest]
        public IEnumerator PlayAgainAfterAQuickMatch_ShowsTheAdBeforeTheSearch()
        {
            yield return Open(PlayedBefore);
            yield return EndMatch();

            GameFixture.Press(_game.TableRouter, "PlayAgainButton");
            yield return Frames(5);

            // The scripted table would be found again at once, so the test ends with the ad still up.
            Assert.AreEqual(1, _network.Shows, "the ad shows before the search");
            Assert.AreEqual(AppState.InMatch, _game.Flow.State, "the search waits for the ad to go");
            Assert.AreEqual(ScreenId.MatchResults, _game.TableRouter.Current);
        }

        [UnityTest]
        public IEnumerator LeavingAPrivateRoomsResultsForTheMainMenu_LeavesTheRoomBeforeTheAd()
        {
            var room = new ScriptedRoom(_events);
            yield return _game.Boot(seed: PlayedBefore, extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IRoomService>(room);
                RegisterAds(builder);
            });
            room.Deal(_game);
            yield return room.Play(_game);
            room.End();
            yield return UntilResults();

            GameFixture.Press(_game.TableRouter, "MainMenuButton");
            yield return Frames(5);

            CollectionAssert.AreEqual(new[] { ScriptedRoom.Left, ScriptedAdNetwork.Shown }, _events, "the friends see the player go before the ad shows");
            Assert.AreEqual(AppState.InMatch, _game.Flow.State, "the menu waits for the ad to go");

            _network.Showing.Close();
            yield return _game.UntilMenu();
            Assert.AreEqual(ScreenId.MainMenu, _game.MenuRouter.Current);
            Assert.AreEqual(1, room.Leaves, "the menu finds no room left to leave");
        }

        [UnityTest]
        public IEnumerator AfterThePlayersVeryFirstMatch_NoAdShows()
        {
            yield return Open(null);
            Assert.IsTrue(Ads.IsAvailable, "an ad is loaded all the same");
            yield return EndMatch();
            Assert.AreEqual(1, _game.Services.Resolve<IStatsService>().Current.gamesPlayed, "the player's first match");

            GameFixture.Press(_game.TableRouter, "MainMenuButton");
            yield return _game.UntilMenu();

            Assert.AreEqual(0, _network.Shows);
        }

        [UnityTest]
        public IEnumerator BuyingNoAds_LetsTheLoadedAdGo_AndNoneShowsOrLoads()
        {
            yield return Open(PlayedBefore);
            Assert.IsTrue(Ads.IsAvailable, "an ad is loaded");
            INoAds noAds = _game.Services.Resolve<INoAds>();
            Task<NoAdsPurchase> buying = noAds.BuyAsync(CancellationToken.None);
            yield return GameFixture.Until(() => buying.IsCompleted, "No Ads to be bought");
            Assert.IsTrue(noAds.Owned, "No Ads is owned");
            Assert.AreEqual(1, _network.Destroys, "buying it let the loaded ad go");
            yield return EndMatch();

            GameFixture.Press(_game.TableRouter, "MainMenuButton");
            yield return _game.UntilMenu();

            Assert.AreEqual(0, _network.Shows);
            Assert.AreEqual(1, _network.Loads, "and nothing more was requested");
        }

        /// <summary>One earlier match in the statistics, so the scripted one is not the player's first.</summary>
        private static void PlayedBefore(ISaveStore saves)
        {
            new StatsService(saves).RecordMatch("earlier-match", 2, 150, abandoned: false, roundsPlayed: 5);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        /// <summary>The game's own ads between matches over the scripted network, with the game's No Ads.</summary>
        private void RegisterAds(IContainerBuilder builder)
        {
            builder.Register<IInterstitialAds>(resolver => new PreloadedInterstitialAds(_network, resolver.Resolve<INoAds>(), () => DateTime.UtcNow), Lifetime.Singleton);
        }

        /// <summary>
        /// Boots with the scripted table and opens it through Play Online. The scripted ads are queued
        /// for every scope built meanwhile, after the scope's own registrations, so they win over the
        /// game's stand-in; only the project scope's are ever made.
        /// </summary>
        private IEnumerator Open(Action<ISaveStore> seed)
        {
            using (LifetimeScope.Enqueue(RegisterAds))
            {
                yield return _table.Open(_game, seed: seed);
            }
        }

        private IEnumerator EndMatch()
        {
            _table.End();
            yield return UntilResults();
        }

        private IEnumerator UntilResults()
        {
            yield return GameFixture.Until(() => _game.TableRouter != null && _game.TableRouter.Current == ScreenId.MatchResults, "the match results to open");
            yield return null;
        }

        /// <summary>An ad network whose ads load at once and stay up until the test closes them.</summary>
        private sealed class ScriptedAdNetwork : IInterstitialAdNetwork
        {
            public const string Shown = "an ad shown";

            private readonly List<string> _events;

            public ScriptedAdNetwork(List<string> events)
            {
                _events = events;
            }

            public int Shows { get; private set; }

            public int Loads { get; private set; }

            public int Destroys { get; private set; }

            /// <summary>The ad on screen; null when none is.</summary>
            public ScriptedAd Showing { get; private set; }

            public Task<bool> StartAsync() => Task.FromResult(true);

            public Task<IInterstitialAd> LoadAsync()
            {
                Loads++;
                return Task.FromResult<IInterstitialAd>(new ScriptedAd(this));
            }

            public sealed class ScriptedAd : IInterstitialAd
            {
                private readonly ScriptedAdNetwork _network;
                private Action _closed;

                public ScriptedAd(ScriptedAdNetwork network)
                {
                    _network = network;
                }

                public bool CanShow => _closed == null;

                public void Show(Action closed, Action<string> failed)
                {
                    _closed = closed;
                    _network.Shows++;
                    _network.Showing = this;
                    _network._events.Add(Shown);
                }

                /// <summary>The player closes the ad.</summary>
                public void Close()
                {
                    _network.Showing = null;
                    _closed();
                }

                public void Destroy()
                {
                    _network.Destroys++;
                }
            }
        }

        /// <summary>
        /// A private room this device is a guest in, as the room service has it once the test deals its
        /// match: a real <see cref="OnlineMatchSession"/> that the test feeds as the room's host would.
        /// Leaving it is noted in the order of events, and from then on the service has no room.
        /// </summary>
        private sealed class ScriptedRoom : IRoomService, IRoomSession
        {
            public const string Left = "left the room";

            private const string MatchId = "room-SCRIPT-1";
            private const Seat Host = Seat.South;
            private const int ClockMs = 15000;

            private readonly List<string> _events;
            private MatchEngine _engine;
            private PlayerView _view;
            private Seat _local;
            private uint _seq = 1;
            private bool _joined;

            public ScriptedRoom(List<string> events)
            {
                _events = events;
            }

            public event Action Changed
            {
                add { }
                remove { }
            }

            public event Action MatchStarting
            {
                add { }
                remove { }
            }

            public event Action<RoomError> Closed;

            public int Leaves { get; private set; }

            public int Sent { get; private set; }

            public OnlineMatchSession Session { get; private set; }

            public IRoomSession Current => _joined && Leaves == 0 ? this : null;

            public string Code => "SCRIPT";

            public RoomSettings Settings { get; } = new RoomSettings("Test room", 30);

            public IReadOnlyList<RoomSeat> Seats => Array.Empty<RoomSeat>();

            public bool IsHost => false;

            public int LocalSeatIndex => (int)_local;

            public IMatchSession Match => Session;

            public Task<RoomResult> CreateRoomAsync(RoomSettings settings, CancellationToken ct) => Task.FromResult(new RoomResult(null, RoomError.Unknown));

            public Task<RoomResult> JoinRoomAsync(string code, CancellationToken ct) => Task.FromResult(new RoomResult(null, RoomError.NotFound));

            public Task SetReadyAsync(bool ready, CancellationToken ct) => Task.CompletedTask;

            public Task<bool> StartAsync(bool fillWithBots, CancellationToken ct) => Task.FromResult(false);

            public Task KickAsync(int seatIndex, CancellationToken ct) => Task.CompletedTask;

            public Task LeaveAsync(CancellationToken ct)
            {
                if (Current != null)
                {
                    Leaves++;
                    _events.Add(Left);
                    Closed?.Invoke(RoomError.None);
                }

                return Task.CompletedTask;
            }

            public void Dispose()
            {
            }

            /// <summary>The host deals round 1, and this device is in the room with that match.</summary>
            public void Deal(GameFixture game)
            {
                var seats = new[]
                {
                    new SeatProfile("Hari", "1", CosmeticsService.ClassicCrimson, false),
                    new SeatProfile("Asha", "2", CosmeticsService.ClassicCrimson, false),
                    new SeatProfile("Bikash", "3", CosmeticsService.ClassicCrimson, false),
                    new SeatProfile("Suman", "4", CosmeticsService.ClassicCrimson, false),
                };
                _engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(7), new MatchSetup(MatchId, seats, Host));
                _engine.Start();
                _local = _engine.CurrentSeat;
                Assert.AreNotEqual(Host, _local);
                _view = _engine.ViewFor(_local);
                Session = new OnlineMatchSession(
                    MatchMode.PrivateRoom,
                    _view,
                    Status(ClockMs),
                    payload => Sent++,
                    game.Services.Resolve<IStatsService>(),
                    game.Settings);
                _joined = true;
            }

            /// <summary>The room's host starts the match: the table opens, and the session gets the whole state.</summary>
            public IEnumerator Play(GameFixture game)
            {
                _ = game.Flow.StartMatchAsync(new MatchLaunchRequest(MatchMode.PrivateRoom, BotDifficulty.Normal), CancellationToken.None);
                yield return game.UntilTable();
                yield return GameFixture.Until(() => Sent > 0, "the table to tell the host it is ready");
                Session.OnSync(new TableUpdate(_seq, null, _view, Status(ClockMs)));
                yield return null;
                Assert.AreEqual(SessionStatus.Running, Session.Status);
            }

            /// <summary>The host plays the match to its end and sends this player the finished table.</summary>
            public void End()
            {
                for (int moves = 0; _engine.Phase != Phase.MatchOver; moves++)
                {
                    Assert.Less(moves, 5000, "the scripted match to finish");
                    Step(_engine);
                }

                _view = _engine.ViewFor(_local);
                Session.OnSync(new TableUpdate(++_seq, null, _view, Status(TableStatus.NoClock)));
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

            /// <summary>Everyone connected; this player's clock as given (<see cref="TableStatus.NoClock"/> for none).</summary>
            private TableStatus Status(int clockMs)
            {
                return new TableStatus(_local, clockMs, clockMs == TableStatus.NoClock ? 0 : clockMs, TableStatus.NoClock, new SeatConnection[SeatExtensions.Count]);
            }
        }
    }
}
