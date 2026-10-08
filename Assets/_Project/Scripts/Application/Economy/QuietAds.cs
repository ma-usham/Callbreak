using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The Earn Coins ads with the game silent under them (the owner, 8 Oct 2026; ADR 035). On
    /// Android the game keeps running under a full-screen ad, so its music and sound effects would
    /// play over the ad. While a show of the wrapped ads is under way the game's audio is held silent
    /// (<see cref="IAudioSilencer"/>); the show's end lets it go, however the show ends: watched,
    /// skipped, failed, thrown or cancelled. A show that is over at once (no ad loaded, one already
    /// up, an ad that couldn't be shown) silences nothing. Results, faults and cancellation pass
    /// through unchanged, and a silencer that fails never stops the ad. Every call comes on the main
    /// thread, and the sound comes back there.
    /// </summary>
    public sealed class QuietRewardedAds : IRewardedAds, IDisposable
    {
        private readonly IRewardedAds _ads;
        private readonly IAudioSilencer _audio;
        private readonly Action<string> _log;

        /// <param name="ads">The ads shown: on phones, AdMob's (<see cref="PreloadedRewardedAds"/>).</param>
        /// <param name="audio">The game's audio, held silent while an ad shows.</param>
        /// <param name="log">Where the silence's progress goes, a line each (no player data); nowhere when null.</param>
        public QuietRewardedAds(IRewardedAds ads, IAudioSilencer audio, Action<string> log = null)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _log = log ?? (_ => { });
        }

        public bool IsAvailable => _ads.IsAvailable;

        public Task<bool> ShowAsync(CancellationToken ct) => QuietShow.Run(_ads.ShowAsync(ct), _audio, _log, "Rewarded ads");

        /// <summary>Disposes the wrapped ads, when they can be.</summary>
        public void Dispose() => (_ads as IDisposable)?.Dispose();
    }

    /// <summary>
    /// The ads between matches with the game silent under them, as <see cref="QuietRewardedAds"/>
    /// keeps the Earn Coins ads: silent while a show is under way, the sound back once it is over,
    /// however it ends (closed, failed, thrown or cancelled); a show that is over at once (none
    /// loaded, No Ads owned) silences nothing.
    /// </summary>
    public sealed class QuietInterstitialAds : IInterstitialAds, IDisposable
    {
        private readonly IInterstitialAds _ads;
        private readonly IAudioSilencer _audio;
        private readonly Action<string> _log;

        /// <param name="ads">The ads shown: on phones, AdMob's (<see cref="PreloadedInterstitialAds"/>).</param>
        /// <param name="audio">The game's audio, held silent while an ad shows.</param>
        /// <param name="log">Where the silence's progress goes, a line each (no player data); nowhere when null.</param>
        public QuietInterstitialAds(IInterstitialAds ads, IAudioSilencer audio, Action<string> log = null)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _log = log ?? (_ => { });
        }

        public bool IsAvailable => _ads.IsAvailable;

        public Task<bool> ShowAsync(CancellationToken ct) => QuietShow.Run(_ads.ShowAsync(ct), _audio, _log, "Interstitial ads");

        /// <summary>Disposes the wrapped ads, when they can be.</summary>
        public void Dispose() => (_ads as IDisposable)?.Dispose();
    }

    /// <summary>One show of a full-screen ad, with the game silent until it is over.</summary>
    internal static class QuietShow
    {
        /// <summary>
        /// <paramref name="show"/>, with the game silent until it is over; as it is when it is over
        /// already, since then no ad came up.
        /// </summary>
        public static Task<bool> Run(Task<bool> show, IAudioSilencer audio, Action<string> log, string ads)
        {
            return show.IsCompleted ? show : RunSilentAsync(show, audio, log, ads);
        }

        private static async Task<bool> RunSilentAsync(Task<bool> show, IAudioSilencer audio, Action<string> log, string ads)
        {
            IDisposable silence = null;
            try
            {
                silence = audio.Silence();
                log(ads + ": the game is silent while the ad is up.");
            }
            catch (Exception e)
            {
                log(ads + ": the game's sound could not be silenced: " + e.Message);
            }

            try
            {
                // Resumes on the caller's context: the main thread, where the sound comes back.
                return await show;
            }
            finally
            {
                if (silence != null)
                {
                    try
                    {
                        silence.Dispose();
                        log(ads + ": the ad has gone; the game's sound is back.");
                    }
                    catch (Exception e)
                    {
                        log(ads + ": the game's sound could not be brought back: " + e.Message);
                    }
                }
            }
        }
    }
}
