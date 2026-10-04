using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The pictures the shop shows for backgrounds and tables (decks show their real backs from
    /// the card sprites): one per catalogue id and kind, set when the screen is built from the art
    /// that exists then (the generated thumbnails and previews, or their placeholders).
    /// </summary>
    public sealed class ShopPictures : MonoBehaviour
    {
        // "<kind>/<id>", e.g. "Table/royal-crimson", and the picture of each.
        [SerializeField] private string[] _keys = new string[0];
        [SerializeField] private Sprite[] _sprites = new Sprite[0];

        public static string KeyOf(string kind, string id) => kind + "/" + id;

        /// <summary>The picture for an item, or null when the screen has none for it.</summary>
        public Sprite Get(string kind, string id)
        {
            string key = KeyOf(kind, id);
            for (int i = 0; i < _keys.Length && i < _sprites.Length; i++)
            {
                if (_keys[i] == key)
                {
                    return _sprites[i];
                }
            }

            return null;
        }
    }
}
