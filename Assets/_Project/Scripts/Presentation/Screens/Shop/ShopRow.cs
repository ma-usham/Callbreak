using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// A short row of icons and words that stays centred when its words change (the shop's status
    /// line "(radio) Equipped", its price line "(coin) 350" and the button's "Buy for (coin) 350";
    /// mockups 15, 20 and 21). The screen builder places the items where the mockup draws them and
    /// calls <see cref="Capture"/>, which keeps the gaps between them and the row's centre;
    /// <see cref="Refresh"/> lays the items out again from left to right with those gaps, centred
    /// where the mockup centres them. Words are measured as drawn (their text bounds), pictures by
    /// their rectangles, both the same way at build time and at run time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopRow : MonoBehaviour
    {
        [SerializeField] private RectTransform[] _items;
        [SerializeField] private float[] _gaps;
        [SerializeField] private float _centreX;

        /// <summary>Remembers the gaps and the centre of the row as it stands (the builder calls it once).</summary>
        public void Capture()
        {
            if (_items == null || _items.Length == 0)
            {
                return;
            }

            _gaps = new float[_items.Length - 1];
            for (int i = 0; i < _gaps.Length; i++)
            {
                Extent(_items[i], out _, out float right);
                Extent(_items[i + 1], out float left, out _);
                _gaps[i] = left - right;
            }

            Extent(_items[0], out float rowLeft, out _);
            Extent(_items[_items.Length - 1], out _, out float rowRight);
            _centreX = (rowLeft + rowRight) / 2f;
        }

        /// <summary>Lays the row out again for its current words.</summary>
        public void Refresh()
        {
            if (_items == null || _items.Length == 0 || _gaps == null || _gaps.Length != _items.Length - 1)
            {
                return;
            }

            float total = 0f;
            for (int i = 0; i < _items.Length; i++)
            {
                Extent(_items[i], out float left, out float right);
                total += right - left;
                if (i < _gaps.Length)
                {
                    total += _gaps[i];
                }
            }

            float x = _centreX - total / 2f;
            for (int i = 0; i < _items.Length; i++)
            {
                Extent(_items[i], out float left, out float right);
                RectTransform item = _items[i];
                item.anchoredPosition = new Vector2(item.anchoredPosition.x + (x - left), item.anchoredPosition.y);
                x += right - left;
                if (i < _gaps.Length)
                {
                    x += _gaps[i];
                }
            }
        }

        /// <summary>An item's left and right edges in the row's space: the drawn words of a label, the rectangle of anything else.</summary>
        private static void Extent(RectTransform item, out float left, out float right)
        {
            float origin = item.anchoredPosition.x;
            var label = item.GetComponent<TMP_Text>();
            if (label != null)
            {
                label.ForceMeshUpdate(true, true);
                if (label.textInfo != null && label.textInfo.characterCount > 0)
                {
                    Bounds bounds = label.textBounds;
                    left = origin + bounds.min.x;
                    right = origin + bounds.max.x;
                    return;
                }

                left = right = origin;
                return;
            }

            Rect rect = item.rect;
            left = origin + rect.xMin;
            right = origin + rect.xMax;
        }
    }
}
