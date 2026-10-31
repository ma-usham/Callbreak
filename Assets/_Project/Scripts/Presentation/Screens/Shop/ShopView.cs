using System;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The shop (Figma "V1 Rebuild · Shop", ADR 064; mockup 15's decks, the owner, 5 Oct 2026): the
    /// equipped background behind it, Back at the top left, a cream panel with the tab's title and
    /// subtitle and the wallet (its "+" opens Earn Coins), and the drawer on the right, always open
    /// (<see cref="ShopDrawer"/>): Themes, Cards, and No Ads under them while the store sells it.
    /// Themes shows a row of every theme that scrolls sideways (<see cref="ThemeShelf"/>); tapping one
    /// opens its preview over the shop (<see cref="ThemePreview"/>). Cards shows the three decks as
    /// they were (<see cref="ShopTiles"/>, nothing scrolls) with the primary button under them ("Use
    /// Selected Style", or "Buy for (coin) N"); tapping a deck turns its picture over to the deck's ace
    /// of spades for a moment and back (<see cref="PeekFace"/>, timed by <see cref="DeckPeek"/>). A
    /// primary button that is not interactable shows its disabled look with its words dimmed.
    /// </summary>
    public sealed class ShopView : UIScreen
    {
        public const int CategoryCount = 2;
        public const int Themes = 0;
        public const int Cards = 1;

        private const float DimmedAlpha = 0.5f;
        private const int NoTile = -1;

        [SerializeField] private Button _backButton;
        [SerializeField] private ShopDrawer _drawer;
        [SerializeField] private GameObject[] _title;
        [SerializeField] private GameObject[] _subtitle;
        [SerializeField] private GameObject _themesPage;
        [SerializeField] private ThemeShelf _shelf;
        [SerializeField] private GameObject _cardsPage;
        [SerializeField] private ShopTiles _decks;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private CanvasGroup _primaryContent;
        [SerializeField] private GameObject _useLabel;
        [SerializeField] private ShopRow _buyRow;
        [SerializeField] private TMP_Text _buyPrice;
        [SerializeField] private TMP_Text _walletBalance;
        [SerializeField] private Button _earnCoinsButton;
        [SerializeField] private ThemePreview _preview;
        [SerializeField] private ShopPictures _pictures;
        [SerializeField] private TableBackdropLibrary _library;

        private int _category;
        private bool _buying;

        private MotionHandle _peekMotion;
        private int _peeking = NoTile;
        private Sprite _peekFace;
        private Sprite _peekBase;
        private Sprite _peekBack;
        private bool _peekWithoutFlip;
        private float _peekTime;

        public event Action BackClicked;

        /// <summary>A drawer tab: 0 Themes, 1 Cards.</summary>
        public event Action<int> CategoryClicked;

        /// <summary>The drawer's No Ads button.</summary>
        public event Action NoAdsClicked;

        /// <summary>A theme's tile was tapped, by its place in the row.</summary>
        public event Action<int> ThemeClicked;

        /// <summary>A deck's tile was tapped, by its place.</summary>
        public event Action<int> DeckClicked;

        public event Action PrimaryClicked;

        /// <summary>The wallet's "+", on the panel or in the preview.</summary>
        public event Action EarnCoinsClicked;

        public event Action PreviewClosed;

        /// <summary>The preview's arrows: -1 the previous theme, +1 the next.</summary>
        public event Action<int> PreviewStepped;

        public event Action PreviewBuyClicked;

        public event Action PreviewEquipClicked;

        /// <summary>The tab that shows: 0 Themes, 1 Cards.</summary>
        public int Category => _category;

        public ShopDrawer Drawer => _drawer;

        public ThemeShelf Shelf => _shelf;

        public ThemePreview Preview => _preview;

        /// <summary>The primary button under the decks shows "Buy for (coin) N" (otherwise "Use Selected Style").</summary>
        public bool ShowsBuy => _buying;

        public bool PrimaryInteractable => _primaryButton.interactable;

        public string BuyPrice => _buyPrice.text;

        public string Coins => _walletBalance.text;

        /// <summary>The three decks' tiles.</summary>
        public ShopTiles Decks => _decks;

        public TableBackdropLibrary Library => _library;

        public void ShowCategory(int category)
        {
            _category = Mathf.Clamp(category, 0, CategoryCount - 1);
            if (_category != Cards)
            {
                EndPeek();
            }

            for (int i = 0; i < CategoryCount; i++)
            {
                bool shows = i == _category;
                SetActive(_title[i], shows);
                SetActive(_subtitle[i], shows);
            }

            SetActive(_themesPage, _category == Themes);
            SetActive(_cardsPage, _category == Cards);
            _drawer.SetActive(_category);
            ShowPrimaryWords();

            // Words measured while their tiles were hidden were never drawn: measure them now.
            if (_category == Cards)
            {
                _decks.RefreshRows();
            }
            else
            {
                _shelf.RefreshRows();
            }
        }

        public override void OnShown()
        {
            base.OnShown();
            _shelf.RefreshRows();
            _decks.RefreshRows();
            if (_buying)
            {
                _buyRow.Refresh();
            }

            _preview.RefreshRows();
        }

        public void SetCoins(string coins)
        {
            if (_walletBalance.text != coins)
            {
                _walletBalance.text = coins;
            }

            _preview.SetCoins(coins);
        }

        public void SetNoAdsVisible(bool visible) => _drawer.SetNoAdsVisible(visible);

        /// <summary>One theme's tile; a null picture keeps the one the tile has.</summary>
        public void SetTheme(int index, string themeName, Sprite picture, bool isNew, ShopTileStatus status, string statusText, string priceText, bool affordable)
        {
            ThemeTile tile = _shelf.Tile(index);
            if (tile != null)
            {
                tile.Show(themeName, picture, isNew, status, statusText, priceText, affordable);
            }
        }

        public void SetDeckCount(int count) => _decks.SetCount(count);

        /// <summary>
        /// One deck: its name, picture and status, and whether it is the chosen one. A missing picture
        /// keeps the current one; a deck that is showing its face keeps doing so and turns back to
        /// this picture.
        /// </summary>
        public void SetDeck(int index, string deckName, Sprite picture, ShopTileStatus status, string statusText, string priceText, bool chosen)
        {
            ShopItemTile tile = _decks.Tile(index);
            if (tile == null)
            {
                return;
            }

            if (picture != null)
            {
                SetDeckPicture(index, tile, picture);
            }

            tile.Show(deckName, status, statusText, priceText, chosen);
        }

        /// <summary>"Use Selected Style".</summary>
        public void SetPrimaryUse(bool interactable)
        {
            _buying = false;
            ShowPrimaryWords();
            SetPrimaryInteractable(interactable);
        }

        /// <summary>"Buy for (coin) <paramref name="price"/>".</summary>
        public void SetPrimaryBuy(string price, bool interactable)
        {
            _buying = true;
            if (_buyPrice.text != price)
            {
                _buyPrice.text = price;
            }

            ShowPrimaryWords();
            _buyRow.Refresh();
            SetPrimaryInteractable(interactable);
        }

        /// <summary>A theme's tile picture (its background over its cloth), or null.</summary>
        public Sprite ThemePicture(string id) => _pictures != null ? _pictures.Get("Theme", id) : null;

        /// <summary>A background's picture, and whether it is a night scene, whose words are drawn light.</summary>
        public Sprite BackgroundPicture(string id, out bool nightScene)
        {
            nightScene = false;
            if (_library == null)
            {
                return null;
            }

            Sprite picture = _library.BackgroundPicture(id, out string shown);
            nightScene = _library.Background(shown)?.NightScene ?? false;
            return picture;
        }

        /// <summary>A cloth's picture and its border (null for none).</summary>
        public Sprite ClothPicture(string id, out Sprite frame)
        {
            frame = _library != null ? _library.TableFrame(id) : null;
            return _library != null ? _library.TablePicture(id, out _) : null;
        }

        public void ShowPreview(string themeName, string description, Sprite background, bool nightScene, Sprite cloth, Sprite frame, ThemeAction action, string price, bool interactable)
        {
            _preview.Open();
            _preview.Show(themeName, description, background, nightScene, cloth, frame, action, price, interactable);
        }

        public void ClosePreview() => _preview.Close();

        /// <summary>Brings a theme's tile into view (after the preview's arrows have moved on).</summary>
        public void ScrollToTheme(int index) => _shelf.ScrollTo(index);

        /// <summary>
        /// Lets the player see a deck's faces: the deck's picture turns over to <paramref name="face"/>
        /// (it narrows to its edge, changes side and opens again), stays for
        /// <see cref="DeckPeek.FaceHoldSeconds"/> and turns back to <paramref name="back"/>. Another
        /// deck that was still showing its face is put back on its back at once; the same deck, asked
        /// again while it peeks, keeps its face up for another hold without a jump
        /// (<see cref="DeckPeek.TimeAfterTapAgain"/>). With <paramref name="reduceMotion"/> the picture
        /// changes side without turning.
        /// </summary>
        public void PeekFace(int index, Sprite face, Sprite cardBase, Sprite back, bool reduceMotion)
        {
            ShopItemTile tile = _decks.Tile(index);
            if (tile == null || face == null)
            {
                EndPeek();
                return;
            }

            bool again = index == _peeking && reduceMotion == _peekWithoutFlip;
            float from = again ? DeckPeek.TimeAfterTapAgain(_peekTime, reduceMotion) : 0f;
            if (again)
            {
                StopPeekMotion();
            }
            else
            {
                EndPeek();
                _peeking = index;
                _peekWithoutFlip = reduceMotion;
                _peekBack = tile.Face.sprite;
            }

            _peekFace = face;
            _peekBase = cardBase;
            if (back != null)
            {
                _peekBack = back;
            }

            ApplyPeek(from);

            float seconds = DeckPeek.Seconds(reduceMotion);
            _peekMotion = LMotion.Create(from, seconds, seconds - from)
                .WithOnComplete(OnPeekComplete)
                .Bind(this, (time, view) => view.ApplyPeek(time));
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _primaryButton.onClick.AddListener(OnPrimary);
            _earnCoinsButton.onClick.AddListener(OnEarnCoins);
            _drawer.CategoryClicked += OnCategory;
            _drawer.NoAdsClicked += OnNoAds;
            _shelf.TileClicked += OnTheme;
            _decks.TileClicked += OnDeck;
            _preview.CloseButton.onClick.AddListener(OnPreviewClose);
            _preview.PreviousButton.onClick.AddListener(OnPreviewPrevious);
            _preview.NextButton.onClick.AddListener(OnPreviewNext);
            _preview.BuyButton.onClick.AddListener(OnPreviewBuy);
            _preview.EquipButton.onClick.AddListener(OnPreviewEquip);
            _preview.EarnCoinsButton.onClick.AddListener(OnEarnCoins);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _primaryButton.onClick.RemoveListener(OnPrimary);
            _earnCoinsButton.onClick.RemoveListener(OnEarnCoins);
            _drawer.CategoryClicked -= OnCategory;
            _drawer.NoAdsClicked -= OnNoAds;
            _shelf.TileClicked -= OnTheme;
            _decks.TileClicked -= OnDeck;
            _preview.CloseButton.onClick.RemoveListener(OnPreviewClose);
            _preview.PreviousButton.onClick.RemoveListener(OnPreviewPrevious);
            _preview.NextButton.onClick.RemoveListener(OnPreviewNext);
            _preview.BuyButton.onClick.RemoveListener(OnPreviewBuy);
            _preview.EquipButton.onClick.RemoveListener(OnPreviewEquip);
            _preview.EarnCoinsButton.onClick.RemoveListener(OnEarnCoins);

            // A screen that opens again shows every deck by its back.
            EndPeek();
        }

        private void ShowPrimaryWords()
        {
            SetActive(_useLabel, !_buying);
            SetActive(_buyRow.gameObject, _buying);
        }

        private void SetPrimaryInteractable(bool interactable)
        {
            if (_primaryButton.interactable != interactable)
            {
                _primaryButton.interactable = interactable;
            }

            _primaryContent.alpha = interactable ? 1f : DimmedAlpha;
        }

        private void SetDeckPicture(int index, ShopItemTile tile, Sprite picture)
        {
            if (index == _peeking)
            {
                bool showsBack = tile.Face.sprite == _peekBack;
                _peekBack = picture;
                if (!showsBack)
                {
                    return;
                }
            }

            tile.SetPicture(picture);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void ApplyPeek(float time)
        {
            ShopItemTile tile = _peeking == NoTile ? null : _decks.Tile(_peeking);
            if (tile == null)
            {
                return;
            }

            _peekTime = time;
            DeckPeek.Pose(time, _peekWithoutFlip, out bool showsFace, out float width);
            Sprite side = showsFace ? _peekFace : _peekBack;
            if (tile.Face.sprite != side)
            {
                tile.Face.sprite = side;
            }

            ShowBase(tile, showsFace ? _peekBase : null);

            tile.Picture.localScale = new Vector3(width, 1f, 1f);
        }

        private void OnPeekComplete()
        {
            // This runs inside the motion's own completion: forget the handle rather than cancel it.
            _peekMotion = default;
            RestOnBack();
        }

        /// <summary>Stops a peek where it is and puts that deck back on its back.</summary>
        private void EndPeek()
        {
            StopPeekMotion();
            RestOnBack();
        }

        private void StopPeekMotion()
        {
            if (_peekMotion.IsActive())
            {
                _peekMotion.Cancel();
            }

            _peekMotion = default;
        }

        private void RestOnBack()
        {
            if (_peeking == NoTile)
            {
                return;
            }

            // Also reached while the screen is being torn down, when its children may be gone already.
            ShopItemTile tile = _decks != null ? _decks.Tile(_peeking) : null;
            if (tile != null && tile.Face != null)
            {
                tile.Face.sprite = _peekBack;
                ShowBase(tile, null);
            }

            if (tile != null && tile.Picture != null)
            {
                tile.Picture.localScale = Vector3.one;
            }

            _peeking = NoTile;
            _peekFace = null;
            _peekBase = null;
            _peekBack = null;
        }

        /// <summary>
        /// The deck's base under the face it shows (ADR 053), the face drawn with its premultiplied
        /// material (ADR 054); null hides the base, under a back.
        /// </summary>
        private static void ShowBase(ShopItemTile tile, Sprite cardBase)
        {
            Image under = tile.Base;
            if (under == null)
            {
                return;
            }

            CardFaceMaterial.Apply(tile.Face, tile.FaceMaterial, cardBase != null);

            if (under.sprite != cardBase)
            {
                under.sprite = cardBase;
            }

            bool visible = cardBase != null;
            if (under.enabled != visible)
            {
                under.enabled = visible;
            }
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnCategory(int category) => CategoryClicked?.Invoke(category);

        private void OnNoAds() => NoAdsClicked?.Invoke();

        private void OnTheme(int index) => ThemeClicked?.Invoke(index);

        private void OnDeck(int index) => DeckClicked?.Invoke(index);

        private void OnPrimary() => PrimaryClicked?.Invoke();

        private void OnEarnCoins() => EarnCoinsClicked?.Invoke();

        private void OnPreviewClose() => PreviewClosed?.Invoke();

        private void OnPreviewPrevious() => PreviewStepped?.Invoke(-1);

        private void OnPreviewNext() => PreviewStepped?.Invoke(1);

        private void OnPreviewBuy() => PreviewBuyClicked?.Invoke();

        private void OnPreviewEquip() => PreviewEquipClicked?.Invoke();
    }
}
