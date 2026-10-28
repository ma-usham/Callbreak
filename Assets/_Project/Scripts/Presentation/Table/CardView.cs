using Callbreak.Core.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Table
{
    /// <summary>Where a card is within its parent: position, turn, size, and how flat it lies on the table.</summary>
    public readonly struct CardPlacement
    {
        public CardPlacement(Vector2 position, float rotation, float scale, float flat)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Flat = flat;
        }

        /// <summary>Anchored position of the card's centre.</summary>
        public Vector2 Position { get; }

        /// <summary>Degrees anticlockwise.</summary>
        public float Rotation { get; }

        /// <summary>1 is the hand size (180 wide at 1080p).</summary>
        public float Scale { get; }

        /// <summary>0 upright (hand, fans), 1 lying on the table in its perspective (trick).</summary>
        public float Flat { get; }

        public static CardPlacement Lerp(in CardPlacement a, in CardPlacement b, float t)
        {
            return new CardPlacement(
                Vector2.LerpUnclamped(a.Position, b.Position, t),
                Mathf.LerpAngle(a.Rotation, b.Rotation, t),
                Mathf.LerpUnclamped(a.Scale, b.Scale, t),
                Mathf.Lerp(a.Flat, b.Flat, t));
        }
    }

    /// <summary>
    /// One playing card (GDD §23 "52 CardViews are pooled per scene"): a face or a back, with the
    /// effects the game draws itself (GDD §13 "glow, dimming and shadows are drawn by the game,
    /// never baked"): a soft shadow, the gold glow of a legal card and the overlay of a subdued one.
    /// It only shows what it is told; the hand, the fans and the trick area place it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _shadow;
        [SerializeField] private Image _glow;
        [SerializeField] private Image _base;
        [SerializeField] private Image _face;
        [SerializeField] private Image _dim;
        [SerializeField] private CardPerspective[] _perspectives;

        // A card lying on the table is drawn shorter, and narrower at the far edge (mockup 08).
        [SerializeField] private float _flatHeight = 0.64f;
        [SerializeField] private float _flatTopScale = 0.9f;

        private float _scale = 1f;
        private float _flat;
        private float _rotation;

        public RectTransform Rect => _rect;

        /// <summary>The card shown; meaningful while <see cref="IsFaceUp"/>.</summary>
        public Card Card { get; private set; }

        public bool IsFaceUp { get; private set; }

        /// <summary>Size at scale 1, in canvas units.</summary>
        public Vector2 Size => _rect.sizeDelta;

        public bool IsGlowing => _glow.enabled;

        public bool IsDimmed => _dim.enabled;

        public CardPlacement Placement
        {
            get => new CardPlacement(_rect.anchoredPosition, _rotation, _scale, _flat);
            set
            {
                _rect.anchoredPosition = value.Position;
                SetRotation(value.Rotation);
                _scale = value.Scale;
                SetFlat(value.Flat);
            }
        }

        /// <summary>
        /// The card face up: its face over its deck's base, the border and paper the shared faces
        /// leave out (ADR 053). A null base shows the face alone.
        /// </summary>
        public void ShowFace(Card card, Sprite face, Sprite cardBase)
        {
            Card = card;
            IsFaceUp = true;
            SetSprite(face);
            SetBase(face != null ? cardBase : null);
        }

        public void ShowBack(Sprite back)
        {
            IsFaceUp = false;
            SetSprite(back);
            SetBase(null);
        }

        /// <summary>The gold glow of a legal card (and of the card that wins a trick).</summary>
        public void SetGlow(bool on)
        {
            if (_glow.enabled != on)
            {
                _glow.enabled = on;
            }
        }

        /// <summary>The overlay of a card that cannot be played now; it stays readable.</summary>
        public void SetDimmed(bool on)
        {
            if (_dim.enabled != on)
            {
                _dim.enabled = on;
            }
        }

        public void SetShadow(bool on)
        {
            if (_shadow.enabled != on)
            {
                _shadow.enabled = on;
            }
        }

        public void SetAlpha(float alpha)
        {
            _group.alpha = alpha;
        }

        public void SetPosition(Vector2 anchoredPosition)
        {
            _rect.anchoredPosition = anchoredPosition;
        }

        public void SetRotation(float degrees)
        {
            _rotation = degrees;
            _rect.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        public void SetScale(float scale)
        {
            _scale = scale;
            ApplyScale();
        }

        /// <summary>0 upright, 1 lying on the table.</summary>
        public void SetFlat(float flat)
        {
            _flat = Mathf.Clamp01(flat);
            ApplyScale();
            float top = Mathf.Lerp(1f, _flatTopScale, _flat);
            for (int i = 0; i < _perspectives.Length; i++)
            {
                _perspectives[i].TopScale = top;
            }
        }

        /// <summary>Back to a plain, upright, fully visible card (when it returns to the pool).</summary>
        public void ResetState()
        {
            SetGlow(false);
            SetDimmed(false);
            SetShadow(true);
            SetAlpha(1f);
            Placement = new CardPlacement(Vector2.zero, 0f, 1f, 0f);
        }

        private void SetSprite(Sprite sprite)
        {
            if (_face.sprite != sprite)
            {
                _face.sprite = sprite;
            }

            bool visible = sprite != null;
            if (_face.enabled != visible)
            {
                _face.enabled = visible;
            }
        }

        private void SetBase(Sprite cardBase)
        {
            if (_base == null)
            {
                return;
            }

            if (_base.sprite != cardBase)
            {
                _base.sprite = cardBase;
            }

            bool visible = cardBase != null;
            if (_base.enabled != visible)
            {
                _base.enabled = visible;
            }
        }

        private void ApplyScale()
        {
            _rect.localScale = new Vector3(_scale, _scale * Mathf.Lerp(1f, _flatHeight, _flat), 1f);
        }
    }
}
