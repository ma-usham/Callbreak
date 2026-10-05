using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 10: the final standings and what comes next. The headline follows the local
    /// player's place ("You Win!", "Shared Victory!", "2nd Place"...); the trophy shows only for a
    /// win; every winner's row carries a crown (ties share first place); the gold band marks the
    /// local player's row. After a Stake Match that paid this player, its row also shows the prize,
    /// the gold coin and "+200 coins", between the name and the points (the owner, 5 Oct 2026).
    /// Rows are numbered 0-3 from the top. Each time the page is shown its
    /// panel rises into place, and the first time the trophy is seen it drops in and sparkles
    /// (GDD §14 "Results" and "Victory"), unless <see cref="ReduceMotion"/> is set.
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
        [SerializeField] private TrophyEntrance _trophyEntrance;
        [SerializeField] private RiseIn _panelRise;
        [SerializeField] private TMP_Text _footer;
        [SerializeField] private LabelFlanks _footerFlanks;
        [SerializeField] private RectTransform[] _row;
        [SerializeField] private TMP_Text[] _rowRank;
        [SerializeField] private UnityEngine.UI.Image[] _rowAvatar;
        [SerializeField] private GameObject[] _rowCrown;
        [SerializeField] private TMP_Text[] _rowName;
        [SerializeField] private TMP_Text[] _rowTotal;
        [SerializeField] private GameObject[] _rowSeparator;

        // Each row's Stake Match prize, hidden unless it is the paid player's: the coin and its line.
        [SerializeField] private GameObject[] _rowPrize;
        [SerializeField] private TMP_Text[] _rowPrizeLabel;
        [SerializeField] private RectTransform _highlightBand;
        [SerializeField] private Color _totalColor = new Color32(0x19, 0x19, 0x14, 0xFF);
        [SerializeField] private Color _negativeColor = new Color32(0x82, 0x12, 0x16, 0xFF);

        // The trophy makes its entrance once: the first time it is seen on the page after being
        // switched on (the prefab has it on), not again when the player comes back to the page.
        private bool _trophyEnters = true;
        private bool _showing;

        public event Action HomeClicked;
        public event Action ScoresClicked;
        public event Action SettingsClicked;
        public event Action PlayAgainClicked;
        public event Action MainMenuClicked;
        public event Action ScorecardClicked;

        /// <summary>
        /// GDD §14 Reduce motion: the page only fades in; the panel does not rise and the trophy is
        /// simply there.
        /// </summary>
        public bool ReduceMotion { get; set; }

        /// <summary>"You Win!", "Shared Victory!", "2nd Place"...</summary>
        public void SetHeadline(string headline) => ResultsTableRows.SetText(_headline, headline);

        /// <summary>
        /// The trophy with its laurels and sparkles: shown only when the local player won. Switched
        /// on while the page is showing it drops in at once; switched on before, it drops in when
        /// the page is shown.
        /// </summary>
        public void SetTrophyVisible(bool visible)
        {
            if (_trophy.activeSelf == visible)
            {
                return;
            }

            _trophy.SetActive(visible);
            _trophyEnters = visible;
            if (visible && _showing)
            {
                EnterTrophy();
            }
        }

        /// <summary>The panel rises about 30 px into place (400 ms, OutCubic); a trophy not yet seen drops in.</summary>
        public override void OnShown()
        {
            _showing = true;
            if (!ReduceMotion && _panelRise != null)
            {
                _panelRise.Play();
            }

            if (_trophyEnters && _trophy.activeSelf)
            {
                EnterTrophy();
            }
        }

        public override void OnHidden() => _showing = false;

        private void EnterTrophy()
        {
            _trophyEnters = false;
            if (!ReduceMotion && _trophyEntrance != null)
            {
                _trophyEntrance.Play();
            }
        }

        /// <summary>
        /// One standing: rank ("1"; tied players share a rank), name, total points ("21.3") and the
        /// crown for a winner. A negative total ("−6.1") is drawn in burgundy like every negative score.
        /// </summary>
        public void SetRow(int row, string rank, string name, string total, bool isWinner, bool totalNegative = false)
        {
            ResultsTableRows.SetText(_rowRank[row], rank);
            ResultsTableRows.SetText(_rowName[row], name);
            ResultsTableRows.SetValue(_rowTotal[row], total, totalNegative, _totalColor, _negativeColor);
            _rowCrown[row].SetActive(isWinner);
        }

        /// <summary>The player's avatar picture on a row.</summary>
        public void SetRowAvatar(int row, Sprite avatar) => _rowAvatar[row].sprite = avatar;

        /// <summary>Puts the gold band on the local player's row; -1 hides it.</summary>
        public void SetHighlightRow(int row) => ResultsTableRows.Highlight(_highlightBand, _row, _rowSeparator, row);

        /// <summary>
        /// The coin and <paramref name="prize"/> ("+200 coins") on one row, the prize a Stake Match
        /// paid; every other row shows none. -1, or no text, hides it everywhere.
        /// </summary>
        public void SetStakePrize(int row, string prize)
        {
            if (_rowPrize == null || _rowPrizeLabel == null)
            {
                return;
            }

            for (int i = 0; i < _rowPrize.Length && i < _rowPrizeLabel.Length; i++)
            {
                bool show = i == row && !string.IsNullOrEmpty(prize);
                if (show)
                {
                    ResultsTableRows.SetText(_rowPrizeLabel[i], prize);
                }

                if (_rowPrize[i].activeSelf != show)
                {
                    _rowPrize[i].SetActive(show);
                }
            }
        }

        /// <summary>"5 rounds completed"; the rules either side follow the text.</summary>
        public void SetFooter(string footer)
        {
            ResultsTableRows.SetText(_footer, footer);
            _footerFlanks.Refresh();
        }

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
