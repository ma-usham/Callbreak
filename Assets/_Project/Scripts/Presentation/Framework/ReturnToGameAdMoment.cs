using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Greets the player back with an app open ad (the owner, 15 Oct 2026; ADR 047). It tells
    /// <see cref="IReturnToGameAds"/> when the game went to the background, and on the return asks it
    /// for an ad only on the bare main menu: the menu up and visible (no curtain, so no store sheet
    /// either), no dialog open, the name already answered and no prize coins waiting to fly. So never
    /// in a match, in matchmaking, in a room, in the shop or over a purchase; whether one is due (time
    /// away, No Ads, matches played, three minutes since the last full-screen ad, an ad loaded) is the
    /// rule's. It looks the moment the game is back, before anything on the menu moves.
    /// </summary>
    public sealed class ReturnToGameAdMoment : IStartable, IDisposable
    {
        private readonly IAppForeground _foreground;
        private readonly IReturnToGameAds _ads;
        private readonly ScreenRouter _router;
        private readonly IAppFlow _flow;
        private readonly IProfileService _profile;
        private readonly IDialogs _dialogs;
        private readonly ILoadingCurtain _curtain;
        private readonly IUnseenCoins _unseen;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _listening;
        private bool _disposed;

        public ReturnToGameAdMoment(
            IAppForeground foreground,
            IReturnToGameAds ads,
            ScreenRouter router,
            IAppFlow flow,
            IProfileService profile,
            IDialogs dialogs,
            ILoadingCurtain curtain,
            IUnseenCoins unseen)
        {
            _foreground = foreground;
            _ads = ads;
            _router = router;
            _flow = flow;
            _profile = profile;
            _dialogs = dialogs;
            _curtain = curtain;
            _unseen = unseen;
        }

        public void Start()
        {
            _foreground.WentToBackground += OnLeft;
            _foreground.CameToForeground += OnBack;
            _listening = true;
        }

        public void Dispose()
        {
            // The Menu scene's scope goes with every match: nothing may keep listening.
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_listening)
            {
                _listening = false;
                _foreground.WentToBackground -= OnLeft;
                _foreground.CameToForeground -= OnBack;
            }

            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private void OnLeft(DateTime atUtc)
        {
            _ads.Left(atUtc);
        }

        private void OnBack()
        {
            if (!_disposed && IsBareMainMenu())
            {
                _ = ShowAsync();
            }
        }

        private async Task ShowAsync()
        {
            try
            {
                await _ads.ShowIfDueAsync(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // The menu went away while the ad was up.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private bool IsBareMainMenu()
        {
            return _router.Current == ScreenId.MainMenu
                && _flow.State == AppState.Menu
                && !_curtain.IsCovering
                && !_dialogs.IsOpen
                && !FirstRun.AsksName(_profile)
                && _unseen.Amount == 0;
        }
    }
}
