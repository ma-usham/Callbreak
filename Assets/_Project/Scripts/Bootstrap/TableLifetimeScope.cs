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
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IMatchSession>(CreateSession, Lifetime.Scoped);
            builder.RegisterEntryPoint<MatchSessionDriver>();
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
            MatchLaunchRequest request = resolver.Resolve<IAppFlow>().CurrentLaunch
                ?? new MatchLaunchRequest(MatchMode.Offline, settings.Difficulty);
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
                        resolver.Resolve<IStatsService>());
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
