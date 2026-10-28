using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// One set of faces for every deck, each deck's base under them (ADR 053): the library gives a
    /// deck's base and back by its id, falls back to the default deck, and shares the faces; the
    /// Card prefab draws the base under a face and hides it under a back. They use Unity sprites
    /// and the Card prefab, so they live outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class CardDeckBaseTests
    {
        private const string CardPrefab = "Assets/_Project/Prefabs/Table/Card.prefab";

        private Texture2D _texture;
        private GameObject _card;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4);
        }

        [TearDown]
        public void TearDown()
        {
            if (_card != null)
            {
                Object.DestroyImmediate(_card);
            }

            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void ADeck_HasItsOwnBaseAndBack_AndAnUnknownDeckFallsBackToTheDefault()
        {
            Sprite[] faces = Faces();
            Sprite crimsonBase = NewSprite("CrimsonBase"), crimsonBack = NewSprite("CrimsonBack");
            Sprite ivoryBase = NewSprite("IvoryBase"), ivoryBack = NewSprite("IvoryBack");
            CardSpriteProvider library = Library(
                new CardSpriteProvider.DeckSprites("classic-crimson", crimsonBack, crimsonBase, faces),
                new CardSpriteProvider.DeckSprites("himalayan-ivory", ivoryBack, ivoryBase, faces));
            try
            {
                Assert.AreSame(ivoryBase, library.Base("himalayan-ivory"));
                Assert.AreSame(ivoryBack, library.Back("himalayan-ivory"));
                Assert.AreSame(crimsonBase, library.Base("classic-crimson"));
                Assert.AreSame(crimsonBase, library.Base("no-such-deck"), "an unknown deck shows the default deck's base");
                Assert.AreSame(crimsonBase, library.Base(null));
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void EveryDeck_SharesTheSameFaces()
        {
            Sprite[] faces = Faces();
            CardSpriteProvider library = Library(
                new CardSpriteProvider.DeckSprites("classic-crimson", NewSprite("Back1"), NewSprite("Base1"), faces),
                new CardSpriteProvider.DeckSprites("kathmandu-gold", NewSprite("Back2"), NewSprite("Base2"), faces));
            try
            {
                var ace = new Card(Suit.Spades, Rank.Ace);
                Assert.AreSame(faces[ace.Id], library.Face("classic-crimson", ace));
                Assert.AreSame(library.Face("classic-crimson", ace), library.Face("kathmandu-gold", ace));
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void TheCardPrefab_DrawsTheBaseUnderAFace_AndHidesItUnderABack()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefab);
            Assert.IsNotNull(prefab, CardPrefab);
            _card = Object.Instantiate(prefab);
            var view = _card.GetComponent<CardView>();
            Image cardBase = Child("Base");
            Image face = Child("Face");
            Assert.Less(cardBase.transform.GetSiblingIndex(), face.transform.GetSiblingIndex(), "the base lies under the face");

            Sprite under = NewSprite("Base"), front = NewSprite("Face"), back = NewSprite("Back");
            view.ShowFace(new Card(Suit.Hearts, Rank.King), front, under);
            Assert.IsTrue(cardBase.enabled, "face up: the deck's base shows");
            Assert.AreSame(under, cardBase.sprite);
            Assert.AreSame(front, face.sprite);

            view.ShowBack(back);
            Assert.IsFalse(cardBase.enabled, "face down: only the back shows");
            Assert.AreSame(back, face.sprite);

            view.ShowFace(new Card(Suit.Hearts, Rank.King), front, null);
            Assert.IsFalse(cardBase.enabled, "a face with no base shows alone");
        }

        private Image Child(string name)
        {
            Transform child = _card.transform.Find(name);
            Assert.IsNotNull(child, "the Card prefab has " + name);
            return child.GetComponent<Image>();
        }

        private Sprite[] Faces()
        {
            var faces = new Sprite[Card.Count];
            for (int i = 0; i < faces.Length; i++)
            {
                faces[i] = NewSprite("Face" + i);
            }

            return faces;
        }

        private Sprite NewSprite(string name)
        {
            Sprite sprite = Sprite.Create(_texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            return sprite;
        }

        private static CardSpriteProvider Library(params CardSpriteProvider.DeckSprites[] decks)
        {
            var library = ScriptableObject.CreateInstance<CardSpriteProvider>();
            library.SetDecks(decks, decks[0].Id);
            return library;
        }
    }
}
