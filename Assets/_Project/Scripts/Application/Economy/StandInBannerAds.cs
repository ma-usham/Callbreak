namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The table's banner where there is no ad network: the tests, desktop builds and the Editor with
    /// its test banner switched off (TableLifetimeScope, MenuLifetimeScope; ADR 071). It never has a banner, so the table looks as it always did and nothing
    /// is requested; the table still says where one would go, as on phones.
    /// </summary>
    public sealed class StandInBannerAds : IBannerAds
    {
        public bool IsShowing => false;

        public bool MayShow => false;

        public void ShowIn(BannerSlot slot)
        {
        }

        public void Hide()
        {
        }
    }
}
