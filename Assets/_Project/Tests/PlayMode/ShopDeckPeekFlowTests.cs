using System.Collections;
using System.Collections.Generic;
using Callbreak.Application.Cosmetics;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The deck pictures of the shop's Cards category ("Card Collection") in the real scenes (GDD
    /// §11 screen 15: "Selecting a deck flips its preview to the Ace of Spades face and back"):
    /// every deck rests on its back; a tap on a deck, the chosen one included, shows its ace for a
    /// moment; a tap on another deck meanwhile puts the first one back at once; a tap on the deck
    /// while its face is up keeps the face up for another hold; with Reduce motion the picture
    /// changes side without turning.
    /// </summary>
    public sealed class ShopDeckPeekFlowTests
    {
        private const int DeckCount = 3;
        private const float PeekTimeout = 5f;

        private GameFixture _game;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator EveryDeckRestsOnItsBack_AndATap_TheChosenDeckIncluded_ShowsItsAceForAMoment()
        {
            yield return OpenCardCollection();
            for (int i = 0; i < DeckCount; i++)
            {
                Assert.IsTrue(ShowsBack(i) && Flat(i), $"deck {i} rests on its back");
            }

            // The chosen (equipped) deck, tapped again.
            Assert.AreEqual(DeckId(0), _game.Services.Resolve<ICosmeticsService>().SelectedDeckId, "deck 0 is the chosen one");
            Tap(0);
            yield return GameFixture.Until(() => ShowsFace(0), "the chosen deck to show its ace", PeekTimeout);
            yield return GameFixture.Until(() => ShowsBack(0) && Flat(0), "the chosen deck to turn back", PeekTimeout);

            // Another deck, then a third one while the second still shows its face.
            Tap(1);
            yield return GameFixture.Until(() => ShowsFace(1), "deck 1 to show its ace", PeekTimeout);
            Tap(2);
            Assert.IsTrue(ShowsBack(1) && Flat(1), "deck 1 is back on its back at once");
            yield return GameFixture.Until(() => ShowsFace(2), "deck 2 to show its ace", PeekTimeout);
            yield return GameFixture.Until(() => ShowsBack(2) && Flat(2), "deck 2 to turn back", PeekTimeout);
        }

        [UnityTest]
        public IEnumerator ATapWhileTheFaceIsUp_KeepsItUpForAnotherHold()
        {
            yield return OpenCardCollection();
            Tap(1);
            yield return GameFixture.Until(() => ShowsFace(1) && Flat(1), "deck 1 to lie on its face", PeekTimeout);

            // Well inside the hold: without the second tap the card is back on its back 0.44 s
            // later; with it, the face stays up for 1.2 s from the tap.
            yield return new WaitForSeconds(1.0f);
            Assert.IsTrue(ShowsFace(1), "still on its face before the second tap");
            Tap(1);
            yield return new WaitForSeconds(0.9f);
            Assert.IsTrue(ShowsFace(1) && Flat(1), "the second tap started the hold again");
            yield return GameFixture.Until(() => ShowsBack(1) && Flat(1), "deck 1 to turn back", PeekTimeout);
        }

        [UnityTest]
        public IEnumerator WithReduceMotion_TheFaceShowsAtOnce_AndTheCardNeverTurns()
        {
            yield return OpenCardCollection();
            _game.Settings.SetReduceMotion(true);

            Tap(2);
            Assert.IsTrue(ShowsFace(2) && Flat(2), "the ace at once, flat");
            bool narrowed = false;
            yield return GameFixture.Until(
                () =>
                {
                    narrowed |= !Flat(2);
                    return ShowsBack(2);
                },
                "deck 2 to show its back again",
                PeekTimeout);
            Assert.IsFalse(narrowed, "Reduce motion: the card never turns");
        }

        private IEnumerator OpenCardCollection()
        {
            yield return _game.Boot();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "ShopButton");
            yield return GameFixture.Until(() => router.Current == ScreenId.Shop && !_game.Curtain.IsCovering, "the shop to open");
            Assert.AreEqual(0, Screen.Category, "the shop opens on Cards (Card Collection)");
        }

        private ShopView Screen => _game.MenuRouter.Get<ShopView>(ScreenId.Shop);

        private void Tap(int deck)
        {
            GameFixture.PressIn(Screen, "Tile" + deck);
        }

        /// <summary>The deck tile's picture (its "Face" image; its parent turns with the shadow).</summary>
        private Image Picture(int deck)
        {
            ShopItemTile tile = Screen.Carousel(0).Tile(deck);
            Assert.IsNotNull(tile, "Cards has a tile " + deck);
            return tile.Face;
        }

        private bool ShowsFace(int deck) => Picture(deck).sprite == Sprites().Face(DeckId(deck), new Card(Suit.Spades, Rank.Ace));

        private bool ShowsBack(int deck) => Picture(deck).sprite == Sprites().Back(DeckId(deck));

        /// <summary>The card (the picture with its shadow) is not turning: its full width.</summary>
        private bool Flat(int deck) => Mathf.Approximately(Picture(deck).transform.parent.localScale.x, 1f);

        private CardSpriteProvider Sprites() => _game.Services.Resolve<CardSpriteProvider>();

        private string DeckId(int deck)
        {
            IReadOnlyList<string> decks = _game.Services.Resolve<ICosmeticsService>().DeckIds;
            return decks[deck];
        }
    }
}
