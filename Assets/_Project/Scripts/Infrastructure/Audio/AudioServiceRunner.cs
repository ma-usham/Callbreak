using System;
using UnityEngine;

namespace Callbreak.Infrastructure.Audio
{
    /// <summary>
    /// Gives <see cref="AudioService"/> a per-frame tick (unscaled time, so fades and ducking keep
    /// running while the game is paused). Added by the service to the GameObject it was given.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class AudioServiceRunner : MonoBehaviour
    {
        internal Action<float> Tick { get; set; }

        private void Update()
        {
            Tick?.Invoke(Time.unscaledDeltaTime);
        }
    }
}
