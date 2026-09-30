using System.Collections.Generic;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Presentation.Table;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>GDD §11 screen 08 "TURN PROMPT COPY", the status pill's copy and the hand's order.</summary>
    public sealed class TurnPromptTests
    {
        private static readonly RuleSet Rules = RuleSet.NepaliStandard;
        private readonly TableStrings _text = new TableStrings();

        private static Trick TrickOf(Seat leader, params string[] cards)
        {
            var trick = new Trick(leader, Rules.Direction);
            foreach (string card in cards)
            {
                trick = trick.With(Card.Parse(card));
            }

            return trick;
        }

        private string Prompt(string hand, Trick trick, out CardSet highlighted)
        {
            PlayObligation obligation = LegalMoves.Classify(CardSet.Parse(hand), trick, Rules);
            highlighted = obligation.Legal;
            return TurnPrompt.ForObligation(obligation, _text);
        }

        [Test]
        public void YouLead_AnyCard_AllHighlighted()
        {
            string prompt = Prompt("A♠ K♠ 9♥ 3♣", TrickOf(Seat.South), out CardSet legal);
            Assert.AreEqual("Your turn · Lead any card", prompt);
            Assert.AreEqual(CardSet.Parse("A♠ K♠ 9♥ 3♣"), legal);
        }

        [Test]
        public void Following_CanBeat_NamesTheCardToBeat_Mockup08()
        {
            // Suman led 9♥, Asha played Q♥, Bikash 10♥: only A♥ beats the queen.
            string prompt = Prompt("A♠ K♠ 9♠ 6♠ 2♠ A♥ 8♥ 4♥ K♦ 5♦ 2♦ 7♣ 3♣", TrickOf(Seat.East, "9♥", "Q♥", "10♥"), out CardSet legal);
            Assert.AreEqual("Your turn · Beat Q♥", prompt);
            Assert.AreEqual(CardSet.Parse("A♥"), legal);
        }

        [Test]
        public void Following_CannotBeat_FollowTheSuit()
        {
            string prompt = Prompt("A♠ 8♥ 4♥ K♦", TrickOf(Seat.East, "Q♥"), out CardSet legal);
            Assert.AreEqual("Your turn · Follow ♥", prompt);
            Assert.AreEqual(CardSet.Parse("8♥ 4♥"), legal);
        }

        [Test]
        public void Void_CanTrump_WithASpade()
        {
            string prompt = Prompt("A♠ 2♠ K♦ 3♣", TrickOf(Seat.East, "Q♥"), out CardSet legal);
            Assert.AreEqual("Your turn · Trump with a spade", prompt);
            Assert.AreEqual(CardSet.Parse("A♠ 2♠"), legal);
        }

        [Test]
        public void Void_CannotWin_AnyCard()
        {
            string prompt = Prompt("K♦ 5♦ 3♣", TrickOf(Seat.East, "Q♥"), out CardSet legal);
            Assert.AreEqual("Your turn · Any card", prompt);
            Assert.AreEqual(CardSet.Parse("K♦ 5♦ 3♣"), legal);
        }

        [Test]
        public void OthersTurns_AndTheTrickWinner()
        {
            Assert.AreEqual("Waiting for Asha…", TurnPrompt.WaitingFor("Asha", _text));
            Assert.AreEqual("Suman wins the trick", TurnPrompt.TrickWon("Suman", false, _text));
            Assert.AreEqual("You win the trick", TurnPrompt.TrickWon("You", true, _text));
            Assert.AreEqual("You lead", TurnPrompt.YouLead(_text));
            Assert.AreEqual("Asha is calling…", TurnPrompt.Calling("Asha", _text));
        }

        [Test]
        public void CardLabels_ReadLikeTheCardIndex()
        {
            Assert.AreEqual("Q♥", TurnPrompt.CardLabel(Card.Parse("Q♥")));
            Assert.AreEqual("10♠", TurnPrompt.CardLabel(Card.Parse("10♠")));
            Assert.AreEqual("A♣", TurnPrompt.CardLabel(Card.Parse("A♣")));
            Assert.AreEqual("2♦", TurnPrompt.CardLabel(Card.Parse("2♦")));
        }

        [Test]
        public void EveryPromptKey_IsInTheStringTable()
        {
            foreach (string key in new[]
            {
                TurnPrompt.LeadAnyKey, TurnPrompt.BeatKey, TurnPrompt.FollowKey, TurnPrompt.TrumpKey, TurnPrompt.AnyCardKey,
                TurnPrompt.WaitingForKey, TurnPrompt.WinsTrickKey, TurnPrompt.YouWinTrickKey, TurnPrompt.YouLeadKey, TurnPrompt.CallingKey,
                SeatStatus.WaitingKey, SeatStatus.CallingKey, SeatStatus.ChoosingBidKey, SeatStatus.BidKey, SeatStatus.BidAutoKey,
                SeatStatus.BidWonKey, SeatStatus.BidWonMetKey, SeatStatus.AwayKey, SeatStatus.ReconnectingKey, SeatStatus.BotPlayingKey,
            })
            {
                Assert.IsTrue(_text.Has(key), key);
            }
        }

        // ------------------------------------------------------------------ status pill

        private string Status(bool local, SeatConnection connection, bool calling, int call, bool auto, bool playing, int won)
        {
            return SeatStatus.Text(new SeatStatusInfo(local, connection, calling, call, auto, playing, won), _text);
        }

        [Test]
        public void StatusPill_FollowsTheRound()
        {
            Assert.AreEqual("Waiting", Status(false, SeatConnection.Connected, false, CallRules.NotCalled, false, false, 0));
            Assert.AreEqual("Calling…", Status(false, SeatConnection.Connected, true, CallRules.NotCalled, false, false, 0));
            Assert.AreEqual("Choosing bid", Status(true, SeatConnection.Connected, true, CallRules.NotCalled, false, false, 0), "mockup 07, the local seat");
            Assert.AreEqual("Bid 3", Status(false, SeatConnection.Connected, false, 3, false, false, 0));
            Assert.AreEqual("Bid 3 · auto", Status(false, SeatConnection.Connected, false, 3, true, false, 0));
            Assert.AreEqual("Bid 3 · Won 1", Status(false, SeatConnection.Connected, false, 3, false, true, 1));
            Assert.AreEqual("Bid 4 · Won 2", Status(true, SeatConnection.Connected, false, 4, false, true, 2), "mockup 08, the local seat");
            Assert.AreEqual("Bid 3 · Won 1", Status(false, SeatConnection.Connected, false, 3, true, true, 1), "the auto tag goes once play starts");
        }

        [Test]
        public void StatusPill_TurnsGoldWithATick_OnceTheCallIsMet()
        {
            var short1 = new SeatStatusInfo(false, SeatConnection.Connected, false, 3, false, true, 2);
            var met = new SeatStatusInfo(false, SeatConnection.Connected, false, 3, false, true, 3);
            var over = new SeatStatusInfo(false, SeatConnection.Connected, false, 3, false, true, 5);
            var justCalled = new SeatStatusInfo(false, SeatConnection.Connected, false, 1, false, false, 0);
            Assert.IsFalse(SeatStatus.IsMet(short1));
            Assert.IsTrue(SeatStatus.IsMet(met));
            Assert.IsTrue(SeatStatus.IsMet(over));
            Assert.IsFalse(SeatStatus.IsMet(justCalled));
            Assert.AreEqual("Bid 3 · Won 3 ✓", SeatStatus.Text(met, _text));
            Assert.AreEqual("Bid 3 · Won 5 ✓", SeatStatus.Text(over, _text));
        }

        [Test]
        public void StatusPill_ShowsTheConnectionChip_WhenTheSeatIsNotPlayingItself()
        {
            Assert.AreEqual("Away", Status(false, SeatConnection.Away, false, 3, false, true, 1));
            Assert.AreEqual("Reconnecting…", Status(false, SeatConnection.Reconnecting, true, CallRules.NotCalled, false, false, 0));
            Assert.AreEqual("Bot playing", Status(false, SeatConnection.BotControlled, false, 3, false, true, 1));
            Assert.AreEqual("Bot playing", Status(false, SeatConnection.Left, false, 3, false, true, 1));
        }

        [Test]
        public void StatusPill_IsGoldOnlyWhileItShowsTheMetCall()
        {
            // A seat that met its call and then went away shows the chip, not a gold "Away".
            foreach (SeatConnection connection in new[] { SeatConnection.Away, SeatConnection.Reconnecting, SeatConnection.BotControlled, SeatConnection.Left })
            {
                var gone = new SeatStatusInfo(false, connection, false, 3, false, true, 4);
                Assert.IsFalse(SeatStatus.IsMet(gone), connection.ToString());
            }

            Assert.IsTrue(SeatStatus.IsMet(new SeatStatusInfo(true, SeatConnection.Connected, false, 3, false, true, 4)));
        }

        // ------------------------------------------------------------------ hand order

        private static string Sorted(string hand, HandSortOrder order)
        {
            var cards = new List<Card>();
            HandSorter.Sort(CardSet.Parse(hand), order, cards);
            return string.Join(" ", cards);
        }

        [Test]
        public void Hand_SortsBySuitThenHighToLow_AsMockup08()
        {
            Assert.AreEqual(
                "A♠ K♠ 9♠ 6♠ 2♠ A♥ 8♥ 4♥ K♦ 5♦ 2♦ 7♣ 3♣",
                Sorted("3♣ 7♣ 2♦ 5♦ K♦ 4♥ 8♥ A♥ 2♠ 6♠ 9♠ K♠ A♠", HandSortOrder.BySuit));
        }

        [Test]
        public void Hand_AlternatingColours_PutsClubsBetweenTheRedSuits()
        {
            Assert.AreEqual(
                "A♠ K♠ 9♠ 6♠ 2♠ A♥ 8♥ 4♥ 7♣ 3♣ K♦ 5♦ 2♦",
                Sorted("3♣ 7♣ 2♦ 5♦ K♦ 4♥ 8♥ A♥ 2♠ 6♠ 9♠ K♠ A♠", HandSortOrder.AlternatingColours));
            Assert.AreEqual(HandSortOrder.AlternatingColours, HandSorter.Toggle(HandSortOrder.BySuit));
            Assert.AreEqual(HandSortOrder.BySuit, HandSorter.Toggle(HandSortOrder.AlternatingColours));
        }

        [Test]
        public void Sort_HandlesEmptyAndSingleSuitHands()
        {
            Assert.AreEqual(string.Empty, Sorted("-", HandSortOrder.BySuit));
            Assert.AreEqual("K♥ 10♥ 2♥", Sorted("2♥ K♥ 10♥", HandSortOrder.AlternatingColours));
        }
    }
}
