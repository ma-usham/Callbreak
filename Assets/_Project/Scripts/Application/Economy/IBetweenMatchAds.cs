using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// A full-screen ad between matches (port). On phones it is Google AdMob
    /// (<see cref="PreloadedInterstitialAds"/> over the AdMob adapter); in the Editor, in the tests
    /// and on desktop <see cref="StandInInterstitialAds"/>, which never has one.
    /// </summary>
    public interface IInterstitialAds
    {
        /// <summary>An ad is loaded and can be shown now.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Shows the loaded ad and completes once it has gone away: true when it was shown; false
        /// when none was loaded, it couldn't be shown or another ad is showing. It never throws, but
        /// for the <see cref="OperationCanceledException"/> of a cancelled token.
        /// </summary>
        Task<bool> ShowAsync(CancellationToken ct);
    }

    /// <summary>
    /// The ads between matches (the owner, 5 Oct 2026): a full-screen ad as the player leaves the
    /// results of a finished match, when one is due (<see cref="BetweenMatchAds"/>). No Ads
    /// (<see cref="INoAds"/>) removes them.
    /// </summary>
    public interface IBetweenMatchAds
    {
        /// <summary>
        /// The player leaves a finished match's results for <paramref name="next"/>: shows an ad when
        /// one is due and loaded, and completes once it has gone away; at once when none is due or
        /// none is loaded, as the game never waits for one. True when an ad was shown. It never
        /// throws, but for the <see cref="OperationCanceledException"/> of a cancelled token.
        /// </summary>
        Task<bool> ShowIfDueAsync(AfterMatch next, CancellationToken ct);
    }

    /// <summary>Where the player goes from the results of a finished match.</summary>
    public enum AfterMatch
    {
        /// <summary>Play Again offline: a new match is dealt.</summary>
        NewMatch,

        /// <summary>Play Again after a Quick or Stake Match: the search starts.</summary>
        Search,

        /// <summary>Play Again in a private room: back to the lobby, where the friends wait.</summary>
        PrivateLobby,

        /// <summary>Main Menu, from any match.</summary>
        MainMenu,
    }
}
