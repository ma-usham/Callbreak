using NUnit.Framework;

namespace Callbreak.Tests.EditMode
{
    /// <summary>Proves the EditMode test assembly compiles and the runner finds it (Developer Handbook M0 step 6).</summary>
    public sealed class SmokeTests
    {
        [Test]
        public void TestAssembly_Loads_RunnerFindsIt()
        {
            Assert.AreEqual(4, 2 + 2);
        }
    }
}
