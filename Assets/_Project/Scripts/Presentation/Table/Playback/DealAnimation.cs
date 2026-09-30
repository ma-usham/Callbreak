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
    /// The deal (GDD §6, §14 "Deal: cards fly from the dealer to each seat, one at a time", about
    /// 1.6 s, OutQuad): 52 cards leave the dealer's seat in turn, starting at the dealer's right and
    /// going round in the direction of play. A card for an opponent joins that seat's fan as a back;
    /// a card for the local seat turns face up in the hand, which fills from the left in sorted
    /// order. A tap skips it; the table then shows the dealt state at once.
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
        private readonly RectTransform _dealer;
        private readonly Target[] _targets;
        private readonly int _firstTarget;
        private readonly int _step;
        private readonly IReadOnlyList<Card> _handCards;
        private readonly Func<Card, Sprite> _face;
        private readonly Action _onCard;
        private readonly List<CardFlight> _flights = new List<CardFlight>(12);
        private readonly List<int> _flightTargets = new List<int>(12);
        private readonly int _total;
        private readonly float _interval;
        private float _elapsed;
        private int _launched;
        private int _handDealt;

        /// <param name="pool">Where the cards come from.</param>
        /// <param name="hand">The local hand.</param>
        /// <param name="dealer">The dealer's avatar: every card starts here.</param>
        /// <param name="targets">The four seats by relative position (0 is the local seat).</param>
        /// <param name="firstTarget">Relative seat that gets the first card (the dealer's right).</param>
        /// <param name="step">+1 when play goes to the next relative seat (anticlockwise), +3 when clockwise.</param>
        /// <param name="handCards">The local hand in display order.</param>
        /// <param name="face">The face sprite of a card in the local deck.</param>
        /// <param name="onCard">Called as each card leaves the dealer (the deal sound).</param>
        public DealAnimation(
            CardViewPool pool,
            HandView hand,
            RectTransform dealer,
            Target[] targets,
            int firstTarget,
            int step,
            IReadOnlyList<Card> handCards,
            Func<Card, Sprite> face,
            Action onCard)
        {
            _pool = pool;
            _hand = hand;
            _dealer = dealer;
            _targets = targets;
            _firstTarget = firstTarget;
            _step = step;
            _handCards = handCards;
            _face = face;
            _onCard = onCard;
            _total = handCards.Count * targets.Length;
            _interval = _total > 1 ? (Seconds - FlightSeconds) / (_total - 1) : 0f;
        }

        public float Duration => Seconds;

        public void Begin()
        {
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            while (_launched < _total && _elapsed >= _launched * _interval)
            {
                Launch(_launched);
                _launched++;
            }

            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                if (_flights[i].Tick(deltaTime))
                {
                    Land(i);
                }
            }

            return _launched >= _total && _flights.Count == 0;
        }

        public void Finish()
        {
            // Cards in the air land where they were going; the ones not dealt yet are put in place
            // by the table when it shows the dealt state.
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                _flights[i].Finish();
                Land(i);
            }

            _launched = _total;
        }

        private void Launch(int index)
        {
            int target = (_firstTarget + index * _step) % _targets.Length;
            Target seat = _targets[target];
            RectTransform parent = seat.Fan != null ? seat.Fan.Area : _hand.CardsRoot;
            CardView card = _pool.Rent(parent);
            card.ShowBack(seat.Back);
            card.SetShadow(false);

            // Every card starts as a small back at the dealer's avatar.
            float fanScale = FanScale();
            Vector2 start = parent.InverseTransformPoint(_dealer.position);
            card.Placement = new CardPlacement(start, 0f, fanScale, 0f);
            CardPlacement to = seat.Fan != null ? seat.Fan.NextPlacement() : _hand.PlacementOf(Mathf.Min(_handDealt, _handCards.Count - 1), _handCards.Count);
            if (seat.Fan == null)
            {
                _handDealt++;
            }

            var flight = new CardFlight(card, to, FlightSeconds, Ease.OutQuad);
            flight.Begin();
            _flights.Add(flight);
            _flightTargets.Add(target);
            _onCard?.Invoke();
        }

        private void Land(int flightIndex)
        {
            CardView card = _flights[flightIndex].Card;
            Target seat = _targets[_flightTargets[flightIndex]];
            _flights.RemoveAt(flightIndex);
            _flightTargets.RemoveAt(flightIndex);
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
                card.ShowFace(dealt, _face(dealt));
                card.SetShadow(true);
                _hand.Insert(card);
            }
            else
            {
                _pool.Return(card);
            }
        }

        private float FanScale()
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i].Fan != null)
                {
                    return _targets[i].Fan.CardScale;
                }
            }

            return 0.27f;
        }
    }
}
