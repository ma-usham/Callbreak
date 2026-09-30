using System;
using Callbreak.Core.Rules;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Rules
{
    public sealed class RuleSetTests
    {
        [Test]
        public void NepaliStandard_Defaults_MatchGddSection5()
        {
            RuleSet rules = RuleSet.NepaliStandard;

            Assert.AreEqual("nepali-standard", rules.Id);
            Assert.AreEqual(4, rules.PlayerCount);
            Assert.AreEqual(5, rules.Rounds);
            Assert.AreEqual(1, rules.MinCall);
            Assert.AreEqual(8, rules.MaxCall);
            Assert.AreEqual(Direction.Anticlockwise, rules.Direction);
            Assert.IsTrue(rules.MustBeatLedSuit);
            Assert.IsTrue(rules.MustTrumpWhenVoid);
            Assert.IsTrue(rules.MustOvertrump);
            Assert.IsTrue(rules.DiscardWhenCannotOvertrump);
            Assert.AreEqual(1, rules.OvertrickTenths);
            Assert.IsTrue(rules.RedealIfNoSpade);
            Assert.IsFalse(rules.RedealIfNoHonour);
            Assert.IsFalse(rules.BigCallBonus);
            Assert.IsTrue(rules.AutoPlayLastTrick);
            Assert.IsFalse(rules.AutoPlayForcedMoves);
        }

        [Test]
        public void NepaliStandard_TwoReads_SameImmutableInstanceValue()
        {
            Assert.AreEqual(RuleSet.NepaliStandard, RuleSet.NepaliStandard);
            Assert.AreEqual(RuleSet.NepaliStandard.GetHashCode(), RuleSet.NepaliStandard.With().GetHashCode());
        }

        [Test]
        public void With_EachSwitch_ChangesOnlyThatSwitch()
        {
            RuleSet standard = RuleSet.NepaliStandard;

            Assert.IsFalse(standard.With(mustBeatLedSuit: false).MustBeatLedSuit);
            Assert.IsFalse(standard.With(mustTrumpWhenVoid: false).MustTrumpWhenVoid);
            Assert.IsFalse(standard.With(mustOvertrump: false).MustOvertrump);
            Assert.IsFalse(standard.With(discardWhenCannotOvertrump: false).DiscardWhenCannotOvertrump);
            Assert.IsFalse(standard.With(redealIfNoSpade: false).RedealIfNoSpade);
            Assert.IsTrue(standard.With(redealIfNoHonour: true).RedealIfNoHonour);
            Assert.IsTrue(standard.With(bigCallBonus: true).BigCallBonus);
            Assert.IsFalse(standard.With(autoPlayLastTrick: false).AutoPlayLastTrick);
            Assert.IsTrue(standard.With(autoPlayForcedMoves: true).AutoPlayForcedMoves);
            Assert.AreEqual(Direction.Clockwise, standard.With(direction: Direction.Clockwise).Direction);
            Assert.AreEqual(3, standard.With(rounds: 3).Rounds);
            Assert.AreEqual(13, standard.With(maxCall: 13).MaxCall);
            Assert.AreEqual(2, standard.With(minCall: 2).MinCall);
            Assert.AreEqual(0, standard.With(overtrickTenths: 0).OvertrickTenths);
            Assert.AreEqual("house", standard.With(id: "house").Id);

            RuleSet variant = standard.With(mustOvertrump: false);
            Assert.AreEqual(standard.MustBeatLedSuit, variant.MustBeatLedSuit);
            Assert.AreEqual(standard.Rounds, variant.Rounds);
            Assert.AreEqual(standard.Id, variant.Id);
            Assert.IsTrue(RuleSet.NepaliStandard.MustOvertrump, "deriving a variant must not mutate the preset");
        }

        [Test]
        public void Equals_SameValues_EqualAndDifferentValues_NotEqual()
        {
            RuleSet a = RuleSet.NepaliStandard.With(rounds: 3);
            RuleSet b = RuleSet.NepaliStandard.With(rounds: 3);

            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a.Equals(RuleSet.NepaliStandard));
            Assert.IsFalse(a.Equals(null));
        }

        [TestCase(0)]
        [TestCase(11)]
        public void Constructor_RoundsOutOfRange_Throws(int rounds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RuleSet.NepaliStandard.With(rounds: rounds));
        }

        [TestCase(1)]
        [TestCase(10)]
        public void Constructor_RoundsAtBounds_Accepted(int rounds)
        {
            Assert.AreEqual(rounds, RuleSet.NepaliStandard.With(rounds: rounds).Rounds);
        }

        [TestCase(0, 8)]
        [TestCase(1, 14)]
        [TestCase(1, 0)]
        [TestCase(5, 4)]
        [TestCase(14, 14)]
        public void Constructor_CallRangeInvalid_Throws(int minCall, int maxCall)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RuleSet.NepaliStandard.With(minCall: minCall, maxCall: maxCall));
        }

        [TestCase(-1)]
        [TestCase(11)]
        public void Constructor_OvertrickOutOfRange_Throws(int tenths)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RuleSet.NepaliStandard.With(overtrickTenths: tenths));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Constructor_BlankId_Throws(string id)
        {
            Assert.Throws<ArgumentException>(() => new RuleSet(
                id, 5, 1, 8, Direction.Anticlockwise, true, true, true, true, 1, true, false, false, true, false));
        }

        [Test]
        public void Constructor_UndefinedDirection_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RuleSet.NepaliStandard.With(direction: (Direction)9));
        }

        [Test]
        public void Constructor_AllArguments_StoredVerbatim()
        {
            var rules = new RuleSet("x", 7, 2, 13, Direction.Clockwise, false, false, false, false, 10, false, true, true, false, true);

            Assert.AreEqual("x", rules.Id);
            Assert.AreEqual(7, rules.Rounds);
            Assert.AreEqual(2, rules.MinCall);
            Assert.AreEqual(13, rules.MaxCall);
            Assert.AreEqual(Direction.Clockwise, rules.Direction);
            Assert.IsFalse(rules.MustBeatLedSuit);
            Assert.IsFalse(rules.MustTrumpWhenVoid);
            Assert.IsFalse(rules.MustOvertrump);
            Assert.IsFalse(rules.DiscardWhenCannotOvertrump);
            Assert.AreEqual(10, rules.OvertrickTenths);
            Assert.IsFalse(rules.RedealIfNoSpade);
            Assert.IsTrue(rules.RedealIfNoHonour);
            Assert.IsTrue(rules.BigCallBonus);
            Assert.IsFalse(rules.AutoPlayLastTrick);
            Assert.IsTrue(rules.AutoPlayForcedMoves);
        }

        [Test]
        public void ToString_AnyCulture_SameText()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(overtrickTenths: 10, rounds: 10);

            string invariant = CultureRunner.In("", () => rules.ToString());
            string arabic = CultureRunner.In("ar-SA", () => rules.ToString());
            string swedish = CultureRunner.In("sv-SE", () => rules.ToString());

            Assert.AreEqual(invariant, arabic);
            Assert.AreEqual(invariant, swedish);
            StringAssert.Contains("nepali-standard", invariant);
        }
    }
}
