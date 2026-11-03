using Callbreak.Presentation.Table;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    public sealed class TopSeatBannerLayoutTests
    {
        [Test]
        public void Place_WhenBannerIsAboveSeat_LeavesSeatAtHome()
        {
            var banner = new TopSeatBannerLayout.Bounds(0f, 400f, 960f, 540f);
            var seat = new[] { new TopSeatBannerLayout.Bounds(-150f, 200f, 150f, 380f) };

            TopSeatBannerLayout.Placement result = TopSeatBannerLayout.Place(banner, seat, new[] { new TopSeatBannerLayout.Bounds(-600f, 400f, -408f, 500f) }, 8f);

            Assert.IsTrue(result.Fits);
            Assert.AreEqual(0f, result.LeftShift);
        }

        [Test]
        public void Place_WhenTwoPiecesReachBanner_ShiftsOnlyAsFarAsWidestPieceNeeds()
        {
            var banner = new TopSeatBannerLayout.Bounds(0f, 385f, 960f, 540f);
            var seat = new[]
            {
                new TopSeatBannerLayout.Bounds(-200f, 420f, -60f, 460f),
                new TopSeatBannerLayout.Bounds(-170f, 350f, 150f, 405f)
            };

            TopSeatBannerLayout.Placement result = TopSeatBannerLayout.Place(banner, seat, new[] { new TopSeatBannerLayout.Bounds(-600f, 400f, -408f, 500f) }, 8f);

            Assert.IsTrue(result.Fits);
            Assert.AreEqual(158f, result.LeftShift, 0.001f);
        }

        [Test]
        public void Place_WhenLogoLimitsSlide_ReportsThatBannerCannotFit()
        {
            var banner = new TopSeatBannerLayout.Bounds(0f, 385f, 960f, 540f);
            var seat = new[]
            {
                new TopSeatBannerLayout.Bounds(-215f, 420f, -64f, 459f),
                new TopSeatBannerLayout.Bounds(-174f, 276f, 151f, 405f)
            };

            var logo = new[] { new TopSeatBannerLayout.Bounds(-749f, 407f, -367f, 502f) };
            TopSeatBannerLayout.Placement result = TopSeatBannerLayout.Place(banner, seat, logo, 8f, 48f);

            Assert.IsFalse(result.Fits);
            Assert.AreEqual(104f, result.LeftShift, 0.001f);
        }

        [Test]
        public void Place_WhenBannerAlreadyClear_LeavesSeatAtHome()
        {
            var banner = new TopSeatBannerLayout.Bounds(350f, 385f, 1200f, 540f);
            var seat = new[] { new TopSeatBannerLayout.Bounds(-215f, 350f, 330f, 460f) };

            TopSeatBannerLayout.Placement result = TopSeatBannerLayout.Place(banner, seat, new[] { new TopSeatBannerLayout.Bounds(-600f, 400f, -408f, 500f) }, 8f);

            Assert.IsTrue(result.Fits);
            Assert.AreEqual(0f, result.LeftShift);
        }

        [Test]
        public void Place_WhenSpeechIsBelowLogo_DoesNotLetItLimitTheSeat()
        {
            var banner = new TopSeatBannerLayout.Bounds(120f, 385f, 960f, 540f);
            var seat = new[]
            {
                new TopSeatBannerLayout.Bounds(-215f, 422f, -64f, 459f),
                new TopSeatBannerLayout.Bounds(-420f, 265f, -182f, 350f),
                new TopSeatBannerLayout.Bounds(-174f, 276f, 151f, 405f)
            };
            var obstacles = new[]
            {
                new TopSeatBannerLayout.Bounds(-749f, 407f, -367f, 502f),
                new TopSeatBannerLayout.Bounds(-676f, 358f, -440f, 401f)
            };

            TopSeatBannerLayout.Placement result = TopSeatBannerLayout.Place(banner, seat, obstacles, 8f, 48f);

            Assert.IsTrue(result.Fits);
            Assert.AreEqual(39f, result.LeftShift, 0.001f);
        }

        [Test]
        public void Place_OnNarrowPhone_ShiftsAvatarWhileFullFanStaysBelowBanner()
        {
            var banner = new TopSeatBannerLayout.Bounds(0f, 390f, 960f, 540f);
            var seat = new[]
            {
                new TopSeatBannerLayout.Bounds(-215f, 422f, -64f, 459f),
                new TopSeatBannerLayout.Bounds(-57f, 346f, 56f, 459f),
                new TopSeatBannerLayout.Bounds(-174f, 276f, 151f, 375f),
                new TopSeatBannerLayout.Bounds(-420f, 230f, -182f, 316f),
                new TopSeatBannerLayout.Bounds(-353f, 317f, -250f, 355f)
            };
            var obstacles = new[]
            {
                new TopSeatBannerLayout.Bounds(-749f, 407f, -367f, 502f),
                new TopSeatBannerLayout.Bounds(-676f, 358f, -440f, 401f)
            };

            TopSeatBannerLayout.Placement result = TopSeatBannerLayout.Place(banner, seat, obstacles, 8f, 48f);

            Assert.IsTrue(result.Fits);
            Assert.AreEqual(64f, result.LeftShift, 0.001f);
        }
    }
}
