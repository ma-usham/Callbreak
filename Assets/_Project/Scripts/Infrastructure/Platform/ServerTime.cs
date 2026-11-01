using System;
using Callbreak.Application.Offers;
using Callbreak.Application.Ports;
using UnityEngine.Networking;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// Sets the <see cref="TrustedClock"/> from a server (ADR 067): one HEAD request to Google's
    /// connectivity check at start and each time the game comes back to the foreground, whose HTTP
    /// Date header is the time. It sends nothing about the player. No answer leaves the clock as it
    /// was: on the device's time until a server has answered once.
    /// </summary>
    public sealed class ServerTime : IDisposable
    {
        private const string Url = "https://www.gstatic.com/generate_204";
        private const int TimeoutSeconds = 10;

        private readonly TrustedClock _clock;
        private readonly IAppForeground _foreground;
        private UnityWebRequest _request;
        private bool _started;
        private bool _disposed;

        public ServerTime(TrustedClock clock, IAppForeground foreground)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _foreground = foreground ?? throw new ArgumentNullException(nameof(foreground));
        }

        public void Start()
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _foreground.CameToForeground += Ask;
            Ask();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _foreground.CameToForeground -= Ask;
            _request?.Abort();
        }

        private void Ask()
        {
            if (_request != null || _disposed)
            {
                return;
            }

            UnityWebRequest request = UnityWebRequest.Head(Url);
            request.timeout = TimeoutSeconds;
            _request = request;
            request.SendWebRequest().completed += _ =>
            {
                // Any answer from the server carries its time, an error page too.
                if (!_disposed && (request.result == UnityWebRequest.Result.Success || request.result == UnityWebRequest.Result.ProtocolError))
                {
                    _clock.TrySyncFromHttpDate(request.GetResponseHeader("Date"));
                }

                request.Dispose();
                if (_request == request)
                {
                    _request = null;
                }
            };
        }
    }
}
