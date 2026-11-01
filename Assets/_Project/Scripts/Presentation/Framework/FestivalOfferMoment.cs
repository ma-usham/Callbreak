using Callbreak.Application.Economy;
using Callbreak.Application.Flow;
using Callbreak.Application.Offers;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Presentation.Screens;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Opens a festival offer by itself, once per offer, on the bare main menu (the owner, 31 Oct 2026:
    /// "show popup once, on the bare mainmenu like daily reward"; ADR 067): the shop opens straight on
    /// the offer's theme in its preview, whose cross goes back to the menu. Like
    /// <see cref="DailyRewardsMoment"/> it waits for the main menu up and visible, no dialog, the name
    /// answered and no prize coins still to fly in, and it looks a moment later than Daily Rewards, so
    /// a day's reward opens first and the offer after it, once the menu is bare again. Once it has
    /// opened, the offer's opening is used (<see cref="IFestivalOffers.TakeAutoOpen"/>), also across
    /// restarts; the Shop button's red dot stays until the offer is claimed or ends.
    /// </summary>
    public sealed class FestivalOfferMoment : ITickable
    {
        /// <summary>How often it looks, in seconds.</summary>
        private const float CheckSeconds = 0.5f;

        /// <summary>Its first look, after Daily Rewards' first look (half a second).</summary>
        private const float FirstCheckSeconds = 1f;

        private readonly ScreenRouter _router;
        private readonly IAppFlow _flow;
        private readonly IProfileService _profile;
        private readonly IDialogs _dialogs;
        private readonly ILoadingCurtain _curtain;
        private readonly IFestivalOffers _offers;
        private readonly IUnseenCoins _unseen;
        private readonly ShopOpening _opening;

        private float _nextCheck = -1f;

        public FestivalOfferMoment(
            ScreenRouter router,
            IAppFlow flow,
            IProfileService profile,
            IDialogs dialogs,
            ILoadingCurtain curtain,
            IFestivalOffers offers,
            IUnseenCoins unseen,
            ShopOpening opening)
        {
            _router = router;
            _flow = flow;
            _profile = profile;
            _dialogs = dialogs;
            _curtain = curtain;
            _offers = offers;
            _unseen = unseen;
            _opening = opening;
        }

        public void Tick()
        {
            float now = Time.unscaledTime;
            if (_nextCheck < 0f)
            {
                // The first frame of the menu: the screens that open with it, and Daily Rewards, go first.
                _nextCheck = now + FirstCheckSeconds;
                return;
            }

            if (now < _nextCheck)
            {
                return;
            }

            _nextCheck = now + CheckSeconds;
            if (IsBareMainMenu() && _offers.TakeAutoOpen())
            {
                _opening.RequestOffer();
                _router.Push(ScreenId.Shop);
            }
        }

        private bool IsBareMainMenu()
        {
            return _router.Current == ScreenId.MainMenu
                && _router.Has(ScreenId.Shop)
                && _flow.State == AppState.Menu
                && !_curtain.IsCovering
                && !_dialogs.IsOpen
                && !FirstRun.AsksName(_profile)
                && _unseen.Amount == 0;
        }
    }
}
