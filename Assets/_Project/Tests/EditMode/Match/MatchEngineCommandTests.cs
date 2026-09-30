using System.Collections.Generic;
using System.Linq;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Match
{
    public sealed class MatchEngineCommandTests
    {
        private MatchEngine _engine;

        [SetUp]
        public void StartOrdinaryDeal()
        {
            // Dealer West, so South calls first and leads; South holds 12 clubs and K♠.
            _engine = new MatchEngine(
                RuleSet.NepaliStandard,
                RiggedDeal.Random(MatchEngineStartTests.OrdinaryHands, Seat.West, Direction.Anticlockwise),
                TestSetups.Humans(Seat.West));
            _engine.Start();
            MatchEngineStartTests.Drain(_engine);
        }

        [Test]
        public void PlaceCall_FourCallsInTurnOrder_EachOpensTheNextTurnThenPlayStarts()
        {
            var events = new List<MatchEvent>();
            Seat[] order = { Seat.South, Seat.East, Seat.North, Seat.West };

            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(order[i], _engine.CurrentSeat);
                Assert.AreEqual(new TurnId(i + 1), _engine.CurrentTurn);
                Assert.AreEqual(CommandResult.Accepted, _engine.Execute(new PlaceCall(order[i], i + 2, _engine.CurrentTurn, isAuto: i == 1)));
                _engine.DrainEvents(events);
            }

            CollectionAssert.AreEqual(
                new[] { "CallPlaced", "CallTurnStarted", "CallPlaced", "CallTurnStarted", "CallPlaced", "CallTurnStarted", "CallPlaced", "PlayTurnStarted", "LegalHint" },
                events.Select(e => e.GetType().Name).ToArray());
            var eastCall = (CallPlaced)events[2];
            Assert.AreEqual(Seat.East, eastCall.Seat);
            Assert.AreEqual(3, eastCall.Call);
            Assert.IsTrue(eastCall.IsAuto);
            var playTurn = (PlayTurnStarted)events[7];
            Assert.AreEqual(Seat.South, playTurn.Seat);
            Assert.AreEqual(new TurnId(5), playTurn.TurnId);
            Assert.IsNull(playTurn.LedSuit);
            var hint = (LegalHint)events[8];
            Assert.AreEqual(EventVisibility.Private(Seat.South), hint.Visibility);
            Assert.AreEqual(MatchEngineStartTests.OrdinaryHands[(int)Seat.South], hint.Legal);
            Assert.AreEqual(Phase.Playing, _engine.Phase);
        }

        [TestCase(0)]
        [TestCase(9)]
        [TestCase(-1)]
        public void PlaceCall_OutOfRange_InvalidCall(int call)
        {
            AssertRejectedWithoutChange(RejectReason.InvalidCall, e => e.Execute(new PlaceCall(Seat.South, call, e.CurrentTurn)));
        }

        [Test]
        public void PlaceCall_CustomCallRange_UsesMinAndMaxCall()
        {
            var engine = new MatchEngine(RuleSet.NepaliStandard.With(minCall: 2, maxCall: 13), new Callbreak.Core.Randomness.Pcg32Random(2UL), TestSetups.Humans(Seat.West));
            engine.Start();

            Assert.AreEqual(RejectReason.InvalidCall, engine.Execute(new PlaceCall(Seat.South, 1, engine.CurrentTurn)).Reason);
            Assert.AreEqual(RejectReason.InvalidCall, engine.Execute(new PlaceCall(Seat.South, 14, engine.CurrentTurn)).Reason);
            Assert.IsTrue(engine.Execute(new PlaceCall(Seat.South, 13, engine.CurrentTurn)).IsAccepted);
            Assert.IsTrue(engine.Execute(new PlaceCall(Seat.East, 2, engine.CurrentTurn)).IsAccepted);
        }

        [Test]
        public void PlaceCall_OtherSeat_NotYourTurn()
        {
            AssertRejectedWithoutChange(RejectReason.NotYourTurn, e => e.Execute(new PlaceCall(Seat.East, 3, e.CurrentTurn)));
        }

        [Test]
        public void PlaceCall_UndefinedSeat_InvalidSeat()
        {
            AssertRejectedWithoutChange(RejectReason.InvalidSeat, e => e.Execute(new PlaceCall((Seat)9, 3, e.CurrentTurn)));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(99)]
        public void PlaceCall_WrongTurnId_StaleTurn(int turn)
        {
            AssertRejectedWithoutChange(RejectReason.StaleTurn, e => e.Execute(new PlaceCall(Seat.South, 3, new TurnId(turn))));
        }

        [Test]
        public void PlaceCall_DuringPlay_WrongPhase()
        {
            CallAll(3);

            AssertRejectedWithoutChange(RejectReason.WrongPhase, e => e.Execute(new PlaceCall(Seat.South, 3, e.CurrentTurn)));
        }

        [Test]
        public void Commands_WrongForThePhase_WrongPhase()
        {
            AssertRejectedWithoutChange(RejectReason.WrongPhase, e => e.Execute(new PlayCard(Seat.South, C("A♣"), e.CurrentTurn)));
            AssertRejectedWithoutChange(RejectReason.WrongPhase, e => e.Execute(new ReadyNext(Seat.South)));
        }

        [Test]
        public void Commands_BeforeStart_WrongPhase()
        {
            var engine = new MatchEngine(RuleSet.NepaliStandard, new Callbreak.Core.Randomness.Pcg32Random(1UL), TestSetups.Humans());

            Assert.AreEqual(RejectReason.WrongPhase, engine.Execute(new PlaceCall(Seat.South, 3, new TurnId(1))).Reason);
            Assert.AreEqual(RejectReason.WrongPhase, engine.Execute(new PlayCard(Seat.South, C("A♣"), new TurnId(1))).Reason);
            Assert.AreEqual(Phase.Lobby, engine.Phase);
        }

        [Test]
        public void Execute_SeveralFaults_ReportsPhaseThenSeatThenTurnThenPayload()
        {
            // Wrong phase and wrong seat and stale turn: phase is checked first.
            AssertRejectedWithoutChange(RejectReason.WrongPhase, e => e.Execute(new PlayCard(Seat.East, C("2♣"), new TurnId(77))));

            // Wrong seat and stale turn and invalid call: seat is checked before the turn id.
            AssertRejectedWithoutChange(RejectReason.NotYourTurn, e => e.Execute(new PlaceCall(Seat.East, 99, new TurnId(77))));

            // Right seat, stale turn and invalid call: the turn id is checked before the payload.
            AssertRejectedWithoutChange(RejectReason.StaleTurn, e => e.Execute(new PlaceCall(Seat.South, 99, new TurnId(77))));
        }

        [Test]
        public void PlayCard_CardInHandAndLegal_EmitsCardPlayedAndOpensTheNextTurn()
        {
            CallAll(3);
            var events = new List<MatchEvent>();

            CommandResult result = _engine.Execute(new PlayCard(Seat.South, C("A♣"), _engine.CurrentTurn, isAuto: true));
            _engine.DrainEvents(events);

            Assert.IsTrue(result.IsAccepted);
            var played = (CardPlayed)events[0];
            Assert.AreEqual(Seat.South, played.Seat);
            Assert.AreEqual(C("A♣"), played.Card);
            Assert.IsTrue(played.IsAuto);
            Assert.IsFalse(played.IsForced);
            var next = (PlayTurnStarted)events[1];
            Assert.AreEqual(Seat.East, next.Seat);
            Assert.AreEqual(Suit.Clubs, next.LedSuit);
            Assert.AreEqual(Set("2♣"), ((LegalHint)events[2]).Legal);
        }

        [Test]
        public void PlayCard_CardNotInHand_CardNotInHand()
        {
            CallAll(3);

            AssertRejectedWithoutChange(RejectReason.CardNotInHand, e => e.Execute(new PlayCard(Seat.South, C("A♠"), e.CurrentTurn)));
        }

        [Test]
        public void PlayCard_InHandButIllegal_IllegalCard()
        {
            CallAll(3);
            _engine.Execute(new PlayCard(Seat.South, C("3♣"), _engine.CurrentTurn));

            // East holds 2♣ and must follow clubs; A♥ is in hand but illegal.
            AssertRejectedWithoutChange(RejectReason.IllegalCard, e => e.Execute(new PlayCard(Seat.East, C("A♥"), e.CurrentTurn)));
        }

        [Test]
        public void PlayCard_DoubleTapAfterTheTurnMovedOn_NotYourTurn()
        {
            CallAll(3);
            TurnId southTurn = _engine.CurrentTurn;
            _engine.Execute(new PlayCard(Seat.South, C("A♣"), southTurn));

            AssertRejectedWithoutChange(RejectReason.NotYourTurn, e => e.Execute(new PlayCard(Seat.South, C("K♣"), southTurn)));
        }

        [Test]
        public void PlayCard_OldTurnIdFromTheSeatThatHoldsANewerTurn_StaleTurn()
        {
            CallAll(3);
            // South leads A♣; East 2♣, North Q♠ (trump), West discards; North wins and leads trick 2.
            TurnId leadTurn = _engine.CurrentTurn;
            _engine.Execute(new PlayCard(Seat.South, C("A♣"), leadTurn));
            _engine.Execute(new PlayCard(Seat.East, C("2♣"), _engine.CurrentTurn));
            TurnId northTurn = _engine.CurrentTurn;
            _engine.Execute(new PlayCard(Seat.North, C("Q♠"), northTurn));
            _engine.Execute(new PlayCard(Seat.West, C("2♦"), _engine.CurrentTurn));
            Assert.AreEqual(Seat.North, _engine.CurrentSeat);

            AssertRejectedWithoutChange(RejectReason.StaleTurn, e => e.Execute(new PlayCard(Seat.North, C("A♦"), northTurn)));
        }

        [Test]
        public void TurnIds_EveryCallAndPlayTurn_IncreaseByOne()
        {
            var driver = new MatchDriver(new MatchEngine(RuleSet.NepaliStandard, new Callbreak.Core.Randomness.Pcg32Random(5UL), TestSetups.Humans(Seat.West)), randomPolicy: true);
            driver.Start();
            driver.PlayToEnd();

            List<int> turns = driver.Events
                .Select(e => e is CallTurnStarted c ? c.TurnId.Value : e is PlayTurnStarted p ? p.TurnId.Value : 0)
                .Where(t => t != 0)
                .ToList();

            CollectionAssert.AreEqual(Enumerable.Range(1, turns.Count).ToList(), turns);
        }

        [Test]
        public void CommandResult_RejectedWithNone_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => CommandResult.Rejected(RejectReason.None));
            Assert.AreEqual("Rejected(StaleTurn)", CommandResult.Rejected(RejectReason.StaleTurn).ToString());
            Assert.AreEqual("Accepted", CommandResult.Accepted.ToString());
        }

        private void CallAll(int call)
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(_engine.Execute(new PlaceCall(_engine.CurrentSeat, call, _engine.CurrentTurn)).IsAccepted);
            }

            MatchEngineStartTests.Drain(_engine);
        }

        private void AssertRejectedWithoutChange(RejectReason expected, System.Func<MatchEngine, CommandResult> command)
        {
            MatchEngineStartTests.Drain(_engine);
            PlayerView[] before = Enumerable.Range(0, 4).Select(s => _engine.ViewFor((Seat)s)).ToArray();
            MatchSnapshot snapshotBefore = _engine.Snapshot();

            CommandResult result = command(_engine);

            Assert.AreEqual(CommandResult.Rejected(expected), result);
            Assert.AreEqual(0, MatchEngineStartTests.Drain(_engine).Count, "a rejected command emits nothing");
            for (int s = 0; s < 4; s++)
            {
                Assert.AreEqual(before[s], _engine.ViewFor((Seat)s), "view of " + (Seat)s + " changed");
            }

            Assert.IsTrue(SnapshotComparer.AreEqual(snapshotBefore, _engine.Snapshot()), "the full state changed");
        }
    }
}
