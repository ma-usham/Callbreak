using System;
using System.IO;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// The layout arttool writes for one screen (ArtSource/Screens/&lt;Name&gt;/layout.json): the
    /// background plate and every element with its mockup rectangle, its cut sprite and shadow, and
    /// for labels the ink box and colours measured on the mockup. Units are mockup pixels.
    /// </summary>
    [Serializable]
    public sealed class ScreenLayout
    {
        public string name;
        public string mockup;
        public float mockW = 1672f;
        public float mockH = 941f;
        /// <summary>
        /// The background plate's picture, or <see cref="EquippedBackground"/>: the background the player
        /// equipped in the shop (V2, behind every menu screen).
        /// </summary>
        public string background;
        public float[] backgroundRect;
        public float backgroundPpu;
        public LayoutNode[] nodes;

        /// <summary>The <see cref="background"/> that shows the player's equipped background.</summary>
        public const string EquippedBackground = "equipped";

        public static string PathFor(string screenName) => $"ArtSource/Screens/{screenName}/layout.json";

        public static ScreenLayout Load(string screenName)
        {
            string path = PathFor(screenName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"No layout for {screenName}: run Tools/art job {screenName} first", path);
            }

            return JsonUtility.FromJson<ScreenLayout>(File.ReadAllText(path));
        }
    }

    [Serializable]
    public sealed class LayoutNode
    {
        public string id;
        public string kind;          // sprite | button | label | group | toggle | slider | input | custom ...
        public string parent;
        public string group;         // Centre (default) | HeaderLeft | HeaderRight | BottomLeft | BottomRight | Full
        public string bind;          // view field name (without the underscore) when it differs from id
        public float[] rect;

        // Sprites
        public string sprite;
        public float[] spriteRect;
        public float spritePpu;
        public string shadowSprite;
        public float[] shadowRect;
        public float shadowPpu;
        public float[] slice;
        public bool hidden;
        public bool raycast;

        // Shared sprites ("use"): opacity, 9-slice scale, and a shared shadow placed behind.
        public float opacity;
        public float sliceScale;
        public string shadowUse;
        public float shadowSpread;
        public float shadowDrop;
        public float shadowOpacity;

        // Labels
        public string text;
        public string font;          // SerifBold | SerifSemiBold | SansRegular | SansMedium | SansSemiBold | SansBold
        public string align;         // left | centre | right
        public string style;         // plain | buttonLabel
        public string ink;           // dark | light
        public string key;           // string-table key
        public bool tracked;         // letter-spaced capitals: fit spacing, not width scale
        public float[] inkBox;       // left, top, right, bottom

        // V2's type (Tools/ArtPipeline/V2/screens.py): a label with a size is drawn at that size
        // (canvas units) and letter spacing (TextMeshPro character spacing), its ink placed on the
        // ink box by its alignment's edge, measured with "measure" (V2's text) where that differs
        // from the sample text. Without a size the label is fitted to the ink box.
        public float size;
        public float spacing;
        public string measure;
        public string color;
        public string colorTop;
        public string colorBottom;

        // Widgets
        public string on;
        public string off;
        public string[] options;
        public string note;

        public bool IsLabel => kind == "label";
        public bool IsGroup => kind == "group";
        public Vector2 Centre => rect != null && rect.Length == 4 ? new Vector2(rect[0] + rect[2] / 2f, rect[1] + rect[3] / 2f) : Vector2.zero;
    }
}
