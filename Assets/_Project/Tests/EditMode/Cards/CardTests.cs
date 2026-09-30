using System;
using Callbreak.Core.Cards;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Cards
{
    public sealed class CardTests
    {
        [TestCase(Suit.Clubs, Rank.Two, 0)]
        [TestCase(Suit.Clubs, Rank.Ace, 12)]
        [TestCase(Suit.Diamonds, Rank.Two, 13)]
        [TestCase(Suit.Hearts, Rank.Two, 26)]
        [TestCase(Suit.Hearts, Rank.Ace, 38)]
        [TestCase(Suit.Spades, Rank.Two, 39)]
        [TestCase(Suit.Spades, Rank.Ace, 51)]
        public void Id_GddAppendixExamples_MatchEncoding(Suit suit, Rank rank, int expectedId)
        {
            Assert.AreEqual(expectedId, new Card(suit, rank).Id);
        }

        [Test]
        public void FromId_EveryId_RoundTripsSuitAndRank()
        {
            for (int id = 0; id < 52; id++)
            {
                Card card = Card.FromId(id);

                Assert.AreEqual(id, card.Id);
                Assert.AreEqual((Suit)(id / 13), card.Suit);
                Assert.AreEqual((Rank)(id % 13 + 2), card.Rank);
                Assert.AreEqual(card, new Card(card.Suit, card.Rank));
                Assert.AreEqual(card.Suit == Suit.Spades, card.IsTrump);
            }
        }

        [TestCase(-1)]
        [TestCase(52)]
        [TestCase(255)]
        public void FromId_OutOfRange_Throws(int id)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Card.FromId(id));
        }

        [Test]
        public void Constructor_UndefinedSuitOrRank_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Card((Suit)4, Rank.Ace));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Card(Suit.Clubs, (Rank)1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Card(Suit.Clubs, (Rank)15));
        }

        [TestCase(Suit.Spades, Rank.Ace, "A♠")]
        [TestCase(Suit.Hearts, Rank.Ten, "10♥")]
        [TestCase(Suit.Diamonds, Rank.King, "K♦")]
        [TestCase(Suit.Clubs, Rank.Two, "2♣")]
        public void ToString_AnyCard_RankThenSuitSymbol(Suit suit, Rank rank, string expected)
        {
            Assert.AreEqual(expected, new Card(suit, rank).ToString());
        }

        [TestCase("A♠", Suit.Spades, Rank.Ace)]
        [TestCase("AS", Suit.Spades, Rank.Ace)]
        [TestCase("as", Suit.Spades, Rank.Ace)]
        [TestCase("10♥", Suit.Hearts, Rank.Ten)]
        [TestCase("10h", Suit.Hearts, Rank.Ten)]
        [TestCase("Th", Suit.Hearts, Rank.Ten)]
        [TestCase("qD", Suit.Diamonds, Rank.Queen)]
        [TestCase("2C", Suit.Clubs, Rank.Two)]
        public void Parse_SymbolOrAsciiNotation_ReturnsCard(string text, Suit suit, Rank rank)
        {
            Assert.AreEqual(new Card(suit, rank), Card.Parse(text));
        }

        [TestCase("")]
        [TestCase("A")]
        [TestCase("1H")]
        [TestCase("11H")]
        [TestCase("AX")]
        [TestCase("A♠ ")]
        [TestCase("10")]
        public void TryParse_Garbage_ReturnsFalse(string text)
        {
            Assert.IsFalse(Card.TryParse(text, out _));
            Assert.Throws<FormatException>(() => Card.Parse(text));
        }

        [Test]
        public void TryParse_Null_ReturnsFalse()
        {
            Assert.IsFalse(Card.TryParse(null, out _));
        }

        [Test]
        public void ToString_EveryCard_ParsesBack()
        {
            for (int id = 0; id < 52; id++)
            {
                Card card = Card.FromId(id);
                Assert.AreEqual(card, Card.Parse(card.ToString()));
            }
        }

        [Test]
        public void Equality_SameAndDifferentCards_BehaveAsValues()
        {
            Card a = Card.Parse("K♦");
            Card b = new Card(Suit.Diamonds, Rank.King);
            Card c = Card.Parse("A♦");

            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.IsTrue(a.Equals((object)b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsTrue(a != c);
            Assert.Less(a.CompareTo(c), 0);
        }
    }
}
