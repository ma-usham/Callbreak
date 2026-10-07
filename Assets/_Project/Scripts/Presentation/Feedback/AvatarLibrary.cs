using UnityEngine;

namespace Callbreak.Presentation.Feedback
{
    /// <summary>
    /// The avatar presets players pick from (GDD §11 screen 14), by avatar id. An id this build has
    /// no picture for shows the first preset, so a newer client's avatar never breaks a seat. Each
    /// preset also comes framed (ADR 043): the whole portrait in its own gold ring, as the name
    /// screen at the start shows it; a preset without a framed picture shows its plain one there.
    /// </summary>
    [CreateAssetMenu(menuName = "Callbreak/Avatar Library", fileName = "AvatarLibrary")]
    public sealed class AvatarLibrary : ScriptableObject
    {
        [SerializeField] private Sprite[] _avatars;
        [SerializeField] private Sprite[] _framed;

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

        /// <summary>The preset in its gold ring (ADR 043); the plain picture when it has no framed one.</summary>
        public Sprite Framed(int avatarId)
        {
            int id = avatarId >= 0 && avatarId < Count ? avatarId : 0;
            Sprite framed = _framed != null && id < _framed.Length ? _framed[id] : null;
            return framed != null ? framed : Get(id);
        }
    }
}
