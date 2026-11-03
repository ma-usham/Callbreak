using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Reviews
{
    /// <summary>
    /// The app store's own review card (port), as <see cref="ReviewPrompt"/> asks for it: on Android
    /// Google Play's in-app review, whose stars and comment the player gives without leaving the game.
    /// The store decides whether the card shows (Google Play keeps a quota, and shows it only to an app
    /// installed from it) and never says whether the player rated. In the Editor, in the tests and on
    /// desktop <see cref="StandInStoreReview"/> shows nothing. The task completes on the main thread;
    /// nothing throws but for the <see cref="OperationCanceledException"/> of a cancelled token: a
    /// store that can't be asked just completes.
    /// </summary>
    public interface IStoreReview
    {
        /// <summary>Asks the store for its review card, and completes once the card has gone or did not come.</summary>
        Task RequestAsync(CancellationToken ct);
    }
}
