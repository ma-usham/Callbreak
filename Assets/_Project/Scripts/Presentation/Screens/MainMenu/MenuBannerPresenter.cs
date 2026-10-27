using System;
using Callbreak.Application.Economy;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Presentation.Framework;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The banner ad in the bottom-left corner of the main menu (the owner, 27 Oct 2026, ADR 052). When
    /// the menu opens it makes room there while a banner may come (no No Ads), before the menu first
    /// shows, and keeps that look while the Menu scene is up, so nothing moves under the player when an
    /// ad loads, No Ads is bought or a refund comes; a player who owns No Ads, and the Editor, keep the
    /// menu as it always was. The banner may then show in the menu's banner slot while the bare main
    /// menu is in front of the player. The banner is the ad network's own view, drawn over the whole
    /// game, so it goes off the screen whenever anything covers the main menu: another screen (the
    /// shop, settings, the profile, a room, the daily rewards...), a dialog, the loading curtain, or the
    /// name question. Asked every frame, so a slot that moved (the Resume banner coming or going, the
    /// phone turned round) moves the banner too. Whether there is a banner at all (No Ads, the network)
    /// is <see cref="IBannerAds"/>'s business.
    /// </summary>
    public sealed class MenuBannerPresenter : IStartable, ITickable, IDisposable
    {
        private readonly MainMenuView _menu;
        private readonly ScreenRouter _router;
        private readonly IAppFlow _flow;
        private readonly IProfileService _profile;
        private readonly IBannerAds _banner;
        private readonly IDialogs _dialogs;
        private readonly ILoadingCurtain _curtain;

        public MenuBannerPresenter(
            MainMenuView menu,
            ScreenRouter router,
            IAppFlow flow,
            IProfileService profile,
            IBannerAds banner,
            IDialogs dialogs,
            ILoadingCurtain curtain)
        {
            _menu = menu ?? throw new ArgumentNullException(nameof(menu));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _banner = banner ?? throw new ArgumentNullException(nameof(banner));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _curtain = curtain ?? throw new ArgumentNullException(nameof(curtain));
        }

        public void Start() => _menu.SetBannerRoom(_banner.MayShow);

        public void Tick()
        {
            if (MenuInFront() && _menu.TryGetBannerSlot(out BannerSlot slot))
            {
                _banner.ShowIn(slot);
            }
            else
            {
                _banner.Hide();
            }
        }

        public void Dispose() => _banner.Hide();

        /// <summary>The bare main menu is in front of the player, with nothing over it.</summary>
        private bool MenuInFront()
        {
            return _router.Current == ScreenId.MainMenu
                && _flow.State == AppState.Menu
                && !_dialogs.IsOpen
                && !_curtain.IsCovering
                && !FirstRun.AsksName(_profile);
        }
    }
}
