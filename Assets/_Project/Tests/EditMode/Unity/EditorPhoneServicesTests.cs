using Callbreak.Infrastructure.Platform;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// What a game played in the Editor takes from a phone (ADR 073): AdMob's test ads and the festival
    /// offers, each the Editor's own setting. The PlayMode tests set both for their boot and put them back
    /// as they were.
    /// </summary>
    public sealed class EditorPhoneServicesTests
    {
        [Test]
        public void TheSwitches_AreTheEditorsSettings_PutBackAsTheyWere()
        {
            bool ads = EditorPhoneServices.AdMobTestAds;
            bool offers = EditorPhoneServices.FestivalOffers;
            try
            {
                EditorPhoneServices.AdMobTestAds = true;
                EditorPhoneServices.FestivalOffers = false;
                using (EditorPhoneServices.While(false))
                {
                    Assert.IsFalse(EditorPhoneServices.AdMobTestAds);
                    Assert.IsFalse(EditorPhoneServices.FestivalOffers);
                }

                Assert.IsTrue(EditorPhoneServices.AdMobTestAds, "as it was");
                Assert.IsFalse(EditorPhoneServices.FestivalOffers, "as it was");
            }
            finally
            {
                EditorPhoneServices.AdMobTestAds = ads;
                EditorPhoneServices.FestivalOffers = offers;
            }
        }
    }
}
