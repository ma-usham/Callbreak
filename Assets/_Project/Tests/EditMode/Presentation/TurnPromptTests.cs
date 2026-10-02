using System.Collections.Generic;
using Callbreak.Application.Sessions;
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
                SeatStatus.WaitingKey, SeatStatus.CallingKey, SeatStatus.ChoosingBidKey, SeatStatus.WonBidKey,
                SeatStatus.AwayKey, SeatStatus.ReconnectingKey, SeatStatus.BotPlayingKey,
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
            // Before the call, the words; from the call on, only "won-bid" (the owner, 3 Oct 2026).
            Assert.AreEqual("Waiting", Status(false, SeatConnection.Connected, false, CallRules.NotCalled, false, false, 0));
            Assert.AreEqual("Calling…", Status(false, SeatConnection.Connected, true, CallRules.NotCalled, false, false, 0));
            Assert.AreEqual("Choosing bid", Status(true, SeatConnection.Connected, true, CallRules.NotCalled, false, false, 0), "mockup 07, the local seat");
            Assert.AreEqual("0-3", Status(false, SeatConnection.Connected, false, 3, false, false, 0), "nothing won yet, bid 3");
            Assert.AreEqual("0-3", Status(false, SeatConnection.Connected, false, 3, true, false, 0), "a call the timer placed reads the same");
            Assert.AreEqual("1-3", Status(false, SeatConnection.Connected, false, 3, false, true, 1));
            Assert.AreEqual("2-4", Status(true, SeatConnection.Connected, false, 4, false, true, 2), "the local seat");
            Assert.AreEqual("1-3", Status(false, SeatConnection.Connected, false, 3, true, true, 1));
            Assert.AreEqual("0-8", Status(false, SeatConnection.Connected, false, 8, false, true, 0));
            Assert.AreEqual("13-8", Status(false, SeatConnection.Connected, false, 8, false, true, 13));
        }

        [Test]
        public void StatusPill_TurnsGold_OnceTheCallIsMet()
        {
            var short1 = new SeatStatusInfo(false, SeatConnection.Connected, false, 3, false, true, 2);
            var met = new SeatStatusInfo(false, SeatConnection.Connected, false, 3, false, true, 3);
            var over = new SeatStatusInfo(false, SeatConnection.Connected, false, 3, false, true, 5);
            var justCalled = new SeatStatusInfo(false, SeatConnection.Connected, false, 1, false, false, 0);
            Assert.IsFalse(SeatStatus.IsMet(short1));
            Assert.IsTrue(SeatStatus.IsMet(met));
            Assert.IsTrue(SeatStatus.IsMet(over));
            Assert.IsFalse(SeatStatus.IsMet(justCalled));

            // The gold is the sign: the words stay "won-bid".
            Assert.AreEqual("2-3", SeatStatus.Text(short1, _text));
            Assert.AreEqual("3-3", SeatStatus.Text(met, _text));
            Assert.AreEqual("5-3", SeatStatus.Text(over, _text));
        }

        private static readonly SeatConnection[] NotAtTheTable =
        {
            SeatConnection.Away, SeatConnection.Reconnecting, SeatConnection.BotControlled, SeatConnection.Left,
        };

        [Test]
        public void StatusPill_KeepsItsLine_WhenTheSeatIsNotPlayingItself()
        {
            // What a seat called and has won decides how the others play (GDD 11 screen 08, callout 3):
            // the line stays when the player is away, reconnecting or gone, and a bot plays on.
            foreach (SeatConnection connection in NotAtTheTable)
            {
                string seat = connection.ToString();
                Assert.AreEqual("Waiting", Status(false, connection, false, CallRules.NotCalled, false, false, 0), seat);
                Assert.AreEqual("Calling…", Status(false, connection, true, CallRules.NotCalled, false, false, 0), seat);
                Assert.AreEqual("0-3", Status(false, connection, false, 3, false, false, 0), seat);
                Assert.AreEqual("0-3", Status(false, connection, false, 3, true, false, 0), seat);
                Assert.AreEqual("1-3", Status(false, connection, false, 3, false, true, 1), seat);
                Assert.AreEqual("4-3", Status(false, connection, false, 3, false, true, 4), seat + ": what was won counts, whoever played the cards");
            }
        }

        [Test]
        public void ConnectionChip_SaysWhatTheOthersSee()
        {
            // GDD 8 "What others see": its own short chip beside the pill, not in place of the line.
            // ADR 025: a player who is there has no chip, whether a person or a bot that filled the seat.
            Assert.IsNull(SeatStatus.Chip(SeatConnection.Connected, _text), "a player who is there has no chip");
            Assert.AreEqual("Away", SeatStatus.Chip(SeatConnection.Away, _text));
            Assert.AreEqual("Reconnecting…", SeatStatus.Chip(SeatConnection.Reconnecting, _text));
            Assert.AreEqual("Bot playing", SeatStatus.Chip(SeatConnection.BotControlled, _text), "not back in time: a bot took the seat over");

            // The owner (3 Oct 2026): a player who left on purpose, a host who left and handed the
            // table over too, leaves no chip; a bot plays the seat on silently.
            Assert.IsNull(SeatStatus.Chip(SeatConnection.Left, _text), "a player who left has no chip");

            // Only "Reconnecting…" has the spinner.
            Assert.IsTrue(SeatStatus.ChipIsBusy(SeatConnection.Reconnecting));
            foreach (SeatConnection connection in new[] { SeatConnection.Connected, SeatConnection.Away, SeatConnection.BotControlled, SeatConnection.Left })
            {
                Assert.IsFalse(SeatStatus.ChipIsBusy(connection), connection.ToString());
            }
        }

        [Test]
        public void StatusPill_IsNotGold_WhileTheSeatIsNotPlayingItself()
        {
            // A seat that met its call and then went away keeps its line, but not the gold.
            foreach (SeatConnection connection in NotAtTheTable)
            {
                var gone = new SeatStatusInfo(false, connection, false, 3, false, true, 4);
                Assert.IsFalse(SeatStatus.IsMet(gone), connection.ToString());
            }

            Assert.IsTrue(SeatStatus.IsMet(new SeatStatusInfo(true, SeatConnection.Connected, false, 3, false, true, 4)));
        }

        // ------------------------------------------------------------------ hand order

        private static string Sorted(string hand)
        {
            var cards = new List<Card>();
            HandSorter.Sort(CardSet.Parse(hand), cards);
            return string.Join(" ", cards);
        }

        [Test]
        public void Hand_IsSpadesHeartsClubsDiamonds_ThenHighToLow()
        {
            // The owner's one order (ADR 021): the colours alternate, so the table has no Sort button.
            Assert.AreEqual(
                "A♠ K♠ 9♠ 6♠ 2♠ A♥ 8♥ 4♥ 7♣ 3♣ K♦ 5♦ 2♦",
                Sorted("3♣ 7♣ 2♦ 5♦ K♦ 4♥ 8♥ A♥ 2♠ 6♠ 9♠ K♠ A♠"));
        }

        [Test]
        public void Hand_PutsClubsBeforeDiamonds_WhateverIsMissing()
        {
            Assert.AreEqual("Q♣ 4♦", Sorted("4♦ Q♣"));
            Assert.AreEqual("2♠ A♥ 3♦", Sorted("3♦ A♥ 2♠"));
            Assert.AreEqual("J♥ 5♣", Sorted("5♣ J♥"));
        }

        [Test]
        public void Sort_HandlesEmptyAndSingleSuitHands()
        {
            Assert.AreEqual(string.Empty, Sorted("-"));
            Assert.AreEqual("K♥ 10♥ 2♥", Sorted("2♥ K♥ 10♥"));
        }
    }
}
