using Callbreak.Application.Offers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The theme preview over the shop (Figma "V1 Rebuild · Shop", ADR 064): the theme's name, "One
    /// purchase, both looks", and the theme's two halves side by side with a "+" between them, so the
    /// player sees they get both: the main menu on its background, and a game on its cloth. Each
    /// half is a small screen, a 1920 × 1080 group scaled down: the real background or cloth (with
    /// the cloth's border tiled round its edges) under a picture of the menu or the table taken from
    /// the game. Under them the theme's description and its button: Buy for (coin) N, Equip theme,
    /// or Equipped; for a festival offer's theme (ADR 067), the offer's words and its claim button
    /// ("Watch ad • Free"), which turns to Equip theme in the same place once claimed. The cross
    /// closes it (no arrows: the owner, 31 Oct 2026), and the wallet shows in its corner, its "+"
    /// opening Earn Coins. Opened by itself on the main menu, it is the Festival Offer panel: its
    /// title and the line under it say so (ADR 074).
    /// </summary>
    public sealed class ThemePreview : MonoBehaviour
    {
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _subtitle;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private Image _menuBackground;
        [SerializeField] private Image _menuOverlay;
        [SerializeField] private Sprite _menuOverlayDay;
        [SerializeField] private Sprite _menuOverlayNight;
        [SerializeField] private Image _cloth;
        [SerializeField] private Image _clothFrame;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _buyButton;
        [SerializeField] private CanvasGroup _buyContent;
        [SerializeField] private ShopRow _buyRow;
        [SerializeField] private TMP_Text _buyPrice;
        [SerializeField] private Button _equipButton;
        [SerializeField] private Button _claimButton;
        [SerializeField] private CanvasGroup _claimContent;
        [SerializeField] private ShopRow _claimRow;
        [SerializeField] private Image _claimIcon;
        [SerializeField] private TMP_Text _claimLabel;
        [SerializeField] private Sprite _watchAdIcon;
        [SerializeField] private Sprite _freeIcon;
        [SerializeField] private Sprite _discountIcon;
        [SerializeField] private CanvasGroup _equipContent;
        [SerializeField] private GameObject _equipped;
        [SerializeField] private TMP_Text _walletBalance;
        [SerializeField] private Button _earnCoinsButton;

        public Button CloseButton => _closeButton;

        public Button BuyButton => _buyButton;

        public Button EquipButton => _equipButton;

        public Button ClaimButton => _claimButton;

        public string ClaimLabel => _claimLabel.text;

        public Sprite ClaimIcon => _claimIcon.sprite;

        public Sprite WatchAdIcon => _watchAdIcon;

        public Button EarnCoinsButton => _earnCoinsButton;

        public bool IsOpen => gameObject.activeSelf;

        public string Title => _title.text;

        public string Subtitle => _subtitle != null ? _subtitle.text : null;

        public string Description => _description.text;

        public string BuyPrice => _buyPrice.text;

        public Sprite MenuBackground => _menuBackground.sprite;

        public Sprite Cloth => _cloth.sprite;

        public Sprite ClothFrame => _clothFrame.enabled ? _clothFrame.sprite : null;

        /// <summary>The preview's button: which of Buy, Equip theme, Equipped and the offer's claim shows.</summary>
        public ThemeAction Action { get; private set; }

        public bool ShowsNightMenu => _menuOverlay.sprite == _menuOverlayNight && _menuOverlayNight != null;

        public void Open()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void Close()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The line under the title: <paramref name="text"/>, or the preview's own ("One purchase, both
        /// looks", in the player's language) when null.
        /// </summary>
        public void SetSubtitle(string text)
        {
            if (_subtitle == null)
            {
                return;
            }

            // Its own words follow the language; re-enabled, it puts them back.
            var own = _subtitle.GetComponent<Framework.LocalizedText>();
            bool showOwn = text == null;
            if (own != null && own.enabled != showOwn)
            {
                own.enabled = showOwn;
            }

            if (!showOwn)
            {
                SetText(_subtitle, text);
            }
        }

        /// <summary>
        /// Shows a theme: its name and words, its background under the menu (whose small words turn
        /// cream on a night scene), its cloth with its border (none for a cloth without one) under the
        /// table, and its button; <paramref name="interactable"/> off dims the button (a purchase or a
        /// claim is under way). A claim shows <paramref name="claimLabel"/> after the mark of its kind:
        /// the ad's play mark, a gift, or a coin.
        /// </summary>
        public void Show(string themeName, string description, Sprite background, bool nightScene, Sprite cloth, Sprite frame, ThemeAction action, string price, string claimLabel, FestivalOfferKind claimKind, bool interactable)
        {
            SetText(_title, themeName);
            SetText(_description, description);
            SetSprite(_menuBackground, background);
            Sprite overlay = nightScene && _menuOverlayNight != null ? _menuOverlayNight : _menuOverlayDay;
            SetSprite(_menuOverlay, overlay);
            if (_menuOverlay.enabled != (_menuOverlay.sprite != null))
            {
                _menuOverlay.enabled = _menuOverlay.sprite != null;
            }
            SetSprite(_cloth, cloth);
            if (_clothFrame.sprite != frame)
            {
                _clothFrame.sprite = frame;
            }

            if (_clothFrame.enabled != (frame != null))
            {
                _clothFrame.enabled = frame != null;
            }

            Action = action;
            SetActive(_buyButton.gameObject, action == ThemeAction.Buy);
            SetActive(_equipButton.gameObject, action == ThemeAction.Equip);
            SetActive(_equipped, action == ThemeAction.Equipped);
            SetActive(_claimButton.gameObject, action == ThemeAction.Claim);
            if (action == ThemeAction.Buy)
            {
                SetText(_buyPrice, price);
                _buyRow.Refresh();
            }
            else if (action == ThemeAction.Claim)
            {
                SetText(_claimLabel, claimLabel);
                SetSprite(_claimIcon, claimKind == FestivalOfferKind.WatchAd ? _watchAdIcon : claimKind == FestivalOfferKind.Free ? _freeIcon : _discountIcon);
                _claimRow.Refresh();
            }

            SetInteractable(_buyButton, _buyContent, interactable);
            SetInteractable(_equipButton, _equipContent, interactable);
            SetInteractable(_claimButton, _claimContent, interactable);
        }

        /// <summary>The wallet in the preview's corner.</summary>
        public void SetCoins(string coins) => SetText(_walletBalance, coins);

        /// <summary>Lays the Buy row out again (words measured while the preview was hidden were never drawn).</summary>
        public void RefreshRows()
        {
            if (_buyButton.gameObject.activeSelf)
            {
                _buyRow.Refresh();
            }

            if (_claimButton.gameObject.activeSelf)
            {
                _claimRow.Refresh();
            }
        }

        private static void SetInteractable(Button button, CanvasGroup content, bool interactable)
        {
            if (button.interactable != interactable)
            {
                button.interactable = interactable;
            }

            if (content != null)
            {
                content.alpha = interactable ? 1f : DimmedAlpha;
            }
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (sprite != null && image.sprite != sprite)
            {
                image.sprite = sprite;
            }
        }

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
