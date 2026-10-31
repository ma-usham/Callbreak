using System;
using Callbreak.Application.Offers;
using UnityEngine;

namespace Callbreak.Infrastructure.Services
{
    /// <summary>
    /// The <c>festival_offers</c> text from the backend as <see cref="FestivalOffersConfig"/> (ADR 067),
    /// read with JsonUtility like the save files. Empty text or text that isn't JSON is no config; the
    /// values in it are checked later, by <see cref="FestivalOfferReader"/>.
    /// </summary>
    public static class FestivalOffersJson
    {
        public static FestivalOffersConfig Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<FestivalOffersConfig>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
