using System;
using System.Collections.Generic;
using System.Linq;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Offers;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The backend's festival offers as the game reads them (ADR 067): Tihar 2026 exactly as the owner's
    /// how-to writes it; the three kinds; instants with their time zone; and every way an entry can be
    /// wrong leaving out that entry alone, so a typo gives nothing away and keeps the other offers.
    /// </summary>
    public sealed class FestivalOfferReaderTests
    {
        private static readonly TimeSpan Nepal = new TimeSpan(5, 45, 0);

        [Test]
        public void Tihar2026_AsTheHowToWritesIt()
        {
            IReadOnlyList<FestivalOffer> offers = FestivalOfferReader.Read(Config(Tihar()));

            Assert.AreEqual(1, offers.Count);
            FestivalOffer tihar = offers[0];
            Assert.AreEqual("tihar-2026", tihar.Id);
            Assert.AreEqual("tihar-lights", tihar.ThemeId);
            Assert.AreEqual(FestivalOfferKind.WatchAd, tihar.Kind);
            Assert.AreEqual(0, tihar.Price, "a gift costs nothing");
            Assert.AreEqual(new DateTimeOffset(2026, 10, 31, 0, 0, 0, Nepal), tihar.Start);
            Assert.AreEqual(new DateTimeOffset(2026, 11, 16, 0, 0, 0, Nepal), tihar.End);
            Assert.AreEqual("ui.offer.tihar", tihar.TitleKey);
        }

        [Test]
        public void AnOffer_IsLiveFromItsStart_UpToButNotIncludingItsEnd()
        {
            FestivalOffer tihar = FestivalOfferReader.Read(Config(Tihar()))[0];

            Assert.IsFalse(tihar.IsActiveAt(new DateTimeOffset(2026, 10, 30, 23, 59, 59, Nepal)));
            Assert.IsTrue(tihar.IsActiveAt(new DateTimeOffset(2026, 10, 31, 0, 0, 0, Nepal)));
            Assert.IsTrue(tihar.IsActiveAt(new DateTimeOffset(2026, 11, 15, 23, 59, 59, Nepal)));
            Assert.IsFalse(tihar.IsActiveAt(new DateTimeOffset(2026, 11, 16, 0, 0, 0, Nepal)));
            Assert.IsTrue(tihar.IsActiveAt(new DateTimeOffset(2026, 10, 30, 18, 15, 0, TimeSpan.Zero)), "the same instant in UTC");
            Assert.IsFalse(tihar.IsActiveAt(new DateTimeOffset(2026, 10, 30, 18, 14, 59, TimeSpan.Zero)));
        }

        [Test]
        public void TheKinds_AreFreeWatchAdAndDiscount_InAnyCase()
        {
            FestivalOfferEntry free = Tihar("free-2026");
            free.kind = "Free";
            FestivalOfferEntry ad = Tihar("ad-2026");
            ad.kind = " WATCH-AD ";
            FestivalOfferEntry discount = Tihar("discount-2026");
            discount.kind = "discount";
            discount.price = 500;

            IReadOnlyList<FestivalOffer> offers = FestivalOfferReader.Read(Config(free, ad, discount));

            CollectionAssert.AreEqual(
                new[] { FestivalOfferKind.Free, FestivalOfferKind.WatchAd, FestivalOfferKind.Discount },
                offers.Select(o => o.Kind));
            CollectionAssert.AreEqual(new[] { 0, 0, 500 }, offers.Select(o => o.Price), "only a discount has a price");
        }

        [Test]
        public void Instants_MayBeUtc_OrWithoutSeconds()
        {
            FestivalOfferEntry entry = Tihar();
            entry.start = "2026-10-30T18:15:00Z";
            entry.end = "2026-11-16T00:00+05:45";

            FestivalOffer offer = FestivalOfferReader.Read(Config(entry)).Single();

            Assert.AreEqual(new DateTimeOffset(2026, 10, 31, 0, 0, 0, Nepal), offer.Start);
            Assert.AreEqual(new DateTimeOffset(2026, 11, 16, 0, 0, 0, Nepal), offer.End);
        }

        [Test]
        public void NoTitle_GetsTheGamesOwnWords()
        {
            FestivalOfferEntry entry = Tihar();
            entry.title = " ";

            Assert.AreEqual(FestivalOfferReader.DefaultTitleKey, FestivalOfferReader.Read(Config(entry)).Single().TitleKey);
            Assert.AreEqual("ui.offer.default", FestivalOfferReader.DefaultTitleKey);
        }

        [Test]
        public void NoConfig_OrNoOffers_IsNoOffer()
        {
            Assert.AreEqual(0, FestivalOfferReader.Read(null).Count);
            Assert.AreEqual(0, FestivalOfferReader.Read(new FestivalOffersConfig { offers = null }).Count);
            Assert.AreEqual(0, FestivalOfferReader.Read(new FestivalOffersConfig()).Count);
            Assert.AreEqual(0, FestivalOfferReader.Read(Config((FestivalOfferEntry)null)).Count);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Tihar-2026")]
        [TestCase("tihar 2026")]
        [TestCase("tihar_2026")]
        [TestCase("tihar-2026-0123456789-0123456789-0123456789-0123456789-0123456789")]
        public void ABadId_LeavesTheEntryOut(string id)
        {
            FestivalOfferEntry entry = Tihar();
            entry.id = id;

            AssertOnlyTheGoodOneIsRead(entry);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("tihar")]
        [TestCase("durbar-square")]
        [TestCase("classic-crimson")]
        public void ATheme_ThatIsntAPaidThemeOfTheShop_LeavesTheEntryOut(string theme)
        {
            FestivalOfferEntry entry = Tihar();
            entry.theme = theme;

            AssertOnlyTheGoodOneIsRead(entry);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("gift")]
        [TestCase("watch_ad")]
        public void AnUnknownKind_LeavesTheEntryOut(string kind)
        {
            FestivalOfferEntry entry = Tihar();
            entry.kind = kind;

            AssertOnlyTheGoodOneIsRead(entry);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(CosmeticCatalog.Themes.TiharLightsPrice)]
        [TestCase(CosmeticCatalog.Themes.TiharLightsPrice + 500)]
        public void ADiscount_NotBelowTheShopsPrice_LeavesTheEntryOut(int price)
        {
            FestivalOfferEntry entry = Tihar();
            entry.kind = "discount";
            entry.price = price;

            AssertOnlyTheGoodOneIsRead(entry);
        }

        [TestCase(null, "2026-11-16T00:00:00+05:45")]
        [TestCase("2026-10-31", "2026-11-16T00:00:00+05:45")]
        [TestCase("2026-10-31T00:00:00", "2026-11-16T00:00:00+05:45")]
        [TestCase("31/10/2026 00:00 +05:45", "2026-11-16T00:00:00+05:45")]
        [TestCase("2026-10-31T00:00:00+05:45", "soon")]
        [TestCase("2026-10-31T00:00:00+05:45", "2026-10-31T00:00:00+05:45")]
        [TestCase("2026-11-16T00:00:00+05:45", "2026-10-31T00:00:00+05:45")]
        public void ABadOrZonelessStartOrEnd_LeavesTheEntryOut(string start, string end)
        {
            FestivalOfferEntry entry = Tihar();
            entry.start = start;
            entry.end = end;

            AssertOnlyTheGoodOneIsRead(entry);
        }

        [Test]
        public void ASecondEntry_WithTheSameId_IsLeftOut()
        {
            FestivalOfferEntry second = Tihar();
            second.kind = "free";

            IReadOnlyList<FestivalOffer> offers = FestivalOfferReader.Read(Config(Tihar(), second));

            Assert.AreEqual(1, offers.Count);
            Assert.AreEqual(FestivalOfferKind.WatchAd, offers[0].Kind, "the first one written");
        }

        private static void AssertOnlyTheGoodOneIsRead(FestivalOfferEntry bad)
        {
            IReadOnlyList<FestivalOffer> offers = FestivalOfferReader.Read(Config(bad, Tihar("good-2026")));

            CollectionAssert.AreEqual(new[] { "good-2026" }, offers.Select(o => o.Id));
        }

        private static FestivalOffersConfig Config(params FestivalOfferEntry[] entries) =>
            new FestivalOffersConfig { offers = entries.ToList() };

        /// <summary>The Tihar 2026 offer as Docs/Setup/FestivalOffers.md writes it.</summary>
        internal static FestivalOfferEntry Tihar(string id = "tihar-2026") => new FestivalOfferEntry
        {
            id = id,
            theme = "tihar-lights",
            kind = "watch-ad",
            start = "2026-10-31T00:00:00+05:45",
            end = "2026-11-16T00:00:00+05:45",
            title = "ui.offer.tihar",
        };
    }
}
