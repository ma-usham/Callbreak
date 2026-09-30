using UnityEngine;

namespace Callbreak.Presentation.Feedback
{
    /// <summary>
    /// The avatar presets players pick from (GDD §11 screen 14), by avatar id. An id this build has
    /// no picture for shows the first preset, so a newer client's avatar never breaks a seat.
    /// </summary>
    [CreateAssetMenu(menuName = "Callbreak/Avatar Library", fileName = "AvatarLibrary")]
    public sealed class AvatarLibrary : ScriptableObject
    {
        [SerializeField] private Sprite[] _avatars;

        public int Count => _avatars != null ? _avatars.Length : 0;

        public Sprite Get(int avatarId)
        {
            if (Count == 0)
            {
                return null;
            }

            Sprite picked = avatarId >= 0 && avatarId < _avatars.Length ? _avatars[avatarId] : null;
            return picked != null ? picked : _avatars[0];
        }
    }
}
