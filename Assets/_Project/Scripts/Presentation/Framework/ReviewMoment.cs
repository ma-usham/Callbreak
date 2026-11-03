using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Reviews;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Asks for Google Play's review card on the bare main menu after a win (the owner, 3 Nov 2026;
    /// ADR 080): at most once per Menu scene, once the menu has stood bare for
    /// <see cref="SettleSeconds"/>. Bare is the main menu up and visible, no dialog, no curtain, the
    /// name answered and no prize coins still to fly in. Daily Rewards and a festival offer open by
    /// themselves on the bare menu, so they go first, and the card comes once the menu is bare again.
    /// Not once the game has been in the background in this Menu scene, as an app open ad may then
    /// be over the menu (<see cref="ReturnToGameAdMoment"/>). Whether an ask is due is
    /// <see cref="IReviewPrompt"/>'s.
    /// </summary>
    public sealed class ReviewMoment : IStartable, ITickable, IDisposable
    {
        /// <summary>How long the menu stands bare before the card is asked for, in seconds.</summary>
        public const float SettleSeconds = 1.5f;

        /// <summary>How often it looks, in seconds.</summary>
        private const float CheckSeconds = 0.5f;

        /// <summary>Its first look, after Daily Rewards' (half a second) and the festival offer's (one second).</summary>
        private const float FirstCheckSeconds = 2f;

        private readonly IReviewPrompt _prompt;
        private readonly IAppForeground _foreground;
        private readonly ScreenRouter _router;
        private readonly IAppFlow _flow;
        private readonly IProfileService _profile;
        private readonly IDialogs _dialogs;
        private readonly ILoadingCurtain _curtain;
        private readonly IUnseenCoins _unseen;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private float _nextCheck = -1f;
        private float _bareSince = -1f;
        private bool _done;
        private bool _listening;
        private bool _disposed;

        public ReviewMoment(
            IReviewPrompt prompt,
            IAppForeground foreground,
            ScreenRouter router,
            IAppFlow flow,
            IProfileService profile,
            IDialogs dialogs,
            ILoadingCurtain curtain,
            IUnseenCoins unseen)
        {
            _prompt = prompt;
            _foreground = foreground;
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
            _listening = true;
        }

        public void Tick()
        {
            if (_done || _disposed)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (_nextCheck < 0f)
            {
                // The first frame of the menu: the screens that open with it, Daily Rewards and an offer go first.
                _nextCheck = now + FirstCheckSeconds;
                return;
            }

            if (now < _nextCheck)
            {
                return;
            }

            _nextCheck = now + CheckSeconds;
            if (!_prompt.IsDue)
            {
                return;
            }

            if (!IsBareMainMenu())
            {
                _bareSince = -1f;
                return;
            }

            if (_bareSince < 0f)
            {
                _bareSince = now;
                return;
            }

            if (now - _bareSince >= SettleSeconds)
            {
                _done = true;
                _ = AskAsync();
            }
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
            }

            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private void OnLeft(DateTime atUtc)
        {
            _done = true;
        }

        private async Task AskAsync()
        {
            try
            {
                await _prompt.AskIfDueAsync(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // The menu went away while the card was up.
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
