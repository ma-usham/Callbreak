using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The local hand (GDD §11 T rules "LOCAL HAND", §12 "Cards on screen"): up to 13 cards on an
    /// arc laid out by <see cref="HandLayout"/>, legal cards lifted with a gold glow, the others
    /// subdued. Input is one touch area over the whole hand, so a finger can slide along it: the
    /// card under the finger rises. A card is played by throwing it (ADR 022, in place of the
    /// GDD's tap): pulled up out of the hand it follows the finger, and let go far enough above
    /// the hand, or flicked upwards, it is chosen; let go sooner it slides back into its place. A
    /// tap plays nothing. Which cards may be played is the presenter's decision; a card it refuses
    /// slides back too. While the hand takes no plays (calling, or another seat's turn) a touched
    /// card still rises and grows a little for a closer look, and is never played (GDD screen 07,
    /// callout 10). The hand's cards may be drawn larger than the shared card the trick, the fans
    /// and the deal use (<see cref="CardScale"/>); the layout's card width is the width as drawn.
    /// </summary>
    public sealed class HandView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float MillimetresPerInch = 25.4f;

        // GDD §14 "Sort hand: cards slide into their sorted slots, 250 ms, OutBack 0.8": a slide
        // runs a little past its slot (2% of the way) and settles back.
        private const float SlideOvershoot = 0.8f;

        [SerializeField] private RectTransform _cardsRoot;
        [SerializeField] private RectTransform _widthReference;
        [SerializeField] private CardViewPool _pool;
        [SerializeField] private HandLayoutSettings _layout = new HandLayoutSettings();
        [SerializeField] private float _cardScale = 1f;
        [SerializeField] private float _minStripMillimetres = 7f;
        [SerializeField] private float _arrangeSeconds = 0.25f;
        [SerializeField] private float _inspectScale = 1.12f;

        // Throwing a card (canvas units at the 1080p reference). A card leaves the hand once it is
        // pulled up this far, more up than sideways; it is thrown when let go this far above where
        // the pull began, or when it is still going up this fast.
        [SerializeField] private float _pullDistance = 24f;
        [SerializeField] private float _throwDistance = 110f;
        [SerializeField] private float _throwSpeed = 900f;
        [SerializeField] private float _heldScale = 1.06f;

        private readonly List<CardView> _views = new List<CardView>(Deck.HandSize);
        private readonly List<CardView> _previous = new List<CardView>(Deck.HandSize);
        private CardPlacement[] _from = new CardPlacement[Deck.HandSize];
        private CardPlacement[] _to = new CardPlacement[Deck.HandSize];
        private CardPose[] _poses = new CardPose[Deck.HandSize];
        private bool[] _lifted = new bool[Deck.HandSize];
        private CardSet _liftedCards;
        private CardSet _dimmedCards;
        private CardSpriteProvider _sprites;
        private string _deckId;
        private int _hovered = -1;
        private int _shakeIndex = -1;
        private CardView _grabbed;
        private CardView _held;
        private Vector2 _pullStart;
        private Vector2 _holdOffset;
        private float _restRotation;
        private Vector2 _lastPointer;
        private float _lastPointerTime;
        private float _riseSpeed;
        private bool _pressed;
        private bool _inspecting;
        private MotionHandle _arrangeMotion;
        private MotionHandle _shakeMotion;
        private Canvas _canvas;

        /// <summary>A card was thrown: let go far enough above the hand, or flicked upwards. It is where the finger left it.</summary>
        public event Action<Card> CardChosen;

        /// <summary>The hand was touched while it takes no input, or beside every card (a tap on the table).</summary>
        public event Action Touched;

        /// <summary>While false, touches only raise <see cref="Touched"/> and a touched card is only looked at.</summary>
        public bool Interactable { get; set; }

        /// <summary>With Reduce motion a card that is not thrown goes back to its place at once.</summary>
        public bool ReduceMotion { get; set; }

        /// <summary>The card following the finger, if any.</summary>
        public CardView Held => _held;

        public int Count => _views.Count;

        /// <summary>Parent of the hand's cards; its centre is the centre of the middle card of an unlifted hand.</summary>
        public RectTransform CardsRoot => _cardsRoot;

        public HandLayoutSettings Layout => _layout;

        /// <summary>The size of a resting hand card, against the shared card (1: as large as it).</summary>
        public float CardScale => _cardScale;

        /// <summary>The deck whose faces the hand shows (the player's own, GDD §13); set before the first cards.</summary>
        public void SetDeck(CardSpriteProvider sprites, string deckId)
        {
            if (_sprites == sprites && _deckId == deckId)
            {
                return;
            }

            _sprites = sprites;
            _deckId = deckId;
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].ShowFace(_views[i].Card, _sprites.Face(deckId, _views[i].Card), _sprites.Base(deckId));
            }
        }

        /// <summary>
        /// Shows exactly <paramref name="cards"/>, left to right. Cards already in the hand keep
        /// their views and slide to their new places; new ones appear in place; the rest go back to
        /// the pool.
        /// </summary>
        public void SetCards(IReadOnlyList<Card> cards, bool animate)
        {
            _previous.Clear();
            _previous.AddRange(_views);
            _views.Clear();
            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                CardView view = Take(_previous, card);
                bool isNew = view == null;
                if (isNew)
                {
                    view = _pool.Rent(_cardsRoot);
                    view.ShowFace(card, _sprites.Face(_deckId, card), _sprites.Base(_deckId));
                }

                _views.Add(view);
                if (isNew)
                {
                    // A new card starts where it belongs rather than sliding in from the centre.
                    view.Placement = PlacementOf(i, cards.Count);
                }
            }

            for (int i = 0; i < _previous.Count; i++)
            {
                _pool.Return(_previous[i]);
            }

            // A held card that is no longer in the hand (the clock played it) is let go.
            if (_held != null && !_views.Contains(_held))
            {
                _held = null;
            }

            _previous.Clear();
            _hovered = -1;
            ApplyHighlights();
            Arrange(animate);
        }

        /// <summary>Legal cards rise and glow; subdued cards get the overlay. Cards in neither set are plain.</summary>
        public void SetHighlights(CardSet lifted, CardSet dimmed, bool animate)
        {
            if (_liftedCards == lifted && _dimmedCards == dimmed)
            {
                return;
            }

            _liftedCards = lifted;
            _dimmedCards = dimmed;
            ApplyHighlights();
            Arrange(animate);
        }

        public CardView ViewOf(Card card)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                if (_views[i].Card == card)
                {
                    return _views[i];
                }
            }

            return null;
        }

        /// <summary>Where card <paramref name="index"/> of a hand of <paramref name="count"/> rests when nothing is lifted.</summary>
        public CardPlacement PlacementOf(int index, int count)
        {
            EnsureCapacity(count);
            HandLayout.Compute(count, SafeWidth(), EffectiveLayout(), null, -1, _poses);
            CardPose pose = _poses[index];
            return new CardPlacement(new Vector2(pose.X, pose.Y), pose.Rotation, _cardScale, 0f);
        }

        /// <summary>
        /// Takes a card out of the hand for its flight to the trick. The view keeps its place on
        /// screen and now belongs to the caller; the rest of the hand closes up.
        /// </summary>
        public CardView Release(Card card, bool animate)
        {
            CardView view = ViewOf(card);
            if (view == null)
            {
                return null;
            }

            _views.Remove(view);
            if (_grabbed == view)
            {
                _grabbed = null;
            }

            if (_held == view)
            {
                _held = null;
            }

            view.SetGlow(false);
            view.SetDimmed(false);
            _hovered = -1;
            Arrange(animate);
            return view;
        }

        /// <summary>
        /// Puts a face-up card view into the hand (a dealt card arriving, or a refused play rolling
        /// back). It keeps its place on screen until the next <see cref="SetCards"/> arranges it.
        /// </summary>
        public void Insert(CardView view)
        {
            view.Rect.SetParent(_cardsRoot, true);
            view.SetFlat(0f);
            if (!_views.Contains(view))
            {
                _views.Add(view);
            }
        }

        /// <summary>How visible the hand's cards are, 0 to 1 (the deal fades them in with Reduce motion).</summary>
        public void SetAlpha(float alpha)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].SetAlpha(alpha);
            }
        }

        /// <summary>
        /// The refusal of an illegal card: three quick sideways shakes (GDD §14, 240 ms). A card that
        /// was thrown slides back into its place instead, which says the same.
        /// </summary>
        public void Shake(Card card)
        {
            int index = IndexOf(card);
            if (index < 0 || _views[index] == _held)
            {
                return;
            }

            CancelShake();
            _shakeIndex = index;
            _shakeMotion = LMotion.Create(0f, 1f, 0.24f).Bind(this, (t, hand) => hand.ApplyShake(t));
        }

        /// <summary>Returns every card to the pool.</summary>
        public void Clear()
        {
            Cancel(ref _arrangeMotion);
            CancelShake();
            for (int i = 0; i < _views.Count; i++)
            {
                _pool.Return(_views[i]);
            }

            _views.Clear();
            _liftedCards = CardSet.Empty;
            _dimmedCards = CardSet.Empty;
            _hovered = -1;
            _grabbed = null;
            _held = null;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _held = null;
            if (!Interactable)
            {
                // Not a play: the card under the finger is only looked at.
                Touched?.Invoke();
                _pressed = true;
                _inspecting = true;
                _grabbed = null;
                SetHovered(IndexAt(eventData));
                return;
            }

            _pressed = true;
            _inspecting = false;
            int index = IndexAt(eventData);
            _grabbed = index >= 0 ? _views[index] : null;
            _pullStart = eventData.position;
            _riseSpeed = 0f;
            if (index < 0)
            {
                Touched?.Invoke();
            }

            SetHovered(index);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_pressed)
            {
                return;
            }

            if (_held != null)
            {
                Follow(eventData);
                return;
            }

            if (_inspecting || !Interactable)
            {
                SetHovered(IndexAt(eventData));
                return;
            }

            // Pulled up out of the hand: the card under the finger leaves it and follows.
            float scale = CanvasScale();
            float up = (eventData.position.y - _pullStart.y) / scale;
            float sideways = Mathf.Abs(eventData.position.x - _pullStart.x) / scale;
            if (_grabbed != null && _views.Contains(_grabbed) && up >= _pullDistance && up >= sideways)
            {
                Hold(_grabbed, eventData);
                return;
            }

            // Sliding along the hand: the pull is measured from the card the finger is on now.
            int index = IndexAt(eventData);
            if (index >= 0 && _views[index] != _grabbed)
            {
                _grabbed = _views[index];
                _pullStart = eventData.position;
            }

            if (index >= 0)
            {
                SetHovered(index);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressed)
            {
                return;
            }

            _pressed = false;
            if (_inspecting)
            {
                _inspecting = false;
                _grabbed = null;
                SetHovered(-1);
                return;
            }

            CardView held = _held;
            _grabbed = null;
            if (held == null)
            {
                // A tap, or a slide along the hand: nothing is played.
                SetHovered(-1);
                return;
            }

            Follow(eventData);
            _held = null;
            _hovered = -1;
            float up = (eventData.position.y - _pullStart.y) / CanvasScale();
            bool thrown = up >= _throwDistance || (up >= _pullDistance && _riseSpeed >= _throwSpeed);
            if (thrown && Interactable)
            {
                // The presenter takes the card from here, or refuses it.
                CardChosen?.Invoke(held.Card);
            }

            if (_views.Contains(held))
            {
                // Not thrown, or refused: it slides back into its place.
                Arrange(!ReduceMotion);
            }
        }

        private void OnDisable()
        {
            Cancel(ref _arrangeMotion);
            CancelShake();
            _pressed = false;
            _inspecting = false;
            _grabbed = null;
            if (_held != null)
            {
                _held = null;
                Arrange(false);
            }
        }

        /// <summary>The card leaves the hand and follows the finger, the spot that was touched staying under it.</summary>
        private void Hold(CardView card, PointerEventData eventData)
        {
            _held = card;
            _restRotation = card.Placement.Rotation;
            _holdOffset = card.Placement.Position - LocalPoint(_pullStart, eventData.pressEventCamera);
            _lastPointer = eventData.position;
            _lastPointerTime = Time.unscaledTime;
            CancelShake();
            card.Rect.SetAsLastSibling();
            Follow(eventData);
        }

        /// <summary>
        /// The held card under the finger: it straightens and grows a little as it is pulled up.
        /// How fast the finger rises is kept for the flick.
        /// </summary>
        private void Follow(PointerEventData eventData)
        {
            CardView card = _held;
            if (card == null)
            {
                return;
            }

            float scale = CanvasScale();
            float now = Time.unscaledTime;
            if (now > _lastPointerTime)
            {
                _riseSpeed = (eventData.position.y - _lastPointer.y) / scale / (now - _lastPointerTime);
                _lastPointerTime = now;
                _lastPointer = eventData.position;
            }

            float pulled = Mathf.Clamp01((eventData.position.y - _pullStart.y) / scale / _throwDistance);
            float rotation = Mathf.LerpAngle(_restRotation, 0f, pulled);
            card.Placement = new CardPlacement(LocalPoint(eventData) + _holdOffset, rotation, _cardScale * Mathf.Lerp(1f, _heldScale, pulled), 0f);
        }

        private Vector2 LocalPoint(PointerEventData eventData) => LocalPoint(eventData.position, eventData.pressEventCamera);

        private Vector2 LocalPoint(Vector2 screenPoint, Camera eventCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_cardsRoot, screenPoint, eventCamera, out Vector2 local);
            return local;
        }

        private void OnRectTransformDimensionsChange()
        {
            // The safe area or the screen changed: the hand's width depends on it.
            if (isActiveAndEnabled && _views.Count > 0)
            {
                Arrange(false);
            }
        }

        private int IndexAt(PointerEventData eventData)
        {
            // The rightmost card lies on top, so it wins where cards overlap.
            for (int i = _views.Count - 1; i >= 0; i--)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(_views[i].Rect, eventData.position, eventData.pressEventCamera))
                {
                    return i;
                }
            }

            return -1;
        }

        private int IndexOf(Card card)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                if (_views[i].Card == card)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SetHovered(int index)
        {
            if (_hovered == index)
            {
                return;
            }

            _hovered = index;
            Arrange(false);
        }

        private void ApplyHighlights()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                CardView view = _views[i];
                bool lifted = _liftedCards.Contains(view.Card);
                view.SetGlow(lifted);
                view.SetDimmed(!lifted && _dimmedCards.Contains(view.Card));
            }
        }

        private void Arrange(bool animate)
        {
            Cancel(ref _arrangeMotion);
            CancelShake();
            int count = _views.Count;
            EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                _lifted[i] = _liftedCards.Contains(_views[i].Card);
            }

            HandLayout.Compute(count, SafeWidth(), EffectiveLayout(), _lifted, _hovered, _poses);
            for (int i = 0; i < count; i++)
            {
                CardView view = _views[i];
                view.Rect.SetSiblingIndex(i);
                _from[i] = view.Placement;
                float scale = _cardScale * (_inspecting && i == _hovered ? _inspectScale : 1f);
                _to[i] = new CardPlacement(new Vector2(_poses[i].X, _poses[i].Y), _poses[i].Rotation, scale, 0f);
            }

            // A held card stays under the finger, on top of the rest.
            if (_held != null)
            {
                _held.Rect.SetAsLastSibling();
            }

            if (!animate || !isActiveAndEnabled || count == 0)
            {
                ApplyBlend(1f);
                return;
            }

            // Linear time; ApplyBlend gives it the slide's own easing.
            _arrangeMotion = LMotion.Create(0f, 1f, _arrangeSeconds).Bind(this, (t, hand) => hand.ApplyBlend(t));
        }

        private void ApplyBlend(float t)
        {
            int count = Mathf.Min(_views.Count, _to.Length);
            float eased = OutBack(t);
            for (int i = 0; i < count; i++)
            {
                if (_views[i] != _held)
                {
                    _views[i].Placement = t >= 1f ? _to[i] : CardPlacement.Lerp(_from[i], _to[i], eased);
                }
            }
        }

        /// <summary>OutBack with the GDD's overshoot of 0.8 (the usual 1.70158 would throw a card 10% past its slot).</summary>
        private static float OutBack(float t)
        {
            float u = t - 1f;
            return 1f + ((SlideOvershoot + 1f) * u * u * u) + (SlideOvershoot * u * u);
        }

        private void ApplyShake(float t)
        {
            int index = _shakeIndex;
            if (index < 0 || index >= _views.Count)
            {
                return;
            }

            float offset = Mathf.Sin(t * 3f * 2f * Mathf.PI) * 12f * (1f - t);
            _views[index].SetPosition(_to[index].Position + new Vector2(offset, 0f));
        }

        private void CancelShake()
        {
            Cancel(ref _shakeMotion);
        }

        private float SafeWidth()
        {
            RectTransform reference = _widthReference != null ? _widthReference : _cardsRoot;
            return reference.rect.width;
        }

        /// <summary>The prefab's layout, with the 7 mm strip turned into canvas units for this screen.</summary>
        private HandLayoutSettings EffectiveLayout()
        {
            float dpi = Screen.dpi;
            if (dpi > 1f && _minStripMillimetres > 0f)
            {
                float strip = _minStripMillimetres / MillimetresPerInch * dpi / CanvasScale();
                _layout.MinStrip = Mathf.Max(HandLayout.HardMinStrip, strip);
            }

            return _layout;
        }

        private float CanvasScale()
        {
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            float scale = _canvas != null ? _canvas.rootCanvas.scaleFactor : 1f;
            return scale > 0.001f ? scale : 1f;
        }

        private void EnsureCapacity(int count)
        {
            if (_poses.Length >= count)
            {
                return;
            }

            _poses = new CardPose[count];
            _from = new CardPlacement[count];
            _to = new CardPlacement[count];
            _lifted = new bool[count];
        }

        private static CardView Take(List<CardView> views, Card card)
        {
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].Card == card)
                {
                    CardView view = views[i];
                    views.RemoveAt(i);
                    return view;
                }
            }

            return null;
        }

        private static void Cancel(ref MotionHandle handle)
        {
            if (handle.IsActive())
            {
                handle.Cancel();
            }

            handle = default;
        }
    }
}
