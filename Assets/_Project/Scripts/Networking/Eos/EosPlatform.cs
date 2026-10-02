using System;
using Epic.OnlineServices.Logging;
using PlayEveryWare.EpicOnlineServices;
using UnityEngine;

namespace Callbreak.Networking.Eos
{
    /// <summary>
    /// Epic Online Services for the game. It tells whether the plugin's config is complete, and
    /// starts the plugin's EOSManager, which starts the SDK from that config and ticks it every
    /// frame. The config is set in EOS Plugin > EOS Configuration and kept in
    /// Assets/StreamingAssets/EOS. The composition root decides with this; it keeps no state.
    /// </summary>
    public static class EosPlatform
    {
        /// <summary>True while the SDK runs (an EOSManager started it).</summary>
        public static bool IsRunning => EOSManager.Instance.GetEOSPlatformInterface() != null;

        /// <summary>
        /// True when the config names the product and this platform's deployment, and holds complete
        /// client credentials. EOS cannot start without them.
        /// </summary>
        public static bool IsConfigured()
        {
            try
            {
                ProductConfig product = Config.Get<ProductConfig>();
                if (product == null || product.ProductId == Guid.Empty || string.IsNullOrWhiteSpace(product.ProductName)
                    || string.IsNullOrWhiteSpace(product.ProductVersion) || !product.TryGetFirstCompleteNamedClientCredentials(out _))
                {
                    return false;
                }

                // Read afresh, not through PlatformManager.GetPlatformConfig, which keeps its first
                // answer for good: a config finished while the Editor runs then counts too. A
                // platform config without credentials of its own takes the product's first
                // complete ones as it is read.
                return PlatformManager.TryGetConfig(PlatformManager.CurrentPlatform, out PlatformConfig platform)
                    && platform != null && platform.deployment.IsComplete
                    && platform.clientCredentials != null && platform.clientCredentials.IsComplete;
            }
            catch (Exception e)
            {
                Debug.LogWarning("EOS: its config could not be read (" + e.GetType().Name + ").");
                return false;
            }
        }

        /// <summary>
        /// Starts EOS once for the app. It adds the plugin's EOSManager to a new root object, whose
        /// Awake keeps the object for the app's whole life (DontDestroyOnLoad), starts the SDK from
        /// the config and ticks it every frame. With EOS running already, nothing is added. True
        /// when EOS runs afterwards.
        /// <para>
        /// Nothing may destroy that object. The plugin shuts EOS down from it when the app quits (in
        /// the Editor: when Play mode ends). Destroying it earlier skips that, and the next start of
        /// EOS in the same Editor then hangs the Editor while it unloads the SDK that still runs.
        /// </para>
        /// </summary>
        public static bool Start()
        {
            if (IsRunning)
            {
                return true;
            }

            new GameObject("EOSManager").AddComponent<EOSManager>();
            if (!IsRunning)
            {
                Debug.LogWarning("EOS did not start (its error is above).");
                return false;
            }

            // In the Editor the plugin logs every SDK message (VeryVerbose); warnings and errors are enough.
            EOSManager.Instance.SetLogLevel(LogCategory.AllCategories, LogLevel.Warning);
            return true;
        }
    }
}
