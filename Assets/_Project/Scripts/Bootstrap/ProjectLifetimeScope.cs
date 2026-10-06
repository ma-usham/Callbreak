using Callbreak.Application.Config;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Flow;
using Callbreak.Application.Localization;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Application.Updates;
using Callbreak.Core.Randomness;
using Callbreak.Infrastructure.Ads;
using Callbreak.Infrastructure.Audio;
using Callbreak.Infrastructure.Localization;
using Callbreak.Infrastructure.Persistence;
using Callbreak.Infrastructure.Platform;
using Callbreak.Infrastructure.Services;
using Callbreak.Networking.Eos;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// The app-wide scope in the Boot scene (GDD §21 FIG 21.1): lives for the whole session
    /// (DontDestroyOnLoad) and holds the services every scene shares. The Menu and Table scopes
    /// are its children. The only place that binds these interfaces to implementations.
    /// </summary>
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        [SerializeField] private LocalizationConfig _localization;
        [SerializeField] private AudioCueLibrary _audioLibrary;
        [SerializeField] private GameObject _audioHost;
        [SerializeField] private ToastView _toast;
        [SerializeField] private LoadingCurtain _curtain;
        [SerializeField] private DialogView _dialog;
        [SerializeField] private AvatarLibrary _avatars;
        [SerializeField] private CardSpriteProvider _cardSprites;

        protected override void Awake()
        {
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // Persistence and the player's local data (GDD §29).
            builder.RegisterInstance<ISaveStore>(JsonSaveStore.CreateDefault());
            builder.Register<SettingsService>(Lifetime.Singleton).As<ISettingsService>();
            builder.Register<NameFilter>(Lifetime.Singleton);
            builder.Register<IProfileService>(
                resolver => new ProfileService(resolver.Resolve<ISaveStore>(), resolver.Resolve<NameFilter>(), () => System.DateTime.UtcNow),
                Lifetime.Singleton);
            builder.Register<StatsService>(Lifetime.Singleton).As<IStatsService>();

            // The shop and its coins (the owner, 4 Oct 2026): the wallet, the cosmetics the player owns
            // and has equipped (one object behind the deck choice and the shop), and the Earn Coins ads
            // with their privacy options.
            builder.Register<Wallet>(Lifetime.Singleton).As<IWallet>();
            builder.Register(resolver => new CosmeticsService(resolver.Resolve<ISaveStore>(), resolver.Resolve<IWallet>()), Lifetime.Singleton)
                .As<ICosmeticsService>()
                .As<ICosmeticsShop>();
            RegisterAds(builder);
            builder.Register<IAdRewards>(
                resolver => new AdRewards(resolver.Resolve<IRewardedAds>(), resolver.Resolve<IWallet>(), resolver.Resolve<ISaveStore>(), () => System.DateTime.UtcNow),
                Lifetime.Singleton);

            // No Ads (the owner, 5 Oct 2026): the one-time purchase that removes the ads between matches.
            RegisterPurchases(builder);

            // Stake Match (the owner, 5 Oct 2026): the online match the player chose on Play Online
            // (Quick Match until a stake is picked), and the stakes taken and prizes paid, by match.
            builder.Register<OnlineMatchChoice>(Lifetime.Singleton).As<IOnlineMatchChoice>();
            builder.Register<StakeLedger>(Lifetime.Singleton).As<IStakeLedger>();

            builder.Register<OfflineMatchStore>(Lifetime.Singleton).As<IOfflineMatchStore>();
            builder.Register<IRejoinStore>(resolver => new RejoinStore(resolver.Resolve<ISaveStore>(), () => System.DateTime.UtcNow, resolver.Resolve<IStatsService>()), Lifetime.Singleton);

            // Language (GDD §15).
            builder.RegisterInstance(StringTablesFactory.Create(_localization));
            builder.Register<LocalizationService>(Lifetime.Singleton).As<ILocalization>();

            // Platform adapters (GDD §14, §20).
            builder.Register<UnityClock>(Lifetime.Singleton).As<IClock>();
            builder.Register<NetworkStatus>(Lifetime.Singleton).As<INetworkStatus>().AsSelf();
            builder.Register<ClipboardService>(Lifetime.Singleton).As<IClipboard>();
            builder.Register<ShareService>(Lifetime.Singleton).As<IShareService>();
            builder.Register<HapticsService>(Lifetime.Singleton).As<IHaptics>();
            builder.Register<OnScreenKeyboard>(Lifetime.Singleton).As<IOnScreenKeyboard>();
            builder.Register<LocalAuthService>(Lifetime.Singleton).As<IAuthService>();
            builder.Register<LogAnalytics>(Lifetime.Singleton).As<IAnalytics>();
            builder.Register<AppExit>(Lifetime.Singleton).As<IAppExit>();
            builder.Register<DeepLinkService>(Lifetime.Singleton).As<IDeepLinks>();
            builder.Register<AudioService>(Lifetime.Singleton).As<IAudioService>()
                .WithParameter(_audioHost)
                .WithParameter(_audioLibrary);

            builder.RegisterInstance(_avatars);
            builder.RegisterInstance(_cardSprites);

            // Names in any script: the device's fonts stand behind the game's Latin-only ones.
            builder.RegisterEntryPoint<SystemFontFallbacks>().AsSelf();

            // Layers above every scene: dialogs, toasts and the loading curtain (GDD §23).
            builder.RegisterComponent(_dialog);
            builder.Register<DialogService>(Lifetime.Singleton).As<IDialogs>();
            builder.RegisterComponent(_toast);
            builder.Register<ToastService>(Lifetime.Singleton).As<IToast>();
            builder.RegisterComponent(_curtain).As<ILoadingCurtain>();

            // App flow (GDD §21).
            builder.Register<SceneLoader>(Lifetime.Singleton).As<ISceneLoader>();
            builder.Register<AppFlow>(Lifetime.Singleton).As<IAppFlow>();
            RegisterUpdates(builder);

            // Bots and online play (GDD §9, §24-27). Rooms and Quick Match live for the whole app,
            // because a room outlasts the Menu and Table scenes.
            builder.Register<BotFactory>(Lifetime.Singleton).As<IBotFactory>();

            // The optional beginner assist asks the bots' estimator for a call (GDD §11 screen 07).
            builder.Register<BotCallAdvisor>(Lifetime.Singleton);
            builder.Register<TableCallAdvisor>(Lifetime.Singleton).As<Callbreak.Presentation.Table.ICallAdvisor>();
            RegisterRoomTransport(builder);

            builder.Register(CreateRoomService, Lifetime.Singleton).As<IRoomService>().AsSelf();
            builder.Register(CreateQuickMatch, Lifetime.Singleton).As<IMatchmakingService>().AsSelf();
            builder.RegisterEntryPoint<OnlineTicker>();

            builder.RegisterEntryPoint<GameEntryPoint>();
        }

        /// <summary>
        /// The Earn Coins ads (the owner, 5 Oct 2026; Docs/Setup/AdMob.md). On phones, Google AdMob:
        /// Google's consent form, then rewarded ads, one kept loaded; development builds get Google's
        /// test ads (<see cref="AdMobIds"/>). Its privacy options, which Settings offers where Google
        /// says they are required, learn that from the same consent flow. In the Editor, in the tests
        /// and on desktop, a stand-in completes every ad and never requires privacy options. Also the
        /// full-screen ads between matches: AdMob interstitials on phones, one kept loaded (Google's
        /// test ads in development builds); elsewhere a stand-in that never has one.
        /// </summary>
        private static void RegisterAds(IContainerBuilder builder)
        {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            string adUnit = AdMobIds.RewardedAdUnit(UnityEngine.Application.platform == RuntimePlatform.IPhonePlayer, Debug.isDebugBuild);
            var privacy = new AdMobAdPrivacy();
            builder.RegisterInstance<IAdPrivacy>(privacy);
            var rewarded = new AdMobRewardedAdNetwork(adUnit, privacy);
            builder.Register<IRewardedAds>(
                _ => new PreloadedRewardedAds(rewarded, () => System.DateTime.UtcNow, Debug.Log),
                Lifetime.Singleton);

            // The ads between matches start AdMob through the rewarded ads: one consent form, one SDK start.
            // A player who owns No Ads gets none of them requested.
            string interstitialUnit = AdMobIds.InterstitialAdUnit(UnityEngine.Application.platform == RuntimePlatform.IPhonePlayer, Debug.isDebugBuild);
            builder.Register<IInterstitialAds>(
                resolver => new PreloadedInterstitialAds(new AdMobInterstitialAdNetwork(interstitialUnit, rewarded.StartAsync), resolver.Resolve<INoAds>(), () => System.DateTime.UtcNow, Debug.Log),
                Lifetime.Singleton);
#else
            builder.Register<StandInRewardedAds>(Lifetime.Singleton).As<IRewardedAds>();
            builder.Register<StandInAdPrivacy>(Lifetime.Singleton).As<IAdPrivacy>();
            builder.Register<StandInInterstitialAds>(Lifetime.Singleton).As<IInterstitialAds>();
#endif

            // When an ad shows between matches: leaving a finished match's results, at most one every
            // three minutes, never after the first match or with No Ads (the owner, 5 Oct 2026).
            builder.Register<IBetweenMatchAds>(
                resolver => new BetweenMatchAds(resolver.Resolve<IInterstitialAds>(), resolver.Resolve<IStatsService>(), resolver.Resolve<INoAds>(), () => System.DateTime.UtcNow, Debug.Log),
                Lifetime.Singleton);
        }

        /// <summary>
        /// No Ads, the one-time purchase (Docs/Setup/PlayBilling.md). On phones, the store's: Unity
        /// IAP over Google Play Billing, the ownership saved on the device and listed again by the
        /// store at each start (a reinstall, a new phone, a refund) and on the game's return to the
        /// foreground when that can bring news. In the Editor, in the tests and on desktop, a
        /// stand-in sells it at once, in memory only.
        /// </summary>
        private static void RegisterPurchases(IContainerBuilder builder)
        {
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            // A registration of its own, so the container disposes the adapter: it follows the focus.
            builder.Register<IStoreBilling>(_ => new Callbreak.Infrastructure.Purchasing.UnityIapBilling(), Lifetime.Singleton);
            builder.Register<INoAds>(
                resolver => new StoreNoAds(resolver.Resolve<IStoreBilling>(), resolver.Resolve<ISaveStore>(), () => System.DateTime.UtcNow, Debug.Log),
                Lifetime.Singleton);
#else
            builder.Register<StandInNoAds>(Lifetime.Singleton).As<INoAds>();
#endif
        }

        /// <summary>
        /// Forced updates (the owner, 5 Oct 2026: "any new Play version"; ADR 036). On Android, Google
        /// Play's in-app updates: when Play has a newer version, its full-screen immediate update, and
        /// "Update required" when the player backs out of it. In the Editor, in the tests and on
        /// desktop, a stand-in that answers at once and never has one; a test may bring its own.
        /// </summary>
        private static void RegisterUpdates(IContainerBuilder builder)
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            builder.Register<IAppUpdates>(_ => new Callbreak.Infrastructure.Updates.PlayAppUpdates(), Lifetime.Singleton);
#else
            builder.Register<IAppUpdates>(_ => new StandInAppUpdates(), Lifetime.Singleton);
