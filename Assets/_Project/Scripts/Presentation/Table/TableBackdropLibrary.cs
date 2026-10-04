using System;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>One picture of the backdrop behind the table, and the part of the backdrop's frame it covers.</summary>
    [Serializable]
    public struct BackdropLayer
    {
        [SerializeField] private Sprite _sprite;
        [SerializeField] private Rect _area;

        public BackdropLayer(Sprite sprite, Rect area)
        {
            _sprite = sprite;
            _area = area;
        }

        /// <summary>The picture; null when the layer shows nothing.</summary>
        public Sprite Sprite => _sprite;

        /// <summary>
        /// Where the picture lies in the frame, in frame pixels from the frame's top-left corner. An
        /// empty area means the whole frame (a layer cut down to its content says where it was cut from).
        /// </summary>
        public Rect Area => _area;

        public bool IsEmpty => _sprite == null;
    }

    /// <summary>
    /// What the table shows behind everything else: the background's scenery, the table on it, and
    /// the scenery's foreground in front of the table's near edge, in one frame.
    /// </summary>
    public readonly struct TableBackdrop
    {
        public TableBackdrop(string backgroundId, string tableId, BackdropLayer back, BackdropLayer table, BackdropLayer front, Vector2 frame)
        {
            BackgroundId = backgroundId;
            TableId = tableId;
            Back = back;
            Table = table;
            Front = front;
            Frame = frame;
        }

        /// <summary>The background shown (the default one when the asked-for one has no art).</summary>
        public string BackgroundId { get; }

        /// <summary>The table shown (the default one when the asked-for one has no art).</summary>
        public string TableId { get; }

        public BackdropLayer Back { get; }

        public BackdropLayer Table { get; }

        /// <summary>Empty for a background without a foreground.</summary>
        public BackdropLayer Front { get; }

        /// <summary>The frame's size in frame pixels: BG_Table's 2230 × 1255 (the mockup plus margins for 21:9 and 4:3).</summary>
        public Vector2 Frame { get; }
    }

    /// <summary>
    /// The art behind the table (the owner, 4 Oct 2026; mockups 20 and 21): every background the shop
    /// sells as two layers, its scenery (the back, behind the table) and an optional foreground (the
    /// front, before the table's near edge), and every table, each by its shop id. All of them share
    /// one frame, BG_Table's, so any background goes with any table. A background or table without art
    /// shows the default one. The editor builder (Callbreak/Table/Build Table Backdrops) fills it and the
    /// Table prefab holds it, so its pictures load with the table and nothing is looked up by name.
    /// </summary>
    [CreateAssetMenu(menuName = "Callbreak/Table Backdrop Library", fileName = "TableBackdrops")]
    public sealed class TableBackdropLibrary : ScriptableObject
    {
        /// <summary>One background: its shop id, its scenery and its foreground (empty when it has none).</summary>
        [Serializable]
        public sealed class BackgroundArt
        {
            [SerializeField] private string _id;
            [SerializeField] private BackdropLayer _back;
            [SerializeField] private BackdropLayer _front;

            public BackgroundArt()
            {
            }

            public BackgroundArt(string id, BackdropLayer back, BackdropLayer front)
            {
                _id = id;
                _back = back;
                _front = front;
            }

            public string Id => _id;

            public BackdropLayer Back => _back;

            public BackdropLayer Front => _front;
        }

        /// <summary>One table: its shop id and its picture.</summary>
        [Serializable]
        public sealed class TableArt
        {
            [SerializeField] private string _id;
            [SerializeField] private BackdropLayer _table;

            public TableArt()
            {
            }

            public TableArt(string id, BackdropLayer table)
            {
                _id = id;
                _table = table;
            }

            public string Id => _id;

            public BackdropLayer Table => _table;
        }

        [SerializeField] private Vector2 _frame = new Vector2(2230f, 1255f);
        [SerializeField] private BackgroundArt[] _backgrounds = Array.Empty<BackgroundArt>();
        [SerializeField] private TableArt[] _tables = Array.Empty<TableArt>();
        [SerializeField] private string _defaultBackgroundId = "himalayan-vista";
        [SerializeField] private string _defaultTableId = "himalayan-ivory";

        /// <summary>The frame every layer is drawn in, in frame pixels.</summary>
        public Vector2 Frame => _frame;

        public string DefaultBackgroundId => _defaultBackgroundId;

        public string DefaultTableId => _defaultTableId;

        public int BackgroundCount => _backgrounds.Length;

        public int TableCount => _tables.Length;

        public BackgroundArt BackgroundAt(int index) => _backgrounds[index];

        public TableArt TableAt(int index) => _tables[index];

        /// <summary>The default background on the default table.</summary>
        public TableBackdrop Default => Backdrop(_defaultBackgroundId, _defaultTableId);

        /// <summary>A background by its shop id; null when this library does not list it.</summary>
        public BackgroundArt Background(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < _backgrounds.Length; i++)
            {
                if (_backgrounds[i] != null && string.Equals(_backgrounds[i].Id, id, StringComparison.Ordinal))
                {
                    return _backgrounds[i];
                }
            }

            return null;
        }

        /// <summary>A table by its shop id; null when this library does not list it.</summary>
        public TableArt Table(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < _tables.Length; i++)
            {
                if (_tables[i] != null && string.Equals(_tables[i].Id, id, StringComparison.Ordinal))
                {
                    return _tables[i];
                }
            }

            return null;
        }

        /// <summary>
        /// The backdrop for a background and a table. One that this library does not list, or lists
        /// without its picture yet, gives way to the default one; <see cref="TableBackdrop.BackgroundId"/>
        /// and <see cref="TableBackdrop.TableId"/> say what is shown. The default table may have no
        /// picture of its own while the default background's picture still has the table in it.
        /// </summary>
        public TableBackdrop Backdrop(string backgroundId, string tableId)
        {
            BackgroundArt background = Background(backgroundId);
            if (background == null || background.Back.IsEmpty)
            {
                background = Background(_defaultBackgroundId);
            }

            TableArt table = Table(tableId);
            if (table == null || table.Table.IsEmpty)
            {
                table = Table(_defaultTableId);
            }

            return new TableBackdrop(
                background?.Id ?? _defaultBackgroundId,
                table?.Id ?? _defaultTableId,
                background?.Back ?? default,
                table?.Table ?? default,
                background?.Front ?? default,
                _frame);
        }

        /// <summary>Replaces the library (editor builder and tests only).</summary>
        public void Set(Vector2 frame, BackgroundArt[] backgrounds, TableArt[] tables, string defaultBackgroundId, string defaultTableId)
        {
            _frame = frame;
            _backgrounds = backgrounds ?? Array.Empty<BackgroundArt>();
            _tables = tables ?? Array.Empty<TableArt>();
            _defaultBackgroundId = defaultBackgroundId;
            _defaultTableId = defaultTableId;
        }
    }
}
