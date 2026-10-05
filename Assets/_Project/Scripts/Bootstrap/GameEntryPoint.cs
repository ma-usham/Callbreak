using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Updates;
using Callbreak.Infrastructure.Platform;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// The startup sequence (GDD §21): settings, profile and language are already loaded when the
    /// scope builds; sign-in starts in the background and never blocks the UI; a newer version on
    /// Google Play is asked about first (ADR 036); the Menu scene then loads behind the curtain. Also
    /// polls connectivity for the whole session.
    /// </summary>
    public sealed class GameEntryPoint : IAsyncStartable, ITickable
    {
        private const int TargetFrameRate = 60;

        // The longest the launch waits behind the curtain for Google Play's answer.
        private static readonly TimeSpan UpdateCheckWait = TimeSpan.FromSeconds(3);

        private readonly IAppFlow _flow;
        private readonly IAuthService _auth;
        private readonly NetworkStatus _network;
        private readonly ForcedUpdate _update;

        public GameEntryPoint(IAppFlow flow, IAuthService auth, NetworkStatus network, ForcedUpdate update)
        {
            _flow = flow;
            _auth = auth;
            _network = network;
            _update = update;
        }

        public async Awaitable StartAsync(CancellationToken cancellation = default)
        {
            // GDD §1 and §30: 60 fps is the target; phones default to 30 unless asked.
            UnityEngine.Application.targetFrameRate = TargetFrameRate;
            _ = SignInAsync(cancellation);
            await AskAboutUpdatesAsync(cancellation);
            await _flow.GoToMenuAsync(MenuEntry.MainMenu, cancellation);
        }

        public void Tick() => _network.Tick();

        /// <summary>
        /// A newer version comes before the menu and what it does by itself (an invite link, the
        /// rejoin and resume offers, the first-run guide): the launch asks first and waits behind the
        /// curtain for at most <see cref="UpdateCheckWait"/>. An answer that comes later is judged
        /// where the player is by then: the menu's moments join the check (ForcedUpdateMoments).
        /// </summary>
        private async Task AskAboutUpdatesAsync(CancellationToken ct)
        {
            Task check = _update.CheckAsync(() => _flow.State == AppState.Boot);
            if (!check.IsCompleted)
            {
                await Task.WhenAny(check, Task.Delay(UpdateCheckWait, ct));
            }
        }

        private async Task SignInAsync(CancellationToken ct)
        {
            try
            {
                await _auth.SignInAsync(ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
