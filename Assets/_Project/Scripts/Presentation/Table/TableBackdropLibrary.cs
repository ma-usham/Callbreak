using System;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The pictures of the backgrounds and the tables the shop sells (mockups 20 and 21), by shop id,
    /// each one picture of the whole backdrop frame: the 1920 × 1080 screen plus margins for 21:9 and
    /// 4:3 (2230 × 1255 frame pixels, 2560 × 1440 canvas units). V2 (the owner, 28 Oct 2026): a table
    /// fills the screen at the table (<see cref="TableView.SetTable"/>), and the equipped background
    /// shows behind the menus instead (<see cref="Framework.EquippedBackground"/>). The tables are
    /// cloths now (the owner, 29 Oct 2026, ADR 058); a cloth may have a border, a picture tiled round
    /// the screen's edges (<see cref="Picture.Frame"/>). A background or table without a picture shows
    /// the default one. The editor builder (Callbreak/Table/Build Table
    /// Backdrops) fills it; the Table prefab and the menu screens hold it, so its pictures load with
    /// them and nothing is looked up by name.
    /// </summary>
    [CreateAssetMenu(menuName = "Callbreak/Table Backdrop Library", fileName = "TableBackdrops")]
    public sealed class TableBackdropLibrary : ScriptableObject
    {
        /// <summary>One background or table: its shop id and its picture (null while it has none).</summary>
        [Serializable]
        public sealed class Picture
        {
            [SerializeField] private string _id;
            [SerializeField] private Sprite _sprite;
            [SerializeField] private Sprite _frame;
            [SerializeField] private bool _nightScene;

            public Picture()
            {
            }

            public Picture(string id, Sprite sprite, Sprite frame = null, bool nightScene = false)
            {
                _id = id;
                _sprite = sprite;
                _frame = frame;
                _nightScene = nightScene;
            }

            public string Id => _id;

            public Sprite Sprite => _sprite;

            /// <summary>A cloth's border, sliced and tiled round the screen's edges; null for none.</summary>
            public Sprite Frame => _frame;

            /// <summary>
            /// A background that is a night scene (Tihar Lights, Phewa Moonlight; ADR 064): the words
            /// a menu draws straight on it are drawn light (<see cref="Framework.BackgroundInk"/>).
            /// </summary>
            public bool NightScene => _nightScene;
        }

        [SerializeField] private Picture[] _backgrounds = Array.Empty<Picture>();
        [SerializeField] private Picture[] _tables = Array.Empty<Picture>();
        [SerializeField] private string _defaultBackgroundId = "durbar-square";
        [SerializeField] private string _defaultTableId = "green-dhaka";

        public string DefaultBackgroundId => _defaultBackgroundId;

        public string DefaultTableId => _defaultTableId;

        public int BackgroundCount => _backgrounds.Length;

        public int TableCount => _tables.Length;

        public Picture BackgroundAt(int index) => _backgrounds[index];

        public Picture TableAt(int index) => _tables[index];

        /// <summary>A background by its shop id; null when this library does not list it.</summary>
        public Picture Background(string id) => Find(_backgrounds, id);

        /// <summary>A table by its shop id; null when this library does not list it.</summary>
        public Picture Table(string id) => Find(_tables, id);

        /// <summary>
        /// The picture of a background, behind the menus. One that this library does not list, or
        /// lists without its picture yet, gives way to the default one; <paramref name="shownId"/>
        /// says which is shown. Null only when the default has no picture either.
        /// </summary>
        public Sprite BackgroundPicture(string id, out string shownId) => PictureOf(_backgrounds, id, _defaultBackgroundId, out shownId);

        /// <summary>The picture of a table, filling the screen at the table; as <see cref="BackgroundPicture"/>.</summary>
        public Sprite TablePicture(string id, out string shownId) => PictureOf(_tables, id, _defaultTableId, out shownId);

        /// <summary>Whether the background <see cref="BackgroundPicture"/> shows for that id is a night scene.</summary>
        public bool IsNightScene(string id)
        {
            BackgroundPicture(id, out string shownId);
            return Find(_backgrounds, shownId)?.NightScene ?? false;
        }

        /// <summary>The border of the table <see cref="TablePicture"/> shows for that id; null when it has none.</summary>
        public Sprite TableFrame(string id)
        {
            TablePicture(id, out string shownId);
            return Find(_tables, shownId)?.Frame;
        }

        /// <summary>Replaces the library (editor builder and tests only).</summary>
        public void Set(Picture[] backgrounds, Picture[] tables, string defaultBackgroundId, string defaultTableId)
        {
            _backgrounds = backgrounds ?? Array.Empty<Picture>();
            _tables = tables ?? Array.Empty<Picture>();
            _defaultBackgroundId = defaultBackgroundId;
            _defaultTableId = defaultTableId;
        }

        private static Sprite PictureOf(Picture[] pictures, string id, string defaultId, out string shownId)
        {
            Picture picture = Find(pictures, id);
            if (picture == null || picture.Sprite == null)
            {
                picture = Find(pictures, defaultId);
            }

            shownId = picture?.Id ?? defaultId;
            return picture?.Sprite;
        }

        private static Picture Find(Picture[] pictures, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < pictures.Length; i++)
            {
                if (pictures[i] != null && string.Equals(pictures[i].Id, id, StringComparison.Ordinal))
                {
                    return pictures[i];
                }
            }

            return null;
        }
    }
}
