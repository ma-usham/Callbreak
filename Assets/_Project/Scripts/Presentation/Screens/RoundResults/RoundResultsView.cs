using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 09: how the round went and the running totals. Four rows (avatar, name, bid,
    /// won, round delta, total); the gold band marks the local player's row; negative values are
    /// burgundy with a true minus sign (the presenter formats the text, the view only colours it).
    /// Rows are numbered 0-3 from the top. Each time the page is shown its panel rises into place
    /// (GDD §14 "Results"), unless <see cref="ReduceMotion"/> is set.
    /// </summary>
    public sealed class RoundResultsView : UIScreen
    {
        public const int RowCount = 4;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _nextButton;
        [SerializeField] private UnityEngine.UI.Button _scorecardButton;
        [SerializeField] private TMP_Text _roundTitle;
        [SerializeField] private TMP_Text _nextLabel;
        [SerializeField] private TMP_Text _footer;
        [SerializeField] private LabelFlanks _footerFlanks;
        [SerializeField] private RectTransform[] _row;
        [SerializeField] private UnityEngine.UI.Image[] _rowAvatar;
        [SerializeField] private TMP_Text[] _rowName;
        [SerializeField] private TMP_Text[] _rowBid;
        [SerializeField] private TMP_Text[] _rowWon;
        [SerializeField] private TMP_Text[] _rowDelta;
        [SerializeField] private TMP_Text[] _rowTotal;
        [SerializeField] private GameObject[] _rowSeparator;
        [SerializeField] private RectTransform _highlightBand;
        [SerializeField] private RiseIn _panelRise;
        [SerializeField] private Color _valueColor = new Color32(0x1B, 0x1B, 0x17, 0xFF);
        [SerializeField] private Color _negativeColor = new Color32(0x82, 0x12, 0x16, 0xFF);

        public event Action BackClicked;
        public event Action SettingsClicked;

        /// <summary>Next Round (online: "I'm ready"), or See Final Results after round 5.</summary>
        public event Action NextClicked;
        public event Action ScorecardClicked;

        /// <summary>GDD §14 Reduce motion: the page only fades in; its panel does not rise.</summary>
        public bool ReduceMotion { get; set; }

        /// <summary>"ROUND 1 OF 5".</summary>
        public void SetRoundTitle(string title) => ResultsTableRows.SetText(_roundTitle, title);

        /// <summary>One row of the table; values arrive formatted ("+4.1", "−3.0").</summary>
        public void SetRow(int row, string name, string bid, string won, string delta, string total, bool deltaNegative, bool totalNegative)
        {
            ResultsTableRows.SetText(_rowName[row], name);
            ResultsTableRows.SetValue(_rowBid[row], bid, false, _valueColor, _negativeColor);
            ResultsTableRows.SetValue(_rowWon[row], won, false, _valueColor, _negativeColor);
            ResultsTableRows.SetValue(_rowDelta[row], delta, deltaNegative, _valueColor, _negativeColor);
            ResultsTableRows.SetValue(_rowTotal[row], total, totalNegative, _valueColor, _negativeColor);
        }

        /// <summary>The player's avatar picture on a row.</summary>
        public void SetRowAvatar(int row, Sprite avatar) => _rowAvatar[row].sprite = avatar;

        /// <summary>Shows or hides a whole row (rows reveal top to bottom, GDD §11).</summary>
        public void SetRowVisible(int row, bool visible) => _row[row].gameObject.SetActive(visible);

        /// <summary>Puts the gold "You" band on a row; -1 hides it.</summary>
        public void SetHighlightRow(int row) => ResultsTableRows.Highlight(_highlightBand, _row, _rowSeparator, row);

        /// <summary>"Next Round", "Next Round (10)" or "See Final Results".</summary>
        public void SetNextLabel(string label) => ResultsTableRows.SetText(_nextLabel, label);

        public void SetNextInteractable(bool interactable) => _nextButton.interactable = interactable;

        /// <summary>"Round 2 begins next"; the arrows either side follow the text.</summary>
        public void SetFooter(string footer)
        {
            ResultsTableRows.SetText(_footer, footer);
            _footerFlanks.Refresh();
        }

        /// <summary>The panel, with everything on it, rises about 30 px into place (400 ms, OutCubic).</summary>
        public override void OnShown()
        {
            if (!ReduceMotion && _panelRise != null)
            {
                _panelRise.Play();
            }
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _settingsButton.onClick.AddListener(OnSettings);
            _nextButton.onClick.AddListener(OnNext);
            _scorecardButton.onClick.AddListener(OnScorecard);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _nextButton.onClick.RemoveListener(OnNext);
            _scorecardButton.onClick.RemoveListener(OnScorecard);
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnNext() => NextClicked?.Invoke();

        private void OnScorecard() => ScorecardClicked?.Invoke();
    }
}
