using Callbreak.Presentation.Framework;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// A text field stays above the on-screen keyboard: the name question at the first start is a
    /// panel in the middle of the screen, and a phone held sideways has a keyboard over half its
    /// height. Canvas units from the bottom of a 1080-unit screen; the field is 72 high.
    /// </summary>
    public sealed class KeyboardLiftTests
    {
        private const float Screen = 1080f;

        [Test]
        public void KeyboardDown_NothingMoves()
        {
            Assert.AreEqual(0f, KeyboardLift.For(480f, 552f, 0f, Screen));
        }

        [Test]
        public void KeyboardBelowTheField_NothingMoves()
        {
            Assert.AreEqual(0f, KeyboardLift.For(700f, 772f, 600f, Screen), "the keyboard ends well below the field");
            Assert.AreEqual(0f, KeyboardLift.For(624f, 696f, 600f, Screen), "exactly the gap below the field");
        }

        [Test]
        public void KeyboardOverTheField_ThePanelRisesUntilTheFieldIsJustAboveIt()
        {
            float lift = KeyboardLift.For(480f, 552f, 650f, Screen);
            Assert.AreEqual(650f + KeyboardLift.Gap - 480f, lift, 1e-4f);
            Assert.AreEqual(650f + KeyboardLift.Gap, 480f + lift, 1e-4f, "the field's bottom sits the gap above the keyboard");
        }

        [Test]
        public void AKeyboardAlmostAsTallAsTheScreen_TheFieldStopsAtTheTop()
        {
            float lift = KeyboardLift.For(480f, 552f, 1000f, Screen);
            Assert.AreEqual(Screen - KeyboardLift.Gap, 552f + lift, 1e-4f, "the field's top stays the gap below the top of the screen");
        }

        [Test]
        public void NoRoomAbove_ThePanelStays()
        {
            Assert.AreEqual(0f, KeyboardLift.For(980f, 1060f, 1000f, Screen), "a field already at the top cannot go higher");
        }
    }
}
