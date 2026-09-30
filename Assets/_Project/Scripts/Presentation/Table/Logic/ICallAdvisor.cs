using Callbreak.Core.Views;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The call the estimator would make with a hand: what the beginner assist marks on the call
    /// panel (GDD §11 screen 07 "a faint dot marks the estimator's suggested call"), the same
    /// estimate a timed-out call is placed with (GDD §8). The table only asks the question;
    /// whoever knows the estimator answers it, so the table needs to know nothing about bots.
    /// </summary>
    public interface ICallAdvisor
    {
        /// <summary>
        /// The suggested call for the viewer of <paramref name="view"/>, between the rule set's
        /// lowest and highest call; 0 when there is nothing to suggest.
        /// </summary>
        int SuggestCall(PlayerView view);
    }
}
