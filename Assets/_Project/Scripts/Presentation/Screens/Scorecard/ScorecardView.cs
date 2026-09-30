using System;
using Callbreak.Presentation.Framework;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 16: every round's score at a glance, as a read-only overlay during a match
    /// and after it. Four rows sorted by rank (avatar, name, five round scores, total, rank); the
    /// gold band marks the local player's row; negative values are burgundy; unplayed rounds show
    /// a dash (the presenter supplies the text). Rows are numbered 0-3 from the top.
    /// </summary>
    public sealed class ScorecardView : UIScreen
    {
        public const int RowCount = 4;
        public const int RoundCount = 5;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _primaryButton;
        [SerializeField] private TMP_Text _primaryLabel;
        [SerializeField] private TMP_Text _footer;
        [SerializeField] private RectTransform[] _row;
        [SerializeField] private UnityEngine.UI.Image[] _rowAvatar;
        [SerializeField] private TMP_Text[] _rowName;

        // Row-major: the label of row r, round i (0-4) is _rowRound[r * RoundCount + i].
        [SerializeField] private TMP_Text[] _rowRound;
        [SerializeField] private TMP_Text[] _rowTotal;
        [SerializeField] private TMP_Text[] _rowRank;
        [SerializeField] private GameObject[] _rowSeparator;
        [SerializeField] private RectTransform _highlightBand;
        [SerializeField] private Color _valueColor = new Color32(0x1E, 0x1E, 0x1C, 0xFF);
        [SerializeField] private Color _negativeColor = new Color32(0x82, 0x12, 0x16, 0xFF);

        public event Action BackClicked;
        public event Action SettingsClicked;

        /// <summary>The crimson button: "Back to Results" after a match, "Back to Game" during one.</summary>
        public event Action PrimaryClicked;

        /// <summary>
        /// One player's line: five round scores ("4.1", "−3.0", or "–" for a round not played yet)
        /// with a flag per negative score, the total, and the rank ("1").
        /// </summary>
        public void SetRow(int row, string name, string[] rounds, bool[] roundNegative, string total, bool totalNegative, string rank)
        {
            ResultsTableRows.SetText(_rowName[row], name);
            for (int i = 0; i < RoundCount; i++)
            {
                string text = rounds != null && i < rounds.Length ? rounds[i] : string.Empty;
                bool negative = roundNegative != null && i < roundNegative.Length && roundNegative[i];
                ResultsTableRows.SetValue(_rowRound[row * RoundCount + i], text, negative, _valueColor, _negativeColor);
            }

            ResultsTableRows.SetValue(_rowTotal[row], total, totalNegative, _valueColor, _negativeColor);
            ResultsTableRows.SetText(_rowRank[row], rank);
        }

        /// <summary>The player's avatar picture on a row.</summary>
        public void SetRowAvatar(int row, Sprite avatar) => _rowAvatar[row].sprite = avatar;

        /// <summary>Puts the gold band on the local player's row; -1 hides it.</summary>
        public void SetHighlightRow(int row) => ResultsTableRows.Highlight(_highlightBand, _row, _rowSeparator, row);

        /// <summary>"Final standings · 5 rounds completed".</summary>
        public void SetFooter(string footer) => ResultsTableRows.SetText(_footer, footer);

        /// <summary>"Back to Results" or "Back to Game".</summary>
        public void SetPrimaryLabel(string label) => ResultsTableRows.SetText(_primaryLabel, label);

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _settingsButton.onClick.AddListener(OnSettings);
            _primaryButton.onClick.AddListener(OnPrimary);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _primaryButton.onClick.RemoveListener(OnPrimary);
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnPrimary() => PrimaryClicked?.Invoke();
    }
}
