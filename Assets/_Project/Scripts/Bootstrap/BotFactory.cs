using Callbreak.Application.Sessions;
using Callbreak.Bots;

namespace Callbreak.Bootstrap
{
    /// <summary>Binds the application's bot factory to the strategies of Callbreak.Bots (GDD §9).</summary>
    public sealed class BotFactory : IBotFactory
    {
        public IBotStrategy Create(BotLevel level) => BotStrategies.Create(level);
    }
}