#endif
            builder.Register(
                resolver => new ForcedUpdate(
                    resolver.Resolve<IAppUpdates>(),
                    resolver.Resolve<IDialogs>(),
                    resolver.Resolve<ILocalization>(),
                    resolver.Resolve<IRejoinStore>(),
                    resolver.Resolve<INetworkStatus>(),
                    () => System.DateTime.UtcNow,
                    Debug.Log),
                Lifetime.Singleton);
        }

        /// <summary>
        /// Online rooms (ADR 016, 028). Epic Online Services, peer to peer, once its config holds the
        /// client credentials (Docs/Setup/EOS.md). EOS then starts here and runs until the app
        /// quits: the plugin's EOSManager object is its own and is never destroyed. Otherwise none
        /// (the PurrNet relay that once stood in is gone, ADR 037). The log says which, never with a
        /// credential. A scope above this one that brings a transport of its own (the PlayMode tests'
        /// GameFixture) keeps all of this off: its transport serves, and EOS never starts.
        /// </summary>
        private static void RegisterRoomTransport(IContainerBuilder builder)
        {
            if (builder.Exists(typeof(IRoomTransport), includeInterfaceTypes: true, findParentScopes: true))
            {
                Debug.Log("Online rooms: the enclosing scope's transport (EOS is not started).");
                return;
            }

            if (EosPlatform.IsConfigured() && EosPlatform.Start())
            {
                // One EOS network: rooms by code, and the directory of Quick Match tables (ADR 029).
                builder.Register(resolver => new EosPeerNetwork(() => resolver.Resolve<IProfileService>().DisplayName), Lifetime.Singleton)
                    .As<IPeerNetwork>()
                    .As<IQuickTableDirectory>();
                builder.Register(resolver => new PeerRoomTransport(resolver.Resolve<IPeerNetwork>(), Debug.LogException), Lifetime.Singleton)
                    .As<IRoomTransport>();
                Debug.Log("Online rooms: Epic Online Services (peer to peer).");
                return;
            }

            builder.Register<NoRoomTransport>(Lifetime.Singleton).As<IRoomTransport>();
            Debug.Log("Online rooms: off (EOS is not set up, or did not start).");
        }

        private static RoomService CreateRoomService(IObjectResolver resolver)
        {
            return new RoomService(
                resolver.Resolve<IRoomTransport>(),
                resolver.Resolve<IProfileService>(),
                resolver.Resolve<ICosmeticsService>(),
                resolver.Resolve<IAuthService>(),
                resolver.Resolve<IBotFactory>(),
                resolver.Resolve<IStatsService>(),
                resolver.Resolve<ISettingsService>(),
                resolver.Resolve<INetworkStatus>(),
                resolver.Resolve<NameFilter>(),
                NextSeed,
                tickets: resolver.Resolve<IRejoinStore>(),
                analytics: resolver.Resolve<IAnalytics>());
        }

        /// <summary>
        /// Quick Match meets real players at tables in the directory when there is one (EOS) and the
        /// transport can reach them; otherwise every match is at a table of the device's own, with
        /// bots (ADR 029). Stake Match is the same search among the chosen stake's tables, and takes
        /// the stake from the wallet when its match begins.
        /// </summary>
        private static QuickMatchService CreateQuickMatch(IObjectResolver resolver)
        {
            resolver.TryResolve(out IQuickTableDirectory directory);
            return new QuickMatchService(
                new QuickMatchConfig(),
                resolver.Resolve<IProfileService>(),
                resolver.Resolve<ICosmeticsService>(),
                resolver.Resolve<IAuthService>(),
                resolver.Resolve<IBotFactory>(),
                resolver.Resolve<IStatsService>(),
                resolver.Resolve<ISettingsService>(),
                resolver.Resolve<INetworkStatus>(),
                resolver.Resolve<NameFilter>(),
                NextSeed,
                transport: resolver.Resolve<IRoomTransport>() as IQuickMatchTransport,
                directory: directory,
                utcNow: () => System.DateTime.UtcNow,
                choice: resolver.Resolve<IOnlineMatchChoice>(),
                stakes: resolver.Resolve<IStakeLedger>());
        }

        /// <summary>GDD §22: shuffle seeds come from the OS's secure random source.</summary>
        private static ulong NextSeed() => new CryptoRandom().NextSeed();
    }
}
