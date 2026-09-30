using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// The row of call chips (GDD §11 screen 07, §12 "Chips are circles on ivory; the selected one
    /// is crimson with white text"): one chip per call from the rule set's minimum to its maximum, a
    /// single choice with nothing chosen at first. One crimson chip image moves onto whichever chip
    /// is chosen, and that chip's number turns white.
    /// </summary>
    public sealed class BidChipRow : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button[] _chips = Array.Empty<UnityEngine.UI.Button>();
        [SerializeField] private TMP_Text[] _labels = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform _selection;
        [SerializeField] private Color _labelColor = Color.black;
        [SerializeField] private Color _selectedLabelColor = Color.white;
        [SerializeField] private int _firstCall = 1;
        [SerializeField] private int _selected;

        private readonly List<UnityAction> _handlers = new List<UnityAction>(8);
        private int _lastCall;

        /// <summary>A chip was tapped: its call.</summary>
        public event Action<int> Chosen;

        /// <summary>The chosen call, or 0 when nothing is chosen.</summary>
        public int Selected => _selected;

        /// <summary>How many chips the row can show.</summary>
        public int Capacity => _chips.Length;

        /// <summary>
        /// Shows the chips for calls <paramref name="minCall"/> to <paramref name="maxCall"/>
        /// (RuleSet.MinCall and MaxCall), numbered from the left, and hides the rest.
        /// </summary>
        public void SetRange(int minCall, int maxCall)
        {
            _firstCall = minCall;
            _lastCall = Mathf.Min(maxCall, minCall + _chips.Length - 1);
            for (int i = 0; i < _chips.Length; i++)
            {
                int call = minCall + i;
                bool used = call <= _lastCall;
                if (_chips[i].gameObject.activeSelf != used)
                {
                    _chips[i].gameObject.SetActive(used);
                }

                if (_labels[i].gameObject.activeSelf != used)
                {
                    _labels[i].gameObject.SetActive(used);
                }

                if (used)
                {
                    _labels[i].SetText("{0}", call);
                }
            }

            if (_selected < minCall || _selected > _lastCall)
            {
                Select(0);
            }
        }

        /// <summary>Marks <paramref name="call"/> as chosen without raising <see cref="Chosen"/>; 0 clears the choice.</summary>
        public void Select(int call)
        {
            _selected = call;
            int index = call - _firstCall;
            bool any = call != 0 && index >= 0 && index < _chips.Length && _chips[index].gameObject.activeSelf;
            if (_selection.gameObject.activeSelf != any)
            {
                _selection.gameObject.SetActive(any);
            }

            if (any)
            {
                _selection.anchoredPosition = ((RectTransform)_chips[index].transform).anchoredPosition;
            }

            for (int i = 0; i < _labels.Length; i++)
            {
                bool chosen = any && i == index;
                TMP_Text label = _labels[i];
                if (label.enableVertexGradient)
                {
                    label.enableVertexGradient = false;
                }

                Color color = chosen ? _selectedLabelColor : _labelColor;
                if (label.color != color)
                {
                    label.color = color;
                }
            }
        }

        /// <summary>False once the call is confirmed (or while it is not the player's turn).</summary>
        public void SetInteractable(bool interactable)
        {
            for (int i = 0; i < _chips.Length; i++)
            {
                if (_chips[i].interactable != interactable)
                {
                    _chips[i].interactable = interactable;
                }
            }
        }

        private void OnEnable()
        {
            _handlers.Clear();
            for (int i = 0; i < _chips.Length; i++)
            {
                int index = i;
                UnityAction handler = () => OnChip(index);
                _handlers.Add(handler);
                _chips[i].onClick.AddListener(handler);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _chips.Length && i < _handlers.Count; i++)
            {
                _chips[i].onClick.RemoveListener(_handlers[i]);
            }

            _handlers.Clear();
        }

        private void OnChip(int index)
        {
            int call = _firstCall + index;
            Select(call);
            Chosen?.Invoke(call);
        }
    }
}
