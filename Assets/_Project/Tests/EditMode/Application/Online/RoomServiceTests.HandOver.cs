using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// The in-game host hand-over (ADR 030), as whole matches between devices over the in-memory
    /// peer-to-peer network: when the host goes quiet or its device is gone, the next person after it
    /// in seat order takes the table over and the match goes on from the same trick, exactly as it was;
    /// a host that comes back rejoins as a player; two players lost at once cost the round, dealt again;
    /// and nobody's cards reach anyone else but the new host, at the hand-over.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        [TestCase(1, 4)]
        [TestCase(5, 4)]
        [TestCase(1, 2)]
        [TestCase(5, 2)]
        public void HandOver_QuickMatch_HostLostMidTrick_TheNextInLineGoesOnFromTheSameTrick(int round, int people)
        {
            List<QuickPlayer> players = QuickTable(people);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, round), 20000);
            RoomHost table = host.Service.Host;
            MatchSnapshot truth = table.Table.Snapshot();
            QuickPlayer next = AtSeat(players, table.NextHost.NextSeat);
            Assert.AreEqual(NextPersonAfter(table.HostSeat, host.Match), table.NextHost.NextSeat, "the next person after the host in seat order, bots skipped");

            double lostAt = _now;
            GoQuiet(host, unplugged: true);
            SettleQuickUntil(() => next.Service.Host != null && AllRunning(players, except: host), 20);

            RoomHost taken = next.Service.Host;
            Assert.That(_now - lostAt, Is.LessThan(Protocol.HostLostSeconds + 2.0), "everyone plays on a few seconds after the host went quiet");
            Assert.AreEqual(1, taken.Generation, "the next generation of the table");
            Assert.AreEqual(Describe(truth), Describe(taken.Table.Snapshot()), "the same trick, every hand where it was");
            Assert.AreEqual(SeatConnection.Reconnecting, next.Match.Seats[table.HostSeat].Connection, "a bot covers the lost host's seat, held for it");
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, except: host);
        }

        [TestCase(4)]
        [TestCase(2)]
        public void HandOver_QuickMatch_HostPausedAndBack_LetsItsTableGoAndTakesItsSeatBackAtTheNewHost(int people)
        {
            List<QuickPlayer> players = QuickTable(people);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 2), 20000);
            RoomHost table = host.Service.Host;
            int hostSeat = table.HostSeat;
            MatchSnapshot truth = table.Table.Snapshot();
            QuickPlayer next = AtSeat(players, table.NextHost.NextSeat);

            Pause(host);
            SettleQuickUntil(() => next.Service.Host != null && AllRunning(players, except: host), 20);
            Assert.AreEqual(Describe(truth), Describe(next.Service.Host.Table.Snapshot()), "the same trick");
            PlayQuick(40);
            Assert.AreEqual(SeatConnection.Reconnecting, next.Match.Seats[hostSeat].Connection, "the old host's seat is held for it");
            Resume(host);
            SettleQuickUntil(() => host.Service.Host == null && host.Match.Status == SessionStatus.Running, 15);

            Assert.AreEqual(SeatConnection.Connected, next.Match.Seats[hostSeat].Connection, "back in its seat, as a player");
            Assert.AreEqual(hostSeat, (int)host.Match.LocalSeat);
            Assert.IsFalse(host.Service.Client.IsHost, "it hosts nothing any more");
            Assert.AreEqual(1, players.FindAll(p => p.Service.Host != null).Count, "one table: no split brain");
            Assert.AreEqual(next.Match.View.MatchId, host.Match.View.MatchId);
            PlayQuick(20000);
            AssertPlayedToTheEnd(players);
        }

        [Test]
        public void HandOver_QuickMatch_HostAndAnotherPlayerLostAtOnce_TheRoundIsDealtAgainAndTheScoresStay()
        {
            List<QuickPlayer> players = QuickTable(4);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 3), 20000);
            RoomHost table = host.Service.Host;
            MatchSnapshot before = table.Table.Snapshot();
            QuickPlayer next = AtSeat(players, table.NextHost.NextSeat);
            QuickPlayer other = players.Find(p => p != host && p != next);
            var told = new List<string>();
            foreach (QuickPlayer p in players)
            {
                QuickPlayer player = p;
                p.Match.EventReceived += e =>
                {
                    if (e is Redealt redealt && redealt.Reason == RedealReason.HostLost && redealt.Round == 3)
                    {
                        told.Add(player.Name);
                    }
                };
            }

            GoQuiet(host, unplugged: true);
            GoQuiet(other, unplugged: true);
            SettleQuickUntil(() => next.Service.Host != null && AllRunning(players, except: new[] { host, other }), 30);

            MatchSnapshot after = next.Service.Host.Table.Snapshot();
            Assert.AreEqual(3, after.round, "the same round");
            Assert.AreEqual((int)Phase.Calling, after.phase, "dealt again: calling from the start");
            Assert.AreEqual(before.dealer, after.dealer, "by the same dealer");
            Assert.That(after.completedTricks, Is.Empty);
            Assert.That(after.currentTrick.cards, Is.Empty);
            Assert.AreEqual(Describe(Rounds(before)), Describe(Rounds(after)), "the finished rounds keep their scores");
            var dealt = new HashSet<int>();
            foreach (SeatSnapshot seat in after.seats)
            {
                Assert.AreEqual(13, seat.hand.Length);
                Assert.AreEqual(-1, seat.call);
                dealt.UnionWith(seat.hand);
            }

            Assert.AreEqual(52, dealt.Count, "a whole new deal");
            CollectionAssert.AreEquivalent(players.FindAll(p => p != host && p != other).ConvertAll(p => p.Name), told, "every screen still here says so, once");
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, except: new[] { host, other });
        }

        /// <param name="firstHostBack">
        /// The first host came back as a player before the second host was lost; otherwise it never
        /// came back, and its seat passed to a bot for good when the seat hold ran out (GDD §8). Either
        /// way every hand but the lost host's is known again. (Lost while its seat is still held for it,
        /// it would be a second person whose cards nobody brought, and the round would be dealt again.)
        /// </param>
        [TestCase(true)]
        [TestCase(false)]
        public void HandOver_QuickMatch_TheNewHostIsLostToo_TheNextInLineTakesOverAgain(bool firstHostBack)
        {
            List<QuickPlayer> players = QuickTable(4);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 1), 20000);
            QuickPlayer next = AtSeat(players, host.Service.Host.NextHost.NextSeat);
            int firstHostSeat = host.Service.Host.HostSeat;
            GoQuiet(host, unplugged: !firstHostBack);
            SettleQuickUntil(() => next.Service.Host != null && AllRunning(players, except: host), 20);

            if (firstHostBack)
            {
                PlayQuick(20);
                Resume(host);
                SettleQuickUntil(() => host.Service.Host == null && host.Match.Status == SessionStatus.Running, 15);
                PlayQuickUntil(() => next.Service.Host.Table.Round >= 2 && MidTrick(next, next.Service.Host.Table.Round), 20000);
            }
            else
            {
                // Nobody plays for a while (the clocks and the bots do), so the seat hold runs out mid-match.
                SettleQuick(Protocol.SeatHoldSeconds + 1);
                PlayQuickUntil(() => MidTrick(next, next.Service.Host.Table.Round), 20000);
                Assert.AreEqual(SeatConnection.BotControlled, next.Match.Seats[firstHostSeat].Connection, "the first host's seat is a bot's for good");
            }

            RoomHost second = next.Service.Host;
            MatchSnapshot truth = second.Table.Snapshot();
            QuickPlayer third = AtSeat(players, second.NextHost.NextSeat);
            Assert.AreEqual(NextPersonAfter(second.HostSeat, next.Match, firstHostBack ? -1 : firstHostSeat), second.NextHost.NextSeat, "the next person after the new host, bots and the gone skipped");
            GoQuiet(next, unplugged: true);
            QuickPlayer[] gone = firstHostBack ? new[] { next } : new[] { host, next };
            SettleQuickUntil(() => third.Service.Host != null && AllRunning(players, except: gone), 20);

            Assert.AreEqual(2, third.Service.Host.Generation, "a second hand-over");
            Assert.AreEqual(Describe(truth), Describe(third.Service.Host.Table.Snapshot()), "the same trick again");
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, except: gone);
        }

        [Test]
        public void HandOver_QuickMatch_TheNextInLineLeaves_TheHostNamesTheNextOne_WhoTakesOverFromTheSameTrick()
        {
            List<QuickPlayer> players = QuickTable(4);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 2), 20000);
            RoomHost table = host.Service.Host;
            QuickPlayer leaving = AtSeat(players, table.NextHost.NextSeat);

            leaving.Match.LeaveAsync(CancellationToken.None);
            leaving.Stopped = true;
            PlayQuick(6);
            QuickPlayer next = AtSeat(players, table.NextHost.NextSeat);
            Assert.AreEqual(NextPersonAfter(table.HostSeat, host.Match, (int)leaving.Match.LocalSeat), table.NextHost.NextSeat, "the next person after the one who left");
            Assert.AreEqual(Phase.Playing, host.Match.View.Phase, "still the same round");
            MatchSnapshot truth = table.Table.Snapshot();
            GoQuiet(host, unplugged: true);
            SettleQuickUntil(() => next.Service.Host != null && AllRunning(players, except: new[] { host, leaving }), 20);

            Assert.AreEqual(Describe(truth), Describe(next.Service.Host.Table.Snapshot()), "the seat that was left is a bot's, whose hand the new host was sent");
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, except: new[] { host, leaving });
        }

        /// <param name="cutSeconds">
        /// How long the next host in line cannot reach the host: less than its wait for the others'
        /// reports (it gives the take-over up before it rebuilds anything), or more (it has rebuilt a
        /// table nobody came to, and gives that up).
        /// </param>
        [TestCase(6.0)]
        [TestCase(16.0)]
        public void HandOver_QuickMatch_TheNextInLineIsCutOffNotTheHost_ItGivesTheTakeOverUpAndPlaysOnAtTheHost(double cutSeconds)
        {
            List<QuickPlayer> players = QuickTable(4);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 2), 20000);
            QuickPlayer next = AtSeat(players, host.Service.Host.NextHost.NextSeat);
            var others = players.FindAll(p => p != host && p != next);

            _peers.Cut(host.Network, next.Network);
            for (double until = _now + cutSeconds; _now < until;)
            {
                PlayQuick(1);
                Assert.IsNotNull(host.Service.Host, "the host hosts on");
                foreach (QuickPlayer p in others)
                {
                    Assert.IsNull(p.Service.Host, "the others stay at the host");
                    Assert.AreEqual(SessionStatus.Running, p.Match.Status, "and play on there");
                }
            }

            _peers.Mend(host.Network, next.Network);
            SettleQuickUntil(() => next.Service.Host == null && next.Match.Status == SessionStatus.Running
                && host.Match.Seats[(int)next.Match.LocalSeat].Connection == SeatConnection.Connected, 20);

            Assert.AreEqual(1, players.FindAll(p => p.Service.Host != null).Count, "one table: no split brain");
            Assert.IsNotNull(host.Service.Host, "the host's");
            Assert.AreEqual(0, host.Service.Host.Generation);
            PlayQuick(20000);
            AssertPlayedToTheEnd(players);
        }

        /// <summary>
        /// Every message of a whole match with a hand-over, put back together from the packets and
        /// searched for every hand any seat held (as the 8-byte card set the wire carries): a person's
        /// cards go only to that person, and from that person to the new host in its report; a bot's
        /// only in the package the host sends the next host in line, and only to the player it names
        /// (ADR 030).
        /// </summary>
        [Test]
        public void HandOver_PayloadScan_APersonsCardsReachOnlyItsOwnerAndTheNewHost_ABotsOnlyTheNextInLine()
        {
            List<QuickPlayer> players = QuickTable(3);
            QuickPlayer host = Hosting(players);
            var owners = new Dictionary<ulong, int>();
            var botSeats = new HashSet<int>();
            PlayQuickUntil(() => Recorded(players, owners, botSeats) && MidTrick(host, 2), 20000);
            QuickPlayer next = AtSeat(players, host.Service.Host.NextHost.NextSeat);
            string code = host.Service.Client.Code;
            GoQuiet(host, unplugged: true);
            SettleQuickUntil(() => Recorded(players, owners, botSeats) && next.Service.Host != null && AllRunning(players, except: host), 20);
            PlayQuickUntil(() => Recorded(players, owners, botSeats) && players.TrueForAll(p => p == host || p.Match.Status == SessionStatus.Ended), 20000);

            var seatAt = new Dictionary<string, int>();
            foreach (QuickPlayer p in players)
            {
                seatAt[p.Network.Address] = (int)p.Match.LocalSeat;
            }

            var hostOf = new Dictionary<string, string>
            {
                [PeerRoomTransport.ChannelOf(code, 0)] = host.Network.Address,
                [PeerRoomTransport.ChannelOf(code, 1)] = next.Network.Address,
            };
            var streams = new Dictionary<string, PacketAssembler>();
            var named = new List<(string Host, int Seat)>();
            var packages = new List<(int At, string Host, string To)>();
            int ownHands = 0;
            int reports = 0;
            foreach (var sent in _peers.Sent)
            {
                string key = sent.From + "\n" + sent.To + "\n" + sent.Channel;
                if (!streams.TryGetValue(key, out PacketAssembler stream))
                {
                    stream = new PacketAssembler(1 << 20);
                    streams[key] = stream;
                }

                if (stream.Add(sent.Packet, out byte[] message) != PacketOutcome.Message || !hostOf.TryGetValue(sent.Channel, out string channelHost))
                {
                    continue;
                }

                bool fromHost = sent.From == channelHost;
                byte kind = message[0];
                if (fromHost && kind == (byte)HostMessageKind.NextHost)
                {
                    var reader = new WireReader(message, 1, message.Length - 1);
                    named.Add((sent.From, NextHostNotice.Read(reader).NextSeat));
                }
                else if (fromHost && kind == (byte)HostMessageKind.Succession)
                {
                    packages.Add((named.Count, sent.From, sent.To));
                }
                else if (!fromHost && kind == (byte)ClientMessageKind.Report)
                {
                    reports++;
                }

                for (int i = 0; i + 8 <= message.Length; i++)
                {
                    ulong bits = BitConverter.ToUInt64(message, i);
                    if (!owners.TryGetValue(bits, out int seat))
                    {
                        continue;
                    }

                    string what = (fromHost ? "host message " + kind : "member message " + kind) + " from " + sent.From + " to " + sent.To + " on " + sent.Channel + " carries seat " + seat + "'s cards";
                    if (botSeats.Contains(seat))
                    {
                        Assert.IsTrue(fromHost && kind == (byte)HostMessageKind.Succession, what + ": a bot's, outside the package");
                        continue;
                    }

                    bool toOwner = fromHost && seatAt[sent.To] == seat;
                    bool reportToNewHost = !fromHost && seatAt[sent.From] == seat && kind == (byte)ClientMessageKind.Report && sent.Channel != PeerRoomTransport.ChannelOf(code, 0);
                    Assert.IsTrue(toOwner || reportToNewHost, what + ": a person's, to someone else");
                    ownHands += toOwner ? 1 : 0;
                }
            }

            Assert.Greater(ownHands, 20, "the scan sees each person's own hand in its updates");
            Assert.AreEqual(1, reports, "one report reached the new host: the only other person's");
            Assert.That(packages, Is.Not.Empty, "the next host in line was sent the bot's hand");
            foreach (var package in packages)
            {
                int to = seatAt[package.To];
                (string Host, int Seat) before = named.FindLast(n => n.Host == package.Host && named.IndexOf(n) < package.At);
                (string Host, int Seat) after = named.Find(n => n.Host == package.Host && named.IndexOf(n) >= package.At);
                Assert.IsTrue(before.Seat == to || after.Seat == to, "a package goes only to the next host in line its host names");
            }
        }

        /// <summary>
        /// Notes every hand the hosting table holds now, and each seat's hand as dealt, by its 8-byte card
        /// set (three cards or more, which nothing else on the wire looks like). Always true, for use in a
        /// condition that is checked every step.
        /// </summary>
        private static bool Recorded(List<QuickPlayer> players, Dictionary<ulong, int> owners, HashSet<int> botSeats)
        {
            QuickPlayer hosting = players.Find(p => !p.Stopped && p.Service.Host != null && p.Service.Host.Table != null);
            if (hosting == null)
            {
                return true;
            }

            TableHost table = hosting.Service.Host.Table;
            MatchSnapshot now = table.Snapshot();
            for (int s = 0; s < 4; s++)
            {
                if (now.seats[s].isBot)
                {
                    botSeats.Add(s);
                }

                CardSet hand = CardSet.Empty;
                foreach (int id in now.seats[s].hand)
                {
                    hand = hand.With(Card.FromId(id));
                }

                foreach (CardSet cards in new[] { hand, table.DealtHandOf((Seat)s) })
                {
                    if (cards.Count >= 3)
                    {
                        owners[cards.Bits] = s;
                    }
                }
            }

            return true;
        }

        [Test]
        public void HandOver_PrivateRoom_HostLostMidTrick_TheNextInLineListsTheCodeAgainAndAComebackFindsIt()
        {
            UsePeers();
            Device host = NewPeerDevice("Hari");
            Device asha = NewPeerDevice("Asha");
            Device suman = NewPeerDevice("Suman");
            string code = OpenPeerRoom(host, asha, suman);
            StartPeerMatch(host, asha, suman);
            PlayPeersUntil(() => MidTrick(host.Rooms.Host, 2), host, asha, suman);
            RoomHost table = host.Rooms.Host;
            MatchSnapshot truth = table.Table.Snapshot();
            Device next = DeviceAt(table.NextHost.NextSeat, asha, suman);
            Device other = next == asha ? suman : asha;

            Unplug(host);
            SettleUntil(() => next.Rooms.Host != null && Running(asha, suman), 20);

            Assert.AreEqual(Describe(truth), Describe(next.Rooms.Host.Table.Snapshot()), "the same trick, every hand where it was");
            Assert.IsTrue(next.Rooms.Current.IsHost, "the next in line hosts the room now");
            Assert.AreEqual(_peerOf[next].Address, _peers.ListedBy(code), "the code leads to it");
            Assert.AreEqual(1, _peers.ListedGeneration(code), "one generation up: the lost host's listing loses");

            // A player whose app is killed comes back with its ticket, and the code finds the new host.
            Seat seat = other.Rooms.Current.Match.LocalSeat;
            PlayPeers(10, asha, suman);
            Unplug(other);
            Settle(3);
            Device back = NewPeerDevice(other.Profile.DisplayName, other.Saves);
            Assert.IsTrue(back.Tickets.TryGet(out string ticket));
            Assert.AreEqual(code, ticket);
            Task<RoomResult> rejoining = back.Rooms.JoinRoomAsync(ticket, CancellationToken.None);
            SettleUntil(() => rejoining.IsCompleted, 20);
            Assert.IsTrue(rejoining.Result.Succeeded, "rejoined at the new host");
            Assert.AreEqual(seat, back.Rooms.Current.Match.LocalSeat, "in its own seat");
            back.Rooms.Current.Match.Start();
            PlayPeers(20000, next, back);
            Assert.AreEqual(SessionStatus.Ended, next.Rooms.Current.Match.Status);
            Assert.AreEqual(SessionStatus.Ended, back.Rooms.Current.Match.Status);
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void HandOver_PrivateRoom_HostPausedAndBack_PlaysOnAsAGuestAndTheRoomStaysWithTheNewHost()
        {
            UsePeers();
            Device host = NewPeerDevice("Hari");
            Device asha = NewPeerDevice("Asha");
            Device suman = NewPeerDevice("Suman");
            OpenPeerRoom(host, asha, suman);
            StartPeerMatch(host, asha, suman);
            PlayPeersUntil(() => MidTrick(host.Rooms.Host, 1), host, asha, suman);
            RoomHost table = host.Rooms.Host;
            int hostSeat = table.HostSeat;
            Device next = DeviceAt(table.NextHost.NextSeat, asha, suman);

            _peers.Pause(_peerOf[host]);
            SettleUntil(() => next.Rooms.Host != null && Running(asha, suman), 20);
            PlayPeers(40, asha, suman);
            _peers.Resume(_peerOf[host]);
            SettleUntil(() => host.Rooms.Host == null && Running(host), 15);

            Assert.IsFalse(host.Rooms.Current.IsHost, "a player now");
            Assert.AreEqual(hostSeat, (int)host.Rooms.Current.Match.LocalSeat);
            Assert.AreEqual(SeatConnection.Connected, next.Rooms.Current.Match.Seats[hostSeat].Connection);
            PlayPeers(20000, host, asha, suman);
            foreach (Device d in new[] { host, asha, suman })
            {
                Assert.AreEqual(SessionStatus.Ended, d.Rooms.Current.Match.Status, d.Profile.DisplayName);
                Assert.AreEqual(1, d.Stats.Current.gamesPlayed, d.Profile.DisplayName);
            }

            Settle(2);
            Assert.IsTrue(next.Rooms.Current.IsHost, "after the match the room stays with the player who took it over");
            Assert.IsTrue(host.Rooms.Current.Seats[hostSeat].Occupied, "and the old host sits in it as a guest");
            Assert.IsFalse(host.Rooms.Current.Seats[hostSeat].IsHost);
            Assert.That(_peerFaults, Is.Empty);
        }

        /// <summary>The host creates a room over the peer-to-peer network and every guest joins it by its code, which is returned.</summary>
        private string OpenPeerRoom(Device host, params Device[] guests)
        {
            string code = OpenPeerRoom(host, guests[0]);
            for (int i = 1; i < guests.Length; i++)
            {
                Task<RoomResult> joining = guests[i].Rooms.JoinRoomAsync(code, CancellationToken.None);
                SettleUntil(() => joining.IsCompleted, 20);
                Assert.IsTrue(joining.Result.Succeeded, "guest " + i + " joined");
            }

            return code;
        }

        private void StartPeerMatch(Device host, params Device[] guests)
        {
            foreach (Device guest in guests)
            {
                guest.Rooms.Current.SetReadyAsync(true, CancellationToken.None);
            }

            Settle();
            Assert.IsTrue(host.Rooms.Current.StartAsync(true, CancellationToken.None).Result);
            Settle();
            host.Rooms.Current.Match.Start();
            foreach (Device guest in guests)
            {
                guest.Rooms.Current.Match.Start();
            }

            Settle();
        }

        /// <summary>Every device here plays like the test bots while time passes; a paused one does nothing.</summary>
        private void PlayPeers(int steps, params Device[] devices)
        {
            for (int i = 0; i < steps; i++)
            {
                bool over = true;
                foreach (Device d in devices)
                {
                    IMatchSession session = d.Rooms.Current != null ? d.Rooms.Current.Match : null;
                    if (session != null && session.Status != SessionStatus.Ended && session.Status != SessionStatus.Failed && !_peerOf[d].IsPaused && _devices.Contains(d))
                    {
                        over = false;
                        RoomFixture.Act(session);
                    }
                }

                if (over)
                {
                    return;
                }

                Settle();
            }
        }

        private void PlayPeersUntil(Func<bool> done, params Device[] devices)
        {
            for (int i = 0; i < 20000 && !done(); i++)
            {
                PlayPeers(1, devices);
            }

            Assert.IsTrue(done(), "it came about");
        }

        private void SettleUntil(Func<bool> done, double seconds)
        {
            double until = _now + seconds;
            while (!done() && _now < until)
            {
                Settle();
            }

            Assert.IsTrue(done(), "it came about within " + seconds + " s");
        }

        /// <summary>The device's app is gone without a word: its connections are lost, and nothing of it runs any more.</summary>
        private void Unplug(Device device)
        {
            _peers.Unplug(_peerOf[device]);
            _devices.Remove(device);
        }

        private static bool MidTrick(RoomHost host, int round)
        {
            MatchSnapshot now = host.Table.Snapshot();
            return now.round == round && now.phase == (int)Phase.Playing && now.currentTrick.cards.Length >= 1 && now.currentTrick.cards.Length <= 3;
        }

        private static Device DeviceAt(int seat, params Device[] devices) => Array.Find(devices, d => (int)d.Rooms.Current.Match.LocalSeat == seat);

        private static bool Running(params Device[] devices) => Array.TrueForAll(devices, d => d.Rooms.Current != null && d.Rooms.Current.Match.Status == SessionStatus.Running);

        private static MatchSnapshot Rounds(MatchSnapshot s) => new MatchSnapshot
        {
            seats = new SeatSnapshot[0],
            currentTrick = new TrickSnapshot { cards = new int[0] },
            completedTricks = new TrickSnapshot[0],
            rounds = s.rounds,
        };

        /// <summary><paramref name="people"/> players at one Quick Match table, its match started; the first ones chose <paramref name="decks"/>, in order.</summary>
        private List<QuickPlayer> QuickTable(int people, params string[] decks)
        {
            UseQuickMatch(listingDelay: 0.5);
            string[] names = { "Hari", "Asha", "Suman", "Bikash" };
            var players = new List<QuickPlayer>();
            for (int i = 0; i < people; i++)
            {
                players.Add(NewQuickPlayer(names[i]));
                if (i < decks.Length)
                {
                    players[i].Device.Cosmetics.SelectDeck(decks[i]);
                }
            }

            foreach (QuickPlayer p in players)
            {
                Tap(p);
            }

            SettleQuickUntil(() => players.TrueForAll(p => p.Search.IsCompleted && p.Search.Result.Found), 30);
            foreach (QuickPlayer p in players)
            {
                p.Match.Start();
            }

            SettleQuick(1);
            Assert.AreEqual(1, players.FindAll(p => p.Service.Host != null).Count, "one table");
            return players;
        }

        private static QuickPlayer Hosting(List<QuickPlayer> players) => players.Find(p => p.Service.Host != null && p.Service.Host.InMatch);

        private static QuickPlayer AtSeat(List<QuickPlayer> players, int seat) => players.Find(p => p.Match != null && (int)p.Match.LocalSeat == seat);

        /// <summary>The next seat after <paramref name="hostSeat"/>, in the order turns go, whose player is a person, <paramref name="skip"/> aside.</summary>
        private static int NextPersonAfter(int hostSeat, IMatchSession match, int skip = -1)
        {
            var seat = (Seat)hostSeat;
            for (int step = 1; step < 4; step++)
            {
                seat = seat.Next(match.Rules.Direction);
                if (!match.Seats[(int)seat].IsBot && (int)seat != skip)
                {
                    return (int)seat;
                }
            }

            return -1;
        }

        /// <summary>The host's table is in <paramref name="round"/>, with one to three cards of a trick on it.</summary>
        private static bool MidTrick(QuickPlayer host, int round)
        {
            MatchSnapshot now = host.Service.Host.Table.Snapshot();
            return now.round == round && now.phase == (int)Phase.Playing && now.currentTrick.cards.Length >= 1 && now.currentTrick.cards.Length <= 3;
        }

        private void PlayQuickUntil(Func<bool> done, int maxSteps)
        {
            for (int i = 0; i < maxSteps && !done(); i++)
            {
                PlayQuick(1);
            }

            Assert.IsTrue(done(), "it came about within " + maxSteps + " steps");
        }

        private static bool AllRunning(List<QuickPlayer> players, params QuickPlayer[] except)
        {
            foreach (QuickPlayer p in players)
            {
                if (Array.IndexOf(except, p) < 0 && p.Match.Status != SessionStatus.Running)
                {
                    return false;
                }
            }

            return true;
        }

        private void AssertPlayedToTheEnd(List<QuickPlayer> players, params QuickPlayer[] except)
        {
            QuickPlayer first = null;
            foreach (QuickPlayer p in players)
            {
                if (Array.IndexOf(except, p) >= 0)
                {
                    continue;
                }

                Assert.AreEqual(SessionStatus.Ended, p.Match.Status, p.Name + " played the match to its end");
                Assert.AreEqual(1, p.Device.Stats.Current.gamesPlayed, p.Name + "'s match counts once");
                Assert.AreEqual(4, p.Match.View.FinalStandings.Count, p.Name);
                first = first ?? p;
                for (int s = 0; s < 4; s++)
                {
                    Assert.AreEqual(first.Match.View.Scores.TotalOf((Seat)s), p.Match.View.Scores.TotalOf((Seat)s), p.Name + " ends on the same score for seat " + s);
                }
            }

            Assert.That(_peerFaults, Is.Empty);
        }

        /// <summary>A snapshot as text, field by field, so two can be compared and their difference read.</summary>
        private static string Describe(MatchSnapshot s)
        {
            var text = new StringBuilder();
            text.Append("phase=").Append(s.phase).Append(" round=").Append(s.round).Append(" fixed=").Append(s.hasFixedFirstDealer)
                .Append(" first=").Append(s.firstDealer).Append(" dealer=").Append(s.dealer).Append(" seat=").Append(s.currentSeat)
                .Append(" turn=").Append(s.currentTurn).Append(" counter=").Append(s.turnCounter).Append(" ended=").Append(s.matchEnded)
                .Append(" match=").Append(s.matchId).Append(" rules=").Append(s.ruleSetId).Append('\n');
            for (int i = 0; i < s.seats.Length; i++)
            {
                SeatSnapshot seat = s.seats[i];
                text.Append("seat ").Append(i).Append(": ").Append(seat.name).Append(' ').Append(seat.avatarId).Append(' ').Append(seat.deckThemeId)
                    .Append(" bot=").Append(seat.isBot).Append(" hand=").Append(Ids(seat.hand)).Append(" call=").Append(seat.call)
                    .Append(" won=").Append(seat.tricksWon).Append(" ready=").Append(seat.ready).Append('\n');
            }

            text.Append("trick: ").Append(s.currentTrick.leader).Append(' ').Append(Ids(s.currentTrick.cards)).Append('\n');
            foreach (TrickSnapshot trick in s.completedTricks)
            {
                text.Append("done: ").Append(trick.leader).Append(' ').Append(Ids(trick.cards)).Append('\n');
            }

            foreach (RoundSnapshot round in s.rounds)
            {
                text.Append("round ").Append(round.round).Append(": ").Append(Ids(round.calls)).Append(" / ").Append(Ids(round.won)).Append(" / ").Append(Ids(round.deltas)).Append('\n');
            }

            return text.ToString();
        }

        private static string Ids(int[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                parts[i] = values[i].ToString(CultureInfo.InvariantCulture);
            }

            return string.Join(",", parts);
        }
    }
}
