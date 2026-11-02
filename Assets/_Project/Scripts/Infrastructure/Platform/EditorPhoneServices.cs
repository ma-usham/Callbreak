#if UNITY_EDITOR
using System;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// What a game played in the Editor takes from a phone (ADR 073), the Editor's own setting, on until
    /// it is turned off (Callbreak &gt; Editor Play), read as the game starts: the festival offers from
    /// Firebase Remote Config. Ads are not among them: the owner checks ads on Android (3 Nov 2026,
    /// ADR 077), so the Editor plays with the stand-ins. The PlayMode tests boot without the offers
    /// unless they ask (<see cref="While"/>). Builds never read this.
    /// </summary>
    public static class EditorPhoneServices
    {
        public const string FestivalOffersKey = "Callbreak.EditorFestivalOffers";

        public static bool FestivalOffers
        {
            get => UnityEditor.EditorPrefs.GetBool(FestivalOffersKey, true);
            set => UnityEditor.EditorPrefs.SetBool(FestivalOffersKey, value);
        }

        /// <summary>Sets the switch until the returned handle is disposed, then puts it back as it was.</summary>
        public static IDisposable While(bool on)
        {
            var restore = new Restore(FestivalOffers);
            FestivalOffers = on;
            return restore;
        }

        private sealed class Restore : IDisposable
        {
            private readonly bool _festivalOffers;
            private bool _done;

            public Restore(bool festivalOffers)
            {
                _festivalOffers = festivalOffers;
            }

            public void Dispose()
            {
                if (_done)
                {
                    return;
                }

                _done = true;
                FestivalOffers = _festivalOffers;
            }
        }
    }
}
#endif
