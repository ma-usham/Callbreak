using System;
using Callbreak.Presentation.Framework;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 10: the final standings and what comes next. The headline follows the local
    /// player's place ("You Win!", "Shared Victory!", "2nd Place"...); the trophy shows only for a
    /// win; every winner's row carries a crown (ties share first place); the gold band marks the
    /// local player's row. Rows are numbered 0-3 from the top.
    /// </summary>
    public sealed class MatchResultsView : UIScreen
    {
        public const int RowCount = 4;

        [SerializeField] private UnityEngine.UI.Button _homeButton;
        [SerializeField] private UnityEngine.UI.Button _scoresButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _playAgainButton;
        [SerializeField] private UnityEngine.UI.Button _mainMenuButton;
        [SerializeField] private UnityEngine.UI.Button _scorecardButton;
        [SerializeField] private TMP_Text _headline;
        [SerializeField] private GameObject _trophy;
        [SerializeField] private TMP_Text _footer;
        [SerializeField] private RectTransform[] _row;
        [SerializeField] private TMP_Text[] _rowRank;
        [SerializeField] private UnityEngine.UI.Image[] _rowAvatar;
        [SerializeField] private GameObject[] _rowCrown;
        [SerializeField] private TMP_Text[] _rowName;
        [SerializeField] private TMP_Text[] _rowTotal;
        [SerializeField] private GameObject[] _rowSeparator;
        [SerializeField] private RectTransform _highlightBand;

        public event Action HomeClicked;
        public event Action ScoresClicked;
        public event Action SettingsClicked;
        public event Action PlayAgainClicked;
        public event Action MainMenuClicked;
        public event Action ScorecardClicked;

        /// <summary>"You Win!", "Shared Victory!", "2nd Place"...</summary>
        public void SetHeadline(string headline) => ResultsTableRows.SetText(_headline, headline);

        /// <summary>The trophy with its laurels and sparkles: shown only when the local player won.</summary>
        public void SetTrophyVisible(bool visible) => _trophy.SetActive(visible);

        /// <summary>One standing: rank ("1"), name, total points ("21.3"), and the crown for a winner.</summary>
        public void SetRow(int row, string rank, string name, string total, bool isWinner)
        {
            ResultsTableRows.SetText(_rowRank[row], rank);
            ResultsTableRows.SetText(_rowName[row], name);
            ResultsTableRows.SetText(_rowTotal[row], total);
            _rowCrown[row].SetActive(isWinner);
        }

        /// <summary>The player's avatar picture on a row.</summary>
        public void SetRowAvatar(int row, Sprite avatar) => _rowAvatar[row].sprite = avatar;

        /// <summary>Puts the gold band on the local player's row; -1 hides it.</summary>
        public void SetHighlightRow(int row) => ResultsTableRows.Highlight(_highlightBand, _row, _rowSeparator, row);

        /// <summary>"5 rounds completed".</summary>
        public void SetFooter(string footer) => ResultsTableRows.SetText(_footer, footer);

        private void OnEnable()
        {
            _homeButton.onClick.AddListener(OnHome);
            _scoresButton.onClick.AddListener(OnScores);
            _settingsButton.onClick.AddListener(OnSettings);
            _playAgainButton.onClick.AddListener(OnPlayAgain);
            _mainMenuButton.onClick.AddListener(OnMainMenu);
            _scorecardButton.onClick.AddListener(OnScorecard);
        }

        private void OnDisable()
        {
            _homeButton.onClick.RemoveListener(OnHome);
            _scoresButton.onClick.RemoveListener(OnScores);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _playAgainButton.onClick.RemoveListener(OnPlayAgain);
            _mainMenuButton.onClick.RemoveListener(OnMainMenu);
            _scorecardButton.onClick.RemoveListener(OnScorecard);
        }

        private void OnHome() => HomeClicked?.Invoke();

        private void OnScores() => ScoresClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnPlayAgain() => PlayAgainClicked?.Invoke();

        private void OnMainMenu() => MainMenuClicked?.Invoke();

        private void OnScorecard() => ScorecardClicked?.Invoke();
    }
}
