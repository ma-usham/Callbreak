using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The shop (GDD §11 screen 15; mockups 15, 20 and 21; designer's spec
    /// shop-drawer-and-tables-prompts.md). It opens on Cards with the drawer open and each
    /// category's choice on its equipped item, every row at its start. The drawer's storefront
    /// disc opens and closes it; its buttons switch categories, each keeping its choice and its
    /// row's position. Tapping a tile chooses it (a preview: a deck also shows its ace for a
    /// moment). The primary button equips an owned choice ("Use Selected ...", off while it is
    /// equipped) or buys one that is not owned: with enough coins a confirmation names the item and
    /// its price, and a bought item is owned (not equipped), so the button turns to Use; without
    /// enough, a dialog offers the Earn Coins page or Not now. Coming back from Earn Coins keeps
    /// the shop as it was. Back leaves the shop.
    /// </summary>
    public sealed class ShopPresenter : IStartable, IDisposable
    {
        private static readonly Card AceOfSpades = new Card(Suit.Spades, Rank.Ace);

        private readonly ShopView _view;
        private readonly ScreenRouter _router;
        private readonly ICosmeticsShop _shop;
        private readonly IWallet _wallet;
        private readonly IDialogs _dialogs;
        private readonly ILocalization _localization;
        private readonly CardSpriteProvider _cards;
        private readonly ISettingsService _settings;
        private readonly IAudioService _audio;
        private readonly ShopState _state;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _showing;
        private bool _covered;
        private int _depth;
        private bool _busy;
        private bool _disposed;

        public ShopPresenter(
            ShopView view,
            ScreenRouter router,
            ICosmeticsShop shop,
            IWallet wallet,
            IDialogs dialogs,
            ILocalization localization,
            CardSpriteProvider cards,
            ISettingsService settings,
            IAudioService audio)
        {
            _view = view;
            _router = router;
            _shop = shop;
            _wallet = wallet;
            _dialogs = dialogs;
            _localization = localization;
            _cards = cards;
            _settings = settings;
            _audio = audio;
            _state = new ShopState(shop, wallet);
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.DrawerToggleClicked += OnDrawerToggle;
            _view.CategoryClicked += OnCategory;
            _view.TileClicked += OnTile;
            _view.PrimaryClicked += OnPrimary;
            _router.Changed += OnScreenChanged;
            _shop.Changed += Render;
            _wallet.Changed += Render;
            _localization.LanguageChanged += Render;
            _settings.Changed += RenderMotion;
            RenderMotion();
            OpenAfresh();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.DrawerToggleClicked -= OnDrawerToggle;
            _view.CategoryClicked -= OnCategory;
            _view.TileClicked -= OnTile;
            _view.PrimaryClicked -= OnPrimary;
            _router.Changed -= OnScreenChanged;
            _shop.Changed -= Render;
            _wallet.Changed -= Render;
            _localization.LanguageChanged -= Render;
            _settings.Changed -= RenderMotion;

            // A question still open is answered "no" and closes with the screen.
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        /// <summary>
        /// The shop showing again is a fresh visit, unless it is coming back from a page opened on
        /// top of it (Earn Coins), which finds it as it was left.
        /// </summary>
        private void OnScreenChanged(ScreenId id)
        {
            if (id == ScreenId.Shop)
            {
                if (!_covered || _router.Depth != _depth)
                {
                    OpenAfresh();
                }
                else
                {
                    Render();
                }

                _showing = true;
                _covered = false;
                _depth = _router.Depth;
                return;
            }

            if (_showing)
            {
                _covered = _router.Depth > _depth;
                _showing = false;
            }
        }

        private void OpenAfresh()
        {
            _state.Reset();
            _view.ResetScroll();
            _view.SetDrawerOpen(true, instant: true);
            Render();
        }

        private void RenderMotion() => _view.SetReduceMotion(_settings.ReduceMotion);

        private void Render()
        {
            if (_disposed)
            {
                return;
            }

            foreach (CosmeticKind kind in ShopState.Kinds)
            {
                RenderCategory(kind);
            }

            CosmeticKind shown = _state.Category;
            _view.ShowCategory(Index(shown));
            CosmeticItem chosen = _state.Chosen(shown);
            if (chosen != null && _state.ActionFor(shown) == ShopAction.Buy)
            {
                _view.SetPrimaryBuy(ShopDisplay.Price(chosen.Price), !_busy);
            }
            else
            {
                _view.SetPrimaryUse(!_busy && _state.CanUse(shown));
            }
        }

        private void RenderCategory(CosmeticKind kind)
        {
            IReadOnlyList<CosmeticItem> items = _shop.Catalog(kind);
            int category = Index(kind);
            int chosen = _state.ChosenIndex(kind);
            _view.SetTileCount(category, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                CosmeticItem item = items[i];
                ShopTileStatus status = _state.StatusOf(kind, i);
                string statusKey = ShopDisplay.StatusKey(status);
                _view.SetTile(
                    category,
                    i,
                    _localization.Get(item.NameKey),
                    PictureOf(item),
                    status,
                    statusKey != null ? _localization.Get(statusKey) : null,
                    ShopDisplay.Price(item.Price),
                    i == chosen);
            }
        }

        private Sprite PictureOf(CosmeticItem item)
        {
            return item.Kind == CosmeticKind.Deck ? _cards.Back(item.Id) : _view.Picture(item.Kind, item.Id);
        }

        private void OnBack() => _router.Pop();

        private void OnDrawerToggle() => _view.SetDrawerOpen(!_view.IsDrawerOpen, instant: _settings.ReduceMotion);

        private void OnCategory(int category)
        {
            if (category < 0 || category >= ShopState.Kinds.Count)
            {
                return;
            }

            _state.Show(ShopState.Kinds[category]);
            Render();
        }

        private void OnTile(int category, int index)
        {
            if (category < 0 || category >= ShopState.Kinds.Count)
            {
                return;
            }

            // Template-made tiles have no injected tap sound of their own (one UI cue a frame at most).
            _audio.Play(SoundCue.UiTap);
            CosmeticKind kind = ShopState.Kinds[category];
            if (!_state.Choose(kind, index))
            {
                return;
            }

            Render();
            if (kind == CosmeticKind.Deck)
            {
                string deck = _shop.Catalog(kind)[index].Id;
                _view.PeekFace(index, _cards.Face(deck, AceOfSpades), _cards.Back(deck), _settings.ReduceMotion);
            }
        }

        private void OnPrimary()
        {
            if (_busy)
            {
                return;
            }

            CosmeticKind kind = _state.Category;
            CosmeticItem item = _state.Chosen(kind);
            if (item == null)
            {
                return;
            }

            if (_state.ActionFor(kind) == ShopAction.Use)
            {
                if (_state.CanUse(kind))
                {
                    _shop.Equip(kind, item.Id);
                }

                Render();
                return;
            }

            _ = BuyAsync(kind, item);
        }

        /// <summary>
        /// The purchase (designer's spec: a confirmation with the item and its price; success,
        /// cancellation and not-enough-coins states). Buying never equips.
        /// </summary>
        private async Task BuyAsync(CosmeticKind kind, CosmeticItem item)
        {
            _busy = true;
            Render();
            try
            {
                string itemName = _localization.Get(item.NameKey);
                if (_wallet.Coins < item.Price)
                {
                    await OfferEarnCoinsAsync(itemName, item.Price);
                    return;
                }

                bool buy = await _dialogs.ConfirmAsync(
                    new DialogRequest(
                        _localization.Format("ui.shop.buy.title", itemName),
                        _localization.Format("ui.shop.buy.body", ShopDisplay.Price(item.Price), ShopDisplay.Price(_wallet.Coins)),
                        _localization.Get("ui.shop.buy.confirm"),
                        _localization.Get("ui.shop.buy.cancel")),
                    _lifetime.Token);
                if (!buy || _disposed)
                {
                    return;
                }

                if (_shop.Buy(kind, item.Id) == PurchaseResult.NotEnoughCoins)
                {
                    await OfferEarnCoinsAsync(itemName, item.Price);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _busy = false;
                Render();
            }
        }

        /// <summary>Not enough coins: the Earn Coins page, or Not now.</summary>
        private async Task OfferEarnCoinsAsync(string itemName, int price)
        {
            DialogChoice choice = await _dialogs.AskAsync(
                new DialogRequest(
                    _localization.Get("ui.shop.not_enough.title"),
                    _localization.Format("ui.shop.not_enough.body", itemName, ShopDisplay.Price(price), ShopDisplay.Price(_wallet.Coins)),
                    _localization.Get("ui.shop.not_enough.earn"),
                    _localization.Get("ui.shop.not_enough.later")),
                _lifetime.Token);
            if (choice == DialogChoice.Confirm && !_disposed && _router.Current == ScreenId.Shop && _router.Has(ScreenId.EarnCoins))
            {
                _router.Push(ScreenId.EarnCoins);
            }
        }

        private static int Index(CosmeticKind kind)
        {
            for (int i = 0; i < ShopState.Kinds.Count; i++)
            {
                if (ShopState.Kinds[i] == kind)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
