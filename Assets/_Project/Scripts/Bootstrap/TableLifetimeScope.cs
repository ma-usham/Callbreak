using System;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Flow;
using Callbreak.Application.Profile;
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
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IMatchSession>(CreateSession, Lifetime.Scoped);
            builder.RegisterEntryPoint<MatchSessionDriver>();
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
                default:
                    throw new NotSupportedException($"No session for {request.Mode} matches yet.");
            }
        }
    }
}
