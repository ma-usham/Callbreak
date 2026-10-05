using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The ad network's privacy options (port). Where the law asks for the player's consent to ads
    /// (the network decides: Google's User Messaging Platform, e.g. in the EEA, the UK and
    /// Switzerland), the player must be able to change that choice later, from an entry point the
    /// game shows only while the network says one is required (Settings' "Privacy options"). On
    /// phones it is AdMob's; in the Editor, in the tests and on desktop
    /// <see cref="StandInAdPrivacy"/> never requires one. Everything happens on the main thread,
    /// <see cref="Changed"/> included.
    /// </summary>
    public interface IAdPrivacy
    {
        /// <summary>The entry point must be shown. False until the network knows.</summary>
        bool IsRequired { get; }

        /// <summary>What the network says about the entry point became known, or changed.</summary>
        event Action Changed;

        /// <summary>
        /// Opens the network's privacy options form, where the player can change their consent;
        /// completes once it has closed, or could not be shown. It never throws, but for the
        /// <see cref="OperationCanceledException"/> of a cancelled token (the form stays up).
        /// </summary>
        Task ShowAsync(CancellationToken ct);
    }
}
