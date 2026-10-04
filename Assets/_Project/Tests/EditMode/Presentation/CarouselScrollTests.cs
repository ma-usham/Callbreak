using Callbreak.Presentation.Screens;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// The shop's carousel (designer's spec shop-drawer-and-tables-prompts.md): three items in view
    /// at a time, browsed by swipe/drag or the chevrons; the track reflects the position; the
    /// arrows are off at the ends; browsing is hidden when everything fits. At rest the row sits on
    /// a whole tile: a chevron moves a page, a drag settles on the nearest tile or flicks on.
    /// </summary>
    public sealed class CarouselScrollTests
    {
        private const int Visible = 3;
        private const float Tolerance = 0.0001f;

        [TestCase(3, false)]
        [TestCase(2, false)]
        [TestCase(0, false)]
        [TestCase(4, true)]
        [TestCase(6, true)]
        public void Browsing_OnlyWhenThereIsMoreThanTheViewHolds(int count, bool browsable)
        {
            Assert.AreEqual(browsable, CarouselScroll.Browsable(count, Visible));
        }

        [Test]
        public void SixBackgrounds_GoAsFarAsTheLastThree()
        {
            Assert.AreEqual(3, CarouselScroll.MaxOffset(6, Visible));
            Assert.AreEqual(0, CarouselScroll.MaxOffset(3, Visible));
            Assert.AreEqual(0, CarouselScroll.MaxOffset(1, Visible));
        }

        [Test]
        public void TheArrows_AreOffAtTheEnds_AndWhenEverythingFits()
        {
            Assert.IsFalse(CarouselScroll.CanGoBack(0f, 6, Visible), "at the start");
            Assert.IsTrue(CarouselScroll.CanGoForward(0f, 6, Visible));
            Assert.IsTrue(CarouselScroll.CanGoBack(3f, 6, Visible));
            Assert.IsFalse(CarouselScroll.CanGoForward(3f, 6, Visible), "at the end");
            Assert.IsTrue(CarouselScroll.CanGoBack(1f, 6, Visible));
            Assert.IsTrue(CarouselScroll.CanGoForward(1f, 6, Visible));
            Assert.IsFalse(CarouselScroll.CanGoBack(0f, 3, Visible), "everything fits");
            Assert.IsFalse(CarouselScroll.CanGoForward(0f, 3, Visible), "everything fits");
        }

        [Test]
        public void AChevron_MovesAPage_NoFurtherThanTheEnds()
        {
            Assert.AreEqual(3, CarouselScroll.Page(0f, +1, 6, Visible));
            Assert.AreEqual(3, CarouselScroll.Page(3f, +1, 6, Visible), "already at the end");
            Assert.AreEqual(0, CarouselScroll.Page(3f, -1, 6, Visible));
            Assert.AreEqual(0, CarouselScroll.Page(2f, -1, 6, Visible), "not past the start");
            Assert.AreEqual(2, CarouselScroll.Page(0f, +1, 5, Visible), "five items: the last three");
            Assert.AreEqual(3, CarouselScroll.Page(0.4f, +1, 7, Visible), "from the tile the row rests on");
            Assert.AreEqual(0, CarouselScroll.Page(0f, +1, 3, Visible), "everything fits");
        }

        [Test]
        public void ADrag_FollowsTheFinger_AndResistsPastEitherEnd()
        {
            Assert.AreEqual(1.25f, CarouselScroll.Drag(1f, 0.25f, 6, Visible), Tolerance);
            Assert.AreEqual(-1f * CarouselScroll.Resistance, CarouselScroll.Drag(0f, -1f, 6, Visible), Tolerance);
            Assert.AreEqual(3f + 0.5f * CarouselScroll.Resistance, CarouselScroll.Drag(2f, 1.5f, 6, Visible), Tolerance);
            Assert.AreEqual(0.4f * CarouselScroll.Resistance, CarouselScroll.Drag(0f, 0.4f, 3, Visible), Tolerance, "everything fits: it only gives a little");
        }

        [Test]
        public void ASlowRelease_SettlesOnTheNearestTile()
        {
            Assert.AreEqual(1, CarouselScroll.Settle(1.4f, 0.2f, 6, Visible));
            Assert.AreEqual(2, CarouselScroll.Settle(1.6f, -0.2f, 6, Visible));
            Assert.AreEqual(0, CarouselScroll.Settle(-0.3f, 0f, 6, Visible), "back from past the start");
            Assert.AreEqual(3, CarouselScroll.Settle(3.3f, 0f, 6, Visible), "back from past the end");
        }

        [Test]
        public void AFlick_GoesOnAtLeastOneTileItsWay_FurtherWhenFaster()
        {
            Assert.AreEqual(2, CarouselScroll.Settle(1.2f, CarouselScroll.FlickSpeed, 6, Visible), "on to the next tile");
            Assert.AreEqual(2, CarouselScroll.Settle(1.0f, CarouselScroll.FlickSpeed, 6, Visible), "from a whole tile, the next one");
            Assert.AreEqual(1, CarouselScroll.Settle(1.8f, -CarouselScroll.FlickSpeed, 6, Visible), "back to the previous tile");
            Assert.AreEqual(3, CarouselScroll.Settle(0.2f, 12f, 6, Visible), "a fast flick, as far as the end");
            Assert.AreEqual(0, CarouselScroll.Settle(2.9f, -20f, 6, Visible), "a fast flick back, as far as the start");
        }

        [Test]
        public void TheTrack_ReflectsThePosition()
        {
            Assert.AreEqual(0f, CarouselScroll.ThumbPosition(0f, 6, Visible), Tolerance);
            Assert.AreEqual(1f / 3f, CarouselScroll.ThumbPosition(1f, 6, Visible), Tolerance);
            Assert.AreEqual(1f, CarouselScroll.ThumbPosition(3f, 6, Visible), Tolerance);
            Assert.AreEqual(0f, CarouselScroll.ThumbPosition(-0.3f, 6, Visible), Tolerance, "the thumb stays on the track past the start");
            Assert.AreEqual(1f, CarouselScroll.ThumbPosition(3.4f, 6, Visible), Tolerance, "and past the end");
            Assert.AreEqual(0f, CarouselScroll.ThumbPosition(0f, 3, Visible), Tolerance, "everything fits");
        }
    }
}
