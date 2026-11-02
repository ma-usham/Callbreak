using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Offers;
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
    /// The shop (GDD §11 screen 15; Figma "V1 Rebuild · Shop", ADR 064; mockup 15's decks and the
    /// designer's spec shop-drawer-and-tables-prompts.md). It opens on Themes. The drawer is always
    /// open; its tabs switch between Themes and Cards (No Ads is bought on the main menu only, the
    /// owner, 2 Nov 2026; ADR 068). The wallet shows the balance as it changes; its "+" opens Earn
    /// Coins.
    /// <para>Themes: every theme's tile says Equipped, Owned, or its price (red while the wallet can't
    /// pay it). Tapping a tile opens its preview, the theme's menu background and its cloth side by
    /// side; its button buys the theme, equips it (its background
    /// and its cloth together), or says it is equipped. Buying asks once, naming the theme and its
    /// price; a bought theme is owned, not equipped, so the button turns to Equip theme and a toast
    /// says it is the player's. Without enough coins a dialog offers the Earn Coins page or Not now.</para>
    /// <para>The themes come in the shop's order: the one bought last first, then the others the
    /// player owns, then a festival offer's theme, then the rest, each group from the cheapest to the
    /// dearest (the owner, 31 Oct and 2 Nov 2026). A festival offer's theme (ADR 067) says Free under a
    /// Festival tag and the shop opens scrolled to it; its preview's button claims it (Tihar: "Watch
    /// ad • Free"), then turns to Equip theme in the same place. When the main menu opens the offer by
    /// itself, the shop shows only that preview, as the Festival Offer panel over the menus' background
    /// (the owner, 2 Nov 2026: "this should be on the main menu screen like daily login popups panel";
    /// ADR 074), and its cross goes back to the menu.</para>
    /// <para>Cards, as before: tapping a deck chooses it and shows its ace for a moment; the primary
    /// button equips an owned choice ("Use Selected Style", off while it is equipped) or buys one that
    /// is not owned, with the same questions.</para>
    /// Coming back from Earn Coins keeps the shop as it was, preview and all. Back leaves the shop.
    /// </summary>
    public sealed class ShopPresenter : IStartable, IDisposable
    {
        private static readonly Card AceOfSpades = new Card(Suit.Spades, Rank.Ace);

        // The themes that came with the shop's themes (ADR 064): their tiles carry a NEW tag.
        private static readonly HashSet<string> NewThemes = new HashSet<string>
        {
            CosmeticCatalog.Themes.HimalayanSunrise, CosmeticCatalog.Themes.LaliguransForest, CosmeticCatalog.Themes.GoldenTerraces,
            CosmeticCatalog.Themes.TiharLights, CosmeticCatalog.Themes.PrayerFlagPass, CosmeticCatalog.Themes.MithilaGarden,
            CosmeticCatalog.Themes.PhewaMoonlight,
        };

        private readonly ShopView _view;
        private readonly ScreenRouter _router;
        private readonly ICosmeticsShop _shop;
        private readonly IWallet _wallet;
        private readonly IDialogs _dialogs;
        private readonly IToast _toast;
        private readonly ILocalization _localization;
        private readonly CardSpriteProvider _cards;
        private readonly ISettingsService _settings;
        private readonly IAudioService _audio;
        private readonly IFestivalOffers _offers;
        private readonly ShopOpening _opening;
        private readonly ShopState _state;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _showing;
        private bool _covered;
        private int _depth;
        private bool _busy;
        private bool _disposed;

        // Opened by the main menu on the festival offer: the preview's cross leaves the shop.
        private bool _offerPopup;

        public ShopPresenter(
            ShopView view,
            ScreenRouter router,
            ICosmeticsShop shop,
            IWallet wallet,
            IDialogs dialogs,
            IToast toast,
            ILocalization localization,
            CardSpriteProvider cards,
            ISettingsService settings,
            IAudioService audio,
            IFestivalOffers offers,
            ShopOpening opening)
        {
            _view = view;
            _router = router;
            _shop = shop;
            _wallet = wallet;
            _dialogs = dialogs;
            _toast = toast;
            _localization = localization;
            _cards = cards;
            _settings = settings;
            _audio = audio;
            _offers = offers;
            _opening = opening;
            _state = new ShopState(shop, wallet, offers);
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.CategoryClicked += OnCategory;
            _view.ThemeClicked += OnTheme;
            _view.DeckClicked += OnDeck;
            _view.PrimaryClicked += OnPrimary;
            _view.EarnCoinsClicked += OnEarnCoins;
            _view.PreviewClosed += OnPreviewClosed;
            _view.PreviewBuyClicked += OnPreviewBuy;
            _view.PreviewEquipClicked += OnPreviewEquip;
            _view.PreviewClaimClicked += OnPreviewClaim;
            _router.Changed += OnScreenChanged;
            _shop.Changed += Render;
            _offers.Changed += Render;
            _wallet.Changed += Render;
            _localization.LanguageChanged += Render;
            OpenAfresh();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.CategoryClicked -= OnCategory;
            _view.ThemeClicked -= OnTheme;
            _view.DeckClicked -= OnDeck;
            _view.PrimaryClicked -= OnPrimary;
            _view.EarnCoinsClicked -= OnEarnCoins;
            _view.PreviewClosed -= OnPreviewClosed;
            _view.PreviewBuyClicked -= OnPreviewBuy;
            _view.PreviewEquipClicked -= OnPreviewEquip;
            _view.PreviewClaimClicked -= OnPreviewClaim;
            _router.Changed -= OnScreenChanged;
            _shop.Changed -= Render;
            _offers.Changed -= Render;
            _wallet.Changed -= Render;
            _localization.LanguageChanged -= Render;

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

        /// <summary>
        /// A fresh visit: Themes, scrolled to the festival offer's theme if there is one, else to the
        /// start; or, asked by the main menu, straight on the offer's preview.
        /// </summary>
        private void OpenAfresh()
        {
            _state.Reset();
            _offerPopup = _opening.TakeOffer() && _state.OpenOffer();
            _view.ShowPage(!_offerPopup);
            Render();
            _view.ScrollToTheme(Math.Max(0, _state.FestivalIndex));
        }

        private void Render()
        {
            if (_disposed)
            {
                return;
            }

            _view.SetCoins(ShopDisplay.Price(_wallet.Coins));
            RenderThemes();
            RenderDecks();
            _view.ShowCategory(_state.Category == CosmeticKind.Deck ? ShopView.Cards : ShopView.Themes);
            RenderPreview();

            CosmeticItem chosen = _state.Chosen(CosmeticKind.Deck);
            if (chosen != null && _state.ActionFor(CosmeticKind.Deck) == ShopAction.Buy)
            {
                _view.SetPrimaryBuy(ShopDisplay.Price(chosen.Price), !_busy);
            }
            else
            {
                _view.SetPrimaryUse(!_busy && _state.CanUse(CosmeticKind.Deck));
            }
        }

        private void RenderThemes()
        {
            IReadOnlyList<CosmeticItem> themes = _state.Themes;
            FestivalOffer offer = _offers.Claimable;
            for (int i = 0; i < themes.Count; i++)
            {
                CosmeticItem theme = themes[i];
                ShopTileStatus status = _state.StatusOf(CosmeticKind.Theme, i);
                int price = theme.Price;
                if (status == ShopTileStatus.Offer && offer.Kind == FestivalOfferKind.Discount)
                {
                    // A discount shows its own price where the shop's goes.
                    status = ShopTileStatus.Price;
                    price = offer.Price;
                }

                string statusKey = ShopDisplay.StatusKey(status);
                _view.SetTheme(
                    i,
                    _localization.Get(theme.NameKey),
                    _view.ThemePicture(theme.Id),
                    NewThemes.Contains(theme.Id),
                    _state.IsFestival(i),
                    status,
                    statusKey != null ? _localization.Get(statusKey) : null,
                    ShopDisplay.Price(price),
                    _wallet.Coins >= price,
                    _settings.ReduceMotion);
            }
        }

        private void RenderDecks()
        {
            IReadOnlyList<CosmeticItem> decks = _shop.Catalog(CosmeticKind.Deck);
            int chosen = _state.ChosenIndex(CosmeticKind.Deck);
            _view.SetDeckCount(decks.Count);
            for (int i = 0; i < decks.Count; i++)
            {
                CosmeticItem deck = decks[i];
                ShopTileStatus status = _state.StatusOf(CosmeticKind.Deck, i);
                string statusKey = ShopDisplay.StatusKey(status);
                _view.SetDeck(
                    i,
                    _localization.Get(deck.NameKey),
                    _cards.Back(deck.Id),
                    status,
                    statusKey != null ? _localization.Get(statusKey) : null,
                    ShopDisplay.Price(deck.Price),
                    i == chosen);
            }
        }

        private void RenderPreview()
        {
            CosmeticItem theme = _state.PreviewTheme;
            if (theme == null)
            {
                _view.ClosePreview();
                return;
            }

            Sprite background = _view.BackgroundPicture(theme.BackgroundId, out bool nightScene);
            Sprite cloth = _view.ClothPicture(theme.TableId, out Sprite frame);
            ThemeAction action = _state.PreviewAction;
            FestivalOffer offer = action == ThemeAction.Claim ? _offers.Claimable : null;

            // Opened by itself on the main menu, it is the Festival Offer panel, the theme named under the title.
            string themeName = _localization.Get(theme.NameKey);
            _view.SetPreviewSubtitle(_offerPopup ? _localization.Format("ui.offer.popup.subtitle", themeName.ToUpperInvariant()) : null);
            _view.ShowPreview(
                _offerPopup ? _localization.Get("ui.offer.popup.title") : themeName,
                offer != null ? OfferWords(offer) : _localization.Get(theme.NameKey + ".about"),
                background,
                nightScene,
                cloth,
                frame,
                action,
                ShopDisplay.Price(theme.Price),
                offer != null ? ClaimLabel(offer) : null,
                offer?.Kind ?? FestivalOfferKind.Free,
                !_busy);
        }

        /// <summary>The offer's words from the backend's key, or the game's own when the tables don't have that key.</summary>
        private string OfferWords(FestivalOffer offer)
        {
            string words = _localization.Get(offer.TitleKey);
            return words == offer.TitleKey ? _localization.Get(FestivalOfferReader.DefaultTitleKey) : words;
        }

        private string ClaimLabel(FestivalOffer offer)
        {
            switch (offer.Kind)
            {
                case FestivalOfferKind.WatchAd:
                    return _localization.Get("ui.offer.claim.watch_ad");
                case FestivalOfferKind.Discount:
                    return _localization.Format("ui.offer.claim.discount", ShopDisplay.Price(offer.Price));
                default:
                    return _localization.Get("ui.offer.claim.free");
            }
        }

        private void OnBack() => _router.Pop();

        private void OnCategory(int category)
        {
            if (category < 0 || category >= ShopState.Kinds.Count)
            {
                return;
            }

            _state.Show(ShopState.Kinds[category]);
            Render();
        }

        private void OnTheme(int index)
        {
            // Template-made tiles have no injected tap sound of their own (one UI cue a frame at most).
            _audio.Play(SoundCue.UiTap);
            if (_state.OpenPreview(index))
            {
                Render();
            }
        }

        private void OnPreviewClosed()
        {
            if (_offerPopup)
            {
                // The offer opened by itself over the main menu: its cross goes back there.
                _offerPopup = false;
                _router.Pop();
                return;
            }

            int shown = _state.Previewing;
            _state.ClosePreview();
            Render();
            _view.ScrollToTheme(shown);
        }

        private void OnPreviewEquip()
        {
            CosmeticItem theme = _state.PreviewTheme;
            if (_busy || theme == null || _state.PreviewAction != ThemeAction.Equip)
            {
                return;
            }

            _shop.Equip(CosmeticKind.Theme, theme.Id);
            Render();
        }

        private void OnPreviewBuy()
        {
            CosmeticItem theme = _state.PreviewTheme;
            if (_busy || theme == null || _state.PreviewAction != ThemeAction.Buy)
            {
                return;
            }

            _ = BuyAsync(CosmeticKind.Theme, theme);
        }

        private void OnPreviewClaim()
        {
            if (_busy || _state.PreviewAction != ThemeAction.Claim)
            {
                return;
            }

            _ = ClaimAsync();
        }

        /// <summary>
        /// Claims the festival offer (ADR 067): for Tihar, one rewarded ad watched to the end. The theme
        /// is then owned, not equipped: the button turns to Equip theme and a toast says it is the
        /// player's. No ad to show, or one not watched to the end, says so and gives nothing.
        /// </summary>
        private async Task ClaimAsync()
        {
            FestivalOffer offer = _offers.Claimable;
            if (offer == null)
            {
                // The offer ended since the button was drawn: the preview shows what it is now.
                Render();
                return;
            }

            string themeName = _localization.Get(_state.PreviewTheme?.NameKey ?? string.Empty);
            _busy = true;
            Render();
            try
            {
                if (offer.Kind == FestivalOfferKind.Discount)
                {
                    // A discount is a purchase: the same question, for the offer's price.
                    if (_wallet.Coins < offer.Price)
                    {
                        await OfferEarnCoinsAsync(themeName, offer.Price);
                        return;
                    }

                    bool buy = await _dialogs.ConfirmAsync(
                        new DialogRequest(
                            _localization.Format("ui.shop.buy.title", themeName),
                            _localization.Format("ui.shop.theme.buy.body", ShopDisplay.Price(offer.Price), ShopDisplay.Price(_wallet.Coins)),
                            _localization.Get("ui.shop.buy.confirm"),
                            _localization.Get("ui.shop.buy.cancel")),
                        _lifetime.Token);
                    if (!buy || _disposed)
                    {
                        return;
                    }
                }

                FestivalClaimResult result = await _offers.ClaimAsync(_lifetime.Token);
                if (_disposed)
                {
                    return;
                }

                switch (result)
                {
                    case FestivalClaimResult.Claimed:
                        _toast.Show(_localization.Format("ui.shop.theme.bought", themeName));
                        break;
                    case FestivalClaimResult.AdUnavailable:
                        _toast.Show(_localization.Get("ui.offer.ad_unavailable"));
                        break;
                    case FestivalClaimResult.AdNotWatched:
                        _toast.Show(_localization.Get("ui.offer.ad_not_watched"));
                        break;
                    case FestivalClaimResult.NotEnoughCoins:
                        await OfferEarnCoinsAsync(themeName, offer.Price);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
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

        private void OnEarnCoins()
        {
            if (_router.Current == ScreenId.Shop && _router.Has(ScreenId.EarnCoins))
            {
                _router.Push(ScreenId.EarnCoins);
            }
        }

        private void OnDeck(int index)
        {
            _audio.Play(SoundCue.UiTap);
            if (!_state.Choose(CosmeticKind.Deck, index))
            {
                return;
            }

            Render();
            string deck = _shop.Catalog(CosmeticKind.Deck)[index].Id;
            _view.PeekFace(index, _cards.Face(deck, AceOfSpades), _cards.Base(deck), _cards.Back(deck), _settings.ReduceMotion);
        }

        private void OnPrimary()
        {
            if (_busy)
            {
                return;
            }

            CosmeticItem deck = _state.Chosen(CosmeticKind.Deck);
            if (deck == null)
            {
                return;
            }

            if (_state.ActionFor(CosmeticKind.Deck) == ShopAction.Use)
            {
                if (_state.CanUse(CosmeticKind.Deck))
                {
                    _shop.Equip(CosmeticKind.Deck, deck.Id);
                }

                Render();
                return;
            }

            _ = BuyAsync(CosmeticKind.Deck, deck);
        }

        /// <summary>
        /// The purchase (designer's spec: a confirmation with the item and its price; success,
        /// cancellation and not-enough-coins states). Buying never equips: a bought theme says so in a toast.
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

                string body = kind == CosmeticKind.Theme
                    ? _localization.Format("ui.shop.theme.buy.body", ShopDisplay.Price(item.Price), ShopDisplay.Price(_wallet.Coins))
                    : _localization.Format("ui.shop.buy.body", ShopDisplay.Price(item.Price), ShopDisplay.Price(_wallet.Coins));
                bool buy = await _dialogs.ConfirmAsync(
                    new DialogRequest(
                        _localization.Format("ui.shop.buy.title", itemName),
                        body,
                        _localization.Get("ui.shop.buy.confirm"),
                        _localization.Get("ui.shop.buy.cancel")),
                    _lifetime.Token);
                if (!buy || _disposed)
                {
                    return;
                }

                PurchaseResult result = _shop.Buy(kind, item.Id);
                if (result == PurchaseResult.NotEnoughCoins)
                {
                    await OfferEarnCoinsAsync(itemName, item.Price);
                }
                else if (result == PurchaseResult.Bought && kind == CosmeticKind.Theme)
                {
                    _toast.Show(_localization.Format("ui.shop.theme.bought", itemName));
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
    }
}
