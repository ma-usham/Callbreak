using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// One side of an opponent's fan: backs turn about a pivot, each pointing away from it. A side
    /// seat has one, beside its profile, and it is the whole fan: its cards are centred on the
    /// middle of the full fan (<see cref="CentredPlacement"/>), so as cards are played it closes up
    /// around that middle. The top seat has two, filled from the middle out
    /// (<see cref="Placement"/>), so its middle cards lie on top and it shortens evenly from both
    /// ends (ADR 026). Lengths are canvas units from the centre of the fan's rectangle; angles are
    /// degrees, clockwise from straight up.
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

        /// <summary>
        /// The placement of card <paramref name="index"/> of <paramref name="count"/> on this side,
        /// laid out from the start angle; 0 is nearest the avatar.
        /// </summary>
        public CardPlacement Placement(int index, int count, float scale)
        {
            return At(_startAngle + Mathf.Sign(_stepAngle) * StepFor(count) * index, scale);
        }

        /// <summary>
        /// The placement of card <paramref name="index"/> of <paramref name="count"/> when this side
        /// is the whole fan: the cards are centred on the full fan's middle angle (the start angle
        /// plus half the spread); 0 is nearest the avatar. A full fan, its spread exactly filled,
        /// lies as <see cref="Placement"/> lays it.
        /// </summary>
        public CardPlacement CentredPlacement(int index, int count, float scale)
        {
            float sign = Mathf.Sign(_stepAngle);
            float middle = _startAngle + sign * _maxSpread / 2f;
            return At(middle + sign * StepFor(count) * (index - (count - 1) / 2f), scale);
        }

        /// <summary>The angle between two neighbours: the step, closer when the cards would spread wider than the spread.</summary>
        private float StepFor(int count)
        {
            float step = Mathf.Abs(_stepAngle);
            return count > 1 ? Mathf.Min(step, _maxSpread / (count - 1)) : step;
        }

        private CardPlacement At(float angle, float scale)
        {
            float radians = angle * Mathf.Deg2Rad;
            var position = new Vector2(_pivot.x + _radius * Mathf.Sin(radians), _pivot.y + _radius * Mathf.Cos(radians));
            return new CardPlacement(position, -angle, scale, 0f);
        }
    }

    /// <summary>
    /// An opponent's hand as a fan of face-down cards behind the avatar (GDD §11 T rules: "Opponent
    /// fans show the cards they have left, drawn with the back of the owner's deck"). It knows only
    /// how many cards there are: their faces never exist on this device (GDD §26).
    /// A side seat's fan (one <see cref="FanShape"/>) stays centred on its middle, so when the
    /// opponent plays, the cards left close up around it; the top seat's fan (two halves, from the
    /// middle out) shortens evenly from both ends and its other cards keep their places.
    /// When a card leaves the fan (played) or joins it, the cards that were already there glide to
    /// their new places, all in one motion (0.25 s, OutCubic, on unscaled time); a card that joins
    /// is put at its own place at once. While a deal fills the fan (<see cref="BeginDeal"/>), the
    /// fan is laid out for the whole hand: each dealt card flies straight to the place it keeps and
    /// nothing in the fan moves. With Reduce motion the cards jump to their places; a rebuild of
    /// the fan (<see cref="SetCount"/>) puts every card in its place at once.
    /// </summary>
    public sealed class OpponentHandView : MonoBehaviour
    {
        private const float GlideSeconds = 0.25f;

        // Closer than this (canvas units, degrees) a card is already at its place.
        private const float SamePlace = 0.01f;

        [SerializeField] private RectTransform _area;
        [SerializeField] private CardViewPool _pool;
        [SerializeField] private FanShape[] _sides = Array.Empty<FanShape>();
        [SerializeField] private float _cardScale = 0.268f;

        private readonly List<CardView> _cards = new List<CardView>(Deck.HandSize);
        private CardPlacement[] _from = new CardPlacement[Deck.HandSize];
        private CardPlacement[] _to = new CardPlacement[Deck.HandSize];
        private int _gliding;
        private int _dealing;
        private MotionHandle _arrangeMotion;
        private Sprite _back;

        public int Count => _cards.Count;

        /// <summary>Where the fan's cards live; a dealt or played card is re-parented here or away from here.</summary>
        public RectTransform Area => _area;

        public float CardScale => _cardScale;

        /// <summary>With Reduce motion the cards jump to their new places instead of gliding (GDD §14).</summary>
        public bool ReduceMotion { get; set; }

        /// <summary>The deck back this seat's cards show.</summary>
        public void SetBack(Sprite back)
        {
            _back = back;
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].ShowBack(back);
            }
        }

        /// <summary>
        /// Shows exactly <paramref name="count"/> backs, at once, laid out for that many (a glide under
        /// way ends first, and so does a deal's layout for the whole hand).
        /// </summary>
        public void SetCount(int count)
        {
            FinishGlide();
            _dealing = 0;
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

            Arrange(0);
        }

        /// <summary>
        /// Takes the outermost card out of the fan for a flight to the trick. It keeps its place on
        /// screen; the caller re-parents it, gives it its face and owns it from here on. The cards
        /// left glide to their new places (a side seat's close up around the fan's middle). The
        /// deal's layout for the whole hand ends here.
        /// </summary>
        public CardView Take()
        {
            FinishGlide();
            _dealing = 0;
            if (_cards.Count == 0)
            {
                return null;
            }

            CardView card = _cards[_cards.Count - 1];
            _cards.RemoveAt(_cards.Count - 1);
            Arrange(_cards.Count);
            return card;
        }

        /// <summary>
        /// A deal is about to give this seat <paramref name="cards"/> cards. Until the first card is
        /// played or the fan is laid out again (<see cref="SetCount"/>), it is laid out for the whole
        /// hand: every card dealt to it has its place from the start (<see cref="PlacementOf(int)"/>), and
        /// nothing already in the fan moves while it fills.
        /// </summary>
        public void BeginDeal(int cards)
        {
            FinishGlide();
            _dealing = _cards.Count + Mathf.Max(0, cards);
            Arrange(0);
        }

        /// <summary>
        /// Where the fan's card <paramref name="index"/> lies as the fan is laid out now (0 nearest
        /// the avatar; for the whole hand while a deal fills it): where a dealt card that will be
        /// that card flies.
        /// </summary>
        public CardPlacement PlacementOf(int index)
        {
            return PlacementOf(index, LayoutCount);
        }

        /// <summary>
        /// A card joins the fan (a dealt one): it is re-parented, shown as a back and put at its
        /// place; the cards already in the fan glide to theirs (while a deal fills the fan, they
        /// keep them).
        /// </summary>
        public void Add(CardView card)
        {
            FinishGlide();
            int before = _cards.Count;
            card.Rect.SetParent(_area, true);
            card.ShowBack(_back);
            card.SetShadow(false);
            card.SetGlow(false);
            card.SetDimmed(false);
            _cards.Add(card);
            Arrange(before);
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

        private void OnDisable()
        {
            // A glide is never left half-way.
            FinishGlide();
        }

        /// <summary>How many cards the fan is laid out for: the whole hand while a deal fills it, otherwise the cards it holds.</summary>
        private int LayoutCount => Mathf.Max(_dealing, _cards.Count);

        /// <summary>Where card <paramref name="index"/> of a fan laid out for <paramref name="count"/> lies.</summary>
        private CardPlacement PlacementOf(int index, int count)
        {
            if (_sides.Length == 0)
            {
                return new CardPlacement(Vector2.zero, 0f, _cardScale, 0f);
            }

            // A side seat's fan is one side, centred on its middle: it closes up as it shrinks.
            if (_sides.Length == 1)
            {
                return _sides[0].CentredPlacement(index, count, _cardScale);
            }

            // Cards alternate between the sides, so a two-sided fan stays balanced as it shrinks.
            int sides = _sides.Length;
            int side = index % sides;
            int onSide = count / sides + (side < count % sides ? 1 : 0);
            return _sides[side].Placement(index / sides, onSide, _cardScale);
        }

        /// <summary>
        /// Lays the fan out for <see cref="LayoutCount"/> cards. The first <paramref name="gliding"/>
        /// cards glide from where they are to their places, all in one motion; the others are put
        /// there at once.
        /// </summary>
        private void Arrange(int gliding)
        {
            int count = _cards.Count;
            int layout = LayoutCount;
            EnsureCapacity(count);
            gliding = ReduceMotion || !gameObject.activeInHierarchy ? 0 : Mathf.Min(gliding, count);
            bool moves = false;

            // The card nearest the avatar lies on top; the outermost one is at the back.
            for (int i = count - 1; i >= 0; i--)
            {
                CardView card = _cards[i];
                _to[i] = PlacementOf(i, layout);
                if (i < gliding)
                {
                    _from[i] = card.Placement;
                    moves |= !Near(_from[i], _to[i]);
                }
                else
                {
                    card.Placement = _to[i];
                }

                card.Rect.SetAsLastSibling();
            }

            _gliding = gliding;
            if (!moves)
            {
                ApplyGlide(1f);
                return;
            }

            _arrangeMotion = LMotion.Create(0f, 1f, GlideSeconds)
                .WithEase(Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(this, (t, fan) => fan.ApplyGlide(t));
        }

        /// <summary>The gliding cards <paramref name="t"/> of the way (eased) to their places; at 1 the glide is over.</summary>
        private void ApplyGlide(float t)
        {
            int count = Mathf.Min(_gliding, _cards.Count);
            for (int i = 0; i < count; i++)
            {
                // When the scene unloads a card may already be gone.
                CardView card = _cards[i];
                if (card != null)
                {
                    card.Placement = t >= 1f ? _to[i] : CardPlacement.Lerp(_from[i], _to[i], t);
                }
            }

            if (t >= 1f)
            {
                _gliding = 0;
            }
        }

        /// <summary>A glide under way ends at once, with every card at its place.</summary>
        private void FinishGlide()
        {
            if (_arrangeMotion.IsActive())
            {
                _arrangeMotion.TryComplete();
            }

            _arrangeMotion = default;

            // Also when the motion itself was stopped from outside.
            ApplyGlide(1f);
        }

        private void EnsureCapacity(int count)
        {
            if (_to.Length >= count)
            {
                return;
            }

            Array.Resize(ref _from, count);
            Array.Resize(ref _to, count);
        }

        private static bool Near(in CardPlacement a, in CardPlacement b)
        {
            return (a.Position - b.Position).sqrMagnitude < SamePlace * SamePlace
                && Mathf.Abs(Mathf.DeltaAngle(a.Rotation, b.Rotation)) < SamePlace
                && Mathf.Approximately(a.Scale, b.Scale)
                && Mathf.Approximately(a.Flat, b.Flat);
        }
    }
}
