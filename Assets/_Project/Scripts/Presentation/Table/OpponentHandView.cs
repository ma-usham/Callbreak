using System;
using System.Collections.Generic;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// One side of an opponent's fan: backs turn about a point below the avatar. The top seat has
    /// two of these, mirrored, so its cards spread to both sides of the avatar (mockup 08).
    /// Lengths are canvas units from the centre of the fan's rectangle; angles are degrees,
    /// clockwise from straight up.
    /// </summary>
    [Serializable]
    public sealed class FanShape
    {
        [SerializeField] private Vector2 _pivot;
        [SerializeField] private float _radius = 240f;
        [SerializeField] private float _startAngle;
        [SerializeField] private float _stepAngle = 6f;
        [SerializeField] private float _maxSpread = 30f;

        public FanShape()
        {
        }

        public FanShape(Vector2 pivot, float radius, float startAngle, float stepAngle, float maxSpread)
        {
            _pivot = pivot;
            _radius = radius;
            _startAngle = startAngle;
            _stepAngle = stepAngle;
            _maxSpread = maxSpread;
        }

        /// <summary>The placement of card <paramref name="index"/> of <paramref name="count"/> on this side; 0 is nearest the avatar.</summary>
        public CardPlacement Placement(int index, int count, float scale)
        {
            float step = Mathf.Abs(_stepAngle);
            if (count > 1)
            {
                step = Mathf.Min(step, _maxSpread / (count - 1));
            }

            float angle = _startAngle + Mathf.Sign(_stepAngle) * step * index;
            float radians = angle * Mathf.Deg2Rad;
            var position = new Vector2(_pivot.x + _radius * Mathf.Sin(radians), _pivot.y + _radius * Mathf.Cos(radians));
            return new CardPlacement(position, -angle, scale, 0f);
        }
    }

    /// <summary>
    /// An opponent's hand as a fan of face-down cards behind the avatar (GDD §11 T rules: "Opponent
    /// fans show the cards they have left, drawn with the back of the owner's deck"). It knows only
    /// how many cards there are: their faces never exist on this device (GDD §26).
    /// </summary>
    public sealed class OpponentHandView : MonoBehaviour
    {
        [SerializeField] private RectTransform _area;
        [SerializeField] private CardViewPool _pool;
        [SerializeField] private FanShape[] _sides = Array.Empty<FanShape>();
        [SerializeField] private float _cardScale = 0.268f;

        private readonly List<CardView> _cards = new List<CardView>(13);
        private Sprite _back;

        public int Count => _cards.Count;

        /// <summary>Where the fan's cards live; a dealt or played card is re-parented here or away from here.</summary>
        public RectTransform Area => _area;

        public float CardScale => _cardScale;

        /// <summary>The deck back this seat's cards show.</summary>
        public void SetBack(Sprite back)
        {
            _back = back;
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].ShowBack(back);
            }
        }

        /// <summary>Shows exactly <paramref name="count"/> backs, at once.</summary>
        public void SetCount(int count)
        {
            count = Mathf.Max(0, count);
            while (_cards.Count > count)
            {
                CardView last = _cards[_cards.Count - 1];
                _cards.RemoveAt(_cards.Count - 1);
                _pool.Return(last);
            }

            while (_cards.Count < count)
            {
                CardView card = _pool.Rent(_area);
                card.ShowBack(_back);
                card.SetShadow(false);
                _cards.Add(card);
            }

            Arrange();
        }

        /// <summary>
        /// Takes the outermost card out of the fan for a flight to the trick. It keeps its place on
        /// screen; the caller re-parents it, gives it its face and owns it from here on.
        /// </summary>
        public CardView Take()
        {
            if (_cards.Count == 0)
            {
                return null;
            }

            CardView card = _cards[_cards.Count - 1];
            _cards.RemoveAt(_cards.Count - 1);
            Arrange();
            return card;
        }

        /// <summary>Where the next dealt card will sit once it has joined the fan.</summary>
        public CardPlacement NextPlacement()
        {
            return PlacementOf(_cards.Count, _cards.Count + 1);
        }

        /// <summary>A dealt card joins the fan (it is re-parented, shown as a back and arranged).</summary>
        public void Add(CardView card)
        {
            card.Rect.SetParent(_area, true);
            card.ShowBack(_back);
            card.SetShadow(false);
            card.SetGlow(false);
            card.SetDimmed(false);
            _cards.Add(card);
            Arrange();
        }

        /// <summary>How visible the fan's cards are, 0 to 1 (the deal fades them in with Reduce motion).</summary>
        public void SetAlpha(float alpha)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].SetAlpha(alpha);
            }
        }

        /// <summary>Returns every card to the pool.</summary>
        public void Clear()
        {
            SetCount(0);
        }

        private CardPlacement PlacementOf(int index, int count)
        {
            int sides = Mathf.Max(1, _sides.Length);
            if (_sides.Length == 0)
            {
                return new CardPlacement(Vector2.zero, 0f, _cardScale, 0f);
            }

            // Cards alternate between the sides, so a two-sided fan stays balanced as it shrinks.
            int side = index % sides;
            int onSide = count / sides + (side < count % sides ? 1 : 0);
            return _sides[side].Placement(index / sides, onSide, _cardScale);
        }

        private void Arrange()
        {
            int count = _cards.Count;

            // The card nearest the avatar lies on top; the outermost one is at the back.
            for (int i = count - 1; i >= 0; i--)
            {
                CardView card = _cards[i];
                card.Placement = PlacementOf(i, count);
                card.Rect.SetAsLastSibling();
            }
        }
    }
}
