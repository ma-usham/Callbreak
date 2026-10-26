using System;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using VContainer.Unity;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The banner ad on the table (the owner, 14 and 26 Oct 2026, ADR 049). When the table opens it
    /// makes room at its top while a banner may come (no No Ads), and keeps that look for the whole
    /// table, so nothing moves under the player when an ad loads, No Ads is bought or a refund comes;
    /// a player who owns No Ads, and the Editor, keep the table as it always was. The banner may then
    /// show in the table's banner slot, at the top, while the bare table is in front of the player.
    /// The banner is the ad network's own view, drawn over the whole game, so it goes off the screen
    /// whenever something covers the table or would sit under it: a screen over the table (the
    /// scorecard, the match menu, settings, the rules, a round's or the match's results), a dialog,
    /// the loading curtain, the "Reconnecting…" cover, or a toast at the top of the screen. Asked
    /// every frame, so a slot that moved (the phone turned round, and the notch with it) moves the
    /// banner too. Whether there is a banner at all (No Ads, the network) is <see cref="IBannerAds"/>'s
    /// business.
    /// </summary>
    public sealed class TableBannerPresenter : IStartable, ITickable, IDisposable
    {
        private readonly TableView _table;
        private readonly ScreenRouter _router;
        private readonly IBannerAds _banner;
        private readonly IDialogs _dialogs;
        private readonly ILoadingCurtain _curtain;
        private readonly ToastView _toast;

        /// <param name="toast">The toasts' view, over which the banner never shows; none when null.</param>
        public TableBannerPresenter(
            TableView table,
            ScreenRouter router,
            IBannerAds banner,
            IDialogs dialogs,
            ILoadingCurtain curtain,
            ToastView toast = null)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _banner = banner ?? throw new ArgumentNullException(nameof(banner));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _curtain = curtain ?? throw new ArgumentNullException(nameof(curtain));
            _toast = toast;
        }

        public void Start() => _table.SetBannerRoom(_banner.MayShow);

        public void Tick()
        {
            if (TableInFront() && _table.TryGetBannerSlot(out BannerSlot slot))
            {
                _banner.ShowIn(slot);
            }
            else
            {
                _banner.Hide();
            }
        }

        public void Dispose() => _banner.Hide();

        /// <summary>The bare table is in front of the player, with nothing over it or at the top of the screen.</summary>
        private bool TableInFront()
        {
            return _router.Current == ScreenId.Gameplay
                && !_dialogs.IsOpen
                && !_curtain.IsCovering
                && !_table.IsReconnecting
                && (_toast == null || !_toast.IsShowing);
        }
    }
}
