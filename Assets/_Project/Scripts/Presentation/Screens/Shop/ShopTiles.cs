using System;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// One shop category's items: three tiles side by side in the panel, spaced as the category's
    /// mockup spaces them (15 Cards, 20 Backgrounds, 21 Tables). Nothing scrolls: every category
    /// shows exactly three items (the owner, 5 Oct 2026). The screen builder makes the three tiles;
    /// a catalogue with fewer items hides the tiles it does not need, and one with more shows its
    /// first three. Every category has its own tiles, so each keeps its choice while another one
    /// shows.
    /// </summary>
    public sealed class ShopTiles : MonoBehaviour
    {
        [SerializeField] private ShopItemTile[] _tiles = new ShopItemTile[0];

        private int _count;

        /// <summary>The player tapped the tile at this index.</summary>
        public event Action<int> TileClicked;

        /// <summary>How many tiles show: the catalogue's items, no more than the tiles there are.</summary>
        public int Count => _count;

        public ShopItemTile Tile(int index) => index >= 0 && index < _count ? _tiles[index] : null;

        /// <summary>Shows the first <paramref name="count"/> tiles and hides the others.</summary>
        public void SetCount(int count)
        {
            _count = Mathf.Clamp(count, 0, _tiles.Length);
            for (int i = 0; i < _tiles.Length; i++)
            {
                bool used = i < _count;
                if (_tiles[i].gameObject.activeSelf != used)
                {
                    _tiles[i].gameObject.SetActive(used);
                }
            }
        }

        /// <summary>Lays every tile's status or price line out again (the tiles have just been shown).</summary>
        public void RefreshRows()
        {
            for (int i = 0; i < _count; i++)
            {
                _tiles[i].RefreshRows();
            }
        }

        private void OnEnable()
        {
            foreach (ShopItemTile tile in _tiles)
            {
                tile.Clicked += OnTileClicked;
            }
        }

        private void OnDisable()
        {
            foreach (ShopItemTile tile in _tiles)
            {
                tile.Clicked -= OnTileClicked;
            }
        }

        private void OnTileClicked(ShopItemTile tile)
        {
            int index = Array.IndexOf(_tiles, tile);
            if (index >= 0 && index < _count)
            {
                TileClicked?.Invoke(index);
            }
        }
    }
}
