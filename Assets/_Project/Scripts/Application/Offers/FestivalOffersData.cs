using System;
using System.Collections.Generic;

namespace Callbreak.Application.Offers
{
    /// <summary>
    /// festival-offers.json: the festival offers this player has claimed, and those that have opened
    /// by themselves on the main menu, by offer id. Plain serializable data read and written by
    /// <see cref="FestivalOffers"/>.
    /// </summary>
    [Serializable]
    public sealed class FestivalOffersData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;

        public List<string> claimedOfferIds = new List<string>();

        public List<string> openedOfferIds = new List<string>();
    }
}
