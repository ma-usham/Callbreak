using System;
using System.Threading;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Flow;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Overlays;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Table;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// The Table scene's scope (GDD §21 FIG 21.1): builds the match session the launch request asks
    /// for (offline or online; the table never knows which) and drives it. Disposed with the scene,
    /// which disposes the session.
    /// </summary>
    public sealed class TableLifetimeScope : LifetimeScope
    {
        [SerializeField] private TableView _table;
        [SerializeField] private BiddingPanelView _bidding;
        [SerializeField] private UIScreen[] _screens = Array.Empty<UIScreen>();

        protected override void Awake()
        {
            // The room can close while the table is still loading (the host left). Then there is
            // nothing to play: say so and go back to the menu instead of building an empty table.
            LifetimeScope project = Find<ProjectLifetimeScope>();
            if (project != null && project.Container != null && !HasMatch(project.Container))
            {
                autoRun = false;
                base.Awake();
                IObjectResolver services = project.Container;
                services.Resolve<IToast>().Show(services.Resolve<ILocalization>().Get("ui.error.join_table_failed"));
                _ = services.Resolve<IAppFlow>().GoToMenuAsync(MenuEntry.MainMenu, CancellationToken.None);
                return;
            }

            base.Awake();
            if (Container != null)
            {
                // Disposed before the scene unloads, while the views its presenters hold still exist.
                Container.Resolve<ISceneLoader>().BeforeNextLoad(Dispose);
            }
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IMatchSession>(CreateSession, Lifetime.Scoped);
            builder.Register(CreateAnalytics, Lifetime.Scoped);
            if (_table != null && _bidding != null)
            {
                ConfigureTable(builder);
            }

            // Its PostStart runs after every presenter's Start: they all listen before the first event.
            builder.RegisterEntryPoint<MatchSessionDriver>();
        }

        /// <summary>
        /// The table and its call panel never leave the screen; the router's screens (the empty
        /// gameplay screen at the bottom, then results, scorecard, match menu, settings and the
        /// rules primer) open over them (GDD §23).
        /// </summary>
        private void ConfigureTable(IContainerBuilder builder)
        {
            builder.RegisterComponent(_table);
            builder.RegisterComponent(_bidding);
            foreach (UIScreen screen in _screens)
            {
                if (screen != null)
                {
                    builder.RegisterComponent(screen).AsSelf();
                }
            }

            builder.Register<ScreenRouter>(Lifetime.Scoped);
            builder.Register<MatchLauncher>(Lifetime.Scoped);
            builder.Register(resolver => new PlayedTable(resolver.Resolve<IMatchSession>()), Lifetime.Scoped);

            builder.RegisterEntryPoint<TablePresenter>();
            builder.RegisterEntryPoint<BiddingPresenter>();
            RegisterPresenter<MatchMenuView, MatchMenuPresenter>(builder);
            RegisterPresenter<RoundResultsView, RoundResultsPresenter>(builder);
            RegisterPresenter<MatchResultsView, MatchResultsPresenter>(builder);
            RegisterPresenter<ScorecardView, ScorecardPresenter>(builder);
            RegisterPresenter<SettingsView, SettingsPresenter>(builder);
            RegisterPresenter<HowToPlayView, HowToPlayPresenter>(builder);
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

        private static MatchAnalytics CreateAnalytics(IObjectResolver resolver)
        {
            return new MatchAnalytics(
                resolver.Resolve<IMatchSession>(),
                Launch(resolver),
                resolver.Resolve<IAnalytics>(),
                resolver.Resolve<IClock>());
        }

        private static MatchLaunchRequest Launch(IObjectResolver resolver)
        {
            return resolver.Resolve<IAppFlow>().CurrentLaunch
                ?? new MatchLaunchRequest(MatchMode.Offline, resolver.Resolve<ISettingsService>().Difficulty);
        }

        private static bool HasMatch(IObjectResolver services)
        {
            MatchLaunchRequest request = services.Resolve<IAppFlow>().CurrentLaunch;
            if (request == null)
            {
                return true;
            }

            switch (request.Mode)
            {
                case MatchMode.PrivateRoom:
                    return services.Resolve<IRoomService>().Current?.Match != null;
                case MatchMode.QuickMatch:
                    return services.Resolve<IMatchmakingService>().Match != null;
                default:
                    return true;
            }
        }

        private static IMatchSession CreateSession(IObjectResolver resolver)
        {
            var settings = resolver.Resolve<ISettingsService>();
            MatchLaunchRequest request = Launch(resolver);
            switch (request.Mode)
            {
                case MatchMode.Offline:
                    return new OfflineMatchSession(
                        request,
                        resolver.Resolve<IOfflineMatchStore>(),
                        resolver.Resolve<IBotFactory>(),
                        resolver.Resolve<IProfileService>(),
                        resolver.Resolve<ICosmeticsService>(),
                        settings,
                        resolver.Resolve<IStatsService>(),
                        botWorker: BotDriver.ThreadPool);
                case MatchMode.PrivateRoom:
                    return resolver.Resolve<IRoomService>().Current?.Match
                        ?? throw new InvalidOperationException("The room has no match to play.");
                case MatchMode.QuickMatch:
                    return resolver.Resolve<IMatchmakingService>().Match
                        ?? throw new InvalidOperationException("Quick Match found no table to play.");
                default:
                    throw new NotSupportedException($"No session for {request.Mode} matches.");
            }
        }
    }
}
