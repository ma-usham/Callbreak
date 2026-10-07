using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The main menu's No Ads button (the owner, 5 Oct 2026): the $0.99 one-time purchase that removes
    /// the ads between matches. The button shows while the store can sell No Ads
    /// (<see cref="INoAds.CanBuy"/>) and goes once it is owned, following <see cref="INoAds.Changed"/>;
    /// it also stays while its own purchase is under way, so it doesn't vanish under the finger. A tap
    /// opens the store's purchase sheet, which is the confirmation, and a second tap meanwhile does
    /// nothing. While the sheet is up the game is covered in black with the loading emblem (ADR 042):
    /// the sheet can turn the game to portrait behind it. A toast then says what became of it, once
    /// the cover has gone: thanks once it's bought, that a pending payment brings No Ads once it's
    /// paid, or that the store isn't available; a closed sheet says nothing.
    /// The presenter shows <see cref="Visible"/> and passes the taps on. No UnityEngine, so its rules
    /// are tested without the Editor.
    /// </summary>
    public sealed class NoAdsOffer : IDisposable
    {
        private const string BoughtKey = "ui.main_menu.no_ads.bought";
        private const string AlreadyOwnedKey = "ui.main_menu.no_ads.already_owned";
        private const string PendingKey = "ui.main_menu.no_ads.pending";
        private const string UnavailableKey = "ui.main_menu.no_ads.unavailable";

        private readonly INoAds _noAds;
        private readonly IToast _toast;
        private readonly ILocalization _localization;
        private readonly ILoadingCurtain _curtain;

        private bool _following;
        private bool _buying;

        public NoAdsOffer(INoAds noAds, IToast toast, ILocalization localization, ILoadingCurtain curtain)
        {
            _noAds = noAds;
            _toast = toast;
            _localization = localization;
            _curtain = curtain;
        }

        /// <summary><see cref="Visible"/> may have changed.</summary>
        public event Action Changed;

        /// <summary>The button shows: No Ads can be bought, or this button's purchase of it is under way.</summary>
        public bool Visible => !_noAds.Owned && (_noAds.CanBuy || _buying);

        /// <summary>This button's purchase is under way; a tap now does nothing.</summary>
        public bool Buying => _buying;

        /// <summary>Follows the store from now on.</summary>
        public void Start()
        {
            if (!_following)
            {
                _following = true;
                _noAds.Changed += OnNoAdsChanged;
            }
        }

        public void Dispose()
        {
            if (_following)
            {
                _following = false;
                _noAds.Changed -= OnNoAdsChanged;
            }
        }

        /// <summary>
        /// A tap: covers the game in black, buys No Ads through the store's sheet, takes the cover
        /// away and says in a toast what became of it. Does nothing while a purchase is under way or
        /// while No Ads can't be bought. Completes once the store has answered and the cover has gone.
        /// A cancelled token ends it with the store's <see cref="OperationCanceledException"/> and no
        /// toast. Should the store throw against its contract, the toast says the store isn't
        /// available and the exception is passed on. The cover goes in every case.
        /// </summary>
        public async Task TapAsync(CancellationToken ct)
        {
            if (_buying || _noAds.Owned || !_noAds.CanBuy)
            {
                return;
            }

            _buying = true;
            NoAdsPurchase purchase = NoAdsPurchase.Unavailable;
            ExceptionDispatchInfo broken = null;
            try
            {
                // Black before the sheet opens: the sheet can turn the game to portrait behind it.
                await _curtain.CoverDarkAsync(ct);
                purchase = await _noAds.BuyAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                broken = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                await _curtain.UncoverDarkAsync(CancellationToken.None);
                _buying = false;
                Changed?.Invoke();
            }

            if (broken != null)
            {
                Show(UnavailableKey);
                broken.Throw();
            }

            string key = ToastKey(purchase);
            if (key != null)
            {
                Show(key);
            }
        }

        /// <summary>What the toast says about a purchase; null when it says nothing (the player closed the sheet).</summary>
        private static string ToastKey(NoAdsPurchase purchase)
        {
            switch (purchase)
            {
                case NoAdsPurchase.Bought:
                    return BoughtKey;
                case NoAdsPurchase.AlreadyOwned:
                    return AlreadyOwnedKey;
                case NoAdsPurchase.Pending:
                    return PendingKey;
                case NoAdsPurchase.Unavailable:
                case NoAdsPurchase.Failed:
                    return UnavailableKey;
                default:
                    return null;
            }
        }

        private void Show(string key) => _toast.Show(_localization.Get(key));

        private void OnNoAdsChanged() => Changed?.Invoke();
    }
}
