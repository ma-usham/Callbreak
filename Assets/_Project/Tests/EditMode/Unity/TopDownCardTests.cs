using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The game is seen from straight above, on a cloth (the owner, 29 Oct 2026, ADR 059: "make the
    /// game play in top down view like in other callbreak games; right now the thrown cards are
    /// rotated"). A card lying on the table, in the trick or the deck, is drawn as an upright card is:
    /// as high as it is at that scale, and its far edge as wide as its near one, so none looks turned.
    /// They use the Card prefab, so they live outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class TopDownCardTests
    {
        private const string CardPrefab = "Assets/_Project/Prefabs/Table/Card.prefab";

        private GameObject _card;

        [TearDown]
        public void TearDown()
        {
            if (_card != null)
            {
                Object.DestroyImmediate(_card);
            }
        }

        [TestCase(1f)]
        [TestCase(0.5f)]
        public void ACardLyingOnTheTable_IsDrawnAsAnUprightCard(float flat)
        {
            CardView card = Card();

            card.Placement = new CardPlacement(new Vector2(12f, -30f), 0f, 0.7f, 0f);
            Vector3 upright = card.Rect.localScale;
            card.Placement = new CardPlacement(new Vector2(12f, -30f), 0f, 0.7f, flat);

            Assert.AreEqual(upright.x, card.Rect.localScale.x, 1e-5f, "as wide");
            Assert.AreEqual(upright.y, card.Rect.localScale.y, 1e-5f, "as high: not squashed into a perspective");
            foreach (CardPerspective perspective in card.GetComponentsInChildren<CardPerspective>(true))
            {
                Assert.AreEqual(1f, perspective.TopScale, 1e-5f, perspective.name + ": the far edge is not narrowed");
            }
        }

        private CardView Card()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefab);
            Assert.IsNotNull(prefab, CardPrefab);
            _card = Object.Instantiate(prefab);
            CardView card = _card.GetComponent<CardView>();
            Assert.IsNotNull(card, "the Card prefab has its CardView");
            return card;
        }
    }
}
