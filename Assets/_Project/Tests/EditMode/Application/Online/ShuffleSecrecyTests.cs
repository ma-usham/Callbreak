using System;
using System.Collections.Generic;
using Callbreak.Application.Sessions;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD L235 (a cryptographically secure shuffle), L120 and L2633 (the seed never leaves the
    /// server): nothing a player can see or time says anything about a deal still to come. Each
    /// round is dealt from a seed of its own, drawn when the round begins, and the bots' think times
    /// come from generators that owe nothing to any shuffle.
    /// </summary>
    public sealed class ShuffleSecrecyTests
    {
        [Test]
        public void EachRound_IsDealtFromASeedOfItsOwn_NotFromWhatDealtTheRoundBefore()
        {
            var drawn = new List<ulong>();
            RoomFixture first = NewRoom(Counter(0x1122334455667788UL, drawn));
            PlayUntilRound(first, 1);
            int drawsForRoundOne = drawn.Count;
            PlayUntilRound(first, 2);
            CardSet secondRound = first.Host.Table.ViewFor(Seat.South).Hand;

            // The same draws up to and including the first deal, different ones after it.
            int used = 0;
            RoomFixture second = NewRoom(() => used < drawsForRoundOne ? drawn[used++] : 0x0F0E0D0C0B0A0908UL + (ulong)used++);
            PlayUntilRound(second, 1);
            Assert.AreEqual(first.Host.Table.ViewFor(Seat.South).Round, 2);
            PlayUntilRound(second, 2);

            Assert.AreNotEqual(secondRound, second.Host.Table.ViewFor(Seat.South).Hand, "round 2 came from a draw made for round 2");
        }

        [Test]
        public void BotThinkTimes_FollowNoGeneratorSeededFromADrawOfTheHost()
        {
            const double tick = 0.01;
            var drawn = new List<ulong>();
            var room = new RoomFixture(timing: BotTiming.Hosted, seeds: Counter(0x9E3779B97F4A7C15UL, drawn));
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();

            // What a guest can time: when each bot turn opened, how long the table held, when the move came.
            var thinks = new List<double>();
            var openedAt = new Dictionary<Seat, double>();
            double hold = 0.0;
            guest.Session.EventReceived += e =>
            {
                bool IsBot(Seat s) => guest.Session.Seats[(int)s].IsBot;
                switch (e)
                {
                    case RoundStarted _:
                        hold = Math.Max(hold, room.Now + BotTiming.Hosted.DealSeconds);
                        break;
                    case TrickCompleted _:
                        hold = Math.Max(hold, room.Now + BotTiming.Hosted.TrickDisplaySeconds);
                        break;
                    case CallTurnStarted call when IsBot(call.Seat):
                        openedAt[call.Seat] = room.Now;
                        break;
                    case PlayTurnStarted play when IsBot(play.Seat):
                        openedAt[play.Seat] = room.Now;
                        break;
                    case CallPlaced placed when IsBot(placed.Seat):
                        Timed(placed.Seat);
                        break;
                    case CardPlayed played when IsBot(played.Seat) && !played.IsForced:
                        Timed(played.Seat);
                        break;
                }

                // The first call turn opened before this listener was there: that move is not timed,
                // but it still used a draw, so it stands in the list as a think nobody could match.
                void Timed(Seat seat) => thinks.Add(openedAt.TryGetValue(seat, out double at) ? room.Now - Math.Max(at, hold) : double.NaN);
            };

            for (int i = 0; i < 40000 && guest.Session.View.Round < 3; i++)
            {
                room.Step(tick);
                foreach (RoomFixture.Member m in room.Members)
                {
                    RoomFixture.Act(m.Session);
                }

                room.Network.Pump();
            }

            // A forced move (0.35 s) draws nothing; every other bot move drew the next think time.
            thinks.RemoveAll(t => t < BotTiming.Hosted.ThinkMinSeconds - tick);
            Assert.Greater(thinks.Count, 30, "two rounds of bot moves were timed");
            foreach (ulong seed in drawn)
            {
                var pace = new Pcg32Random(seed, 13);
                int matched = 0;
                foreach (double think in thinks)
                {
                    double predicted = BotTiming.Hosted.ThinkMinSeconds + ((BotTiming.Hosted.ThinkMaxSeconds - BotTiming.Hosted.ThinkMinSeconds) * (pace.NextInt(1000) / 1000.0));
                    if (Math.Abs(think - predicted) <= 3 * tick)
                    {
                        matched++;
                    }
                }

                Assert.Less(matched * 2, thinks.Count, "the think times follow a generator seeded with draw " + seed.ToString("x16"));
            }
        }

        private static RoomFixture NewRoom(Func<ulong> seeds)
        {
            var room = new RoomFixture(seeds: seeds);
            room.Join("player-asha", "Asha");
            room.StartMatch();
            return room;
        }

        private static Func<ulong> Counter(ulong first, List<ulong> drawn)
        {
            ulong next = first;
            return () =>
            {
                ulong draw = next;
                next += 0x0101010101010101UL;
                drawn.Add(draw);
                return draw;
            };
        }

        /// <summary>Everyone plays (call 2, lowest card, ready) until round <paramref name="round"/> has been dealt.</summary>
        private static void PlayUntilRound(RoomFixture room, int round)
        {
            for (int i = 0; i < 20000 && room.Host.Table.ViewFor(Seat.South).Round < round; i++)
            {
                room.Step();
                foreach (RoomFixture.Member m in room.Members)
                {
                    RoomFixture.Act(m.Session);
                }

                room.Network.Pump();
            }

            Assert.AreEqual(round, room.Host.Table.ViewFor(Seat.South).Round, "round " + round + " was dealt");
        }
    }
}
