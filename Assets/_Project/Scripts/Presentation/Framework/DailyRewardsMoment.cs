using Callbreak.Application.Economy;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Opens Daily Rewards by itself once a day while a reward is ready (the owner, 8 Oct 2026; ADR
    /// 046). It only acts on the bare main menu: the menu up and visible (no curtain), no dialog open
    /// and the name already answered. So everything that comes first when the menu opens goes first:
    /// the name question, the rules primer and its practice offer, an invite link, a rejoin, the offer
    /// to resume an offline match, and prize coins flying into the wallet. The page then opens as soon
    /// as the main menu is bare again, also when a new day starts while the player sits on it. Once it
    /// has opened, the day's opening is
    /// used (<see cref="IDailyRewards.TakeAutoOpen"/>), also across restarts; the button below No Ads
    /// still opens it.
    /// </summary>
    public sealed class DailyRewardsMoment : ITickable
    {
        /// <summary>How often it looks, in seconds: a moment for whatever else opens with the menu to show first.</summary>
        private const float CheckSeconds = 0.5f;

        private readonly ScreenRouter _router;
        private readonly IAppFlow _flow;
        private readonly IProfileService _profile;
        private readonly IDialogs _dialogs;
        private readonly ILoadingCurtain _curtain;
        private readonly IDailyRewards _rewards;
        private readonly IUnseenCoins _unseen;

        private float _nextCheck = -1f;

        public DailyRewardsMoment(
            ScreenRouter router,
            IAppFlow flow,
            IProfileService profile,
            IDialogs dialogs,
            ILoadingCurtain curtain,
            IDailyRewards rewards,
            IUnseenCoins unseen)
        {
            _router = router;
            _flow = flow;
            _profile = profile;
            _dialogs = dialogs;
            _curtain = curtain;
            _rewards = rewards;
            _unseen = unseen;
        }

        public void Tick()
        {
            float now = Time.unscaledTime;
            if (_nextCheck < 0f)
            {
                // The first frame of the menu: the screens that open with it get their turn first.
                _nextCheck = now + CheckSeconds;
                return;
            }

            if (now < _nextCheck)
            {
                return;
            }

            _nextCheck = now + CheckSeconds;
            if (IsBareMainMenu() && _rewards.TakeAutoOpen())
            {
                _router.Push(ScreenId.DailyRewards);
            }
        }

        private bool IsBareMainMenu()
        {
            return _router.Current == ScreenId.MainMenu
                && _router.Has(ScreenId.DailyRewards)
                && _flow.State == AppState.Menu
                && !_curtain.IsCovering
                && !_dialogs.IsOpen
                && !FirstRun.AsksName(_profile)
                && _unseen.Amount == 0;
        }
    }
}
