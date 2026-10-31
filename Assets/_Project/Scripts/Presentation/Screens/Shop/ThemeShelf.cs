using System;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The shop's row of themes (Figma "V1 Rebuild · Shop", ADR 064): one <see cref="ThemeTile"/> per
    /// theme, side by side in a row that scrolls sideways, with a thin bar under it that shows where
    /// the row is. The row is as wide as the panel lets it be, so a wider screen shows more tiles and
    /// the next tile always peeks in at the edge. A tile that is tapped is reported by its place.
    /// </summary>
    public sealed class ThemeShelf : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private ThemeTile[] _tiles = Array.Empty<ThemeTile>();

        private Action[] _handlers = Array.Empty<Action>();

        /// <summary>A tile was tapped, by its place in the row.</summary>
        public event Action<int> TileClicked;

        public int Count => _tiles.Length;

        public ScrollRect Scroll => _scroll;

        public ThemeTile Tile(int index) => index >= 0 && index < _tiles.Length ? _tiles[index] : null;

        /// <summary>Scrolls just enough to show the whole tile at that place.</summary>
        public void ScrollTo(int index)
        {
            ThemeTile tile = Tile(index);
            if (tile == null || _scroll == null || _scroll.content == null || _scroll.viewport == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            RectTransform content = _scroll.content;
            float hidden = content.rect.width - _scroll.viewport.rect.width;
            if (hidden <= 0f)
            {
                return;
            }

            var rect = (RectTransform)tile.transform;
            float left = rect.anchoredPosition.x - rect.rect.width * rect.pivot.x;
            float right = left + rect.rect.width;
            float shownLeft = _scroll.horizontalNormalizedPosition * hidden;
            float shownRight = shownLeft + _scroll.viewport.rect.width;
            float margin = 24f;
            if (left - margin < shownLeft)
            {
                shownLeft = left - margin;
            }
            else if (right + margin > shownRight)
            {
                shownLeft = right + margin - _scroll.viewport.rect.width;
            }

            _scroll.StopMovement();
            _scroll.horizontalNormalizedPosition = Mathf.Clamp01(shownLeft / hidden);
        }

        /// <summary>Lays every tile's rows out again (words measured while the row was hidden were never drawn).</summary>
        public void RefreshRows()
        {
            foreach (ThemeTile tile in _tiles)
            {
                if (tile != null)
                {
                    tile.RefreshRows();
                }
            }
        }

        private void OnEnable()
        {
            _handlers = new Action[_tiles.Length];
            for (int i = 0; i < _tiles.Length; i++)
            {
                int index = i;
                _handlers[i] = () => TileClicked?.Invoke(index);
                _tiles[i].Clicked += _handlers[i];
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _tiles.Length && i < _handlers.Length; i++)
            {
                _tiles[i].Clicked -= _handlers[i];
            }
        }
    }
}
