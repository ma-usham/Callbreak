using System;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// <see cref="IDeepLinks"/> over Unity's deep-link hooks: the URL the app was started with and
    /// the ones that arrive while it runs. Anything that is not a valid join link is ignored.
    /// </summary>
    public sealed class DeepLinkService : IDeepLinks, IDisposable
    {
        private string _pending;

        public DeepLinkService()
        {
            UnityEngine.Application.deepLinkActivated += Receive;
            if (InviteLink.TryParse(UnityEngine.Application.absoluteURL, out string code))
            {
                _pending = code;
            }
        }

        public event Action JoinCodeArrived;

        public bool HasJoinCode => _pending != null;

        public string TakeJoinCode()
        {
            string code = _pending;
            _pending = null;
            return code;
        }

        /// <summary>A link handed to the running app by the OS.</summary>
        public void Receive(string url)
        {
            if (!InviteLink.TryParse(url, out string code))
            {
                return;
            }

            _pending = code;
            JoinCodeArrived?.Invoke();
        }

        public void Dispose()
        {
            UnityEngine.Application.deepLinkActivated -= Receive;
        }
    }
}
