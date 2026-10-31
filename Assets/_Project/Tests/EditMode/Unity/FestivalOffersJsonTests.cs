using System;
using System.Collections.Generic;
using Callbreak.Application.Offers;
using Callbreak.Infrastructure.Services;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The festival_offers value from Firebase Remote Config, read with JsonUtility (ADR 067): the JSON
    /// of Docs/Setup/FestivalOffers.md, as pasted into the console, is Tihar 2026; no value, or a value
    /// that isn't JSON, is no config and so no offer; extra fields are ignored.
    /// </summary>
    public sealed class FestivalOffersJsonTests
    {
        /// <summary>Docs/Setup/FestivalOffers.md, "Tihar 2026", word for word.</summary>
        private const string Tihar2026 = @"{
  ""offers"": [
    {
      ""id"": ""tihar-2026"",
      ""theme"": ""tihar-lights"",
      ""kind"": ""watch-ad"",
      ""start"": ""2026-10-31T00:00:00+05:45"",
      ""end"": ""2026-11-16T00:00:00+05:45"",
      ""title"": ""ui.offer.tihar""
    }
  ]
}";

        [Test]
        public void TheHowTosJson_IsTihar2026()
        {
            FestivalOffersConfig config = FestivalOffersJson.Parse(Tihar2026);

            IReadOnlyList<FestivalOffer> offers = FestivalOfferReader.Read(config);
            Assert.AreEqual(1, offers.Count);
            Assert.AreEqual("tihar-2026", offers[0].Id);
            Assert.AreEqual("tihar-lights", offers[0].ThemeId);
            Assert.AreEqual(FestivalOfferKind.WatchAd, offers[0].Kind);
            Assert.AreEqual(new DateTimeOffset(2026, 10, 31, 0, 0, 0, new TimeSpan(5, 45, 0)), offers[0].Start);
            Assert.AreEqual(new DateTimeOffset(2026, 11, 16, 0, 0, 0, new TimeSpan(5, 45, 0)), offers[0].End);
            Assert.AreEqual("ui.offer.tihar", offers[0].TitleKey);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json")]
        [TestCase("{\"offers\": [")]
        public void NoValue_OrNotJson_IsNoConfig(string json)
        {
            Assert.IsNull(FestivalOffersJson.Parse(json));
            Assert.AreEqual(0, FestivalOfferReader.Read(FestivalOffersJson.Parse(json)).Count);
        }

        [Test]
        public void AnEmptyList_OrExtraFields_AreReadAsTheyAre()
        {
            Assert.AreEqual(0, FestivalOfferReader.Read(FestivalOffersJson.Parse("{\"offers\": []}")).Count);
            Assert.AreEqual(0, FestivalOfferReader.Read(FestivalOffersJson.Parse("{}")).Count);

            string extra = Tihar2026.Replace("\"title\"", "\"note\": \"for the owner\", \"title\"");
            Assert.AreEqual(1, FestivalOfferReader.Read(FestivalOffersJson.Parse(extra)).Count);
        }
    }
}
