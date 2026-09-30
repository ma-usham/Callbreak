using System;
using System.Collections.Generic;
using System.Threading;
using Callbreak.Application.Online;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// A private room over the in-memory relay: one host with its own player, any number of guests,
    /// each with its own saved stats and settings, and a hand-driven clock.
    /// </summary>
    internal sealed class RoomFixture
    {
        public const string Code = "ABCDEF";

        private readonly List<Member> _members = new List<Member>();

        /// <param name="clocks">The table's clocks; null runs it as a private room on <paramref name="turnSeconds"/>.</param>
        /// <param name="seeds">The host's seed source; null always answers 42.</param>
        public RoomFixture(int turnSeconds = 0, BotTiming timing = null, TableClocks clocks = null, Func<ulong> seeds = null)
        {
            Network = new InMemoryNetwork();
            HostLink = Network.HostAsync(Code, CancellationToken.None).Result.Link;
            Host = new RoomHost(
                Code,
                new RoomSettings("Hari's Table", turnSeconds),
                HostLink,
                new TestBots(),
                new NameFilter(),
                new Pcg32Random(5),
                new[] { "classic-crimson", "himalayan-ivory", "kathmandu-gold" },
                seeds ?? (() => 42UL),
                timing ?? BotTiming.Instant,
                clocks: clocks);
            HostMember = Add(Host.Local, "player-host", "Hari", Host);
            Assert.AreEqual(RoomError.None, HostMember.Client.JoinAsync(CancellationToken.None).Result);
        }

        public InMemoryNetwork Network { get; }

        public IHostLink HostLink { get; }

        public RoomHost Host { get; }

        public Member HostMember { get; }

        public IReadOnlyList<Member> Members => _members;

        public double Now { get; private set; }

        /// <summary>
        /// Handlers may throw, as a broken presenter would: every exception out of a tick or a
        /// delivery is recorded in <see cref="Faults"/> and time goes on, as the app's ticker and
        /// the relay transport log it and carry on.
        /// </summary>
        public bool CatchFaults
        {
            get => Network.CollectFaults;
            set => Network.CollectFaults = value;
        }

        public List<Exception> Faults => Network.Faults;

        /// <summary>A guest joins and the network settles. Returns the member, seated or not.</summary>
        public Member Join(string playerId, string name, out RoomError error, ushort version = Protocol.Version)
        {
            IClientLink link = Network.JoinAsync(Code, CancellationToken.None).Result.Link;
            Member member = Add(link, playerId, name, null, version);
            var join = member.Client.JoinAsync(CancellationToken.None);
            Network.Pump();
            Assert.IsTrue(join.IsCompleted, "the join was answered");
            error = join.Result;
            return member;
        }

        public Member Join(string playerId, string name)
        {
            Member member = Join(playerId, name, out RoomError error);
            Assert.AreEqual(RoomError.None, error);
            return member;
        }

        /// <summary>A dropped member connects again with the same identity.</summary>
        public RoomError Rejoin(Member member)
        {
            IClientLink link = Network.JoinAsync(Code, CancellationToken.None).Result.Link;
            member.Link = link;
            var join = member.Client.RejoinAsync(link, CancellationToken.None);
            Network.Pump();
            Assert.IsTrue(join.IsCompleted, "the rejoin was answered");
            return join.Result;
        }

        /// <summary>Guests say ready, the host starts, every table loads and the first hand is dealt.</summary>
        public void StartMatch(bool fillWithBots = true)
        {
            foreach (Member m in _members)
            {
                if (m != HostMember)
                {
                    m.Client.SetReadyAsync(true, CancellationToken.None);
                }
            }

            Network.Pump();
            Assert.IsTrue(HostMember.Client.StartAsync(fillWithBots, CancellationToken.None).Result, "the host could start");
            Network.Pump();
            foreach (Member m in _members)
            {
                Assert.IsNotNull(m.Client.Match, m.Name + " was told the match is starting");
                m.Listen();
                m.Session.Start();
            }

            Network.Pump();
            Step();
        }

        /// <summary>Time passes: the host and every member tick, and the network settles.</summary>
        public void Step(double seconds = 0.25)
        {
            Now += seconds;
            Run(() => Host.Tick(Now));
            foreach (Member m in _members)
            {
                Run(() => m.Client.Tick(Now));
            }

            Network.Pump();
        }

        /// <summary>Runs <paramref name="action"/>; with <see cref="CatchFaults"/> set, what it throws is recorded instead.</summary>
        public void Run(Action action)
        {
            if (!CatchFaults)
            {
                action();
                return;
            }

            try
            {
                action();
            }
            catch (Exception e)
            {
                Faults.Add(e);
            }
        }

        /// <summary>Every present person plays like the test bots until the match is over.</summary>
        public void PlayToEnd(int maxSteps = 20000)
        {
            for (int step = 0; step < maxSteps && !Host.Table.IsOver; step++)
            {
                Step();
                foreach (Member m in _members)
                {
                    if (m.Link.IsConnected)
                    {
                        Act(m.Session);
                    }
                }

                Network.Pump();
            }
        }

        /// <summary>Plays the session's seat: call 2, lowest legal card, ready at every summary.</summary>
        public static bool Act(IMatchSession session)
        {
            if (session == null || session.Status != SessionStatus.Running)
            {
                return false;
            }

            PlayerView view = session.View;
            if (view.Phase == Phase.RoundSummary && !view.IsReady(session.LocalSeat))
            {
                session.ReadyNextAsync(CancellationToken.None);
                return true;
            }

            if (!view.IsMyTurn)
            {
                return false;
            }

            if (view.Phase == Phase.Calling)
            {
                session.PlaceCallAsync(2, CancellationToken.None);
            }
            else
            {
                session.PlayCardAsync(view.LegalCards().Lowest(), CancellationToken.None);
            }

            return true;
        }

        private Member Add(IClientLink link, string playerId, string name, RoomHost host, ushort version = Protocol.Version)
        {
            var saves = new InMemorySaveStore();
            var stats = new StatsService(saves);
            var settings = new SettingsService(saves);
            var hello = new HelloMessage(version, playerId, name, _members.Count, "classic-crimson");
            var client = new RoomClient(link, hello, host, MatchMode.PrivateRoom, stats, settings);
            var member = new Member(name, playerId, link, client, stats, settings);
            _members.Add(member);
            return member;
        }

        internal sealed class Member
        {
            public Member(string name, string identity, IClientLink link, RoomClient client, StatsService stats, SettingsService settings)
            {
                Name = name;
                Identity = identity;
                Link = link;
                Client = client;
                Stats = stats;
                Settings = settings;
            }

            public string Name { get; }

            /// <summary>What the member says it is in its hello (in the app, <see cref="RoomIdentity"/>).</summary>
            public string Identity { get; }

            public IClientLink Link { get; set; }

            public RoomClient Client { get; }

            public StatsService Stats { get; }

            public SettingsService Settings { get; }

            public OnlineMatchSession Session => (OnlineMatchSession)Client.Match;

            public List<MatchEvent> Events { get; } = new List<MatchEvent>();

            public int Resyncs { get; private set; }

            public List<int> Emotes { get; } = new List<int>();

            /// <summary>Records what the session tells the table from now on.</summary>
            public void Listen()
            {
                Session.EventReceived += Events.Add;
                Session.Resynced += _ => Resyncs++;
                Session.EmoteReceived += (seat, id) => Emotes.Add(((int)seat * 100) + id);
            }
        }
    }

    /// <summary>Test bots: call 2, play the lowest legal card.</summary>
    internal sealed class TestBots : IBotFactory, IBotStrategy
    {
        public BotLevel Level => BotLevel.Normal;

        public IBotStrategy Create(BotLevel level) => this;

        public int ChooseCall(PlayerView view, IRandom rng) => 2;

        public Card ChooseCard(PlayerView view, IRandom rng) => view.LegalCards().Lowest();
    }
}
