namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The table's banner where there is no ad network: the Editor, the tests and desktop builds
    /// (TableLifetimeScope). It never has a banner, so the table looks as it always did and nothing
    /// is requested; the table still says where one would go, as on phones.
    /// </summary>
    public sealed class StandInBannerAds : IBannerAds
    {
        public bool IsShowing => false;

        public void ShowIn(BannerSlot slot)
        {
        }

        public void Hide()
        {
        }
    }
}
