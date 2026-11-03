using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Reviews
{
    /// <summary>
    /// When the game asks the store for its review card (<see cref="ReviewPrompt"/>, ADR 080). The
    /// menu asks it on the bare main menu (Presentation's ReviewMoment).
    /// </summary>
    public interface IReviewPrompt
    {
        /// <summary>The player has just won, and an ask is due.</summary>
        bool IsDue { get; }

        /// <summary>
        /// Asks the store for its review card when one is due, and completes once the card has gone or
        /// did not come; at once when none is due. True when the store was asked. It never throws, but
        /// for the <see cref="OperationCanceledException"/> of a cancelled token.
        /// </summary>
        Task<bool> AskIfDueAsync(CancellationToken ct);
    }
}
