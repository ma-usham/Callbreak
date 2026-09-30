using System;
using Callbreak.Bots;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Bots
{
    /// <summary>The factory the bootstrap binds the application's bot factory to (GDD §9 "Easy · Normal · Hard").</summary>
    public sealed class BotStrategiesTests
    {
        [Test]
        public void Create_EveryLevel_TheMatchingStrategy()
        {
            Assert.IsInstanceOf<EasyStrategy>(BotStrategies.Create(BotLevel.Easy));
            Assert.IsInstanceOf<NormalStrategy>(BotStrategies.Create(BotLevel.Normal));
            Assert.IsInstanceOf<HardStrategy>(BotStrategies.Create(BotLevel.Hard));
            foreach (BotLevel level in new[] { BotLevel.Easy, BotLevel.Normal, BotLevel.Hard })
            {
                Assert.AreEqual(level, BotStrategies.Create(level).Level);
            }
        }

        [Test]
        public void Create_Hard_TheDefaultBudget()
        {
            var hard = (HardStrategy)BotStrategies.Create(BotLevel.Hard);

            Assert.AreEqual(HardStrategy.DefaultSamples, hard.Samples);
        }

        [Test]
        public void Create_CalledTwice_SeparateInstances()
        {
            Assert.AreNotSame(BotStrategies.Create(BotLevel.Normal), BotStrategies.Create(BotLevel.Normal));
        }

        [Test]
        public void Create_UndefinedLevel_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BotStrategies.Create((BotLevel)3));
        }
    }
}
