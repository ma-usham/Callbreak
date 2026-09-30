using System;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// A single choice among several tappable options: segmented controls (15 sec | 30 sec),
    /// difficulty cards, deck cards with radio buttons. Each option has a button, a "selected"
    /// look that is shown only on the chosen one, an optional "unselected" look, and optional
    /// labels whose colour follows the selection.
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

        public event Action<int> Changed;

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
        }

        private void Select(int index)
        {
            if (index == _selectedIndex)
            {
                return;
            }

            _selectedIndex = index;
            Render();
            Changed?.Invoke(index);
        }

        private void Render()
        {
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
