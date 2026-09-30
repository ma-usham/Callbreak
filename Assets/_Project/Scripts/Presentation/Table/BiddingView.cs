using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// GDD §11 screen 07, "Make your call": the call panel in the middle of the table. It shows only
    /// on the local seat's call turn: chips from the rule set's lowest to highest call with nothing
    /// chosen, and a Confirm button that stays disabled until a chip is chosen. Before that turn the
    /// centre of the table reads "Asha is calling…". The table itself (HUD, seats, hand) is the
    /// <see cref="TableView"/> underneath.
    /// </summary>
    public sealed class BiddingView : UIScreen
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private BidChipRow _chips;
        [SerializeField] private UnityEngine.UI.Button _confirmButton;
        [SerializeField] private TMP_Text _confirmLabel;
        [SerializeField] private TMP_Text _callingText;

        /// <summary>A chip was tapped: the call it stands for.</summary>
        public event Action<int> CallChosen;

        /// <summary>Confirm was pressed; the chosen call is <see cref="SelectedCall"/>.</summary>
        public event Action ConfirmClicked;

        /// <summary>The chosen call, or 0.</summary>
        public int SelectedCall => _chips.Selected;

        public bool IsPanelShown => _panel.activeSelf;

        /// <summary>The chips to offer: RuleSet.MinCall to RuleSet.MaxCall.</summary>
        public void SetCallRange(int minCall, int maxCall)
        {
            _chips.SetRange(minCall, maxCall);
        }

        /// <summary>Shows the panel for the local seat's call turn, or hides it.</summary>
        public void ShowPanel(bool show)
        {
            if (_panel.activeSelf != show)
            {
                _panel.SetActive(show);
            }
        }

        /// <summary>The chosen chip (0 for none).</summary>
        public void SetSelected(int call)
        {
            _chips.Select(call);
        }

        /// <summary>"Confirm" or "Confirm 4", and whether it can be pressed.</summary>
        public void SetConfirm(string label, bool enabled)
        {
            label ??= string.Empty;
            if (_confirmLabel.text != label)
            {
                _confirmLabel.text = label;
            }

            if (_confirmButton.interactable != enabled)
            {
                _confirmButton.interactable = enabled;
            }
        }

        /// <summary>False once the call has been sent: the choice is final (GDD screen 07, callout 7).</summary>
        public void SetChipsEnabled(bool enabled)
        {
            _chips.SetInteractable(enabled);
        }

        /// <summary>"Asha is calling…" in the middle of the table; null or empty hides it.</summary>
        public void SetCalling(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (_callingText.gameObject.activeSelf != show)
            {
                _callingText.gameObject.SetActive(show);
            }

            if (show && _callingText.text != text)
            {
                _callingText.text = text;
            }
        }

        private void OnEnable()
        {
            _chips.Chosen += OnChosen;
            _confirmButton.onClick.AddListener(OnConfirm);
        }

        private void OnDisable()
        {
            _chips.Chosen -= OnChosen;
            _confirmButton.onClick.RemoveListener(OnConfirm);
        }

        private void OnChosen(int call) => CallChosen?.Invoke(call);

        private void OnConfirm() => ConfirmClicked?.Invoke();
    }
}
