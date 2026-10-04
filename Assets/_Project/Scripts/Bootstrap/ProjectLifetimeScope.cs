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
using Callbreak.Core.Randomness;
using Callbreak.Infrastructure.Audio;
using Callbreak.Infrastructure.Localization;
using Callbreak.Infrastructure.Persistence;
using Callbreak.Infrastructure.Platform;
using Callbreak.Infrastructure.Services;
using Callbreak.Networking;
using Callbreak.Networking.Eos;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using PurrNet.Transports;
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
        [SerializeField] private GenericTransport _roomTransport;

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
            builder.Register<ProfileService>(Lifetime.Singleton).As<IProfileService>();
            builder.Register<StatsService>(Lifetime.Singleton).As<IStatsService>();

            // The shop and its coins (the owner, 4 Oct 2026): the wallet, the cosmetics the player owns
            // and has equipped (one object behind the deck choice and the shop), and the Earn Coins ads.
            // No ad network is connected yet: a stand-in completes every ad.
            builder.Register<Wallet>(Lifetime.Singleton).As<IWallet>();
            builder.Register(resolver => new CosmeticsService(resolver.Resolve<ISaveStore>(), resolver.Resolve<IWallet>()), Lifetime.Singleton)
                .As<ICosmeticsService>()
                .As<ICosmeticsShop>();
            builder.Register<StandInRewardedAds>(Lifetime.Singleton).As<IRewardedAds>();
            builder.Register<IAdRewards>(
                resolver => new AdRewards(resolver.Resolve<IRewardedAds>(), resolver.Resolve<IWallet>(), resolver.Resolve<ISaveStore>(), () => System.DateTime.UtcNow),
                Lifetime.Singleton);

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
        /// Online rooms (ADR 016). Epic Online Services, peer to peer, once its config holds the
        /// client credentials (Docs/Setup/EOS.md). EOS then starts here and runs until the app
        /// quits: the plugin's EOSManager object is its own and is never destroyed. Otherwise the
        /// PurrNet relay, for development, when the Boot scene sets one. Otherwise none. The log
        /// says which, never with a credential.
        /// </summary>
        private void RegisterRoomTransport(IContainerBuilder builder)
        {
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

            if (_roomTransport != null)
            {
                GenericTransport relay = _roomTransport;
                builder.Register(_ => new PurrRoomTransport(relay), Lifetime.Singleton).As<IRoomTransport>();
                Debug.Log("Online rooms: PurrNet relay (development fallback: EOS is not set up, or did not start).");
                return;
            }

            builder.Register<NoRoomTransport>(Lifetime.Singleton).As<IRoomTransport>();
            Debug.Log("Online rooms: off (EOS is not set up, or did not start, and no relay is set).");
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
        /// bots (ADR 029).
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
                utcNow: () => System.DateTime.UtcNow);
        }

        /// <summary>GDD §22: shuffle seeds come from the OS's secure random source.</summary>
        private static ulong NextSeed() => new CryptoRandom().NextSeed();
    }
}
