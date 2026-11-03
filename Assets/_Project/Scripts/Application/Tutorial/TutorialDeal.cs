using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;

namespace Callbreak.Application.Tutorial
{
    /// <summary>
    /// The first hand of the first practice match, scripted for the tutorial (ADR 084). The player
    /// deals, so the seat on their right calls and leads first, and the player calls last and plays
    /// last to the first trick. Trick 1: hearts are led and the player follows with a low heart;
    /// the bot on their left wins it with the ace. Trick 2: that bot leads the ace of diamonds, the
    /// player has none and trumps with a low spade, and wins. From then on the hand plays out with
    /// the match's own bots. Every shuffle the engine makes is still its own draw: only the first
    /// one is given (<see cref="Random"/>), the others come from the match's generator.
    /// </summary>
    public sealed class TutorialDeal
    {
        private readonly CardSet[] _hands;
        private readonly int[] _calls;
        private readonly Card[][] _plays;
        private readonly Card[] _playerCards;

        /// <param name="dealer">Deals the hand; the seat after it calls and leads first.</param>
        /// <param name="hands">Four hands of 13 by seat number.</param>
        /// <param name="calls">The bots' calls by seat number (the player's is ignored).</param>
        /// <param name="plays">Per guided trick, the card each bot seat plays, by seat number.</param>
        /// <param name="playerCards">Per guided trick, the card the tutorial points at in the player's hand.</param>
        /// <param name="suggestedCall">The call the tutorial points at.</param>
        public TutorialDeal(Seat dealer, CardSet[] hands, int[] calls, Card[][] plays, Card[] playerCards, int suggestedCall)
        {
            if (hands == null || hands.Length != SeatExtensions.Count)
            {
                throw new ArgumentException("A deal needs four hands.", nameof(hands));
            }

            if (plays == null || playerCards == null || plays.Length != playerCards.Length)
            {
                throw new ArgumentException("Every guided trick needs the bots' cards and the player's.", nameof(plays));
            }

            Dealer = dealer;
            _hands = (CardSet[])hands.Clone();
            _calls = (int[])calls.Clone();
            _plays = (Card[][])plays.Clone();
            _playerCards = (Card[])playerCards.Clone();
            SuggestedCall = suggestedCall;
        }

        /// <summary>The hand every first practice match starts with; the player sits South.</summary>
        public static TutorialDeal First { get; } = new TutorialDeal(
            Seat.South,
            new[]
            {
                CardSet.Parse("A♠ K♠ Q♠ 7♠ 3♠ 7♥ 4♥ A♣ K♣ 9♣ 6♣ 5♣ 2♣"),
                CardSet.Parse("J♠ 8♠ 4♠ 10♥ 9♥ 5♥ 2♥ Q♦ 8♦ 5♦ 2♦ Q♣ 4♣"),
                CardSet.Parse("10♠ 2♠ Q♥ J♥ 6♥ 3♥ J♦ 10♦ 7♦ 4♦ J♣ 10♣ 3♣"),
                CardSet.Parse("9♠ 6♠ 5♠ A♥ K♥ 8♥ A♦ K♦ 9♦ 6♦ 3♦ 8♣ 7♣"),
            },
            new[] { 0, 2, 1, 3 },
            new[]
            {
                // East leads a heart, North and West each beat it; West's ace takes the trick.
                new[] { default, Card.Parse("10♥"), Card.Parse("Q♥"), Card.Parse("A♥") },

                // West leads the ace of diamonds; East and North follow with low diamonds.
                new[] { default, Card.Parse("5♦"), Card.Parse("4♦"), Card.Parse("A♦") },
            },
            new[] { Card.Parse("4♥"), Card.Parse("3♠") },
            suggestedCall: 3);

        public Seat Dealer { get; }

        /// <summary>The call the tutorial suggests: what this hand can comfortably win.</summary>
        public int SuggestedCall { get; }

        /// <summary>How many tricks the tutorial guides; the rest of the hand is played freely.</summary>
        public int GuidedTricks => _plays.Length;

        public CardSet HandOf(Seat seat) => _hands[(int)seat];

        /// <summary>A bot's scripted call for the first hand.</summary>
        public bool TryGetCall(Seat seat, out int call)
        {
            call = _calls[(int)seat];
            return call > 0;
        }

        /// <summary>A bot's scripted card for a guided trick (0-based); false once the guided tricks are over.</summary>
        public bool TryGetPlay(Seat seat, int trickIndex, out Card card)
        {
            card = default;
            if (trickIndex < 0 || trickIndex >= _plays.Length || seat == Seat.South)
            {
                return false;
            }

            card = _plays[trickIndex][(int)seat];
            return _hands[(int)seat].Contains(card);
        }

        /// <summary>The card the tutorial points at in the player's hand during a guided trick.</summary>
        public Card PlayerCard(int trickIndex) => _playerCards[trickIndex];

        /// <summary>
        /// The match's randomness for a match that starts with this hand (dealt by
        /// <see cref="Dealer"/>, which the match setup fixes): the first shuffle deals these
        /// hands, and every draw after it comes from <paramref name="then"/>.
        /// </summary>
        public IRandom Random(RuleSet rules, IRandom then)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return new ScriptedRandom(ShuffleDrawsFor(DeckOrder(Dealer.Next(rules.Direction), rules.Direction)), then);
        }

        /// <summary>The deck, top first, that <see cref="Deck.Deal"/> hands out as these hands, starting at <paramref name="first"/>.</summary>
        private Card[] DeckOrder(Seat first, Direction direction)
        {
            var queues = new Queue<Card>[SeatExtensions.Count];
            for (int s = 0; s < queues.Length; s++)
            {
                queues[s] = new Queue<Card>();
                foreach (Card card in _hands[s])
                {
                    queues[s].Enqueue(card);
                }
            }

            var order = new Card[Deck.Size];
            Seat seat = first;
            for (int k = 0; k < order.Length; k++)
            {
                order[k] = queues[(int)seat].Dequeue();
                seat = seat.Next(direction);
            }

            return order;
        }

        /// <summary>The draws that make <see cref="Deck.Shuffled"/>'s Fisher–Yates put the deck in <paramref name="order"/>.</summary>
        private static int[] ShuffleDrawsFor(IReadOnlyList<Card> order)
        {
            var deck = new int[Deck.Size];
            for (int i = 0; i < deck.Length; i++)
            {
                deck[i] = i;
            }

            var draws = new int[Deck.Size - 1];
            for (int i = Deck.Size - 1; i > 0; i--)
            {
                int j = Array.IndexOf(deck, order[i].Id, 0, i + 1);
                draws[Deck.Size - 1 - i] = j;
                deck[j] = deck[i];
                deck[i] = order[i].Id;
            }

            return draws;
        }

        /// <summary>Gives the scripted draws, then hands every later draw to the match's generator.</summary>
        private sealed class ScriptedRandom : IRandom
        {
            private readonly int[] _draws;
            private readonly IRandom _then;
            private int _next;

            public ScriptedRandom(int[] draws, IRandom then)
            {
                _draws = draws;
                _then = then ?? throw new ArgumentNullException(nameof(then));
            }

            public int NextInt(int maxExclusive)
            {
                return _next < _draws.Length ? _draws[_next++] : _then.NextInt(maxExclusive);
            }
        }
    }
}
