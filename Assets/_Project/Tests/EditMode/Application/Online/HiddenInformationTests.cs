using System;
using System.Collections.Generic;
using System.Text;
using Callbreak.Application.Online;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD §22: the shuffle seed never leaves the host. Whoever holds it can deal all five rounds
    /// in advance and knows every hand before the first call.
    /// </summary>
    public sealed class HiddenInformationTests
    {
        // Every byte differs, so the seed cannot be mistaken for anything else in a payload.
        private const ulong FirstDraw = 0x1122334455667788UL;
        private const ulong DrawStep = 0x0101010101010101UL;

        [Test]
        public void WholeMatch_NoPayloadToAGuestCarriesTheShuffleSeed()
        {
            ulong next = FirstDraw;
            var room = new RoomFixture(seeds: () =>
            {
                ulong draw = next;
                next += DrawStep;
                return draw;
            });
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            ulong seed = room.Host.Table.Seed;

            room.PlayToEnd();

            Assert.IsTrue(room.Host.Table.IsOver);
            Assert.Greater(room.Network.HostPayloads.Count, 50, "the match went over the network");
            foreach (string spelling in Spellings(seed))
            {
                StringAssert.DoesNotContain(spelling, guest.Session.View.MatchId, "the match id every player is sent");
            }

            for (int i = 0; i < room.Network.HostPayloads.Count; i++)
            {
                byte[] payload = room.Network.HostPayloads[i];
                foreach (byte[] form in Forms(seed))
                {
                    Assert.Less(IndexOf(payload, form), 0, "payload " + i + " carries the shuffle seed");
                }
            }
        }

        [Test]
        public void TwoMatchesInOneRoom_GetDifferentIds()
        {
            ulong next = FirstDraw;
            var room = new RoomFixture(seeds: () =>
            {
                ulong draw = next;
                next += DrawStep;
                return draw;
            });
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            string first = guest.Session.View.MatchId;
            room.PlayToEnd();

            room.StartMatch();

            Assert.AreNotEqual(first, guest.Session.View.MatchId, "stats are recorded once per match id");
        }

        /// <summary>The seed as text: hexadecimal in both cases and decimal.</summary>
        private static IEnumerable<string> Spellings(ulong seed)
        {
            yield return seed.ToString("x16");
            yield return seed.ToString("X16");
            yield return seed.ToString();
        }

        /// <summary>The seed as bytes: raw in both byte orders, and each spelling in UTF-8.</summary>
        private static IEnumerable<byte[]> Forms(ulong seed)
        {
            byte[] raw = BitConverter.GetBytes(seed);
            yield return raw;
            var reversed = (byte[])raw.Clone();
            Array.Reverse(reversed);
            yield return reversed;
            foreach (string spelling in Spellings(seed))
            {
                yield return Encoding.UTF8.GetBytes(spelling);
            }
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j])
                {
                    j++;
                }

                if (j == needle.Length)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
