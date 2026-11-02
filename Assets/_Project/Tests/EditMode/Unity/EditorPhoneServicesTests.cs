using Callbreak.Infrastructure.Platform;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// What a game played in the Editor takes from a phone (ADR 073, 077): the festival offers, the
    /// Editor's own setting. The PlayMode tests set it for their boot and put it back as it was.
    /// </summary>
    public sealed class EditorPhoneServicesTests
    {
        [Test]
        public void TheSwitch_IsTheEditorsSetting_PutBackAsItWas()
        {
            bool offers = EditorPhoneServices.FestivalOffers;
            try
            {
                EditorPhoneServices.FestivalOffers = true;
                using (EditorPhoneServices.While(false))
                {
                    Assert.IsFalse(EditorPhoneServices.FestivalOffers);
                }

                Assert.IsTrue(EditorPhoneServices.FestivalOffers, "as it was");
            }
            finally
            {
                EditorPhoneServices.FestivalOffers = offers;
            }
        }
    }
}
