using System;

namespace Callbreak.Application.Offers
{
    /// <summary>
    /// Where festival offers come from (port, ADR 067): Firebase Remote Config on phones, nothing
    /// elsewhere. <see cref="Config"/> is the last value the backend gave that could be read, kept on
    /// the device between sessions; null with no config, no network on the first run, or a value
    /// that isn't JSON. <see cref="Changed"/> comes on the main thread.
    /// </summary>
    public interface IFestivalOfferSource
    {
        event Action Changed;

        FestivalOffersConfig Config { get; }
    }

    /// <summary>The source with no offers: the Editor, desktop, tests, and a phone without Firebase.</summary>
    public sealed class NoFestivalOffers : IFestivalOfferSource
    {
        public event Action Changed
        {
            add { }
            remove { }
        }

        public FestivalOffersConfig Config => null;
    }
}
