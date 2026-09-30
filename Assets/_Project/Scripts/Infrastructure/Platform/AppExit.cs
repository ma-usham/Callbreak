using Callbreak.Application.Ports;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary><see cref="IAppExit"/>: quits the player, or leaves Play mode in the Editor.</summary>
    public sealed class AppExit : IAppExit
    {
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
