using System;
using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Framework;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The shop (mockups 15 Cards, 20 Backgrounds and 21 Tables; designer's spec
    /// shop-drawer-and-tables-prompts.md; the owner, 5 Oct 2026): Back at the top left, the
    /// category's title and subtitle, its three items in the panel (<see cref="ShopTiles"/>, nothing
    /// scrolls), the primary button under them ("Use Selected Style / Background / Table", or "Buy
    /// for (coin) N"), and the drawer on the right, always open (<see cref="ShopDrawer"/>). Each
    /// category has its own title, subtitle, tiles and Use label, each fitted on its own mockup;
    /// showing a category switches them. Tapping a deck turns its picture over to the deck's ace of
    /// spades for a moment and back (<see cref="PeekFace"/>, timed by <see cref="DeckPeek"/>). A
    /// primary button that is not interactable shows its disabled look with its words dimmed.
    /// </summary>
    public sealed class ShopView : UIScreen
    {
        public const int CategoryCount = 3;

        private const int Cards = 0;
        private const float DimmedAlpha = 0.5f;
        private const int NoTile = -1;

        [SerializeField] private Button _backButton;
        [SerializeField] private ShopDrawer _drawer;
        [SerializeField] private GameObject[] _title;
        [SerializeField] private GameObject[] _subtitle;
        [SerializeField] private ShopTiles[] _tiles;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private CanvasGroup _primaryContent;
        [SerializeField] private GameObject[] _useLabel;
        [SerializeField] private ShopRow _buyRow;
        [SerializeField] private TMP_Text _buyPrice;
        [SerializeField] private ShopPictures _pictures;

        private int _category;
        private bool _buying;

        private MotionHandle _peekMotion;
        private int _peeking = NoTile;
        private Sprite _peekFace;
        private Sprite _peekBack;
        private bool _peekWithoutFlip;
        private float _peekTime;

        public event Action BackClicked;

        /// <summary>A drawer button: 0 Cards, 1 Backgrounds, 2 Tables.</summary>
        public event Action<int> CategoryClicked;

        /// <summary>A tile was tapped: its category and its index in that category.</summary>
        public event Action<int, int> TileClicked;

        public event Action PrimaryClicked;

        /// <summary>The category that shows: 0 Cards, 1 Backgrounds, 2 Tables.</summary>
        public int Category => _category;

        public ShopDrawer Drawer => _drawer;

        /// <summary>The primary button shows "Buy for (coin) N" (otherwise a Use label).</summary>
        public bool ShowsBuy => _buying;

        public bool PrimaryInteractable => _primaryButton.interactable;

        public string BuyPrice => _buyPrice.text;

        /// <summary>A category's three tiles: 0 Cards, 1 Backgrounds, 2 Tables.</summary>
        public ShopTiles Tiles(int category) => _tiles[category];

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
                SetActive(_tiles[i].gameObject, shows);
            }

            _drawer.SetActive(_category);
            ShowPrimaryWords();

            // Words measured while their tiles were hidden were never drawn: measure them now.
            _tiles[_category].RefreshRows();
        }

        public override void OnShown()
        {
            base.OnShown();
            _tiles[_category].RefreshRows();
            if (_buying)
            {
                _buyRow.Refresh();
            }
        }

        public void SetTileCount(int category, int count) => _tiles[category].SetCount(count);

        /// <summary>
        /// One tile: the item's name, picture and status, and whether it is the chosen one. A
        /// missing picture keeps the current one; a deck that is showing its face keeps doing so
        /// and turns back to this picture.
        /// </summary>
        public void SetTile(int category, int index, string itemName, Sprite picture, ShopTileStatus status, string statusText, string priceText, bool chosen)
        {
            ShopItemTile tile = _tiles[category].Tile(index);
            if (tile == null)
            {
                return;
            }

            if (picture != null)
            {
                SetPicture(category, index, tile, picture);
            }

            tile.Show(itemName, status, statusText, priceText, chosen);
        }

        /// <summary>"Use Selected Style / Background / Table", for the category that shows.</summary>
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

        /// <summary>The picture the screen has for a background or a table, or null.</summary>
        public Sprite Picture(CosmeticKind kind, string id) => _pictures != null ? _pictures.Get(kind.ToString(), id) : null;

        /// <summary>
        /// Lets the player see a deck's faces: the Cards tile's picture turns over to
        /// <paramref name="face"/> (it narrows to its edge, changes side and opens again), stays
        /// for <see cref="DeckPeek.FaceHoldSeconds"/> and turns back to <paramref name="back"/>.
        /// Another deck that was still showing its face is put back on its back at once; the same
        /// deck, asked again while it peeks, keeps its face up for another hold without a jump
        /// (<see cref="DeckPeek.TimeAfterTapAgain"/>). With <paramref name="reduceMotion"/> the
        /// picture changes side without turning.
        /// </summary>
        public void PeekFace(int index, Sprite face, Sprite back, bool reduceMotion)
        {
            ShopItemTile tile = _tiles[Cards].Tile(index);
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
            _drawer.CategoryClicked += OnCategory;
            for (int i = 0; i < _tiles.Length; i++)
            {
                _tiles[i].TileClicked += TileHandler(i);
            }
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _primaryButton.onClick.RemoveListener(OnPrimary);
            _drawer.CategoryClicked -= OnCategory;
            for (int i = 0; i < _tiles.Length; i++)
            {
                _tiles[i].TileClicked -= TileHandler(i);
            }

            // A screen that opens again shows every deck by its back.
            EndPeek();
        }

        private Action<int> TileHandler(int category)
        {
            switch (category)
            {
                case 0:
                    return OnCardsTile;
                case 1:
                    return OnBackgroundsTile;
                default:
                    return OnTablesTile;
            }
        }

        private void OnCardsTile(int index) => TileClicked?.Invoke(0, index);

        private void OnBackgroundsTile(int index) => TileClicked?.Invoke(1, index);

        private void OnTablesTile(int index) => TileClicked?.Invoke(2, index);

        private void ShowPrimaryWords()
        {
            for (int i = 0; i < _useLabel.Length; i++)
            {
                SetActive(_useLabel[i], !_buying && i == _category);
            }

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

        private void SetPicture(int category, int index, ShopItemTile tile, Sprite picture)
        {
            if (category == Cards && index == _peeking)
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
            ShopItemTile tile = _peeking == NoTile ? null : _tiles[Cards].Tile(_peeking);
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
            ShopItemTile tile = _tiles != null && _tiles.Length > Cards && _tiles[Cards] != null ? _tiles[Cards].Tile(_peeking) : null;
            if (tile != null && tile.Face != null)
            {
                tile.Face.sprite = _peekBack;
            }

            if (tile != null && tile.Picture != null)
            {
                tile.Picture.localScale = Vector3.one;
            }

            _peeking = NoTile;
            _peekFace = null;
            _peekBack = null;
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnCategory(int category) => CategoryClicked?.Invoke(category);

        private void OnPrimary() => PrimaryClicked?.Invoke();
    }
}
