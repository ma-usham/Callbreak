using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Presentation.Table;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>GDD §15 first-time experience: contextual tips in the prompt pill for the first three matches.</summary>
    public sealed class TurnTipsTests
    {
        private static readonly RuleSet Rules = RuleSet.NepaliStandard;
        private readonly TableStrings _text = new TableStrings();

        [TestCase(0, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(40, false)]
        public void AreOn_UntilThreeMatchesAreFinished(int completedMatches, bool expected)
        {
            Assert.AreEqual(expected, TurnTips.AreOn(completedMatches));
        }

        [Test]
        public void Leading_NeedsNoTip()
        {
            Assert.IsNull(Tip("A♠ K♠ 9♥ 3♣", TrickOf(Seat.South)));
        }

        [Test]
        public void Following_CanBeat_IsTheGddExample()
        {
            // Suman led 9♥, Asha played Q♥, Bikash 10♥: only A♥ beats the queen.
            string tip = Tip("A♠ K♠ 9♠ A♥ 8♥ 4♥ K♦ 7♣", TrickOf(Seat.East, "9♥", "Q♥", "10♥"));

            Assert.AreEqual("Tip: you must beat the highest heart if you can.", tip);
        }

        [Test]
        public void Following_CannotBeat_SaysAnyCardOfTheSuit()
        {
            string tip = Tip("A♠ 8♦ 4♦ 7♣", TrickOf(Seat.East, "9♦", "K♦"));

            Assert.AreEqual("Tip: you can't beat it, so any diamond will do.", tip);
        }

        [Test]
        public void Void_CanTrump_SaysASpadeIsRequired()
        {
            string tip = Tip("A♠ 2♠ 8♦ 7♣", TrickOf(Seat.East, "9♥", "K♥"));

            Assert.AreEqual("Tip: you have no hearts, so you must win with a spade.", tip);
        }

        [Test]
        public void Void_CannotWin_SaysAnyCard()
        {
            // No hearts and no spades: nothing to follow with and nothing to trump with.
            string tip = Tip("8♦ 4♦ 7♣", TrickOf(Seat.East, "9♥", "K♥"));

            Assert.AreEqual("Tip: you can't follow or win this trick, so any card will do.", tip);
        }

        [Test]
        public void EveryTip_UsesOnlyCharactersTheGameFontsHave()
        {
            string[] tips =
            {
                Tip("A♥ 8♥", TrickOf(Seat.East, "9♥", "Q♥")),
                Tip("8♣ 4♦", TrickOf(Seat.East, "9♣", "K♣")),
                Tip("A♠ 8♦", TrickOf(Seat.East, "9♥", "K♥")),
                Tip("8♦ 7♣", TrickOf(Seat.East, "9♥", "K♥")),
            };

            foreach (string tip in tips)
            {
                foreach (char c in tip)
                {
                    Assert.IsTrue(c >= ' ' && c <= '~', $"'{c}' in \"{tip}\" is outside the baked Latin set");
                }
            }
        }

        private static Trick TrickOf(Seat leader, params string[] cards)
        {
            var trick = new Trick(leader, Rules.Direction);
            foreach (string card in cards)
            {
                trick = trick.With(Card.Parse(card));
            }

            return trick;
        }

        private string Tip(string hand, Trick trick)
        {
            PlayObligation obligation = LegalMoves.Classify(CardSet.Parse(hand), trick, Rules);
            return TurnTips.ForObligation(obligation, trick, _text);
        }
    }
}
