using System;
using Callbreak.Application.Recording;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The session recording where nothing is recorded (the Editor, the tests, desktop builds, a
    /// phone build without a Smartlook key): screens can still hide themselves, and it counts the
    /// open handles so a test can see that they did; a second dispose does nothing.
    /// </summary>
    public sealed class StandInSessionRecordingTests
    {
        [Test]
        public void CountsTheOpenHandles()
        {
            var recording = new StandInSessionRecording();

            IDisposable first = recording.HideScreen();
            IDisposable second = recording.HideScreen();
            Assert.AreEqual(2, recording.HiddenCount);

            first.Dispose();
            first.Dispose();
            Assert.AreEqual(1, recording.HiddenCount, "a second dispose does nothing");

            second.Dispose();
            Assert.AreEqual(0, recording.HiddenCount);
        }
    }
}
