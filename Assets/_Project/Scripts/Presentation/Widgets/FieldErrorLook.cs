using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// A text field's art in V2's error look (its "Field", State=Error: a crimson rim on a blush
    /// fill) while what was typed cannot be used, and back in its own look once it can.
    /// </summary>
    public sealed class FieldErrorLook : MonoBehaviour
    {
        [SerializeField] private Image _face;
        [SerializeField] private Sprite _errorSprite;

        private Sprite _normalSprite;
        private bool _normalKnown;

        /// <summary>The field shows its error look.</summary>
        public bool IsError { get; private set; }

        public void SetError(bool on)
        {
            if (_face == null || _errorSprite == null)
            {
                return;
            }

            if (!_normalKnown)
            {
                _normalSprite = _face.sprite;
                _normalKnown = true;
            }

            IsError = on;
            Sprite sprite = on ? _errorSprite : _normalSprite;
            if (_face.sprite != sprite)
            {
                _face.sprite = sprite;
            }
        }
    }
}
