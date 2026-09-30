using Callbreak.Application.Rooms;
using Callbreak.Core.Rules;
using Callbreak.Tests.EditMode.Application.Online;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §5 "Rounds" and §11 screen 02 "Match length 5 rounds (fixed in v1)": the number comes from the rules.</summary>
    public sealed class RoomSettingsTests
    {
        [Test]
        public void Rounds_NotGiven_IsTheRuleSetsMatchLength()
        {
            Assert.AreEqual(RuleSet.NepaliStandard.Rounds, RoomSettings.DefaultRounds);
            Assert.AreEqual(RuleSet.NepaliStandard.Rounds, new RoomSettings("Hari's Table", 30).Rounds);
        }

        [Test]
        public void Rounds_Given_IsKept()
        {
            Assert.AreEqual(3, new RoomSettings("Hari's Table", 30, 3).Rounds);
            Assert.AreEqual(3, new RoomSettings("Hari's Table", 30, rounds: 3).Rounds);
        }

        [Test]
        public void Lobby_ShowsTheMatchLengthTheTableThenPlays()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            int shown = guest.Client.Settings.Rounds;

            room.StartMatch();

            Assert.AreEqual(shown, guest.Session.Rules.Rounds, "the lobby's footer and the match agree");
        }
    }
}
