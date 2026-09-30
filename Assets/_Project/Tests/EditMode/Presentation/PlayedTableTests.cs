using System;
using System.Collections.Generic;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Presentation.Table;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// The played table tells its followers (call panel, results, scorecard) about every played
    /// event. One follower failing must not keep the others, or the table, from carrying on.
    /// </summary>
    public sealed class PlayedTableTests
    {
        private static readonly MatchEvent AnEvent = new RoundStarted(1, Seat.West);

        [Test]
        public void ItStartsWithAnEmptyTable_ForTheLocalSeat()
        {
            var table = new PlayedTable(Seat.North, RuleSet.NepaliStandard);
            Assert.AreEqual(Seat.North, table.State.LocalSeat);
            Assert.AreSame(RuleSet.NepaliStandard, table.State.Rules);
            Assert.AreEqual(TablePhase.Idle, table.State.Phase);
            Assert.Throws<ArgumentNullException>(() => new PlayedTable(null));
        }

        [Test]
        public void EveryFollower_HearsOfAPlayedEvent_InOrder()
        {
            var table = new PlayedTable(Seat.South, RuleSet.NepaliStandard);
            var heard = new List<string>();
            table.Played += e => heard.Add("first " + e.GetType().Name);
            table.Played += e => heard.Add("second " + e.GetType().Name);
            table.NotifyPlayed(AnEvent);
            Assert.AreEqual(new[] { "first RoundStarted", "second RoundStarted" }, heard);
        }

        [Test]
        public void AFailingFollower_DoesNotSilenceTheOthers()
        {
            var table = new PlayedTable(Seat.South, RuleSet.NepaliStandard);
            var heard = new List<string>();
            var errors = new List<Exception>();
            table.Played += e => heard.Add("first");
            table.Played += e => throw new InvalidOperationException("the results screen broke");
            table.Played += e => heard.Add("third");

            Assert.DoesNotThrow(() => table.NotifyPlayed(AnEvent, errors.Add));
            Assert.AreEqual(new[] { "first", "third" }, heard);
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual("the results screen broke", errors[0].Message);
        }

        [Test]
        public void WithoutAnErrorSink_AFailureIsThrownOn()
        {
            var table = new PlayedTable(Seat.South, RuleSet.NepaliStandard);
            table.Played += e => throw new InvalidOperationException("broke");
            Assert.Throws<InvalidOperationException>(() => table.NotifyPlayed(AnEvent));
        }

        [Test]
        public void ARebuild_ReachesEveryFollower_EvenPastAFailure()
        {
            var table = new PlayedTable(Seat.South, RuleSet.NepaliStandard);
            int rebuilt = 0;
            var errors = new List<Exception>();
            table.Rebuilt += () => rebuilt++;
            table.Rebuilt += () => throw new InvalidOperationException("broke");
            table.Rebuilt += () => rebuilt++;

            table.NotifyRebuilt(errors.Add);
            Assert.AreEqual(2, rebuilt);
            Assert.AreEqual(1, errors.Count);
            Assert.Throws<InvalidOperationException>(() => table.NotifyRebuilt());
        }

        [Test]
        public void NoFollowers_IsFine()
        {
            var table = new PlayedTable(Seat.South, RuleSet.NepaliStandard);
            Assert.DoesNotThrow(() => table.NotifyPlayed(AnEvent));
            Assert.DoesNotThrow(() => table.NotifyPlayed(AnEvent, e => { }));
            Assert.DoesNotThrow(() => table.NotifyRebuilt());
            Assert.DoesNotThrow(() => table.NotifyRebuilt(e => { }));
        }
    }
}
