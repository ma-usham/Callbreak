using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Offers
{
    /// <summary>
    /// <see cref="IFestivalOffers"/> over the backend's offers (<see cref="IFestivalOfferSource"/>, read
    /// by <see cref="FestivalOfferReader"/>) and the <c>festival-offers</c> save file (ADR 067). An
    /// offer's theme is given through <see cref="ICosmeticsShop.BuyAt"/>, the shop's own ownership, so
    /// it is owned like a theme bought, comes first in the shop as the one bought last, and is never
    /// equipped by the claim. The theme is saved as owned before the claim is written, so an app
    /// killed in between leaves the theme owned and the offer gone with it: nothing is given twice.
    /// An ad watched for an offer that ended while it played still gives the theme. The clock moving an
    /// offer in or out is said as a change by <see cref="Refresh"/>. A file from a
    /// newer app version is ignored and never overwritten, and nothing is claimable or opens: a claim
    /// this build couldn't keep could be made again.
    /// </summary>
    public sealed class FestivalOffers : IFestivalOffers, IDisposable
    {
        public const string SaveKey = "festival-offers";

        private readonly IFestivalOfferSource _source;
        private readonly ICosmeticsShop _shop;
        private readonly IRewardedAds _ads;
        private readonly ISaveStore _store;
        private readonly Func<DateTime> _utcNow;
        private readonly FestivalOffersData _data;
        private readonly bool _readOnly;
        private IReadOnlyList<FestivalOffer> _offers;
        private bool _claiming;
        private bool _disposed;

        // The claimable offer's id when a change was last said, for Refresh.
        private string _saidId;

        /// <param name="utcNow">The wall clock offers go by (<see cref="TrustedClock.UtcNow"/>); a time without a kind is taken as UTC.</param>
        public FestivalOffers(IFestivalOfferSource source, ICosmeticsShop shop, IRewardedAds ads, ISaveStore store, Func<DateTime> utcNow)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            FestivalOffersData loaded = VersionedSave.Load<FestivalOffersData>(store, SaveKey, FestivalOffersData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new FestivalOffersData();
            Repair(_data);
            _offers = FestivalOfferReader.Read(_source.Config);
            _saidId = Claimable?.Id;
            _source.Changed += OnSourceChanged;
            _shop.Changed += OnShopChanged;
        }

        public event Action Changed;

        public FestivalOffer Claimable
        {
            get
            {
                if (_readOnly)
                {
                    return null;
                }

                DateTimeOffset now = new DateTimeOffset(Utc(_utcNow()));
                foreach (FestivalOffer offer in _offers)
                {
                    if (offer.IsActiveAt(now)
                        && !_data.claimedOfferIds.Contains(offer.Id)
                        && !_shop.IsOwned(CosmeticKind.Theme, offer.ThemeId))
                    {
                        return offer;
                    }
                }

                return null;
            }
        }

        /// <summary>True when the file on disk came from a newer app version, so nothing is saved or claimable.</summary>
        public bool IsReadOnly => _readOnly;

        public bool IsOffered(string themeId) => themeId != null && Claimable?.ThemeId == themeId;

        public void Refresh()
        {
            if (!_disposed && Claimable?.Id != _saidId)
            {
                RaiseChanged();
            }
        }

        public bool TakeAutoOpen()
        {
            FestivalOffer offer = Claimable;
            if (offer == null || _data.openedOfferIds.Contains(offer.Id))
            {
                return false;
            }

            _data.openedOfferIds.Add(offer.Id);
            _store.Save(SaveKey, _data);
            return true;
        }

        public async Task<FestivalClaimResult> ClaimAsync(CancellationToken ct)
        {
            if (_claiming)
            {
                return FestivalClaimResult.Busy;
            }

            ct.ThrowIfCancellationRequested();
            FestivalOffer offer = Claimable;
            if (offer == null)
            {
                return FestivalClaimResult.NothingToClaim;
            }

            if (offer.Kind == FestivalOfferKind.WatchAd)
            {
                if (!_ads.IsAvailable)
                {
                    return FestivalClaimResult.AdUnavailable;
                }

                bool watched;
                _claiming = true;
                try
                {
                    watched = await _ads.ShowAsync(ct);
                }
                finally
                {
                    _claiming = false;
                }

                if (!watched)
                {
                    return FestivalClaimResult.AdNotWatched;
                }
            }

            PurchaseResult result = _shop.BuyAt(CosmeticKind.Theme, offer.ThemeId, offer.Price);
            if (result == PurchaseResult.NotEnoughCoins)
            {
                return FestivalClaimResult.NotEnoughCoins;
            }

            if (result == PurchaseResult.UnknownItem)
            {
                // The shop can't keep it (its file is from a newer app version): nothing was given.
                return FestivalClaimResult.NothingToClaim;
            }

            // Bought, or already owned by an earlier claim that was cut short: the offer is used.
            if (!_data.claimedOfferIds.Contains(offer.Id))
            {
                _data.claimedOfferIds.Add(offer.Id);
                _store.Save(SaveKey, _data);
            }

            RaiseChanged();
            return FestivalClaimResult.Claimed;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _source.Changed -= OnSourceChanged;
            _shop.Changed -= OnShopChanged;
        }

        private void OnSourceChanged()
        {
            _offers = FestivalOfferReader.Read(_source.Config);
            RaiseChanged();
        }

        private void OnShopChanged() => RaiseChanged();

        private void RaiseChanged()
        {
            _saidId = Claimable?.Id;
            Changed?.Invoke();
        }

        private static DateTime Utc(DateTime time) =>
            time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : DateTime.SpecifyKind(time, DateTimeKind.Utc);

        /// <summary>Brings a loaded file up to the current schema, without empty or repeated ids, in memory.</summary>
        private static void Repair(FestivalOffersData data)
        {
            data.schemaVersion = FestivalOffersData.CurrentVersion;
            data.claimedOfferIds = Clean(data.claimedOfferIds);
            data.openedOfferIds = Clean(data.openedOfferIds);
        }

        private static List<string> Clean(List<string> ids)
        {
            var clean = new List<string>();
            if (ids != null)
            {
                foreach (string id in ids)
                {
                    if (!string.IsNullOrEmpty(id) && !clean.Contains(id))
                    {
                        clean.Add(id);
                    }
                }
            }

            return clean;
        }
    }
}
