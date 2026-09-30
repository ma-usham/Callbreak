using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// The line under the profile's name field (GDD §11 screen 14): normally the note "Your name is
    /// visible at the table." in its mockup colour, or a validation message in the error colour
    /// (Heading Burgundy from the GDD §12 palette; the mockup shows no error state).
    /// </summary>
    public sealed class ProfileNameMessage : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Color _noteColor = new Color(0.565f, 0.427f, 0.318f);
        [SerializeField] private Color _errorColor = new Color(0.478f, 0.063f, 0.086f);

        public void Show(string text, bool isError)
        {
            if (_label.text != text)
            {
                _label.text = text;
            }

            _label.color = isError ? _errorColor : _noteColor;
        }
    }
}
