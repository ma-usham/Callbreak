using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// Keeps two ornaments (the little rules or arrows either side of a centred caption such as
    /// "Round 2 begins next") at the same distance from the text when the text changes: they move
    /// apart or together by half the change in its width, and hide while there is no text. The
    /// view calls <see cref="Refresh"/> after setting the text.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LabelFlanks : MonoBehaviour
    {
        [SerializeField] private RectTransform _left;
        [SerializeField] private RectTransform _right;

        // The text's width and the ornaments' positions when the screen was built.
        [SerializeField] private float _designWidth;
        [SerializeField] private float _leftX;
        [SerializeField] private float _rightX;

        private TMP_Text _label;

        public void Refresh()
        {
            if (_label == null)
            {
                _label = GetComponent<TMP_Text>();
            }

            bool hasText = !string.IsNullOrEmpty(_label.text);
            _left.gameObject.SetActive(hasText);
            _right.gameObject.SetActive(hasText);
            if (!hasText)
            {
                return;
            }

            _label.ForceMeshUpdate();
            float spread = (Width(_label) - _designWidth) / 2f;
            _left.anchoredPosition = new Vector2(_leftX - spread, _left.anchoredPosition.y);
            _right.anchoredPosition = new Vector2(_rightX + spread, _right.anchoredPosition.y);
        }

        /// <summary>The width of the text as drawn (zero for an empty label).</summary>
        public static float Width(TMP_Text label)
        {
            return label.textInfo != null && label.textInfo.characterCount > 0 ? label.textBounds.size.x : 0f;
        }
    }
}
