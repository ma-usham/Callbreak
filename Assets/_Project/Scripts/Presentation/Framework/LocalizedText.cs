using Callbreak.Application.Ports;
using TMPro;
using UnityEngine;
using VContainer;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// A static label bound to a string-table key. The prefab keeps the English text it was
    /// fitted with, so it renders correctly before anything is injected (and in editor captures);
    /// once <see cref="ILocalization"/> is injected it follows the active language live.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _key;

        private ILocalization _localization;
        private TMP_Text _text;

        public string Key => _key;

        [Inject]
        public void Construct(ILocalization localization)
        {
            _localization = localization;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            if (_localization != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            if (_localization != null)
            {
                _localization.LanguageChanged -= Refresh;
            }
        }

        private void Subscribe()
        {
            _localization.LanguageChanged -= Refresh;
            _localization.LanguageChanged += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            if (string.IsNullOrEmpty(_key))
            {
                return;
            }

            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }

            string value = _localization.Get(_key);
            if (value != _key && _text.text != value)
            {
                _text.text = value;
            }
        }
    }
}
