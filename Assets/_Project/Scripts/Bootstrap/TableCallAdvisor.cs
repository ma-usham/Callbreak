using Callbreak.Application.Sessions;
using Callbreak.Core.Views;
using Callbreak.Presentation.Table;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// Answers the table's question "what would the estimator call with this hand?" (the optional
    /// beginner assist, GDD §11 screen 07) with the application's <see cref="BotCallAdvisor"/>, so
    /// the table knows nothing about bots.
    /// </summary>
    public sealed class TableCallAdvisor : ICallAdvisor
    {
        private readonly BotCallAdvisor _advisor;

        public TableCallAdvisor(BotCallAdvisor advisor)
        {
            _advisor = advisor;
        }

        public int SuggestCall(PlayerView view) => view != null ? _advisor.Suggest(view) : 0;
    }
}
