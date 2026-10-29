using Callbreak.Application.Sessions;
using Callbreak.Presentation.Table.Playback;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// ADR 055: the bots wait for a played card to land before they start thinking, so the flight
    /// they wait for must be the one the table draws. <see cref="CardFlight"/> uses UnityEngine,
    /// so this check lives outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class TablePaceTests
    {
        [Test]
        public void TheBots_WaitForTheFlightTheTableDraws()
        {
            Assert.AreEqual(CardFlight.Seconds, (float)BotTiming.TableCardFlightSeconds, 1e-6f);
            Assert.AreEqual(CardFlight.Seconds, (float)BotTiming.Offline.CardFlightSeconds, 1e-6f);
            Assert.AreEqual(CardFlight.Seconds, (float)BotTiming.Hosted.CardFlightSeconds, 1e-6f);
        }
    }
}
