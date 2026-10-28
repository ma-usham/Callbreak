using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// My deck everywhere (ADR 031): on a player's screen every card is in the deck that player
    /// chose, whatever deck anyone else at the table chose. The real Table scene plays a real
    /// <see cref="OnlineMatchSession"/>, given the deck this player chose as the room's client gives
    /// it, and fed here as the host feeds it, at a table where every seat's profile names another
    /// deck: the shuffle pile, every card dealt, the opponents' face-down fans, the hand and the
    /// trick all show this player's deck. The same table on the screen of a player who chose another
    /// deck shows that one. Saves go to a temporary folder.
    /// </summary>
    public sealed class OwnDeckTableTests
    {
        private const Seat Host = Seat.South;
        private const int ClockMs = 15000;

        /// <summary>The card a deck's back stands for in <see cref="_art"/>.</summary>
        private const int BackArt = -1;

        /// <summary>A deck's base in <see cref="_art"/>: the border and paper under the shared faces (ADR 053).</summary>
        private const int BaseArt = -2;

        private readonly Dictionary<Sprite, (string Deck, int Card)> _art = new Dictionary<Sprite, (string Deck, int Card)>();
        private GameFixture _game;
        private FoundTable _search;
        private MatchEngine _engine;
        private OnlineMatchSession _session;
        private Seat _local;
        private uint _seq;
        private int _sent;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _search = new FoundTable();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator ChoseKathmanduGold_EveryCardOnTheScreenIsKathmanduGold()
        {
            yield return EveryCardIsInTheDeckThisPlayerChose(CosmeticsService.KathmanduGold, CosmeticsService.HimalayanIvory);
        }

        [UnityTest]
        public IEnumerator ChoseHimalayanIvory_TheSameTableIsHimalayanIvoryOnThisScreen()
        {
            yield return EveryCardIsInTheDeckThisPlayerChose(CosmeticsService.HimalayanIvory, CosmeticsService.KathmanduGold);
        }

        /// <summary>
        /// This player chose <paramref name="mine"/>; the seats' profiles name <paramref name="theirs"/>
        /// and the classic deck. From the shuffle to the first trick, every card on the screen is in
        /// <paramref name="mine"/>.
        /// </summary>
        private IEnumerator EveryCardIsInTheDeckThisPlayerChose(string mine, string theirs)
        {
            yield return OpenTheTable(mine, theirs);
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();
            Assert.IsNotNull(table, "the table is open");

            // The host deals round 1: the shuffle pile in the middle, then every card flies to its seat.
            _engine.Start();
            SendNews(_engine.CurrentSeat);
            int mostBacks = 0;
            yield return GameFixture.Until(
                () =>
                {
                    mostBacks = Math.Max(mostBacks, AssertEveryCardIn(table, mine, "while the deck is shuffled and dealt"));
                    return table.Hand.Count == Deck.HandSize && FansHold(table, Deck.HandSize);
                },
                "the deal to fill the hand and the fans");
            Assert.Greater(mostBacks, 3 * Deck.HandSize, "the shuffle pile was on the screen, more backs than the three fans hold");

            // Everyone calls, and the two seats before this one play to the first trick.
            while (_engine.Phase == Phase.Calling)
            {
                Assert.IsTrue(_engine.Execute(new PlaceCall(_engine.CurrentSeat, 2, _engine.CurrentTurn)).IsAccepted);
            }

            while (_engine.CurrentSeat != _local)
            {
                Seat seat = _engine.CurrentSeat;
                Assert.IsTrue(_engine.Execute(new PlayCard(seat, _engine.ViewFor(seat).LegalCards().Lowest(), _engine.CurrentTurn)).IsAccepted);
            }

            SendNews(_local);
            PlayerView view = _engine.ViewFor(_local);
            Trick trick = view.CurrentTrick;
            Assert.AreEqual(2, trick.Count, "two opponents' cards lie in the trick");
            yield return GameFixture.Until(
                () =>
                {
                    AssertEveryCardIn(table, mine, "while the calls and the cards are shown");
                    return table.Trick.Count == trick.Count && FansShow(table, view);
                },
                "the trick and the fans to show the cards played");

            AssertEveryCardIn(table, mine, "the table, the first trick under way");
            Assert.AreEqual(Deck.HandSize, table.Hand.Count, "this player has not played yet");
            foreach (Card card in view.Hand)
            {
                CardView shown = table.Hand.ViewOf(card);
                Assert.IsNotNull(shown, card + " is in the hand");
                Assert.AreEqual((mine, card.Id), ArtOf(shown), card + " in the hand, in this player's deck");
            }

            for (int rel = 1; rel < TableView.SeatCount; rel++)
            {
                CardView[] fan = table.Seat(rel).Fan.Area.GetComponentsInChildren<CardView>();
                Assert.IsNotEmpty(fan, "seat " + rel + " holds cards");
                foreach (CardView back in fan)
                {
                    Assert.AreEqual((mine, BackArt), ArtOf(back), "seat " + rel + "'s fan shows this player's back, whatever deck its profile names");
                }
            }

            for (int i = 0; i < trick.Count; i++)
            {
                CardView lying = table.Trick.CardAt(trick.SeatAt(i).RelativeTo(_local));
                Assert.IsNotNull(lying, trick[i] + " lies in the trick");
                Assert.AreEqual((mine, trick[i].Id), ArtOf(lying), trick[i] + ", " + trick.SeatAt(i) + "'s card in the trick, in this player's deck");
            }
        }

        /// <summary>
        /// Boots, finds the table through Play Online (the search finds it at once) and gives the
        /// session the state its host sends a player whose table is ready: the match about to be dealt.
        /// </summary>
        private IEnumerator OpenTheTable(string mine, string theirs)
        {
            yield return _game.Boot(extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IMatchmakingService>(_search);
            });

            _game.Settings.SetReduceMotion(false);
            CardSpriteProvider sprites = _game.Services.Resolve<CardSpriteProvider>();
            LearnArt(sprites);
            Assert.AreNotSame(sprites.Back(mine), sprites.Back(theirs), "the two decks have backs of their own");
            Assert.AreNotSame(sprites.Base(mine), sprites.Base(theirs), "and bases of their own under the faces");
            Assert.AreSame(sprites.Face(mine, new Card(Suit.Spades, Rank.Ace)), sprites.Face(theirs, new Card(Suit.Spades, Rank.Ace)), "ADR 053: every deck shares the faces");

            // Every deck but the first costs coins since 6 Oct 2026: this player bought theirs.
            _game.Services.Resolve<IWallet>().Add(750);
            _game.Services.Resolve<ICosmeticsShop>().Buy(CosmeticKind.Deck, mine);
            ICosmeticsService cosmetics = _game.Services.Resolve<ICosmeticsService>();
            cosmetics.SelectDeck(mine);
            Assert.AreEqual(mine, cosmetics.SelectedDeckId, "this player chose its deck in the card collection");

            // Every profile names a deck other than this player's: the host's and the bots' are the
            // host's, a guest's the classic one, and this player's own the deck it had when the match
            // began (it chose another since, and came back to the match).
            var seats = new[]
            {
                new SeatProfile("Hari", "1", theirs, false),
                new SeatProfile("Asha", "2", CosmeticsService.ClassicCrimson, false),
                new SeatProfile("Bikash", "3", theirs, true),
                new SeatProfile("Suman", "4", theirs, false),
            };
            _engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(7), new MatchSetup("quick-MYDECK-1", seats, Host));

            // The third seat to play after the host, who deals: two opponents play to the first trick before it.
            Direction direction = RuleSet.NepaliStandard.Direction;
            _local = Host.Next(direction).Next(direction).Next(direction);
            PlayerView view = _engine.ViewFor(_local);
            _session = new OnlineMatchSession(
                MatchMode.QuickMatch,
                view,
                Status(_local, TableStatus.NoClock),
                payload => _sent++,
                _game.Services.Resolve<IStatsService>(),
                _game.Settings,
                deckThemeId: cosmetics.SelectedDeckId);
            _search.Match = _session;

            GameFixture.Press(_game.MenuRouter, "PlayOnlineButton");
            GameFixture.Press(_game.MenuRouter, "PlayQuickButton");
            yield return _game.UntilTable();
            yield return GameFixture.Until(() => _sent > 0, "the table to tell the host it is ready");
            _session.OnSync(new TableUpdate(++_seq, null, view, Status(_local, TableStatus.NoClock)));
            yield return null;
            Assert.AreEqual(SessionStatus.Running, _session.Status);
        }

        /// <summary>What the engine did since the last update, as the host sends it to this player: the events it may see, its view and the clock of <paramref name="turn"/>.</summary>
        private void SendNews(Seat turn)
        {
            var events = new List<MatchEvent>();
            _engine.DrainEvents(events);
            events.RemoveAll(e => !e.Visibility.IsVisibleTo(_local));
            _session.OnBatch(new TableUpdate(++_seq, events, _engine.ViewFor(_local), Status(turn, ClockMs)));
        }

        private static TableStatus Status(Seat turn, int clockMs)
        {
            return new TableStatus(turn, clockMs, clockMs, TableStatus.NoClock, new SeatConnection[SeatExtensions.Count]);
        }

        /// <summary>
        /// Remembers every deck's art: each back and each base by its deck, and the faces, which every
        /// deck shares (ADR 053), by card only.
        /// </summary>
        private void LearnArt(CardSpriteProvider sprites)
        {
            _art.Clear();
            for (int d = 0; d < sprites.DeckCount; d++)
            {
                CardSpriteProvider.DeckSprites deck = sprites.DeckAt(d);
                _art[deck.Back] = (deck.Id, BackArt);
                Assert.IsNotNull(deck.Base, deck.Id + " has a base under its faces");
                _art[deck.Base] = (deck.Id, BaseArt);
                for (int id = 0; id < Card.Count; id++)
                {
                    Sprite face = deck.Face(id);
                    if (face != null)
                    {
                        _art[face] = (null, id);
                    }
                }
            }
        }

        /// <summary>Every card on the screen is in <paramref name="deck"/>. Returns how many backs show.</summary>
        private int AssertEveryCardIn(TableView table, string deck, string when)
        {
            int backs = 0;
            foreach (Image image in table.GetComponentsInChildren<Image>())
            {
                if (image.enabled && image.sprite != null && _art.TryGetValue(image.sprite, out (string Deck, int Card) art) && art.Deck != null)
                {
                    Assert.AreEqual(deck, art.Deck, $"{when}: {image.name} shows a card of another deck ({art.Deck}, card {art.Card})");
                    backs += art.Card == BackArt ? 1 : 0;
                }
            }

            return backs;
        }

        /// <summary>
        /// The card art a card on the table shows: its deck (a back's, or the base under the shared
        /// face), and its card id or <see cref="BackArt"/>.
        /// </summary>
        private (string Deck, int Card) ArtOf(CardView card)
        {
            string deck = null;
            int shown = int.MinValue;
            foreach (Image image in card.GetComponentsInChildren<Image>())
            {
                if (!image.enabled || image.sprite == null || !_art.TryGetValue(image.sprite, out (string Deck, int Card) art))
                {
                    continue;
                }

                if (art.Card == BackArt)
                {
                    return art;
                }

                if (art.Card == BaseArt)
                {
                    deck = art.Deck;
                }
                else
                {
                    shown = art.Card;
                }
            }

            Assert.AreNotEqual(int.MinValue, shown, card.name + " shows no card art");
            return (deck, shown);
        }

        private bool FansHold(TableView table, int cards)
        {
            for (int rel = 1; rel < TableView.SeatCount; rel++)
            {
                if (table.Seat(rel).Fan.Count != cards)
                {
                    return false;
                }
            }

            return true;
        }

        private bool FansShow(TableView table, PlayerView view)
        {
            for (int rel = 1; rel < TableView.SeatCount; rel++)
            {
                Seat seat = (Seat)(((int)_local + rel) % SeatExtensions.Count);
                if (table.Seat(rel).Fan.Count != view.HandCountOf(seat))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A Quick Match search that finds its table at once: the session the test drives.</summary>
        private sealed class FoundTable : IMatchmakingService
        {
            public IMatchSession Match { get; set; }

            public Task<MatchmakingResult> FindMatchAsync(IProgress<MatchmakingProgress> progress, CancellationToken ct)
            {
                return Task.FromResult(new MatchmakingResult(MatchmakingError.None));
            }
        }
    }
}
