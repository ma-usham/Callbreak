using Callbreak.Application.Config;
using Callbreak.Application.Cosmetics;
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
            builder.Register<CosmeticsService>(Lifetime.Singleton).As<ICosmeticsService>();
            builder.Register<OfflineMatchStore>(Lifetime.Singleton).As<IOfflineMatchStore>();
            builder.Register<IRejoinStore>(resolver => new RejoinStore(resolver.Resolve<ISaveStore>(), () => System.DateTime.UtcNow), Lifetime.Singleton);

            // Language (GDD §15).
            builder.RegisterInstance(StringTablesFactory.Create(_localization));
            builder.Register<LocalizationService>(Lifetime.Singleton).As<ILocalization>();

            // Platform adapters (GDD §14, §20).
            builder.Register<UnityClock>(Lifetime.Singleton).As<IClock>();
            builder.Register<NetworkStatus>(Lifetime.Singleton).As<INetworkStatus>().AsSelf();
            builder.Register<ClipboardService>(Lifetime.Singleton).As<IClipboard>();
            builder.Register<ShareService>(Lifetime.Singleton).As<IShareService>();
            builder.Register<HapticsService>(Lifetime.Singleton).As<IHaptics>();
            builder.Register<LocalAuthService>(Lifetime.Singleton).As<IAuthService>();
            builder.Register<LogAnalytics>(Lifetime.Singleton).As<IAnalytics>();
            builder.Register<AppExit>(Lifetime.Singleton).As<IAppExit>();
            builder.Register<DeepLinkService>(Lifetime.Singleton).As<IDeepLinks>();
            builder.Register<AudioService>(Lifetime.Singleton).As<IAudioService>()
                .WithParameter(_audioHost)
                .WithParameter(_audioLibrary);

            builder.RegisterInstance(_avatars);
            builder.RegisterInstance(_cardSprites);

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
            if (_roomTransport != null)
            {
                GenericTransport relay = _roomTransport;
                builder.Register(_ => new PurrRoomTransport(relay), Lifetime.Singleton).As<IRoomTransport>();
            }
            else
            {
                builder.Register<NoRoomTransport>(Lifetime.Singleton).As<IRoomTransport>();
            }

            builder.Register(CreateRoomService, Lifetime.Singleton).As<IRoomService>().AsSelf();
            builder.Register(CreateQuickMatch, Lifetime.Singleton).As<IMatchmakingService>().AsSelf();
            builder.RegisterEntryPoint<OnlineTicker>();

            builder.RegisterEntryPoint<GameEntryPoint>();
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

        private static QuickMatchService CreateQuickMatch(IObjectResolver resolver)
        {
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
                NextSeed);
        }

        /// <summary>GDD §22: shuffle seeds come from the OS's secure random source.</summary>
        private static ulong NextSeed() => new CryptoRandom().NextSeed();
    }
}
