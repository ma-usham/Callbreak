#if UNITY_EDITOR
using System;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// Whether a game played in the Editor shows the drawn test banner (<see cref="EditorTestBannerAdNetwork"/>,
    /// ADR 071): the Editor's own setting, on until it is turned off (Callbreak &gt; Ads &gt; Test Banner in
    /// Play Mode), read as the game starts. The PlayMode tests boot with it as they ask
    /// (<see cref="While"/>), off unless a test is about the banner.
    /// </summary>
    public static class EditorTestBanner
    {
        public const string SettingKey = "Callbreak.EditorTestBanner";

        public static bool On
        {
            get => UnityEditor.EditorPrefs.GetBool(SettingKey, true);
            set => UnityEditor.EditorPrefs.SetBool(SettingKey, value);
        }

        /// <summary>Sets the switch until the returned handle is disposed, then puts it back as it was.</summary>
        public static IDisposable While(bool on)
        {
            var restore = new Restore(On);
            On = on;
            return restore;
        }

        private sealed class Restore : IDisposable
        {
            private readonly bool _before;
            private bool _done;

            public Restore(bool before)
            {
                _before = before;
            }

            public void Dispose()
            {
                if (_done)
                {
                    return;
                }

                _done = true;
                On = _before;
            }
        }
    }
}
#endif
