using System.Collections.Generic;
using Callbreak.Application.Online;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// ADR 026: the bots that fill an online table say a quick phrase now and then, at a moment it
    /// fits, from what everybody can see. South is a person; East, North and West are bots. "Yes"
    /// is a random source that always takes the chance (and the first bot, after the shortest
    /// wait); "No" never does.
    /// </summary>
    public sealed class BotChatterTests
    {
        private const int Rounds = 5;

        private static BotChatter Chatter(bool yes = true) => new BotChatter(seat => seat != Seat.South, yes ? (IRandom)new Yes() : new No(), Rounds);

        /// <summary>Every phrase the chatter lets out from <paramref name="from"/> to <paramref name="to"/>, a tenth of a second at a time.</summary>
        private static List<(Seat seat, int emote, double at)> Said(BotChatter chatter, double from, double to)
        {
            var said = new List<(Seat, int, double)>();
            for (double now = from; now <= to + 1e-9; now += 0.1)
            {
                while (chatter.TryTake(now, out Seat seat, out int emote))
                {
                    said.Add((seat, emote, now));
                }
            }

            return said;
        }

        [Test]
        public void TheFirstDeal_ABotSaysNamaste_AMomentLater()
        {
            BotChatter chatter = Chatter();
            chatter.Observe(new RoundStarted(1, Seat.South), 10.0);

            Assert.IsFalse(chatter.TryTake(10.0, out _, out _), "not at once");
            List<(Seat seat, int emote, double at)> said = Said(chatter, 10.0, 20.0);
            Assert.AreEqual(1, said.Count);
            Assert.AreNotEqual(Seat.South, said[0].seat, "a bot says it");
            Assert.AreEqual(BotChatter.Namaste, said[0].emote);
            Assert.GreaterOrEqual(said[0].at, 11.0, "a moment later");
        }

        [Test]
        public void ALaterRound_NoNamaste()
        {
            BotChatter chatter = Chatter();
            chatter.Observe(new RoundStarted(2, Seat.East), 10.0);
            Assert.IsEmpty(Said(chatter, 10.0, 30.0));
        }

        [Test]
        public void ABoldCall_ABotThatDidNotMakeIt_SaysNiceCall()
        {
            BotChatter chatter = Chatter();
            chatter.Observe(new CallPlaced(Seat.East, 6, false), 5.0);
            List<(Seat seat, int emote, double at)> said = Said(chatter, 5.0, 15.0);
            Assert.AreEqual(1, said.Count);
            Assert.AreEqual(BotChatter.NiceCall, said[0].emote);
            Assert.AreNotEqual(Seat.East, said[0].seat, "not the bot that called");
            Assert.AreNotEqual(Seat.South, said[0].seat);
        }

        [Test]
        public void AnOrdinaryCall_OrOneTheTimerMade_IsLetPass()
        {
            BotChatter chatter = Chatter();
            chatter.Observe(new CallPlaced(Seat.South, 3, false), 5.0);
            chatter.Observe(new CallPlaced(Seat.South, 7, true), 5.0);
            Assert.IsEmpty(Said(chatter, 5.0, 15.0));
        }

        [Test]
        public void ABotsAceCutByASpade_ThatBotSaysOops()
        {
            BotChatter chatter = Chatter();
            Trick trick = new Trick(Seat.East, Direction.Anticlockwise)
                .With(Card.Parse("A♥"))
                .With(Card.Parse("3♥"))
                .With(Card.Parse("4♥"))
                .With(Card.Parse("2♠"));
            var completed = new CompletedTrick(0, trick);
            Assert.AreEqual(Seat.South, completed.Winner, "the person's spade cut it");

            chatter.Observe(new TrickCompleted(completed), 5.0);
            List<(Seat seat, int emote, double at)> said = Said(chatter, 5.0, 15.0);
            Assert.AreEqual(1, said.Count);
            Assert.AreEqual((Seat.East, BotChatter.Oops), (said[0].seat, said[0].emote), "the bot whose ace it was");
        }

        [Test]
        public void ATrickWonInSuit_IsLetPass()
        {
            BotChatter chatter = Chatter();
            Trick trick = new Trick(Seat.East, Direction.Anticlockwise)
                .With(Card.Parse("A♥"))
                .With(Card.Parse("3♥"))
                .With(Card.Parse("4♥"))
                .With(Card.Parse("5♥"));
            chatter.Observe(new TrickCompleted(new CompletedTrick(0, trick)), 5.0);
            Assert.IsEmpty(Said(chatter, 5.0, 15.0));
        }

        [Test]
        public void APersonMeetsABoldCall_ABotSaysWellPlayed()
        {
            BotChatter chatter = Chatter();
            var result = new RoundResult(1, new[]
            {
                new SeatRoundResult(Seat.South, 5, 6, 51, 51),
                new SeatRoundResult(Seat.East, 3, 3, 30, 30),
                new SeatRoundResult(Seat.North, 2, 2, 20, 20),
                new SeatRoundResult(Seat.West, 3, 2, -30, -30),
            });
            chatter.Observe(new RoundScored(result), 5.0);
            List<(Seat seat, int emote, double at)> said = Said(chatter, 5.0, 15.0);
            Assert.AreEqual(1, said.Count);
            Assert.AreEqual(BotChatter.WellPlayed, said[0].emote);
            Assert.AreNotEqual(Seat.South, said[0].seat);
        }

        [Test]
        public void TheLastTrickOfTheMatch_ABotSaysGoodGame_WhileTheTableStillShows()
        {
            BotChatter chatter = Chatter();
            chatter.Observe(new RoundStarted(Rounds, Seat.West), 1.0);
            Assert.IsEmpty(Said(chatter, 1.0, 20.0), "the last round starts quietly");

            Trick trick = new Trick(Seat.East, Direction.Anticlockwise)
                .With(Card.Parse("2♥"))
                .With(Card.Parse("3♥"))
                .With(Card.Parse("4♥"))
                .With(Card.Parse("5♥"));
            chatter.Observe(new TrickCompleted(new CompletedTrick(Deck.HandSize - 1, trick)), 30.0);
            List<(Seat seat, int emote, double at)> said = Said(chatter, 30.0, 40.0);
            Assert.AreEqual(1, said.Count);
            Assert.AreEqual(BotChatter.GoodGame, said[0].emote);
            Assert.Less(said[0].at, 30.0 + 5.0, "before the results cover the table");
        }

        [Test]
        public void APlayerSaysNamaste_ABotAnswersInKind()
        {
            BotChatter chatter = Chatter();
            chatter.Heard(Seat.South, BotChatter.Namaste, 5.0);
            List<(Seat seat, int emote, double at)> said = Said(chatter, 5.0, 15.0);
            Assert.AreEqual(1, said.Count);
            Assert.AreEqual(BotChatter.Namaste, said[0].emote);
            Assert.AreNotEqual(Seat.South, said[0].seat);
        }

        [Test]
        public void OneRemarkAtATime_AndAMomentThatHasPassed_IsNotRemarkedOn()
        {
            BotChatter chatter = Chatter();
            chatter.Observe(new RoundStarted(1, Seat.South), 10.0);
            chatter.Observe(new CallPlaced(Seat.East, 6, false), 10.0);

            List<(Seat seat, int emote, double at)> said = Said(chatter, 10.0, 60.0);
            Assert.AreEqual(1, said.Count, "the table's bots keep quiet for a while after one has spoken, and by then the other moment has passed");
        }

        [Test]
        public void TheChanceSaysNo_NobodySpeaks()
        {
            BotChatter chatter = Chatter(yes: false);
            chatter.Observe(new RoundStarted(1, Seat.South), 1.0);
            chatter.Observe(new CallPlaced(Seat.South, 7, false), 1.0);
            chatter.Heard(Seat.South, BotChatter.Namaste, 1.0);
            Assert.IsEmpty(Said(chatter, 1.0, 30.0));
        }

        [Test]
        public void ABotsOwnPhrase_IsNotAnsweredByTheBots()
        {
            BotChatter chatter = Chatter();
            chatter.Heard(Seat.East, BotChatter.Namaste, 5.0);
            Assert.IsEmpty(Said(chatter, 5.0, 15.0));
        }

        /// <summary>Always takes the chance, picks the first choice and the shortest wait.</summary>
        private sealed class Yes : IRandom
        {
            public int NextInt(int maxExclusive) => 0;
        }

        /// <summary>Never takes the chance.</summary>
        private sealed class No : IRandom
        {
            public int NextInt(int maxExclusive) => maxExclusive - 1;
        }
    }
}
