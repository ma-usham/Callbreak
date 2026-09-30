using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Match
{
    public sealed class PlayerViewTests
    {
        [Test]
        public void ViewFor_EachSeat_OwnHandAndOnlyCountsForOthers()
        {
            MatchEngine engine = StartOrdinary();

            for (int s = 0; s < 4; s++)
            {
                PlayerView view = engine.ViewFor((Seat)s);
                Assert.AreEqual((Seat)s, view.Viewer);
                Assert.AreEqual(MatchEngineStartTests.OrdinaryHands[s], view.Hand);
                for (int other = 0; other < 4; other++)
                {
                    Assert.AreEqual(13, view.HandCountOf((Seat)other));
                }
            }
        }

        [Test]
        public void ViewFor_EveryReachableField_HoldsOnlyOwnAndPlayedCards()
        {
            var driver = new MatchDriver(new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(41UL), TestSetups.Humans(Seat.West)), randomPolicy: true);
            driver.Start();
            int checkedViews = 0;
            do
            {
                for (int s = 0; s < 4; s++)
                {
                    PlayerView view = driver.Engine.ViewFor((Seat)s);
                    CardSet reachable = ReachableCards(view);
                    CardSet played = view.CurrentTrick.Cards;
                    foreach (CompletedTrick trick in view.CompletedTricks)
                    {
                        played |= trick.Trick.Cards;
                    }

                    Assert.IsTrue(reachable.IsSubsetOf(view.Hand | played), "view of " + (Seat)s + " exposes " + reachable.Except(view.Hand | played));
                    Assert.IsTrue(view.Hand.IsSubsetOf(reachable), "the walk must at least find the viewer's own hand");
                    checkedViews++;
                }
            }
            while (driver.Step());

            Assert.Greater(checkedViews, 1000);
        }

        [Test]
        public void ViewFor_DealsDifferingOnlyInOtherHands_BystanderViewsIdentical()
        {
            CardSet[] swapped = MatchEngineStartTests.OrdinaryHands;
            swapped[(int)Seat.South] = swapped[(int)Seat.South].Without(C("3♣")).With(C("5♦"));
            swapped[(int)Seat.North] = swapped[(int)Seat.North].Without(C("5♦")).With(C("3♣"));
            MatchEngine original = StartOrdinary();
            var variant = new MatchEngine(RuleSet.NepaliStandard, RiggedDeal.Random(swapped, Seat.West, Direction.Anticlockwise), TestSetups.Humans(Seat.West));
            variant.Start();

            Assert.AreEqual(original.ViewFor(Seat.East), variant.ViewFor(Seat.East));
            Assert.AreEqual(original.ViewFor(Seat.West), variant.ViewFor(Seat.West));
            Assert.AreNotEqual(original.ViewFor(Seat.South), variant.ViewFor(Seat.South));
        }

        [Test]
        public void LegalCardsAndObligation_OnlyOnTheViewersPlayTurn()
        {
            MatchEngine engine = StartOrdinary();
            CallAll(engine, 3);
            engine.Execute(new PlayCard(Seat.South, C("A♣"), engine.CurrentTurn));

            PlayerView east = engine.ViewFor(Seat.East);
            PlayerView north = engine.ViewFor(Seat.North);

            Assert.IsTrue(east.IsMyTurn);
            Assert.AreEqual(Set("2♣"), east.LegalCards());
            Assert.IsTrue(east.TryGetObligation(out PlayObligation obligation));
            Assert.AreEqual(ObligationKind.FollowSuit, obligation.Kind);
            Assert.AreEqual(Suit.Clubs, obligation.Suit);
            Assert.IsFalse(north.IsMyTurn);
            Assert.AreEqual(CardSet.Empty, north.LegalCards());
            Assert.IsFalse(north.TryGetObligation(out _));
        }

        [Test]
        public void LegalCards_CallTurn_Empty()
        {
            PlayerView south = StartOrdinary().ViewFor(Seat.South);

            Assert.IsTrue(south.IsMyTurn);
            Assert.AreEqual(Phase.Calling, south.Phase);
            Assert.AreEqual(CardSet.Empty, south.LegalCards());
        }

        [Test]
        public void Calls_BeforeAndAfterCalling_NotCalledIsDistinguishable()
        {
            MatchEngine engine = StartOrdinary();
            engine.Execute(new PlaceCall(Seat.South, 4, engine.CurrentTurn));

            PlayerView view = engine.ViewFor(Seat.North);

            Assert.IsTrue(view.HasCalled(Seat.South));
            Assert.AreEqual(4, view.CallOf(Seat.South));
            Assert.IsFalse(view.HasCalled(Seat.East));
            Assert.AreEqual(CallRules.NotCalled, view.CallOf(Seat.East));
        }

        [Test]
        public void PreviousTrick_AfterFirstTrick_LeaderCardsAndWinner()
        {
            MatchEngine engine = StartOrdinary();
            CallAll(engine, 3);
            Assert.IsFalse(engine.ViewFor(Seat.West).TryGetPreviousTrick(out _));
            foreach (string card in new[] { "A♣", "2♣", "Q♠", "2♦" })
            {
                engine.Execute(new PlayCard(engine.CurrentSeat, C(card), engine.CurrentTurn));
            }

            PlayerView view = engine.ViewFor(Seat.West);

            Assert.IsTrue(view.TryGetPreviousTrick(out CompletedTrick previous));
            Assert.AreEqual(0, previous.Index);
            Assert.AreEqual(Seat.South, previous.Leader);
            Assert.AreEqual(Seat.North, previous.Winner);
            Assert.AreEqual(C("Q♠"), previous.WinningCard);
            Assert.AreEqual(1, view.CompletedTricks.Count);
            Assert.AreEqual(1, view.TrickIndex);
            Assert.AreEqual(Seat.North, view.CurrentTrick.Leader);
            Assert.IsTrue(view.CurrentTrick.IsEmpty);
        }

        [Test]
        public void View_TakenEarlier_UnaffectedByLaterCommands()
        {
            MatchEngine engine = StartOrdinary();
            PlayerView before = engine.ViewFor(Seat.South);
            string text = before.ToString();

            CallAll(engine, 3);
            engine.Execute(new PlayCard(Seat.South, C("A♣"), engine.CurrentTurn));

            Assert.AreEqual(text, before.ToString());
            Assert.AreEqual(13, before.Hand.Count);
            Assert.IsFalse(before.HasCalled(Seat.South));
            Assert.AreEqual(Phase.Calling, before.Phase);
        }

        [Test]
        public void Equals_SameStateSameViewer_EqualOtherwiseNot()
        {
            MatchEngine engine = StartOrdinary();

            Assert.AreEqual(engine.ViewFor(Seat.East), engine.ViewFor(Seat.East));
            Assert.AreEqual(engine.ViewFor(Seat.East).GetHashCode(), engine.ViewFor(Seat.East).GetHashCode());
            Assert.AreNotEqual(engine.ViewFor(Seat.East), engine.ViewFor(Seat.West));
            PlayerView beforeCall = engine.ViewFor(Seat.East);
            engine.Execute(new PlaceCall(Seat.South, 3, engine.CurrentTurn));
            Assert.AreNotEqual(beforeCall, engine.ViewFor(Seat.East));
        }

        [Test]
        public void ViewFor_UndefinedSeat_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StartOrdinary().ViewFor((Seat)4));
        }

        [Test]
        public void Constructor_HandNotMatchingItsCount_Throws()
        {
            PlayerView view = StartOrdinary().ViewFor(Seat.South);

            Assert.Throws<ArgumentException>(() => new PlayerView(
                view.Viewer, view.Rules, view.MatchId, view.Seats, view.Phase, view.Round, view.Dealer, view.FirstDealer,
                view.CurrentSeat, view.CurrentTurn, view.Hand.Without(view.Hand.Lowest()),
                new[] { -1, -1, -1, -1 }, new[] { 0, 0, 0, 0 }, new[] { 13, 13, 13, 13 }, new[] { false, false, false, false },
                view.CurrentTrick, view.CompletedTricks, view.Scores, view.FinalStandings));
        }

        [Test]
        public void Seats_ProfilesFromTheSetup()
        {
            PlayerView view = StartOrdinary().ViewFor(Seat.South);

            Assert.AreEqual("Bikash", view.Seats[(int)Seat.West].Name);
            Assert.AreEqual("himalayan-ivory", view.Seats[(int)Seat.East].DeckThemeId);
            Assert.IsFalse(view.Seats[(int)Seat.North].IsBot);
            Assert.AreEqual("test-match", view.MatchId);
        }

        private static MatchEngine StartOrdinary()
        {
            var engine = new MatchEngine(
                RuleSet.NepaliStandard,
                RiggedDeal.Random(MatchEngineStartTests.OrdinaryHands, Seat.West, Direction.Anticlockwise),
                TestSetups.Humans(Seat.West));
            engine.Start();
            return engine;
        }

        private static void CallAll(MatchEngine engine, int call)
        {
            for (int i = 0; i < 4; i++)
            {
                engine.Execute(new PlaceCall(engine.CurrentSeat, call, engine.CurrentTurn));
            }
        }

        /// <summary>Every card value reachable from <paramref name="root"/> through any field, public or private.</summary>
        private static CardSet ReachableCards(object root)
        {
            CardSet found = CardSet.Empty;
            var visited = new HashSet<object>(new ReferenceComparer());
            var pending = new Stack<object>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                object current = pending.Pop();
                if (current == null)
                {
                    continue;
                }

                Type type = current.GetType();
                Assert.IsFalse(type.Name == "MatchEngine" || type.Name == "MatchState" || type == typeof(MatchSnapshot), "a view must not reference " + type.Name);
                if (current is Card card)
                {
                    found = found.With(card);
                    continue;
                }

                if (current is CardSet set)
                {
                    found |= set;
                    continue;
                }

                if (current is Trick trick)
                {
                    found |= trick.Cards;
                    continue;
                }

                if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type.IsPointer)
                {
                    continue;
                }

                if (!type.IsValueType && !visited.Add(current))
                {
                    continue;
                }

                if (current is Array array)
                {
                    foreach (object item in array)
                    {
                        pending.Push(item);
                    }

                    continue;
                }

                for (Type level = type; level != null; level = level.BaseType)
                {
                    foreach (FieldInfo field in level.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        pending.Push(field.GetValue(current));
                    }
                }
            }

            return found;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
