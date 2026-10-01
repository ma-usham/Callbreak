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
    /// ADR 020, one deck per table: every face and every back at a table, for every player, is in
    /// one deck. A private room plays in the host's deck, whatever its guests chose; Quick Match in
    /// the deck the player chose (ADR 025).
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        [Test]
        public void PrivateRoom_GuestChoseAnotherDeck_EverySeatOfBothTablesIsInTheHostsDeck()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            host.Cosmetics.SelectDeck(CosmeticsService.KathmanduGold);
            guest.Cosmetics.SelectDeck(CosmeticsService.HimalayanIvory);
            JoinRoom(host, guest);

            StartMatch(host, guest);

            IMatchSession guestTable = guest.Rooms.Current.Match;
            AssertTableDeck(host.Rooms.Current.Match, CosmeticsService.KathmanduGold, "the host's table");
            AssertTableDeck(guestTable, CosmeticsService.KathmanduGold, "the guest's table");
            Assert.AreEqual(CosmeticsService.KathmanduGold, guestTable.Seats[(int)guestTable.LocalSeat].DeckThemeId, "the guest's own faces are the host's deck");
            Assert.IsTrue(guestTable.Seats[2].IsBot && guestTable.Seats[3].IsBot, "two of those seats were filled with bots");
        }

        [Test]
        public void PrivateRoom_PlayAgain_TheNextMatchIsInTheHostsDeckToo()
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
            AssertTableDeck(host.Rooms.Current.Match, CosmeticsService.HimalayanIvory, "the host's second table");
            AssertTableDeck(guest.Rooms.Current.Match, CosmeticsService.HimalayanIvory, "the guest's second table");
        }

        [Test]
        public void PrivateRoom_GuestBackAfterAnAppKill_ItsTableIsStillInTheHostsDeck()
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

            Device back = NewDevice("Asha", guest.Saves);
            Assert.AreEqual(CosmeticsService.HimalayanIvory, back.Cosmetics.SelectedDeckId, "the guest's own choice is still its own");
            Assert.IsTrue(back.Tickets.TryGet(out string code));
            Task<RoomResult> rejoining = back.Rooms.JoinRoomAsync(code, CancellationToken.None);
            Settle();

            Assert.IsTrue(rejoining.Result.Succeeded);
            AssertTableDeck(back.Rooms.Current.Match, CosmeticsService.KathmanduGold, "the table the guest is back at");
        }

        [Test]
        public void QuickMatch_EverySeatIsInTheDeckThePlayerChose()
        {
            // ADR 025: Quick Match plays in the player's own deck, as offline and a room they host do.
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
                AssertTableDeck(table, chosen, "chose " + chosen);
                quick.Dispose();
            }
        }

        /// <summary>Every seat of <paramref name="table"/> is in <paramref name="deck"/>: what the table draws, and what the host sent.</summary>
        private static void AssertTableDeck(IMatchSession table, string deck, string what)
        {
            Assert.IsNotNull(table, what + ": a match");
            for (int s = 0; s < table.Seats.Count; s++)
            {
                Assert.AreEqual(deck, table.Seats[s].DeckThemeId, $"{what}: what the table draws for seat {s}");
                Assert.AreEqual(deck, table.View.Seats[s].DeckThemeId, $"{what}: the profile of seat {s}");
            }
        }
    }
}
