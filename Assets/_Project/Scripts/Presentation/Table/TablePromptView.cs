using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The turn prompt above the local hand (GDD §11 T rules "the prompt pill above your hand on
    /// your turn"; screen 08 callout 6): "Your turn · Beat Q♥", "Waiting for Asha…". The mockups do
    /// not draw it, so it borrows the status pill's look. An illegal tap makes it flash: the prompt
    /// is the reason the card cannot be played.
    /// </summary>
    public sealed class TablePromptView : MonoBehaviour
    {
        [SerializeField] private RectTransform _pill;
        [SerializeField] private Image _pillImage;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private float _padding = 64f;
        [SerializeField] private float _minWidth = 200f;
        [SerializeField] private Color _pillColor = Color.white;
        [SerializeField] private Color _refuseColor = new Color(1f, 0.62f, 0.58f, 1f);

        private MotionHandle _motion;

        public bool IsShowing => _pill.gameObject.activeSelf;

        public string Text => _label.text;

        /// <summary>Shows <paramref name="text"/>; the pill hugs it.</summary>
        public void Show(string text)
        {
            text ??= string.Empty;
            if (_label.text != text)
            {
                _label.text = text;
                float width = Mathf.Max(_minWidth, Mathf.Ceil(_label.GetPreferredValues(text).x) + _padding);
                _pill.sizeDelta = new Vector2(width, _pill.sizeDelta.y);
            }

            if (!_pill.gameObject.activeSelf)
            {
                _pill.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            CancelMotion();
            if (_pill.gameObject.activeSelf)
            {
                _pill.gameObject.SetActive(false);
            }
        }

        /// <summary>An illegal tap: the pill flashes red and gives a small kick (the flash alone with Reduce motion).</summary>
        public void Refuse(bool reduceMotion)
        {
            if (!IsShowing)
            {
                return;
            }

            CancelMotion();
            _motion = LMotion.Create(0f, 1f, 0.24f)
                .WithOnComplete(Settle)
                .Bind(this, reduceMotion ? (System.Action<float, TablePromptView>)Flash : FlashAndKick);
        }

        private static void Flash(float t, TablePromptView view)
        {
            view._pillImage.color = Color.Lerp(view._refuseColor, view._pillColor, t);
        }

        private static void FlashAndKick(float t, TablePromptView view)
        {
            Flash(t, view);
            float scale = 1f + 0.08f * Mathf.Sin(t * Mathf.PI);
            view._pill.localScale = new Vector3(scale, scale, 1f);
        }

        private void Settle()
        {
            _motion = default;
            _pillImage.color = _pillColor;
            _pill.localScale = Vector3.one;
        }

        private void CancelMotion()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }

            _motion = default;

            // When the scene unloads the pill may already be gone.
            if (_pillImage != null)
            {
                _pillImage.color = _pillColor;
            }

            if (_pill != null)
            {
                _pill.localScale = Vector3.one;
            }
        }

        private void OnDisable()
        {
            CancelMotion();
        }
    }
}
