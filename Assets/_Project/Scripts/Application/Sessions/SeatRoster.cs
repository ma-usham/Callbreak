using System.Collections.Generic;
using Callbreak.Application.Profile;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;

namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// Who sits where for a match with computer players (GDD §9 "names come from a Nepali pool"):
    /// the local player at South, bots on the other seats with distinct Nepali names and avatars.
    /// Bots have no deck of their own: a table is in one deck (ADR 020).
    /// </summary>
    public static class SeatRoster
    {
        /// <summary>The names the mockups use, East, North, West as drawn (GDD FIG 2.1).</summary>
        private static readonly string[] Mockup = { "Suman", "Asha", "Bikash" };

        private static readonly string[] Pool =
        {
            "Asha", "Bikash", "Suman", "Sita", "Nabin", "Pooja", "Kiran", "Gita", "Anil", "Sarita",
            "Dipesh", "Manisha", "Prakash", "Sunita", "Bibek", "Rojina",
        };

        /// <summary>Bots draw their avatar from the presets the player picks from.</summary>
        public const int AvatarCount = ProfileService.AvatarPresetCount;

        /// <summary>
        /// Seats for an offline match: the player at South; three bots; all four in the player's
        /// deck, every face and every back (ADR 020). The first ever match uses the names from the
        /// mockups; later ones draw from the pool.
        /// </summary>
        public static SeatProfile[] Offline(string playerName, int playerAvatar, string playerDeck, IRandom rng, bool firstMatch)
        {
            var seats = new SeatProfile[4];
            seats[(int)Seat.South] = new SeatProfile(playerName, playerAvatar.ToString(), playerDeck, false);
            var used = new HashSet<string> { playerName };
            Seat[] botSeats = { Seat.East, Seat.North, Seat.West };
            for (int i = 0; i < botSeats.Length; i++)
            {
                string name = firstMatch && !used.Contains(Mockup[i]) ? Mockup[i] : DrawName(rng, used);
                used.Add(name);
                seats[(int)botSeats[i]] = new SeatProfile(name, rng.NextInt(AvatarCount).ToString(), playerDeck, true);
            }

            return seats;
        }

        /// <summary>A fresh name for a bot that takes over a seat or fills an empty one.</summary>
        public static string DrawName(IRandom rng, ICollection<string> used)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                string name = Pool[rng.NextInt(Pool.Length)];
                if (!used.Contains(name))
                {
                    return name;
                }
            }

            return Pool[rng.NextInt(Pool.Length)];
        }
    }
}
