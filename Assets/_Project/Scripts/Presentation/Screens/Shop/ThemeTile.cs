using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// One theme in the shop's Themes row (Figma "V1 Rebuild · Shop", ADR 064): the theme's background
    /// over a strip of its cloth (one picture), a NEW tag on the new themes, its name, "Background +
    /// cloth", and then "Equipped" with a filled radio and a crimson outline, "Owned" with an empty
    /// radio, or a coin and its price, in red while the wallet can't pay it. Tapping it opens the
    /// theme's preview.
    /// </summary>
    public sealed class ThemeTile : MonoBehaviour
    {
        private static readonly Color AffordableInk = new Color32(0x57, 0x2E, 0x12, 0xFF);
        private static readonly Color UnaffordableInk = new Color32(0xB3, 0x26, 0x1E, 0xFF);

        [SerializeField] private Button _button;
        [SerializeField] private Image _picture;
        [SerializeField] private GameObject _newTag;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private GameObject _outline;
        [SerializeField] private ShopRow _statusRow;
        [SerializeField] private GameObject _radioOn;
        [SerializeField] private GameObject _radioOff;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private ShopRow _priceRow;
        [SerializeField] private TMP_Text _price;

        public event Action Clicked;

        public Image Picture => _picture;

        public TMP_Text Name => _name;

        public TMP_Text Price => _price;

        public TMP_Text StatusText => _status;

        public bool ShowsOutline => _outline != null && _outline.activeSelf;

        public bool ShowsNew => _newTag != null && _newTag.activeSelf;

        public bool ShowsPrice => _priceRow != null && _priceRow.gameObject.activeSelf;

        public bool ShowsStatus => _statusRow != null && _statusRow.gameObject.activeSelf;

        public bool RadioFilled => _radioOn != null && _radioOn.activeSelf;

        /// <summary>Puts a theme on the tile; a null picture keeps the one it has.</summary>
        public void Show(string themeName, Sprite picture, bool isNew, ShopTileStatus status, string statusText, string priceText, bool affordable)
        {
            if (picture != null && _picture.sprite != picture)
            {
                _picture.sprite = picture;
            }

            if (_picture.enabled != (_picture.sprite != null))
            {
                _picture.enabled = _picture.sprite != null;
            }

            SetText(_name, themeName);
            SetActive(_newTag, isNew);
            SetActive(_outline, status == ShopTileStatus.Equipped);

            bool price = status == ShopTileStatus.Price;
            SetActive(_priceRow.gameObject, price);
            SetActive(_statusRow.gameObject, !price);
            if (price)
            {
                SetText(_price, priceText);
                Color ink = affordable ? AffordableInk : UnaffordableInk;
                if (_price.color != ink)
                {
                    _price.color = ink;
                }

                _priceRow.Refresh();
            }
            else
            {
                bool filled = ShopDisplay.RadioFilled(status);
                SetActive(_radioOn, filled);
                SetActive(_radioOff, !filled);
                SetText(_status, statusText);
                _statusRow.Refresh();
            }
        }

        /// <summary>Lays the status and price rows out again (their words are measured once drawn).</summary>
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

        private void OnEnable() => _button.onClick.AddListener(OnClick);

        private void OnDisable() => _button.onClick.RemoveListener(OnClick);

        private void OnClick() => Clicked?.Invoke();

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null && label.text != text)
            {
                label.text = text;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
