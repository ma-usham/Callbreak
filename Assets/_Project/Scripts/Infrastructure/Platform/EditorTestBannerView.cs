#if UNITY_EDITOR
using UnityEngine;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// The Editor's test banner on the screen (<see cref="EditorTestBannerAdNetwork"/>, ADR 071): a white
    /// card like AdMob's test banner, with its dark "Test Ad" tag in the top-left corner and its size in the
    /// middle, drawn over everything as AdMob's own view is (immediate-mode GUI comes after every canvas).
    /// Places are in screen pixels from the top-left corner, as the immediate-mode GUI's are.
    /// </summary>
    public sealed class EditorTestBannerView : MonoBehaviour
    {
        private static readonly Color Paper = Color.white;
        private static readonly Color Ink = new Color(0.2f, 0.2f, 0.2f, 1f);
        private static readonly Color Edge = new Color(0.75f, 0.75f, 0.75f, 1f);

        private Rect _place;
        private bool _onScreen;
        private GUIStyle _words;
        private GUIStyle _tag;

        /// <summary>The banner is drawn on the screen now.</summary>
        public bool IsOnScreen => _onScreen;

        /// <summary>Where it is drawn: its top-left corner and size, in screen pixels.</summary>
        public Rect Place => _place;

        public void Show(Rect place)
        {
            _place = place;
            _onScreen = true;
        }

        public void Hide()
        {
            _onScreen = false;
        }

        private void OnGUI()
        {
            if (!_onScreen || Event.current.type != EventType.Repaint)
            {
                return;
            }

            GUI.depth = -1000;
            float line = Mathf.Max(1f, Mathf.Round(_place.height / 50f));
            Fill(_place, Edge);
            Fill(new Rect(_place.x + line, _place.y + line, _place.width - (2f * line), _place.height - (2f * line)), Paper);

            _words ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _words.normal.textColor = Ink;
            _words.fontSize = Mathf.Max(8, Mathf.RoundToInt(_place.height * 0.26f));
            GUI.Label(_place, "Editor test banner · 320 × 50", _words);

            _tag ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _tag.normal.textColor = Paper;
            _tag.fontSize = Mathf.Max(6, Mathf.RoundToInt(_place.height * 0.17f));
            Vector2 tagSize = _tag.CalcSize(new GUIContent("Test Ad"));
            var tag = new Rect(_place.x + line, _place.y + line, tagSize.x + (_place.height * 0.1f), tagSize.y);
            Fill(tag, Ink);
            GUI.Label(tag, "Test Ad", _tag);
        }

        private static void Fill(Rect rect, Color colour)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, colour, 0f, 0f);
        }
    }
}
#endif
