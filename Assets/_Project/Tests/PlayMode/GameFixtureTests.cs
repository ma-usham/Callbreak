using System.Collections;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Networking.Eos;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The fixture boots the real game with Epic Online Services left off: no test starts the SDK,
    /// signs this machine in to Epic's servers or depends on them. On 4-5 Oct 2026 Epic refused this
    /// machine's messaging connection, and the SDK's error log failed whichever test was running.
    /// </summary>
    public sealed class GameFixtureTests
    {
        /// <summary>The online ticker's first frames, in which it warms Quick Match up (signs in, searches).</summary>
        private const int WarmUpFrames = 30;

        private GameFixture _game;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator Boot_Online_LeavesEpicOnlineServicesOff()
        {
            // Online, so the warm-up would sign in if anything could.
            yield return _game.Boot(extra: builder => builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true)));
            for (int i = 0; i < WarmUpFrames; i++)
            {
                yield return null;
            }

            Assert.IsInstanceOf<NoRoomTransport>(_game.Services.Resolve<IRoomTransport>(), "rooms go through a transport that never connects");
            Assert.IsFalse(_game.Services.TryResolve(out IPeerNetwork _), "no peer-to-peer network: nothing signs in");
            Assert.IsFalse(_game.Services.TryResolve(out IQuickTableDirectory _), "no table directory: Quick Match plays at a table of the device's own");
            Assert.IsFalse(EosPlatform.IsRunning, "the EOS SDK was never started");
        }
    }
}
