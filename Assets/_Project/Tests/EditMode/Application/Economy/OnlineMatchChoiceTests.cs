using System;
using Callbreak.Application.Economy;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>The online match the player picks on Play Online (mockups 23 and 24): Quick Match until a stake of 50 or 100 is chosen.</summary>
    public sealed class OnlineMatchChoiceTests
    {
        [Test]
        public void StartsOnQuickMatch()
        {
            Assert.AreEqual(StakeRules.None, new OnlineMatchChoice().Stake);
        }

        [TestCase(50)]
        [TestCase(100)]
        [TestCase(0)]
        public void Choose_QuickMatchOrAStake_IsKept(int stake)
        {
            var choice = new OnlineMatchChoice();
            choice.Choose(100);

            choice.Choose(stake);

            Assert.AreEqual(stake, choice.Stake);
        }

        [TestCase(25)]
        [TestCase(-50)]
        [TestCase(200)]
        public void Choose_AnyOtherAmount_Throws_AndKeepsTheChoice(int stake)
        {
            var choice = new OnlineMatchChoice();
            choice.Choose(50);

            Assert.Throws<ArgumentOutOfRangeException>(() => choice.Choose(stake));

            Assert.AreEqual(50, choice.Stake);
        }
    }
}
