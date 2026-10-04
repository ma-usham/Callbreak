using System;
using System.Collections.Generic;
using Callbreak.Presentation.Framework;
using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// One shop category's row of items (designer's spec shop-drawer-and-tables-prompts.md):
    /// exactly three tiles in view; swipe/drag the row or use the chevrons to browse; the track's
    /// thumb shows where the row is; an arrow is off at its end; the chevrons, the track and the
    /// "Swipe to browse" hint hide when everything fits. The positions and the motion's rules are
    /// <see cref="CarouselScroll"/>; a chevron glides a page, a released drag settles on a tile,
    /// both at once with Reduce motion (the row still follows a finger). Tiles are made from the
    /// category's template as the catalogue needs them. Every category has its own carousel, so
    /// each keeps its position while another one shows.
    /// </summary>
    public sealed class ShopCarousel : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const int Visible = 3;

        /// <summary>A chevron's glide to the next page.</summary>
        public const float PageSeconds = 0.32f;

        /// <summary>The row settling on a tile after a drag.</summary>
        public const float SettleSeconds = 0.24f;

        private const float DisabledAlpha = 0.4f;

        // A finger that rests this long before letting go has not flicked.
        private const float FlickWindow = 0.1f;

        [SerializeField] private RectTransform _viewport;
        [SerializeField] private RectTransform _content;
        [SerializeField] private ShopItemTile _template;

        // Where tile 0 sits in the content at the start of the row, and the distance between tiles.
        [SerializeField] private Vector2 _firstTile;
        [SerializeField] private float _stride = 1f;

        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private CanvasGroup _previousGroup;
        [SerializeField] private CanvasGroup _nextGroup;

        // The chevrons, the track and the hint: hidden when everything fits.
        [SerializeField] private GameObject[] _browsing;
        [SerializeField] private RectTransform _thumb;
        [SerializeField] private float _thumbStartX;
        [SerializeField] private float _thumbTravel;

        private readonly List<ShopItemTile> _tiles = new List<ShopItemTile>();
        private int _count;
        private float _offset;
        private float _target;
        private MotionHandle _motion;
        private bool _dragging;
        private float _dragStartOffset;
        private float _dragStartX;
        private float _sampleTime;
        private float _sampleOffset;
        private float _velocity;

        /// <summary>The player tapped the tile at this index.</summary>
        public event Action<int> TileClicked;

        /// <summary>With Reduce motion the chevrons and a released drag move the row at once.</summary>
        public bool ReduceMotion { get; set; }

        public int Count => _count;

        /// <summary>Where the row is, in tiles: 0 shows tiles 0 to 2.</summary>
        public float Offset => _offset;

        public bool IsMoving => _dragging || _motion.IsActive();

        public bool Browsable => CarouselScroll.Browsable(_count, Visible);

        public bool CanGoBack => CarouselScroll.CanGoBack(_offset, _count, Visible);

        public bool CanGoForward => CarouselScroll.CanGoForward(_offset, _count, Visible);

        /// <summary>Where the track's thumb is, from 0 (the start) to 1 (the end).</summary>
        public float ThumbPosition => CarouselScroll.ThumbPosition(_offset, _count, Visible);

        /// <summary>The visible window of the row (drags in it browse).</summary>
        public RectTransform Viewport => _viewport;

        /// <summary>The distance between two tiles, in canvas units.</summary>
        public float Stride => _stride;

        public ShopItemTile Tile(int index) => index >= 0 && index < _count ? _tiles[index] : null;

        /// <summary>Makes the row hold <paramref name="count"/> tiles, made from the template as needed.</summary>
        public void SetCount(int count)
        {
            count = Math.Max(0, count);
            while (_tiles.Count < count)
            {
                ShopItemTile tile = Instantiate(_template, _content);
                tile.name = "Tile" + _tiles.Count;
                tile.Clicked += OnTileClicked;
                _tiles.Add(tile);
            }

            for (int i = 0; i < _tiles.Count; i++)
            {
                ShopItemTile tile = _tiles[i];
                bool used = i < count;
                if (used)
                {
                    ((RectTransform)tile.transform).anchoredPosition = _firstTile + new Vector2(i * _stride, 0f);
                }

                if (tile.gameObject.activeSelf != used)
                {
                    tile.gameObject.SetActive(used);
                }
            }

            if (_count != count)
            {
                _count = count;
                UiMotion.Stop(ref _motion);
                _offset = _target = CarouselScroll.Clamp(Mathf.Round(_target), count, Visible);
            }

            Apply();
        }

        /// <summary>Lays every tile's status or price line out again (the row has just been shown).</summary>
        public void RefreshRows()
        {
            for (int i = 0; i < _count; i++)
            {
                _tiles[i].RefreshRows();
            }
        }

        /// <summary>Back to the start of the row at once (a shop that opens afresh).</summary>
        public void ResetScroll()
        {
            UiMotion.Stop(ref _motion);
            _dragging = false;
            _offset = _target = 0f;
            Apply();
        }

        /// <summary>A page back (-1) or forward (+1), as the chevrons do.</summary>
        public void Page(int direction)
        {
            MoveTo(CarouselScroll.Page(_target, direction, _count, Visible), PageSeconds);
        }

        /// <summary>
        /// For the screen captures only (ShopPreview): the chevrons, the track and the hint as the
        /// mockups draw them, both arrows lit, whatever the count. The next change of position or
        /// count puts the rules back.
        /// </summary>
        internal void ShowBrowsingAsDrawn()
        {
            foreach (GameObject part in _browsing)
            {
                if (part != null)
                {
                    part.SetActive(true);
                }
            }

            SetArrow(_previousButton, _previousGroup, true);
            SetArrow(_nextButton, _nextGroup, true);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _count == 0)
            {
                return;
            }

            UiMotion.Stop(ref _motion);
            _dragging = true;
            _dragStartOffset = _offset;
            _dragStartX = LocalX(eventData);
            _sampleTime = Time.unscaledTime;
            _sampleOffset = _offset;
            _velocity = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            // A finger moving left brings later tiles in.
            float tilesMoved = (_dragStartX - LocalX(eventData)) / _stride;
            SetOffset(CarouselScroll.Drag(_dragStartOffset, tilesMoved, _count, Visible));

            float now = Time.unscaledTime;
            float elapsed = now - _sampleTime;
            if (elapsed > 0.0001f)
            {
                float speed = (_offset - _sampleOffset) / elapsed;
                _velocity = Mathf.Lerp(_velocity, speed, 0.7f);
                _sampleTime = now;
                _sampleOffset = _offset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            float velocity = Time.unscaledTime - _sampleTime > FlickWindow ? 0f : _velocity;
            MoveTo(CarouselScroll.Settle(_offset, velocity, _count, Visible), SettleSeconds);
        }

        private void OnEnable()
        {
            _previousButton.onClick.AddListener(OnPrevious);
            _nextButton.onClick.AddListener(OnNext);
            Apply();
        }

        private void OnDisable()
        {
            _previousButton.onClick.RemoveListener(OnPrevious);
            _nextButton.onClick.RemoveListener(OnNext);

            // Put away mid-move (another category, or the screen closing): rest where it was going.
            UiMotion.Stop(ref _motion);
            if (_dragging)
            {
                _dragging = false;
                _target = CarouselScroll.Settle(_offset, 0f, _count, Visible);
            }

            _offset = _target;
            Apply();
        }

        private void OnDestroy()
        {
            UiMotion.Stop(ref _motion);
        }

        private void OnPrevious() => Page(-1);

        private void OnNext() => Page(+1);

        private void OnTileClicked(ShopItemTile tile)
        {
            int index = _tiles.IndexOf(tile);
            if (index >= 0 && index < _count)
            {
                TileClicked?.Invoke(index);
            }
        }

        private void MoveTo(float target, float seconds)
        {
            UiMotion.Stop(ref _motion);
            _target = CarouselScroll.Clamp(target, _count, Visible);
            if (ReduceMotion || !isActiveAndEnabled || Mathf.Approximately(_target, _offset))
            {
                SetOffset(_target);
                return;
            }

            _motion = LMotion.Create(_offset, _target, seconds)
                .WithEase(Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(this, (offset, carousel) => carousel.SetOffset(offset));
        }

        private void SetOffset(float offset)
        {
            _offset = offset;
            Apply();
        }

        private void Apply()
        {
            if (_content == null)
            {
                return;     // not wired yet (the screen builder adds the component first)
            }

            _content.anchoredPosition = new Vector2(-_offset * _stride, _content.anchoredPosition.y);

            bool browsable = Browsable;
            foreach (GameObject part in _browsing)
            {
                if (part != null && part.activeSelf != browsable)
                {
                    part.SetActive(browsable);
                }
            }

            SetArrow(_previousButton, _previousGroup, CanGoBack);
            SetArrow(_nextButton, _nextGroup, CanGoForward);
            if (_thumb != null)
            {
                _thumb.anchoredPosition = new Vector2(_thumbStartX + _thumbTravel * ThumbPosition, _thumb.anchoredPosition.y);
            }
        }

        private static void SetArrow(Button button, CanvasGroup group, bool enabled)
        {
            if (button != null && button.interactable != enabled)
            {
                button.interactable = enabled;
            }

            if (group != null)
            {
                group.alpha = enabled ? 1f : DisabledAlpha;
            }
        }

        /// <summary>The pointer's x in the row's window.</summary>
        private float LocalX(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position, eventData.pressEventCamera, out Vector2 local);
            return local.x;
        }
    }
}
