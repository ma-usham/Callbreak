using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// The Menu scene's scope (GDD §21 FIG 21.1): every menu screen is a prefab instance in the
    /// scene, registered as itself (for its presenter) and as a <see cref="UIScreen"/> (for the
    /// router). Presenters are entry points, so they start with the scene and are disposed when it
    /// unloads.
    /// </summary>
    public sealed class MenuLifetimeScope : LifetimeScope
    {
        [SerializeField] private UIScreen[] _screens;

        // The name question when the game starts (ADR 022), on its own canvas above the screens.
        [SerializeField] private NamePromptView _namePrompt;

        protected override void Awake()
        {
            base.Awake();
            if (Container != null)
            {
                // Disposed before the scene unloads, while the views its presenters hold still exist.
                Container.Resolve<ISceneLoader>().BeforeNextLoad(Dispose);
            }
        }

        protected override void Configure(IContainerBuilder builder)
        {
            foreach (UIScreen screen in _screens)
            {
                if (screen != null)
                {
                    builder.RegisterComponent(screen).AsSelf();
                }
            }

            builder.Register<ScreenRouter>(Lifetime.Scoped);
            builder.Register<MatchLauncher>(Lifetime.Scoped);

            RegisterPresenter<MainMenuView, MainMenuPresenter>(builder);
            RegisterPresenter<PlayWithFriendsView, PlayWithFriendsPresenter>(builder);
            RegisterPresenter<CreateRoomView, CreateRoomPresenter>(builder);
            RegisterPresenter<JoinRoomView, JoinRoomPresenter>(builder);
            RegisterPresenter<PrivateLobbyView, PrivateLobbyPresenter>(builder);
            RegisterPresenter<MatchmakingView, MatchmakingPresenter>(builder);
            RegisterPresenter<OfflineSetupView, OfflineSetupPresenter>(builder);
            RegisterPresenter<SettingsView, SettingsPresenter>(builder);
            RegisterPresenter<HowToPlayView, HowToPlayPresenter>(builder);
            RegisterPresenter<ProfileView, ProfilePresenter>(builder);
            RegisterPresenter<ShopView, ShopPresenter>(builder);
            RegisterPresenter<EarnCoinsView, EarnCoinsPresenter>(builder);
            RegisterPresenter<PlayOnlineView, PlayOnlinePresenter>(builder);
            RegisterPresenter<DailyRewardsView, DailyRewardsPresenter>(builder);

            // Before the first screen opens, so the main menu has made room for its banner when its
            // buttons rise into place (ADR 052).
            RegisterBanner(builder);

            // Last, so every presenter is already listening when the first screen opens.
            builder.RegisterEntryPoint<MenuEntryPoint>();

            // Above everything the menu does by itself: it also hears Android back first.
            if (_namePrompt != null)
            {
                builder.RegisterComponent(_namePrompt);
                builder.RegisterEntryPoint<NamePromptPresenter>().AsSelf();
            }

            // After the first screen is up; an invite link outranks the rejoin offer, which
            // outranks the offer to resume an offline match.
            builder.RegisterEntryPoint<RejoinOffer>();
            builder.RegisterEntryPoint<JoinLinkHandler>();

            // A newer version on Google Play, whenever the main menu is on top again (ADR 036).
            builder.RegisterEntryPoint<ForcedUpdateMoments>();

            // Last of all: the first-run guide only acts on a bare main menu.
            builder.RegisterEntryPoint<FirstRunGuide>();

            // Daily Rewards opens by itself on the bare main menu once a day (ADR 046).
            RegisterPresenter<DailyRewardsView, DailyRewardsMoment>(builder);

            // An app open ad may greet the player back on the bare main menu (ADR 047).
            builder.RegisterEntryPoint<ReturnToGameAdMoment>();
        }

        /// <summary>
        /// The banner ad in the bottom-left corner of the main menu (the owner, 27 Oct 2026, ADR 052): over
        /// the project's banner network where there is one (AdMob on phones, with the table's ad unit, or
        /// a test's), made with the Menu scene and released with it; elsewhere a stand-in that never has
        /// one, so the menu makes no room for it. No Ads removes it.
        /// </summary>
        private void RegisterBanner(IContainerBuilder builder)
        {
            if (builder.Exists(typeof(IBannerAdNetwork), includeInterfaceTypes: true, findParentScopes: true))
            {
                builder.Register<IBannerAds>(
                    resolver => new BannerAds(resolver.Resolve<IBannerAdNetwork>(), resolver.Resolve<INoAds>(), Debug.Log),
                    Lifetime.Scoped);
            }
            else
            {
                builder.Register<StandInBannerAds>(Lifetime.Scoped).As<IBannerAds>();
            }

            RegisterPresenter<MainMenuView, MenuBannerPresenter>(builder);
        }

        /// <summary>Registers a screen's presenter only when the scene has that screen.</summary>
        private void RegisterPresenter<TView, TPresenter>(IContainerBuilder builder)
            where TView : UIScreen
        {
            foreach (UIScreen screen in _screens)
            {
                if (screen is TView)
                {
                    builder.RegisterEntryPoint<TPresenter>();
                    return;
                }
            }
        }
    }
}
