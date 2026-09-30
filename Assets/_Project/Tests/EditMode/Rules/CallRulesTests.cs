using Callbreak.Core.Rules;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Rules
{
    public sealed class CallRulesTests
    {
        [TestCase(1, true)]
        [TestCase(4, true)]
        [TestCase(8, true)]
        [TestCase(0, false)]
        [TestCase(9, false)]
        [TestCase(-1, false)]
        [TestCase(13, false)]
        public void IsValid_NepaliStandard_OneToEight(int call, bool expected)
        {
            Assert.AreEqual(expected, CallRules.IsValid(call, RuleSet.NepaliStandard));
        }

        [Test]
        public void IsValid_CustomRange_UsesMinAndMax()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(minCall: 2, maxCall: 13);

            Assert.IsFalse(CallRules.IsValid(1, rules));
            Assert.IsTrue(CallRules.IsValid(2, rules));
            Assert.IsTrue(CallRules.IsValid(13, rules));
            Assert.IsFalse(CallRules.IsValid(14, rules));
        }

        [TestCase(-3, 1)]
        [TestCase(0, 1)]
        [TestCase(4, 4)]
        [TestCase(8, 8)]
        [TestCase(12, 8)]
        public void Clamp_AnyEstimate_IntoMinToMax(int estimate, int expected)
        {
            // GDD §5 edge case 8: an estimator call is clamped to MinCall…MaxCall.
            Assert.AreEqual(expected, CallRules.Clamp(estimate, RuleSet.NepaliStandard));
        }

        [Test]
        public void IsValid_NotCalledSentinel_False()
        {
            Assert.IsFalse(CallRules.IsValid(CallRules.NotCalled, RuleSet.NepaliStandard.With(minCall: 1, maxCall: 13)));
        }
    }
}
