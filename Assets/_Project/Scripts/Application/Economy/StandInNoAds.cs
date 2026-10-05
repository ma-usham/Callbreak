using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// No Ads where there is no store: the Editor, the tests and desktop builds (ProjectLifetimeScope).
    /// It is always ready to sell, at <see cref="StandInPrice"/>, and a purchase is granted at once,
    /// with no sheet and no charge. The grant lives in memory only and is never saved, so every run
    /// starts with the ads again and nothing here can pass for a real purchase.
    /// </summary>
    public sealed class StandInNoAds : INoAds
    {
        /// <summary>The price the stand-in shows: the owner's Play Console price (5 Oct 2026).</summary>
        public const string StandInPrice = "$0.99";

        public event Action Changed;

        public bool Owned { get; private set; }

        public bool CanBuy => !Owned;

        public string Price => StandInPrice;

        public Task<NoAdsPurchase> BuyAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled<NoAdsPurchase>(ct);
            }

            if (Owned)
            {
                return Task.FromResult(NoAdsPurchase.AlreadyOwned);
            }

            Owned = true;
            Changed?.Invoke();
            return Task.FromResult(NoAdsPurchase.Bought);
        }
    }
}
