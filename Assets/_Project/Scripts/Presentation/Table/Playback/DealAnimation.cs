using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Table.Playback
{
    /// <summary>
    /// The deal with Reduce motion (GDD §14 "Deal … Instant, 200 ms fade"; §6 "0.4 s with Reduce
    /// motion"): nothing flies. The dealt table is already laid out when this begins; the hand
    /// and the fans fade in over 200 ms, and the round waits out the rest of its 0.4 s before the
    /// first call turn.
    /// </summary>
    public sealed class DealFade : IEventAnimation
    {
        public const float Seconds = 0.4f;
        public const float FadeSeconds = 0.2f;

        private readonly HandView _hand;
        private readonly OpponentHandView[] _fans;
        private float _elapsed;

        /// <param name="hand">The local hand, already showing the dealt cards.</param>
        /// <param name="fans">The opponents' fans, already showing their backs.</param>
        public DealFade(HandView hand, OpponentHandView[] fans)
        {
            _hand = hand;
            _fans = fans;
        }

        public float Duration => Seconds;

        public void Begin()
        {
            Apply(0f);
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            Apply(Mathf.Clamp01(_elapsed / FadeSeconds));
            return _elapsed >= Seconds;
        }

        public void Finish()
        {
            _elapsed = Seconds;
            Apply(1f);
        }

        private void Apply(float alpha)
        {
            _hand.SetAlpha(alpha);
            for (int i = 0; i < _fans.Length; i++)
            {
                if (_fans[i] != null)
                {
                    _fans[i].SetAlpha(alpha);
                }
            }
        }
    }

    /// <summary>
    /// The deck in the middle of the table while a round is dealt (ADR 022): the cards of the deal,
    /// face down on the felt in the table's perspective, each a hair above the one below. The
    /// shuffle (one of <see cref="ShuffleStyles"/>) puts them in a new order; the deal takes them
    /// from the top, one at a time, so the deck gets smaller as the hands fill.
    /// </summary>
    public sealed class DeckPile
    {
        /// <summary>Height between two cards of the stack, canvas units.</summary>
        public const float Thickness = 0.35f;

        private readonly CardViewPool _pool;
        private readonly List<CardView> _cards;

        /// <param name="pool">The scene's card views.</param>
        /// <param name="area">Where the deck lies (the middle of the table); its centre is the deck's.</param>
        /// <param name="back">The table deck's back.</param>
        /// <param name="count">Cards in the deal (52).</param>
        /// <param name="scale">Card size on the table (that of the trick's cards).</param>
        public DeckPile(CardViewPool pool, RectTransform area, Sprite back, int count, float scale)
        {
            _pool = pool;
            Area = area;
            Scale = scale;
            _cards = new List<CardView>(count);
            for (int i = 0; i < count; i++)
            {
                CardView card = pool.Rent(area);
                card.ShowBack(back);

                // One shadow under the whole deck, not 52 stacked into a blot.
                card.SetShadow(i == 0);
                card.Placement = Resting(i);
                _cards.Add(card);
            }
        }

        public RectTransform Area { get; }

        public float Scale { get; }

        public int Count => _cards.Count;

        /// <summary>The card at <paramref name="index"/> from the bottom.</summary>
        public CardView this[int index] => _cards[index];

        /// <summary>Where the card <paramref name="index"/> from the bottom lies in a squared deck: a hair higher each, and very slightly askew.</summary>
        public CardPlacement Resting(int index)
        {
            float askew = (((index * 37) % 7) - 3) * 0.4f;
            return new CardPlacement(new Vector2(0f, index * Thickness), askew, Scale, 1f);
        }

        /// <summary>Puts the cards in a new order, bottom first (after a riffle), and draws them in it.</summary>
        public void Reorder(IReadOnlyList<CardView> bottomFirst)
        {
            _cards.Clear();
            _cards.AddRange(bottomFirst);
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Rect.SetAsLastSibling();
                _cards[i].SetShadow(i == 0);
            }
        }

        /// <summary>Takes the top card off the deck. It keeps its place on screen and now belongs to the caller.</summary>
        public CardView TakeTop()
        {
            if (_cards.Count == 0)
            {
                return null;
            }

            CardView card = _cards[_cards.Count - 1];
            _cards.RemoveAt(_cards.Count - 1);
            if (_cards.Count > 0)
            {
                _cards[0].SetShadow(true);
            }

            return card;
        }

        /// <summary>The cards not dealt go back to the pool (a skipped deal: the table shows the dealt state itself).</summary>
        public void Clear()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                _pool.Return(_cards[i]);
            }

            _cards.Clear();
        }
    }

    /// <summary>
    /// The shuffle before the deal (ADR 022): the deck appears in the middle of the table, splits
    /// into two halves that slide apart and turn a little, and the halves riffle back together,
    /// one card from each side in turn, to the shuffle's sound; then it squares up. The cards end in
    /// their new order, ready to be dealt from the top. A tap skips it with the deal.
    /// </summary>
    public sealed class ShuffleAnimation : IEventAnimation
    {
        public const float Seconds = 1.1f;

        private const float AppearEnd = 0.15f;
        private const float SplitEnd = 0.4f;
        private const float RiffleEnd = 0.85f;
        private const float DropSeconds = 0.12f;
        private const float Spread = 92f;
        private const float Turn = 7f;
        private const float Lift = 18f;

        private readonly DeckPile _pile;
        private readonly Action _onRiffle;
        private readonly List<CardView> _order = new List<CardView>(52);
        private readonly List<CardPlacement> _split = new List<CardPlacement>(52);
        private float _elapsed;
        private bool _riffled;
        private bool _finished;
        private int _landed;

        /// <param name="pile">The deck in the middle, already laid out.</param>
        /// <param name="onRiffle">Called once as the halves start to riffle together (the shuffle sound).</param>
        public ShuffleAnimation(DeckPile pile, Action onRiffle)
        {
            _pile = pile;
            _onRiffle = onRiffle;
        }

        public float Duration => Seconds;

        public void Begin()
        {
            // The interleaved order: one card from the bottom half, one from the top half, in turn.
            int count = _pile.Count, half = count / 2;
            _order.Clear();
            for (int i = 0; i < half || half + i < count; i++)
            {
                if (i < half)
                {
                    _order.Add(_pile[i]);
                }

                if (half + i < count)
                {
                    _order.Add(_pile[half + i]);
                }
            }

            // Each half's place once it has slid aside: its own little stack, turned a little, with
            // a shadow of its own.
            if (half < count)
            {
                _pile[half].SetShadow(true);
            }

            _split.Clear();
            for (int i = 0; i < count; i++)
            {
                bool left = i < half;
                int inHalf = left ? i : i - half;
                CardPlacement rest = _pile.Resting(inHalf);
                _split.Add(new CardPlacement(rest.Position + new Vector2(left ? -Spread : Spread, 0f), left ? Turn : -Turn, rest.Scale, rest.Flat));
            }

            Apply(0f);
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            Apply(_elapsed);
            if (_elapsed < Seconds)
            {
                return false;
            }

            Finish();
            return true;
        }

        public void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            if (_order.Count == 0)
            {
                Begin();
            }

            _pile.Reorder(_order);
            for (int i = 0; i < _pile.Count; i++)
            {
                _pile[i].SetAlpha(1f);
                _pile[i].Placement = _pile.Resting(i);
            }
        }

        private void Apply(float time)
        {
            int count = _pile.Count;
            if (time < AppearEnd)
            {
                // The deck drops onto the felt as it fades in.
                float t = EaseUtility.Evaluate(time / AppearEnd, Ease.OutQuad);
                for (int i = 0; i < count; i++)
                {
                    CardPlacement rest = _pile.Resting(i);
                    _pile[i].SetAlpha(t);
                    _pile[i].Placement = new CardPlacement(rest.Position + new Vector2(0f, Lift * (1f - t)), rest.Rotation, rest.Scale, rest.Flat);
                }

                return;
            }

            if (time < SplitEnd)
            {
                float t = EaseUtility.Evaluate((time - AppearEnd) / (SplitEnd - AppearEnd), Ease.OutCubic);
                for (int i = 0; i < count; i++)
                {
                    _pile[i].SetAlpha(1f);
                    _pile[i].Placement = CardPlacement.Lerp(_pile.Resting(i), _split[i], t);
                }

                return;
            }

            if (!_riffled)
            {
                _riffled = true;
                _onRiffle?.Invoke();
            }

            // The riffle: card k of the new order drops from its half onto the middle in turn.
            float step = count > 1 ? (RiffleEnd - SplitEnd - DropSeconds) / (count - 1) : 0f;
            for (int k = 0; k < _order.Count; k++)
            {
                CardView card = _order[k];
                int from = IndexInPile(card);
                card.SetAlpha(1f);
                float start = SplitEnd + (k * step);
                if (time < start)
                {
                    card.Placement = _split[from];
                    continue;
                }

                if (k >= _landed)
                {
                    // Each card that starts to fall lands on top of the ones before it.
                    card.Rect.SetAsLastSibling();
                    _landed = k + 1;
                }

                float t = EaseUtility.Evaluate(Mathf.Clamp01((time - start) / DropSeconds), Ease.OutQuad);
                card.Placement = CardPlacement.Lerp(_split[from], _pile.Resting(k), t);
            }
        }

        private int IndexInPile(CardView card)
        {
            for (int i = 0; i < _pile.Count; i++)
            {
                if (_pile[i] == card)
                {
                    return i;
                }
            }

            return 0;
        }
    }

    /// <summary>
    /// The deal (GDD §6, §14 "Deal: cards fly … to each seat, one at a time", about 1.6 s,
    /// OutQuad): after the shuffle, the 52 cards leave the top of the deck in the middle of the
    /// table in turn (ADR 022, where the GDD deals from the dealer's seat), starting at the dealer's
    /// right and going round in the direction of play. A card for an opponent joins that seat's fan
    /// as a back: the fans are laid out for the whole hand while they fill, and each card flies
    /// straight to the place it keeps, though the seat's previous card may still be in the air. A
    /// card for the local seat turns face up in the hand, which fills from the left in sorted order.
    /// Cards land in the order they left. A tap skips it; the table then shows the dealt state at once.
    /// </summary>
    public sealed class DealAnimation : IEventAnimation
    {
        public const float Seconds = 1.6f;
        private const float FlightSeconds = 0.2f;

        /// <summary>Where one seat's cards land.</summary>
        public readonly struct Target
        {
            public Target(OpponentHandView fan, Sprite back)
            {
                Fan = fan;
                Back = back;
            }

            /// <summary>The seat's fan; null for the local seat, whose cards go to the hand.</summary>
            public OpponentHandView Fan { get; }

            public Sprite Back { get; }
        }

        private readonly CardViewPool _pool;
        private readonly HandView _hand;
        private readonly DeckPile _deck;
        private readonly Target[] _targets;
        private readonly int _firstTarget;
        private readonly int _step;
        private readonly IReadOnlyList<Card> _handCards;
        private readonly Func<Card, Sprite> _face;
        private readonly Sprite _cardBase;
        private readonly Action _onCard;
        private readonly List<CardFlight> _flights = new List<CardFlight>(12);
        private readonly List<int> _flightTargets = new List<int>(12);
        private readonly int[] _inAir;
        private readonly int _total;
        private readonly float _interval;
        private float _elapsed;
        private int _launched;
        private int _handDealt;
        private bool _begun;

        /// <param name="pool">The scene's card views (the deck gives back what it does not deal).</param>
        /// <param name="hand">The local hand.</param>
        /// <param name="deck">The shuffled deck in the middle of the table: every card leaves its top.</param>
        /// <param name="targets">The four seats by relative position (0 is the local seat).</param>
        /// <param name="firstTarget">Relative seat that gets the first card (the dealer's right).</param>
        /// <param name="step">+1 when play goes to the next relative seat (anticlockwise), +3 when clockwise.</param>
        /// <param name="handCards">The local hand in display order.</param>
        /// <param name="face">The face sprite of a card in the local deck.</param>
        /// <param name="cardBase">The local deck's base under every face (ADR 053).</param>
        /// <param name="onCard">Called as each card leaves the deck (the deal sound).</param>
        public DealAnimation(
            CardViewPool pool,
            HandView hand,
            DeckPile deck,
            Target[] targets,
            int firstTarget,
            int step,
            IReadOnlyList<Card> handCards,
            Func<Card, Sprite> face,
            Sprite cardBase,
            Action onCard)
        {
            _pool = pool;
            _hand = hand;
            _deck = deck;
            _targets = targets;
            _firstTarget = firstTarget;
            _step = step;
            _handCards = handCards;
            _face = face;
            _cardBase = cardBase;
            _onCard = onCard;
            _inAir = new int[targets.Length];
            _total = handCards.Count * targets.Length;
            _interval = _total > 1 ? (Seconds - FlightSeconds) / (_total - 1) : 0f;
        }

        public float Duration => Seconds;

        /// <summary>The opponents' fans are laid out for the whole hand each will be dealt.</summary>
        public void Begin()
        {
            if (_begun)
            {
                return;
            }

            _begun = true;
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i].Fan != null)
                {
                    _targets[i].Fan.BeginDeal(_handCards.Count);
                }
            }
        }

        public bool Tick(float deltaTime)
        {
            Begin();
            _elapsed += deltaTime;
            while (_launched < _total && _elapsed >= _launched * _interval)
            {
                Launch(_launched);
                _launched++;
            }

            // In the order they left, so each card joins its seat at the place it was flown to.
            for (int i = 0; i < _flights.Count;)
            {
                if (_flights[i].Tick(deltaTime))
                {
                    Land(i);
                }
                else
                {
                    i++;
                }
            }

            bool done = _launched >= _total && _flights.Count == 0;
            if (done)
            {
                _deck.Clear();
            }

            return done;
        }

        public void Finish()
        {
            // Cards in the air land where they were going, in the order they left; the ones not
            // dealt yet go back to the pool, and the table puts the dealt state in place when it
            // shows it.
            while (_flights.Count > 0)
            {
                _flights[0].Finish();
                Land(0);
            }

            _deck.Clear();
            _launched = _total;
        }

        private void Launch(int index)
        {
            int target = (_firstTarget + index * _step) % _targets.Length;
            Target seat = _targets[target];
            RectTransform parent = seat.Fan != null ? seat.Fan.Area : _hand.CardsRoot;

            // The top card of the deck in the middle leaves it where it lies.
            CardView card = _deck.TakeTop();
            if (card != null)
            {
                card.Rect.SetParent(parent, true);
                card.Rect.SetAsLastSibling();
            }
            else
            {
                card = _pool.Rent(parent);
                Vector2 middle = parent.InverseTransformPoint(_deck.Area.position);
                card.Placement = new CardPlacement(middle, 0f, _deck.Scale, 1f);
            }

            card.ShowBack(seat.Back);
            card.SetShadow(false);
            card.SetAlpha(1f);

            // A card for a fan flies to the place it will have there: after the cards the fan holds
            // and those still in the air to it.
            CardPlacement to;
            if (seat.Fan != null)
            {
                to = seat.Fan.PlacementOf(seat.Fan.Count + _inAir[target]);
            }
            else
            {
                to = _hand.PlacementOf(Mathf.Min(_handDealt, _handCards.Count - 1), _handCards.Count);
                _handDealt++;
            }

            var flight = new CardFlight(card, to, FlightSeconds, Ease.OutQuad);
            flight.Begin();
            _flights.Add(flight);
            _flightTargets.Add(target);
            _inAir[target]++;
            _onCard?.Invoke();
        }

        private void Land(int flightIndex)
        {
            CardView card = _flights[flightIndex].Card;
            int target = _flightTargets[flightIndex];
            Target seat = _targets[target];
            _flights.RemoveAt(flightIndex);
            _flightTargets.RemoveAt(flightIndex);
            _inAir[target]--;
            if (seat.Fan != null)
            {
                seat.Fan.Add(card);
                return;
            }

            // The hand fills from the left: the n-th card to land is the n-th card of the sorted hand.
            int index = _hand.Count;
            if (index < _handCards.Count)
            {
                Card dealt = _handCards[index];
                card.ShowFace(dealt, _face(dealt), _cardBase);
                card.SetShadow(true);
                _hand.Insert(card);
            }
            else
            {
                _pool.Return(card);
            }
        }
    }
}
