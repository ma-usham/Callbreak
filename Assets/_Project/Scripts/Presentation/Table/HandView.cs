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
    /// subdued. Input is one touch area over the whole hand, so a finger can slide along it
    /// (tap-and-slide): the card under the finger rises, and releasing on a card, or flicking one
    /// upwards, chooses it. Which cards may be played is the presenter's decision.
    /// </summary>
    public sealed class HandView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const float MillimetresPerInch = 25.4f;

        [SerializeField] private RectTransform _cardsRoot;
        [SerializeField] private RectTransform _widthReference;
        [SerializeField] private CardViewPool _pool;
        [SerializeField] private HandLayoutSettings _layout = new HandLayoutSettings();
        [SerializeField] private float _minStripMillimetres = 7f;
        [SerializeField] private float _flickDistance = 70f;
        [SerializeField] private float _arrangeSeconds = 0.25f;

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
        private Vector2 _pressPosition;
        private bool _pressed;
        private MotionHandle _arrangeMotion;
        private MotionHandle _shakeMotion;
        private Canvas _canvas;

        /// <summary>A card was chosen: released under the finger, or flicked upwards.</summary>
        public event Action<Card> CardChosen;

        /// <summary>The hand was touched while it takes no input, or beside every card (a tap that may skip an animation).</summary>
        public event Action Touched;

        /// <summary>While false, touches only raise <see cref="Touched"/>.</summary>
        public bool Interactable { get; set; }

        public int Count => _views.Count;

        /// <summary>Parent of the hand's cards; its centre is the centre of the middle card of an unlifted hand.</summary>
        public RectTransform CardsRoot => _cardsRoot;

        public HandLayoutSettings Layout => _layout;

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
                _views[i].ShowFace(_views[i].Card, _sprites.Face(deckId, _views[i].Card));
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
                    view.ShowFace(card, _sprites.Face(_deckId, card));
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
            return new CardPlacement(new Vector2(pose.X, pose.Y), pose.Rotation, 1f, 0f);
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

        /// <summary>The refusal of an illegal tap: three quick sideways shakes (GDD §14, 240 ms).</summary>
        public void Shake(Card card)
        {
            int index = IndexOf(card);
            if (index < 0)
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
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable)
            {
                Touched?.Invoke();
                return;
            }

            _pressed = true;
            _pressPosition = eventData.position;
            int index = IndexAt(eventData);
            _grabbed = index >= 0 ? _views[index] : null;
            if (index < 0)
            {
                Touched?.Invoke();
            }

            SetHovered(index);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_pressed || !Interactable)
            {
                return;
            }

            int index = IndexAt(eventData);
            if (index >= 0)
            {
                _grabbed = _views[index];
            }

            SetHovered(index);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressed)
            {
                return;
            }

            _pressed = false;
            int index = IndexAt(eventData);
            CardView chosen = index >= 0 ? _views[index] : null;
            if (chosen == null && _grabbed != null && _views.Contains(_grabbed) && Flicked(eventData))
            {
                chosen = _grabbed;
            }

            _grabbed = null;
            SetHovered(-1);
            if (chosen != null && Interactable)
            {
                CardChosen?.Invoke(chosen.Card);
            }
        }

        private void OnDisable()
        {
            Cancel(ref _arrangeMotion);
            CancelShake();
            _pressed = false;
            _grabbed = null;
        }

        private void OnRectTransformDimensionsChange()
        {
            // The safe area or the screen changed: the hand's width depends on it.
            if (isActiveAndEnabled && _views.Count > 0)
            {
                Arrange(false);
            }
        }

        private bool Flicked(PointerEventData eventData)
        {
            float scale = CanvasScale();
            return (eventData.position.y - _pressPosition.y) / scale >= _flickDistance;
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
                _to[i] = new CardPlacement(new Vector2(_poses[i].X, _poses[i].Y), _poses[i].Rotation, 1f, 0f);
            }

            if (!animate || !isActiveAndEnabled || count == 0)
            {
                ApplyBlend(1f);
                return;
            }

            _arrangeMotion = LMotion.Create(0f, 1f, _arrangeSeconds).WithEase(Ease.OutCubic).Bind(this, (t, hand) => hand.ApplyBlend(t));
        }

        private void ApplyBlend(float t)
        {
            int count = Mathf.Min(_views.Count, _to.Length);
            for (int i = 0; i < count; i++)
            {
                _views[i].Placement = t >= 1f ? _to[i] : CardPlacement.Lerp(_from[i], _to[i], t);
            }
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
