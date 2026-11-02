using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// A card that cannot be played (GDD §13: "a warm-dark overlay, never transparency"): the owner,
    /// 2 Nov 2026, of the subdued hand, "the color isnt looking good". The overlay was a pinkish beige
    /// that left white paper a pinkish grey; now it is a warm near-black, so a subdued card is the
    /// same card in shade: white paper stays neutral, clearly darker than a card that can be played,
    /// still light enough to read (ADR 072). Blended as the game draws it, in linear light.
    /// </summary>
    public sealed class CardDimTests
    {
        private const string CardPrefab = "Assets/_Project/Prefabs/Table/Card.prefab";

        [Test]
        public void ASubduedCard_IsTheSameCardInShade_NotTinted()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefab);
            Assert.IsNotNull(prefab, CardPrefab);
            Image dim = null;
            foreach (Image image in prefab.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Dim")
                {
                    dim = image;
                }
            }

            Assert.IsNotNull(dim, "the card has its overlay");
            Assert.AreSame(dim, Field(prefab.GetComponent<CardView>()), "the view dims with it");
            Color overlay = dim.color;
            Assert.LessOrEqual(Mathf.Max(overlay.r, overlay.g, overlay.b), 0.2f, "a dark overlay");
            Assert.That(overlay.r >= overlay.g && overlay.g >= overlay.b, "a warm one");

            Color paper = Over(Color.white, overlay);
            Assert.AreEqual(paper.r, paper.g, 0.02f, "white paper stays a neutral grey");
            Assert.AreEqual(paper.g, paper.b, 0.02f);
            Assert.That(paper.g, Is.InRange(0.75f, 0.85f), "clearly darker than a card that can be played, still easy to read");

            Color pip = Over(new Color(0.8f, 0.1f, 0.12f), overlay);
            Assert.Greater(pip.r, 2.5f * pip.g, "a red pip stays red");
        }

        /// <summary>The overlay over a colour, blended in linear light as the game draws it.</summary>
        private static Color Over(Color under, Color overlay)
        {
            Color a = under.linear, b = overlay.linear;
            var mixed = new Color(
                Mathf.Lerp(a.r, b.r, overlay.a),
                Mathf.Lerp(a.g, b.g, overlay.a),
                Mathf.Lerp(a.b, b.b, overlay.a));
            return mixed.gamma;
        }

        private static Image Field(CardView view)
        {
            var so = new SerializedObject(view);
            return so.FindProperty("_dim").objectReferenceValue as Image;
        }
    }
}
