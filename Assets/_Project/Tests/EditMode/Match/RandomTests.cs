using System;
using Callbreak.Core.Randomness;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Match
{
    public sealed class RandomTests
    {
        [Test]
        public void NextUInt32_ReferenceSeed42Stream54_MatchesPcgDemoOutput()
        {
            // First outputs of pcg32-demo.c after pcg32_srandom_r(&rng, 42u, 54u).
            var rng = new Pcg32Random(42UL, 54UL);
            uint[] expected = { 0xa15c02b7u, 0x7b47f409u, 0xba1d3330u, 0x83d2f293u, 0xbfa4784bu, 0xcbed606eu };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], rng.NextUInt32(), "output " + i);
            }
        }

        [Test]
        public void NextInt_SameSeed_SameSequence()
        {
            var a = new Pcg32Random(123456789UL);
            var b = new Pcg32Random(123456789UL);

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.NextInt(52), b.NextInt(52));
            }
        }

        [Test]
        public void NextInt_DifferentSeeds_DifferentSequences()
        {
            var a = new Pcg32Random(1UL);
            var b = new Pcg32Random(2UL);
            int same = 0;

            for (int i = 0; i < 100; i++)
            {
                if (a.NextUInt32() == b.NextUInt32())
                {
                    same++;
                }
            }

            Assert.Less(same, 5);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(13)]
        [TestCase(52)]
        [TestCase(int.MaxValue)]
        public void NextInt_AnyBound_StaysInRange(int bound)
        {
            var rng = new Pcg32Random(7UL);

            for (int i = 0; i < 2000; i++)
            {
                int value = rng.NextInt(bound);
                Assert.IsTrue(value >= 0 && value < bound, "value " + value);
            }
        }

        [Test]
        public void NextInt_Bound52_HitsEveryValue()
        {
            var rng = new Pcg32Random(99UL);
            var seen = new bool[52];

            for (int i = 0; i < 5000; i++)
            {
                seen[rng.NextInt(52)] = true;
            }

            CollectionAssert.DoesNotContain(seen, false);
        }

        [Test]
        public void NextInt_NonPositiveBound_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Pcg32Random(1UL).NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Pcg32Random(1UL).NextInt(-5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CryptoRandom().NextInt(0));
        }

        [Test]
        public void FromState_MidSequence_ContinuesIdentically()
        {
            var original = new Pcg32Random(2024UL, 3UL);
            for (int i = 0; i < 17; i++)
            {
                original.NextUInt32();
            }

            Pcg32Random resumed = Pcg32Random.FromState(original.State, original.Increment);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(original.NextInt(1000), resumed.NextInt(1000));
            }
        }

        [Test]
        public void FromState_EvenIncrement_Throws()
        {
            Assert.Throws<ArgumentException>(() => Pcg32Random.FromState(1UL, 2UL));
        }

        [Test]
        public void CryptoRandom_NextInt_StaysInRangeAndSeedsVary()
        {
            var rng = new CryptoRandom();
            for (int i = 0; i < 500; i++)
            {
                int value = rng.NextInt(4);
                Assert.IsTrue(value >= 0 && value < 4);
            }

            Assert.AreNotEqual(rng.NextSeed(), rng.NextSeed());
        }
    }
}
