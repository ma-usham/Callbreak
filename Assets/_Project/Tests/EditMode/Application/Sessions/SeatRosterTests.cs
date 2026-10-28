using System.Collections.Generic;
using System.Linq;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Sessions
{
    /// <summary>ADR 041: every bot at a table has a portrait of its own, never the player's or another bot's.</summary>
    public sealed class SeatRosterTests
    {
        [Test]
        public void Offline_EveryFaceAtTheTableIsDifferent([Range(0, SeatRoster.AvatarCount - 1)] int playerAvatar)
        {
            for (ulong seed = 1; seed <= 200; seed++)
            {
                SeatProfile[] seats = SeatRoster.Offline("Ram", playerAvatar, "classic-crimson", new Pcg32Random(seed), false);

                List<string> faces = seats.Select(s => s.AvatarId).ToList();
                Assert.AreEqual(4, faces.Distinct().Count(), "seed " + seed + ": " + string.Join(",", faces));
                Assert.AreEqual(playerAvatar.ToString(), seats[(int)Seat.South].AvatarId, "the player keeps the one picked");
            }
        }

        [Test]
        public void Offline_TheBotsComeInEveryOtherPortrait()
        {
            var seen = new HashSet<string>();
            for (ulong seed = 1; seed <= 200; seed++)
            {
                foreach (SeatProfile seat in SeatRoster.Offline("Ram", 0, "classic-crimson", new Pcg32Random(seed), seed == 1))
                {
                    if (seat.IsBot)
                    {
                        seen.Add(seat.AvatarId);
                    }
                }
            }

            CollectionAssert.AreEquivalent(Enumerable.Range(1, SeatRoster.AvatarCount - 1).Select(a => a.ToString()), seen);
        }

        [Test]
        public void DrawAvatar_TakesThePortraitNobodyHas()
        {
            var rng = new Pcg32Random(7);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(5, SeatRoster.DrawAvatar(rng, new HashSet<int> { 0, 1, 2, 3, 4 }));
            }
        }

        [Test]
        public void DrawAvatar_EveryPortraitTaken_StillGivesOne()
        {
            var rng = new Pcg32Random(7);
            var everyone = new HashSet<int>(Enumerable.Range(0, SeatRoster.AvatarCount));
            for (int i = 0; i < 100; i++)
            {
                Assert.That(SeatRoster.DrawAvatar(rng, everyone), Is.InRange(0, SeatRoster.AvatarCount - 1));
            }
        }
    }
}
