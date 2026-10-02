using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Online;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// ADR 031, my deck everywhere (it replaces ADR 020's one deck per table and ADR 025's host's
    /// deck): each player sees every card, every face and every back of every seat, in the deck it
    /// chose, on its own device, whatever anyone else at the table chose. The deck a seat's profile
    /// names says what that person chose; no table draws from it.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        [Test]
        public void PrivateRoom_GuestChoseAnotherDeck_EachSeesEveryCardInItsOwnDeck()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            host.Cosmetics.SelectDeck(CosmeticsService.KathmanduGold);
            guest.Cosmetics.SelectDeck(CosmeticsService.HimalayanIvory);
            JoinRoom(host, guest);

            StartMatch(host, guest);

            IMatchSession guestTable = guest.Rooms.Current.Match;
            AssertDrawnIn(host.Rooms.Current.Match, CosmeticsService.KathmanduGold, "the host's table");
            AssertDrawnIn(guestTable, CosmeticsService.HimalayanIvory, "the guest's table");
            Assert.AreEqual(CosmeticsService.KathmanduGold, guestTable.View.Seats[0].DeckThemeId, "the host's profile says what the host chose");
            Assert.AreEqual(CosmeticsService.HimalayanIvory, guestTable.View.Seats[(int)guestTable.LocalSeat].DeckThemeId, "and the guest's what the guest chose");
            Assert.IsTrue(guestTable.Seats[2].IsBot && guestTable.Seats[3].IsBot, "two of those seats were filled with bots");
        }

        [Test]
        public void PrivateRoom_PlayAgain_EachTableStaysInItsPlayersDeck()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            host.Cosmetics.SelectDeck(CosmeticsService.HimalayanIvory);
            guest.Cosmetics.SelectDeck(CosmeticsService.KathmanduGold);
            JoinRoom(host, guest);
            StartMatch(host, guest);
            IMatchSession first = guest.Rooms.Current.Match;
            PlayToEnd(host, guest);
            Settle();
            Assert.AreEqual(SessionStatus.Ended, first.Status, "the first match is over");

            StartMatch(host, guest);

            Assert.AreNotSame(first, guest.Rooms.Current.Match, "a second match");
            AssertDrawnIn(host.Rooms.Current.Match, CosmeticsService.HimalayanIvory, "the host's second table");
            AssertDrawnIn(guest.Rooms.Current.Match, CosmeticsService.KathmanduGold, "the guest's second table");
        }

        [Test]
        public void PrivateRoom_GuestBackAfterAnAppKill_ItsTableIsInTheDeckItHasNow()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            host.Cosmetics.SelectDeck(CosmeticsService.KathmanduGold);
            guest.Cosmetics.SelectDeck(CosmeticsService.HimalayanIvory);
            JoinRoom(host, guest);
            StartMatch(host, guest);
            Play(host, guest, 20);
            Kill(guest);
            Settle(5);

            // Back in the app, the player chooses another deck before going back to the match.
            Device back = NewDevice("Asha", guest.Saves);
            Assert.AreEqual(CosmeticsService.HimalayanIvory, back.Cosmetics.SelectedDeckId, "the guest's own choice is still its own");
            back.Cosmetics.SelectDeck(CosmeticsService.ClassicCrimson);
            Assert.IsTrue(back.Tickets.TryGet(out string code));
            Task<RoomResult> rejoining = back.Rooms.JoinRoomAsync(code, CancellationToken.None);
            Settle();

            Assert.IsTrue(rejoining.Result.Succeeded);
            AssertDrawnIn(back.Rooms.Current.Match, CosmeticsService.ClassicCrimson, "the table the guest is back at");
            AssertDrawnIn(host.Rooms.Current.Match, CosmeticsService.KathmanduGold, "the host's table");
        }

        [Test]
        public void QuickMatch_ThreePlayersOnThreeDecks_EachSeesItsOwn_AlsoAtTheNextHostAfterAHandOver()
        {
            string[] decks = { CosmeticsService.KathmanduGold, CosmeticsService.HimalayanIvory, CosmeticsService.ClassicCrimson };
            List<QuickPlayer> players = QuickTable(3, decks);
            for (int i = 0; i < players.Count; i++)
            {
                Assert.AreEqual(decks[i], players[i].Device.Cosmetics.SelectedDeckId, players[i].Name + " chose its deck");
                AssertDrawnIn(players[i].Match, decks[i], players[i].Name + "'s table");
            }

            Assert.AreEqual(players[0].Match.View.MatchId, players[2].Match.View.MatchId, "one table");
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 1), 20000);
            QuickPlayer next = AtSeat(players, host.Service.Host.NextHost.NextSeat);
            GoQuiet(host, unplugged: true);
            SettleQuickUntil(() => next.Service.Host != null && AllRunning(players, except: host), 20);
            PlayQuick(40);

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != host)
                {
                    AssertDrawnIn(players[i].Match, decks[i], players[i].Name + "'s table, its host handed over");
                }
            }

            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void QuickMatch_EverySeatIsInTheDeckThePlayerChose()
        {
            // ADR 031: the player sees every card in its own deck; at a table of its own, bots only,
            // every profile names that deck too.
            Device device = NewDevice("Hari");
            Assert.Greater(device.Cosmetics.DeckIds.Count, 1, "more than one deck to choose from");
            for (int d = 0; d < device.Cosmetics.DeckIds.Count; d++)
            {
                string chosen = device.Cosmetics.DeckIds[d];
                device.Cosmetics.SelectDeck(chosen);
                Assert.AreEqual(chosen, device.Cosmetics.SelectedDeckId, "the player chose " + chosen);
                ulong tableSeed = (ulong)(d + 1);
                var quick = new QuickMatchService(new QuickMatchConfig(botFillDelaySeconds: 1), device.Profile, device.Cosmetics, device.Auth, new TestBots(),
                    device.Stats, device.Settings, device.Network, new NameFilter(), () => tableSeed, BotTiming.Instant);
                quick.FindMatchAsync(null, CancellationToken.None);
                for (int i = 0; i < 40 && quick.Match == null; i++)
                {
                    _now += 0.25;
                    quick.Tick(_now);
                }

                IMatchSession table = quick.Match;
                Assert.IsNotNull(table, "the bots filled the table");
                AssertDrawnIn(table, chosen, "chose " + chosen);
                for (int s = 0; s < table.View.Seats.Count; s++)
                {
                    Assert.AreEqual(chosen, table.View.Seats[s].DeckThemeId, $"chose {chosen}: the profile of seat {s}");
                }

                quick.Dispose();
            }
        }

        /// <summary>Every card <paramref name="table"/> draws, every seat's faces and backs, is in <paramref name="deck"/>.</summary>
        private static void AssertDrawnIn(IMatchSession table, string deck, string what)
        {
            Assert.IsNotNull(table, what + ": a match");
            for (int s = 0; s < table.Seats.Count; s++)
            {
                Assert.AreEqual(deck, table.Seats[s].DeckThemeId, $"{what}: what the table draws for seat {s}");
            }
        }
    }
}
