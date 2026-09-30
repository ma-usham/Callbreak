using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The quick-phrase picker over the emote button (GDD §8 "Emotes, not chat"; screen 08 callout
    /// 10): the six phrases of the Emotes table. Tapping a phrase raises its index; tapping
    /// anywhere else closes the picker. While the rate limit holds, the phrases are disabled.
    /// </summary>
    public sealed class EmotePickerView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _dismiss;
        [SerializeField] private Button[] _buttons = Array.Empty<Button>();
        [SerializeField] private TMP_Text[] _labels = Array.Empty<TMP_Text>();

        private readonly List<UnityEngine.Events.UnityAction> _handlers = new List<UnityEngine.Events.UnityAction>(6);

        /// <summary>A phrase was tapped: its index in the Emotes table.</summary>
        public event Action<int> EmoteChosen;

        /// <summary>The player tapped outside the picker.</summary>
        public event Action Dismissed;

        public bool IsOpen => _panel.activeSelf;

        public int Count => _buttons.Length;

        public void SetPhrases(IReadOnlyList<string> phrases)
        {
            for (int i = 0; i < _labels.Length; i++)
            {
                string text = i < phrases.Count ? phrases[i] : string.Empty;
                if (_labels[i].text != text)
                {
                    _labels[i].text = text;
                }

                _buttons[i].gameObject.SetActive(text.Length > 0);
            }
        }

        public void Open()
        {
            if (!_panel.activeSelf)
            {
                _panel.SetActive(true);
            }
        }

        public void Close()
        {
            if (_panel.activeSelf)
            {
                _panel.SetActive(false);
            }
        }

        /// <summary>False while the emote limit holds (one every 3 s, ten per round).</summary>
        public void SetAvailable(bool available)
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i].interactable != available)
                {
                    _buttons[i].interactable = available;
                }
            }
        }

        private void OnEnable()
        {
            _handlers.Clear();
            for (int i = 0; i < _buttons.Length; i++)
            {
                int index = i;
                UnityEngine.Events.UnityAction handler = () => EmoteChosen?.Invoke(index);
                _handlers.Add(handler);
                _buttons[i].onClick.AddListener(handler);
            }

            if (_dismiss != null)
            {
                _dismiss.onClick.AddListener(OnDismiss);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _buttons.Length && i < _handlers.Count; i++)
            {
                _buttons[i].onClick.RemoveListener(_handlers[i]);
            }

            _handlers.Clear();
            if (_dismiss != null)
            {
                _dismiss.onClick.RemoveListener(OnDismiss);
            }
        }

        private void OnDismiss() => Dismissed?.Invoke();
    }
}
