using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using VContainer.Unity;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// Starts the session once every table presenter has subscribed (post-start), then advances
    /// its time-driven work each frame: bot think time, auto actions and the turn clock. The
    /// match's analytics listen from before the first event.
    /// </summary>
    public sealed class MatchSessionDriver : IPostStartable, ITickable
    {
        private readonly IMatchSession _session;
        private readonly MatchAnalytics _analytics;
        private readonly IClock _clock;

        public MatchSessionDriver(IMatchSession session, MatchAnalytics analytics, IClock clock)
        {
            _session = session;
            _analytics = analytics;
            _clock = clock;
        }

        public void PostStart()
        {
            _analytics.Start();
            _session.Start();
        }

        public void Tick() => _session.Tick(_clock.Now);
    }
}
