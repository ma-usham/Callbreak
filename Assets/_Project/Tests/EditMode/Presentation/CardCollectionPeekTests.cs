using Callbreak.Presentation.Screens;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// The deck picture's look at its ace of spades (GDD §11 screen 15, "Selecting a deck flips its
    /// preview to the Ace of Spades face and back"): the card narrows to its edge on its back,
    /// opens on its face, holds, and turns back the same way. With Reduce motion it never narrows:
    /// the face is simply there for the hold.
    /// </summary>
    public sealed class CardCollectionPeekTests
    {
        private const float Leg = DeckPeek.FlipLegSeconds;
        private const float Hold = DeckPeek.FaceHoldSeconds;
        private const float FaceUp = 2f * Leg;
        private const float TurnsBack = FaceUp + Hold;
        private const float Tolerance = 0.0005f;

        [Test]
        public void TheTiming_IsTheAgreedOne()
        {
            Assert.AreEqual(0.12f, Leg, Tolerance, "each half of a turn takes about 120 ms");
            Assert.AreEqual(1.2f, Hold, Tolerance, "the face stays for about 1.2 s");
            Assert.AreEqual(Hold + 4f * Leg, DeckPeek.Seconds(false), Tolerance);
            Assert.AreEqual(Hold, DeckPeek.Seconds(true), Tolerance, "Reduce motion: only the hold");
        }

        [Test]
        public void APeek_StartsFlatOnTheBack()
        {
            DeckPeek.Pose(0f, false, out bool showsFace, out float width);

            Assert.IsFalse(showsFace);
            Assert.AreEqual(1f, width, Tolerance);
        }

        [Test]
        public void TheCard_ChangesSideWhileItIsEdgeOn()
        {
            DeckPeek.Pose(Leg - 0.001f, false, out bool beforeFace, out float beforeWidth);
            DeckPeek.Pose(Leg, false, out bool atFace, out float atWidth);

            Assert.IsFalse(beforeFace, "still the back while it closes");
            Assert.Less(beforeWidth, 0.01f);
            Assert.IsTrue(atFace, "the face from the moment it opens");
            Assert.AreEqual(0f, atWidth, Tolerance);
        }

        [Test]
        public void EachHalfOfATurn_EasesOut()
        {
            // OutQuad: three quarters of the way after half the time.
            DeckPeek.Pose(Leg / 2f, false, out _, out float closing);
            DeckPeek.Pose(Leg + Leg / 2f, false, out _, out float opening);

            Assert.AreEqual(0.25f, closing, Tolerance);
            Assert.AreEqual(0.75f, opening, Tolerance);
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(0.999f)]
        public void TheFace_IsHeldFlat(float shareOfTheHold)
        {
            DeckPeek.Pose(FaceUp + shareOfTheHold * Hold, false, out bool showsFace, out float width);

            Assert.IsTrue(showsFace);
            Assert.AreEqual(1f, width, Tolerance);
        }

        [Test]
        public void TheCard_TurnsBackTheSameWay()
        {
            DeckPeek.Pose(TurnsBack + Leg / 2f, false, out bool closingFace, out float closingWidth);
            DeckPeek.Pose(TurnsBack + Leg, false, out bool edgeFace, out float edgeWidth);
            DeckPeek.Pose(TurnsBack + Leg + Leg / 2f, false, out bool openingFace, out float openingWidth);

            Assert.IsTrue(closingFace, "the face until it is edge-on");
            Assert.AreEqual(0.25f, closingWidth, Tolerance);
            Assert.IsFalse(edgeFace, "the back from the moment it opens again");
            Assert.AreEqual(0f, edgeWidth, Tolerance);
            Assert.IsFalse(openingFace);
            Assert.AreEqual(0.75f, openingWidth, Tolerance);
        }

        [TestCase(0f)]
        [TestCase(0.05f)]
        [TestCase(10f)]
        public void APeek_EndsFlatOnTheBack(float secondsAfterTheEnd)
        {
            float time = DeckPeek.Seconds(false) + secondsAfterTheEnd;
            DeckPeek.Pose(time, false, out bool showsFace, out float width);

            Assert.IsFalse(showsFace);
            Assert.AreEqual(1f, width, Tolerance);
        }

        [Test]
        public void WithReduceMotion_TheFaceIsThereForTheHold_AndTheCardNeverNarrows()
        {
            foreach (float time in new[] { 0f, Leg / 2f, Leg, Hold / 2f, Hold - 0.001f })
            {
                DeckPeek.Pose(time, true, out bool showsFace, out float width);
                Assert.IsTrue(showsFace, $"face at {time} s");
                Assert.AreEqual(1f, width, Tolerance, $"flat at {time} s");
            }

            foreach (float time in new[] { Hold, Hold + 0.001f, Hold + 5f })
            {
                DeckPeek.Pose(time, true, out bool showsFace, out float width);
                Assert.IsFalse(showsFace, $"back at {time} s");
                Assert.AreEqual(1f, width, Tolerance, $"flat at {time} s");
            }
        }
    }
}
