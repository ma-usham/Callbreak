using System;
using Callbreak.Presentation.Table;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// GDD §11 T rules "LOCAL HAND" and §12 "Cards on screen": an arc of up to 13 cards in 70-86% of
    /// the safe-area width, spacing that widens as cards are played, a strip of at least 44 px per
    /// card, and legal cards lifted 28 px.
    /// </summary>
    public sealed class HandLayoutTests
    {
        private const float Reference = 1920f;
        private const float Tolerance = 0.01f;

        private static CardPose[] Layout(int count, float width, HandLayoutSettings settings, bool[] lifted = null, int hovered = -1)
        {
            var poses = new CardPose[count];
            HandLayout.Compute(count, width, settings, lifted, hovered, poses);
            return poses;
        }

        [Test]
        public void Defaults_AreTheDesignDocumentsNumbers()
        {
            var settings = new HandLayoutSettings();
            Assert.AreEqual(180f, settings.CardWidth);
            Assert.AreEqual(0.70f, settings.MinWidthFraction);
            Assert.AreEqual(0.86f, settings.MaxWidthFraction);
            Assert.AreEqual(44f, settings.MinStrip);
            Assert.AreEqual(28f, settings.Lift);
        }

        [Test]
        public void FullHand_Takes70PercentOfTheSafeArea()
        {
            var settings = new HandLayoutSettings();
            Assert.AreEqual(0.70f * Reference, HandLayout.Width(13, Reference, settings), Tolerance);
            Assert.AreEqual((0.70f * Reference - 180f) / 12f, HandLayout.Strip(13, Reference, settings), Tolerance);
        }

        [Test]
        public void FullHand_StaysBetween70And86Percent_OnEveryPhoneAndTabletWidth()
        {
            var settings = new HandLayoutSettings();
            for (float width = 1920f; width <= 2600f; width += 40f)
            {
                float fraction = HandLayout.Width(13, width, settings) / width;
                Assert.GreaterOrEqual(fraction, 0.70f - 1e-4f, "width " + width);
                Assert.LessOrEqual(fraction, 0.86f + 1e-4f, "width " + width);
            }
        }

        [Test]
        public void EveryCard_KeepsAtLeast44Pixels([Values(2, 5, 9, 13)] int count, [Values(500f, 900f, 1400f, 1920f, 2520f)] float width)
        {
            Assert.GreaterOrEqual(HandLayout.Strip(count, width, new HandLayoutSettings()), HandLayout.HardMinStrip - 1e-4f);
        }

        [Test]
        public void Spacing_WidensAsCardsArePlayed_ThenTheHandNarrows()
        {
            var settings = new HandLayoutSettings();
            float previous = 0f;
            for (int count = 13; count >= 2; count--)
            {
                float strip = HandLayout.Strip(count, Reference, settings);
                Assert.GreaterOrEqual(strip, previous - 1e-4f, count + " cards");
                Assert.LessOrEqual(strip, settings.MaxStrip + 1e-4f, count + " cards");
                previous = strip;
            }

            Assert.AreEqual(settings.MaxStrip, HandLayout.Strip(4, Reference, settings), Tolerance);
            Assert.Less(HandLayout.Width(4, Reference, settings), HandLayout.Width(13, Reference, settings));
        }

        [Test]
        public void NarrowSafeArea_WidensTheHandToKeepTheStrip_UpTo86Percent()
        {
            var settings = new HandLayoutSettings();

            // At 900 wide, 70% leaves 37.5 px per card; the hand widens until every card shows 44.
            Assert.AreEqual(44f, HandLayout.Strip(13, 900f, settings), Tolerance);
            float fraction = HandLayout.Width(13, 900f, settings) / 900f;
            Assert.Greater(fraction, 0.70f);
            Assert.LessOrEqual(fraction, 0.86f);
        }

        [Test]
        public void ComfortStrip_IsCappedBy86Percent_ButNeverGoesBelow44()
        {
            var settings = new HandLayoutSettings { MinStrip = 130f };
            float widest = (0.86f * Reference - 180f) / 12f;
            Assert.AreEqual(widest, HandLayout.Strip(13, Reference, settings), Tolerance, "7 mm would need more than 86%: the width wins");

            settings.MinStrip = 110f;
            Assert.AreEqual(110f, HandLayout.Strip(13, Reference, settings), Tolerance, "7 mm fits inside 86%: the strip wins over 70%");

            // Too narrow for 44 px even at 86%: the hard strip wins and the hand overflows.
            Assert.AreEqual(44f, HandLayout.Strip(13, 600f, new HandLayoutSettings()), Tolerance);
            Assert.Greater(HandLayout.Width(13, 600f, new HandLayoutSettings()), 0.86f * 600f);
        }

        [Test]
        public void Arc_IsSymmetric_AndLeansOutwards()
        {
            var settings = new HandLayoutSettings();
            CardPose[] poses = Layout(13, Reference, settings);
            for (int i = 0; i < 13; i++)
            {
                CardPose left = poses[i], right = poses[12 - i];
                Assert.AreEqual(-left.X, right.X, Tolerance, "x of " + i);
                Assert.AreEqual(left.Y, right.Y, Tolerance, "y of " + i);
                Assert.AreEqual(-left.Rotation, right.Rotation, Tolerance, "rotation of " + i);
            }

            Assert.AreEqual(0f, poses[6].X, Tolerance);
            Assert.AreEqual(0f, poses[6].Y, Tolerance);
            Assert.AreEqual(0f, poses[6].Rotation, Tolerance);
            Assert.Greater(poses[0].Rotation, 0f, "cards left of centre turn anticlockwise");
            Assert.Less(poses[12].Rotation, 0f, "cards right of centre turn clockwise");
            Assert.Less(poses[0].Y, poses[3].Y, "the ends of the arc hang lower");
            Assert.Less(poses[3].Y, poses[6].Y);
        }

        [Test]
        public void Cards_AreOneStripApartAlongTheArc_LeftToRight([Values(2, 3, 8, 13)] int count)
        {
            var settings = new HandLayoutSettings();
            float strip = HandLayout.Strip(count, Reference, settings);
            CardPose[] poses = Layout(count, Reference, settings);
            for (int i = 1; i < count; i++)
            {
                float dx = poses[i].X - poses[i - 1].X, dy = poses[i].Y - poses[i - 1].Y;
                Assert.Greater(dx, 0f);
                Assert.AreEqual(strip, (float)Math.Sqrt(dx * dx + dy * dy), 0.05f, "between " + (i - 1) + " and " + i);
            }
        }

        [Test]
        public void LegalCards_Lift28Pixels_AlongTheirOwnAxis()
        {
            var settings = new HandLayoutSettings();
            CardPose[] flat = Layout(13, Reference, settings);
            var lifted = new bool[13];
            lifted[6] = true;
            lifted[0] = true;
            CardPose[] poses = Layout(13, Reference, settings, lifted);

            Assert.AreEqual(flat[6].X, poses[6].X, Tolerance);
            Assert.AreEqual(flat[6].Y + 28f, poses[6].Y, Tolerance, "the middle card rises straight up");

            float dx = poses[0].X - flat[0].X, dy = poses[0].Y - flat[0].Y;
            Assert.AreEqual(28f, (float)Math.Sqrt(dx * dx + dy * dy), Tolerance, "a leaning card rises 28 px too");
            Assert.Less(dx, 0f, "and moves outwards with its lean");
            Assert.Greater(dy, 0f);
            Assert.AreEqual(flat[0].Rotation, poses[0].Rotation, Tolerance);

            for (int i = 1; i < 13; i++)
            {
                if (i != 6)
                {
                    Assert.AreEqual(flat[i], poses[i], "card " + i + " stays put");
                }
            }
        }

        [Test]
        public void TheCardUnderTheFinger_RisesOnTop()
        {
            var settings = new HandLayoutSettings();
            var lifted = new bool[5];
            lifted[2] = true;
            CardPose[] rest = Layout(5, Reference, settings, lifted);
            CardPose[] hover = Layout(5, Reference, settings, lifted, hovered: 2);
            Assert.AreEqual(rest[2].Y + settings.HoverLift, hover[2].Y, Tolerance);
            CardPose[] hoverOther = Layout(5, Reference, settings, lifted, hovered: 1);
            Assert.Greater(hoverOther[1].Y, rest[1].Y);
            Assert.AreEqual(rest[2], hoverOther[2]);
        }

        [Test]
        public void OneCard_SitsInTheMiddle_AndAnEmptyHandIsFine()
        {
            var settings = new HandLayoutSettings();
            CardPose[] one = Layout(1, Reference, settings);
            Assert.AreEqual(new CardPose(0f, 0f, 0f), one[0]);
            Assert.AreEqual(0f, HandLayout.Strip(1, Reference, settings));
            Assert.AreEqual(180f, HandLayout.Width(1, Reference, settings));
            Assert.AreEqual(0f, HandLayout.Width(0, Reference, settings));
            Assert.DoesNotThrow(() => HandLayout.Compute(0, Reference, settings, null, -1, new CardPose[0]));
        }

        [Test]
        public void Buffers_MustHoldTheHand()
        {
            var settings = new HandLayoutSettings();
            Assert.Throws<ArgumentException>(() => HandLayout.Compute(5, Reference, settings, null, -1, new CardPose[4]));
            Assert.Throws<ArgumentException>(() => HandLayout.Compute(5, Reference, settings, new bool[4], -1, new CardPose[5]));
            Assert.Throws<ArgumentNullException>(() => HandLayout.Compute(5, Reference, null, null, -1, new CardPose[5]));
            Assert.Throws<ArgumentNullException>(() => HandLayout.Compute(5, Reference, settings, null, -1, null));
        }

        [Test]
        public void TheTablesOwnNumbers_FitToo()
        {
            // The table prefab overrides the width and the lift with what mockup 08 draws, and bends
            // the hand half as much as the mockup does: the owner asked for a gentler arc (ADR 021).
            var settings = new HandLayoutSettings { MinWidthFraction = 0.772f, Lift = 14.4f, ArcRadius = 7480f };
            float strip = HandLayout.Strip(13, Reference, settings);
            Assert.AreEqual(108.5f, strip, 0.1f);
            float fraction = HandLayout.Width(13, Reference, settings) / Reference;
            Assert.GreaterOrEqual(fraction, 0.70f);
            Assert.LessOrEqual(fraction, 0.86f);
            CardPose[] poses = Layout(13, Reference, settings);
            Assert.AreEqual(5f, poses[0].Rotation, 0.25f, "the end cards lean about 5 degrees, half of mockup 08's 10");
            Assert.AreEqual(-28.3f, poses[0].Y, 0.5f, "and hang about 28 px below the middle card");
        }
    }
}
