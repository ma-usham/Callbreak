using System;
using System.Collections.Generic;
using System.Linq;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Tests.EditMode.Scoring;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Match
{
    public sealed class EventTests
    {
        [Test]
        public void Visibility_FullMatch_OnlyHandsAndHintsArePrivateAndToTheirOwnSeat()
        {
            List<MatchEvent> events = PlayMatch(61UL);

            foreach (MatchEvent e in events)
            {
                switch (e)
                {
                    case HandDealt dealt:
                        Assert.AreEqual(EventVisibility.Private(dealt.Seat), e.Visibility);
                        break;
                    case LegalHint hint:
                        Assert.AreEqual(EventVisibility.Private(hint.Seat), e.Visibility);
                        break;
                    default:
                        Assert.IsTrue(e.Visibility.IsPublic, e.ToString());
                        break;
                }
            }

            Assert.AreEqual(20, events.OfType<HandDealt>().Count());
        }

        [Test]
        public void IsVisibleTo_PublicAndPrivate_RoutesCorrectly()
        {
            EventVisibility toEast = EventVisibility.Private(Seat.East);

            Assert.IsTrue(EventVisibility.Public.IsVisibleTo(Seat.West));
            Assert.IsTrue(toEast.IsVisibleTo(Seat.East));
            Assert.IsFalse(toEast.IsVisibleTo(Seat.South));
            Assert.AreEqual(Seat.East, toEast.Seat);
            Assert.Throws<InvalidOperationException>(() => { Seat unused = EventVisibility.Public.Seat; });
            Assert.Throws<ArgumentOutOfRangeException>(() => EventVisibility.Private((Seat)4));
            Assert.AreEqual("private(East)", toEast.ToString());
        }

        [Test]
        public void ToString_EachEventType_StableText()
        {
            ScoreSheet mockup = ScoreSheetTests.MockupMatch();
            var samples = new Dictionary<MatchEvent, string>
            {
                { new MatchStarted("m1", "nepali-standard", Seat.West, TestSetups.Humans().Seats), "MatchStarted match=m1 rules=nepali-standard firstDealer=West [South \"You\" avatar=avatar-0 deck=classic-crimson human] [East \"Suman\" avatar=avatar-1 deck=himalayan-ivory human] [North \"Asha\" avatar=avatar-2 deck=kathmandu-gold human] [West \"Bikash\" avatar=avatar-3 deck=classic-crimson human]" },
                { new RoundStarted(1, Seat.West), "RoundStarted round=1 dealer=West" },
                { new Redealt(2, RedealReason.NoSpade), "Redealt round=2 reason=NoSpade" },
                { new HandDealt(Seat.East, Set("A♠ 2♣")), "HandDealt seat=East hand=A♠ 2♣ [to East]" },
                { new CallTurnStarted(Seat.South, new TurnId(1)), "CallTurnStarted seat=South turn=1" },
                { new CallPlaced(Seat.North, 3, true), "CallPlaced seat=North call=3 auto=1" },
                { new PlayTurnStarted(Seat.West, new TurnId(9), Suit.Hearts), "PlayTurnStarted seat=West turn=9 led=♥" },
                { new PlayTurnStarted(Seat.West, new TurnId(10), null), "PlayTurnStarted seat=West turn=10 led=-" },
                { new LegalHint(Seat.South, new TurnId(5), Set("A♥")), "LegalHint seat=South turn=5 legal=A♥ [to South]" },
                { new CardPlayed(Seat.South, C("A♥"), false, false), "CardPlayed seat=South card=A♥ auto=0 forced=0" },
                { new TrickCompleted(new CompletedTrick(0, Led("9♥ Q♥ 10♥ A♥"))), "TrickCompleted trick=0 South: 9♥ Q♥ 10♥ A♥ winner=West" },
                { new RoundScored(mockup.Rounds[0]), "RoundScored round=1 [South call=4 won=5 delta=41 total=41] [East call=3 won=3 delta=30 total=30] [North call=3 won=3 delta=30 total=30] [West call=3 won=2 delta=-30 total=-30]" },
                { new ReadyConfirmed(Seat.East), "ReadyConfirmed seat=East" },
                { new MatchEnded(Standings.Compute(mockup)), "MatchEnded 1 South total=213 met=5 best=50 | 2 North total=161 met=5 best=40 | 3 East total=120 met=5 best=30 | 4 West total=61 met=4 best=30" },
            };

            foreach (KeyValuePair<MatchEvent, string> sample in samples)
            {
                Assert.AreEqual(sample.Value, sample.Key.ToString());
            }
        }

        [TestCase("ar-SA")]
        [TestCase("sv-SE")]
        [TestCase("tr-TR")]
        public void ToString_WholeMatchUnderAnotherCulture_IdenticalLines(string culture)
        {
            string invariant = CultureRunner.In("", () => string.Join("\n", PlayMatch(67UL)));
            string other = CultureRunner.In(culture, () => string.Join("\n", PlayMatch(67UL)));

            Assert.AreEqual(invariant, other);
            StringAssert.Contains("delta=-", invariant);
        }

        [Test]
        public void Constructors_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new MatchStarted("m", "r", Seat.South, null));
            Assert.Throws<ArgumentException>(() => new MatchStarted("m", "r", Seat.South, TestSetups.Humans().Seats.Take(3).ToList()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RoundStarted(0, Seat.South));
            Assert.Throws<ArgumentNullException>(() => new RoundScored(null));
            Assert.Throws<ArgumentException>(() => new MatchEnded(new Standing[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HandDealt((Seat)4, CardSet.Empty));
        }

        private static List<MatchEvent> PlayMatch(ulong seed)
        {
            var driver = new MatchDriver(new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(seed), TestSetups.Humans()), randomPolicy: true, policySeed: seed);
            driver.Start();
            driver.PlayToEnd();
            return driver.Events;
        }
    }
}
