using System;
using System.Collections.Generic;
using Callbreak.Presentation.Framework;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The quick-phrase picker over the emote button (GDD §8 "Emotes, not chat"; screen 08 callout
    /// 10): the six phrases of the Emotes table. Tapping a phrase raises its index; tapping
    /// anywhere else closes the picker. While the rate limit holds, the phrases are disabled. The
    /// card pops out of its corner by the button and shrinks back into it as it fades (ADR 023);
    /// with Reduce motion it only fades.
    /// </summary>
    public sealed class EmotePickerView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _dismiss;
        [SerializeField] private Button[] _buttons = Array.Empty<Button>();
        [SerializeField] private TMP_Text[] _labels = Array.Empty<TMP_Text>();

        private readonly List<UnityEngine.Events.UnityAction> _handlers = new List<UnityEngine.Events.UnityAction>(6);
        private MotionHandle _motion;
        private CanvasGroup _group;
        private bool _closing;

        /// <summary>A phrase was tapped: its index in the Emotes table.</summary>
        public event Action<int> EmoteChosen;

        /// <summary>The player tapped outside the picker.</summary>
        public event Action Dismissed;

        /// <summary>Open, and taking taps; false as soon as it closes, while it fades out.</summary>
        public bool IsOpen => _panel.activeSelf && !_closing;

        public int Count => _buttons.Length;

        /// <summary>With Reduce motion the picker only fades in and out (GDD §14).</summary>
        public bool ReduceMotion { get; set; }

        private CanvasGroup Group => _group != null ? _group : _group = UiMotion.GroupOf(_panel);

        // The card with the phrases; it grows from its pivot, the corner by the emote button.
        private Transform Card => _buttons.Length > 0 && _buttons[0] != null ? _buttons[0].transform.parent : _panel.transform;

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
            if (IsOpen)
            {
                return;
            }

            // (A picker on its way out goes at once and comes back in.)
            CancelMotion();
            _panel.SetActive(true);
            _motion = UiMotion.PopIn(Card, Group, ReduceMotion, 0.9f, UiMotion.Short);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            CancelMotion();
            _closing = true;
            Group.blocksRaycasts = false;
            _motion = UiMotion.PopOut(Card, Group, ReduceMotion, OnClosed);
        }

        private void OnClosed()
        {
            _motion = default;
            Rest();
        }

        private void CancelMotion()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }

            _motion = default;
            Rest();
        }

        /// <summary>The card as it is at rest: full size, opaque and touchable; a picker that was closing is closed.</summary>
        private void Rest()
        {
            // When the scene unloads the panel may already be gone.
            if (_group != null)
            {
                _group.alpha = 1f;
                _group.blocksRaycasts = true;
            }

            if (_panel != null)
            {
                Card.localScale = Vector3.one;
                if (_closing && _panel.activeSelf)
                {
                    _panel.SetActive(false);
                }
            }

            _closing = false;
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

            CancelMotion();
        }

        private void OnDismiss() => Dismissed?.Invoke();
    }
}
