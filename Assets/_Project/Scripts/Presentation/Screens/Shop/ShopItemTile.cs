using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// One of a shop category's three items (mockups 15, 20 and 21): its picture (a deck's back, a
    /// background's thumbnail, a table's preview), its name, and under it the status line (a filled
    /// radio and "Equipped") for the item in use, the price line (the coin and the price) for one not
    /// owned, or nothing (the owner, 5 Oct 2026). The chosen tile carries the crimson outline. Both
    /// lines stay centred under the name when their words change (<see cref="ShopRow"/>).
    /// </summary>
    public sealed class ShopItemTile : MonoBehaviour
    {
        [SerializeField] private Button _button;

        // What turns when a deck shows its ace: the picture together with its shadow.
        [SerializeField] private RectTransform _picture;
        [SerializeField] private Image _face;

        // Under a deck's face while it peeks: the deck's base (ADR 053). Only deck tiles have one.
        [SerializeField] private Image _base;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private GameObject _outline;
        [SerializeField] private ShopRow _statusRow;
        [SerializeField] private GameObject _radioOn;
        [SerializeField] private GameObject _radioOff;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private ShopRow _priceRow;
        [SerializeField] private TMP_Text _price;

        public event Action<ShopItemTile> Clicked;

        /// <summary>The picture with its shadow, which narrows while a deck turns over.</summary>
        public RectTransform Picture => _picture;

        public Image Face => _face;

        /// <summary>The deck's base under its face while it peeks; null on tiles that are not decks.</summary>
        public Image Base => _base;

        public TMP_Text NameLabel => _name;

        public TMP_Text StatusLabel => _status;

        public TMP_Text PriceLabel => _price;

        public bool IsChosen => _outline.activeSelf;

        public bool RadioFilled => _statusRow.gameObject.activeSelf && _radioOn.activeSelf;

        /// <summary>The status line ("Equipped") shows.</summary>
        public bool ShowsStatus => _statusRow.gameObject.activeSelf;

        public bool ShowsPrice => _priceRow.gameObject.activeSelf;

        /// <summary>Shows an item's name and status (its picture is set by <see cref="SetPicture"/>).</summary>
        public void Show(string itemName, ShopTileStatus status, string statusText, string priceText, bool chosen)
        {
            SetText(_name, itemName);
            _outline.SetActive(chosen);
            bool price = status == ShopTileStatus.Price;
            bool line = status == ShopTileStatus.Equipped;
            _statusRow.gameObject.SetActive(line);
            _priceRow.gameObject.SetActive(price);
            if (price)
            {
                SetText(_price, priceText);
                _priceRow.Refresh();
                return;
            }

            if (!line)
            {
                return;
            }

            bool filled = ShopDisplay.RadioFilled(status);
            _radioOn.SetActive(filled);
            _radioOff.SetActive(!filled);
            SetText(_status, statusText);
            _statusRow.Refresh();
        }

        /// <summary>
        /// Lays the status or price line out again. A tile filled in while its category was hidden
        /// measured words that had never been drawn, so the view calls this once the category shows.
        /// </summary>
        public void RefreshRows()
        {
            if (_statusRow.gameObject.activeSelf)
            {
                _statusRow.Refresh();
            }

            if (_priceRow.gameObject.activeSelf)
            {
                _priceRow.Refresh();
            }
        }

        /// <summary>
        /// The item's picture; none hides it. A picture that fills its frame (a background's
        /// thumbnail) keeps its proportions and is cropped by the frame's rounded mask.
        /// </summary>
        public void SetPicture(Sprite picture)
        {
            if (_face.sprite != picture)
            {
                _face.sprite = picture;
            }

            _face.enabled = picture != null;
            var cover = _face.GetComponent<AspectRatioFitter>();
            if (cover != null && picture != null && picture.rect.height > 0f)
            {
                cover.aspectRatio = picture.rect.width / picture.rect.height;
            }
        }

        private void OnEnable() => _button.onClick.AddListener(OnClick);

        private void OnDisable() => _button.onClick.RemoveListener(OnClick);

        private void OnClick() => Clicked?.Invoke(this);

        private static void SetText(TMP_Text label, string text)
        {
            text = text ?? string.Empty;
            if (label.text != text)
            {
                label.text = text;
            }
        }
    }
}
