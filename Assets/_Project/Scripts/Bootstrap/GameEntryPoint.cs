using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Infrastructure.Platform;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// The startup sequence (GDD §21): settings, profile and language are already loaded when the
    /// scope builds; sign-in starts in the background and never blocks the UI; the Menu scene then
    /// loads behind the curtain. Also polls connectivity for the whole session.
    /// </summary>
    public sealed class GameEntryPoint : IAsyncStartable, ITickable
    {
        private const int TargetFrameRate = 60;

        private readonly IAppFlow _flow;
        private readonly IAuthService _auth;
        private readonly NetworkStatus _network;

        public GameEntryPoint(IAppFlow flow, IAuthService auth, NetworkStatus network)
        {
            _flow = flow;
            _auth = auth;
            _network = network;
        }

        public async Awaitable StartAsync(CancellationToken cancellation = default)
        {
            // GDD §1 and §30: 60 fps is the target; phones default to 30 unless asked.
            UnityEngine.Application.targetFrameRate = TargetFrameRate;
            _ = SignInAsync(cancellation);
            await _flow.GoToMenuAsync(MenuEntry.MainMenu, cancellation);
        }

        public void Tick() => _network.Tick();

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
