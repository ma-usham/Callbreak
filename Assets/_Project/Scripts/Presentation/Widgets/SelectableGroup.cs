using System;
using Callbreak.Presentation.Framework;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// A single choice among several tappable options: segmented controls (15 sec | 30 sec),
    /// difficulty cards, deck cards with radio buttons. Each option has a button, a "selected"
    /// look that is shown only on the chosen one, an optional "unselected" look, and optional
    /// labels whose colour follows the selection. A tapped option's selected look fades in
    /// (ADR 023); one set by a presenter is simply there.
    /// </summary>
    public sealed class SelectableGroup : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button[] _options;
        [SerializeField] private GameObject[] _selectedLooks;
        [SerializeField] private GameObject[] _unselectedLooks;
        [SerializeField] private TMP_Text[] _labels;
        [SerializeField] private Color _selectedLabelColor = Color.white;
        [SerializeField] private Color _labelColor = new Color(0.14f, 0.11f, 0.1f);
        [SerializeField] private int _selectedIndex;

        private UnityEngine.Events.UnityAction[] _handlers;
        private MotionHandle _fade;

        /// <summary>Raised when the player chooses another option.</summary>
        public event Action<int> Changed;

        /// <summary>
        /// Raised on every tap of an option, the chosen one included, after the selection (and
        /// <see cref="Changed"/>) has followed it. For options that do something when tapped again.
        /// </summary>
        public event Action<int> Tapped;

        public int SelectedIndex => _selectedIndex;

        public int Count => _options != null ? _options.Length : 0;

        public void SetSelectedWithoutNotify(int index)
        {
            _selectedIndex = Mathf.Clamp(index, -1, Count - 1);
            Render();
        }

        private void OnEnable()
        {
            _handlers = new UnityEngine.Events.UnityAction[Count];
            for (int i = 0; i < Count; i++)
            {
                int captured = i;
                _handlers[i] = () => Select(captured);
                _options[i].onClick.AddListener(_handlers[i]);
            }

            Render();
        }

        private void OnDisable()
        {
            for (int i = 0; i < Count && _handlers != null; i++)
            {
                _options[i].onClick.RemoveListener(_handlers[i]);
            }

            UiMotion.Finish(ref _fade);
        }

        private void Select(int index)
        {
            if (index != _selectedIndex)
            {
                _selectedIndex = index;
                Render();
                FadeInSelected();
                Changed?.Invoke(index);
            }

            Tapped?.Invoke(index);
        }

        private void FadeInSelected()
        {
            if (_selectedLooks == null || _selectedIndex < 0 || _selectedIndex >= _selectedLooks.Length || _selectedLooks[_selectedIndex] == null)
            {
                return;
            }

            CanvasGroup look = UiMotion.GroupOf(_selectedLooks[_selectedIndex]);
            look.alpha = 0f;
            _fade = UiMotion.Fade(look, 1f, UiMotion.Quick);
        }

        private void Render()
        {
            // A look still fading in is finished first, so no option is left half-chosen.
            UiMotion.Finish(ref _fade);
            for (int i = 0; i < Count; i++)
            {
                bool on = i == _selectedIndex;
                if (_selectedLooks != null && i < _selectedLooks.Length && _selectedLooks[i] != null) _selectedLooks[i].SetActive(on);
                if (_unselectedLooks != null && i < _unselectedLooks.Length && _unselectedLooks[i] != null) _unselectedLooks[i].SetActive(!on);
                if (_labels != null && i < _labels.Length && _labels[i] != null) _labels[i].color = on ? _selectedLabelColor : _labelColor;
            }
        }
    }
}
