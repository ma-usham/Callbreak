using System;
using Callbreak.Presentation.Framework;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// GDD §11 screen 08, the table: the HUD (home, round and trump, scores and settings), the four
    /// seats, the trick area, the local hand, the emote button, the turn prompt and the emote
    /// picker. The hand keeps one order, so there is no sort button (ADR 021), and the emote button
    /// sits in the bottom-right corner where mockup 08 draws that button. It raises one event per
    /// intent and hands out its parts; the presenters and the event playback decide what they
    /// show. Seats are indexed by relative position: 0 bottom (you), 1 right, 2 top, 3 left.
    /// </summary>
    public sealed class TableView : UIScreen
    {
        public const int SeatCount = 4;

        [SerializeField] private UnityEngine.UI.Button _homeButton;
        [SerializeField] private UnityEngine.UI.Button _scoresButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _emoteButton;
        [SerializeField] private UnityEngine.UI.Button _tableTap;
        [SerializeField] private UnityEngine.UI.Button _trickTap;
        [SerializeField] private TMP_Text _roundLabel;
        [SerializeField] private TMP_Text _trickLabel;
        [SerializeField] private SeatView[] _seat;
        [SerializeField] private HandView _hand;
        [SerializeField] private TrickAreaView _trick;
        [SerializeField] private CardViewPool _cards;
        [SerializeField] private TablePromptView _prompt;
        [SerializeField] private EmotePickerView _emotes;
        [SerializeField] private GameObject _reconnecting;
        [SerializeField] private UnityEngine.UI.Button _imBackButton;

        // The GDD asks for a small "Trick 6 / 13" under the round line; mockup 08 does not draw it.
        [SerializeField] private bool _showTrickCounter;

        /// <summary>Home: opens the match menu; it never leaves the table at once (GDD screen 07, callout 2).</summary>
        public event Action HomeClicked;

        public event Action ScoresClicked;

        public event Action SettingsClicked;

        public event Action EmoteClicked;

        /// <summary>A tap on the table itself, beside every control: skips a long animation.</summary>
        public event Action TableTapped;

        /// <summary>A tap on the trick area: skips an animation, or peeks at the previous trick (GDD screen 08).</summary>
        public event Action TrickTapped;

        /// <summary>The "I'm back" banner was tapped (GDD §8: an Away player takes the seat back).</summary>
        public event Action ImBackClicked;

        /// <summary>The app went to the background (GDD §8: an offline match pauses itself).</summary>
        public event Action AppPaused;

        public SeatView Seat(int relativeSeat) => _seat[relativeSeat];

        public HandView Hand => _hand;

        public TrickAreaView Trick => _trick;

        public CardViewPool Cards => _cards;

        public TablePromptView Prompt => _prompt;

        public EmotePickerView Emotes => _emotes;

        /// <summary>Whether the HUD shows "Trick 6 / 13" under the round line (off: mockup 08 has no such line).</summary>
        public bool ShowTrickCounter => _showTrickCounter;

        /// <summary>"ROUND 1 / 5".</summary>
        public void SetRound(string text)
        {
            text ??= string.Empty;
            if (_roundLabel.text != text)
            {
                _roundLabel.text = text;
            }
        }

        /// <summary>The small "Trick 6 / 13" under the round line (GDD screen 08, callout 1); empty hides it.</summary>
        public void SetTrick(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (_trickLabel.gameObject.activeSelf != show)
            {
                _trickLabel.gameObject.SetActive(show);
            }

            if (show && _trickLabel.text != text)
            {
                _trickLabel.text = text;
            }
        }

        /// <summary>
        /// The "Reconnecting…" cover while the session is re-joining: the table stays visible
        /// underneath and takes no input.
        /// </summary>
        public void SetReconnecting(bool reconnecting)
        {
            if (_reconnecting.activeSelf != reconnecting)
            {
                _reconnecting.SetActive(reconnecting);
            }
        }

        /// <summary>
        /// The "I'm back" banner of a seat marked Away after two timeouts: a bot plays its turns
        /// until the player taps it (GDD §8).
        /// </summary>
        public void SetAway(bool away)
        {
            GameObject banner = _imBackButton.gameObject;
            if (banner.activeSelf != away)
            {
                banner.SetActive(away);
            }
        }

        private void OnEnable()
        {
            _homeButton.onClick.AddListener(OnHome);
            _scoresButton.onClick.AddListener(OnScores);
            _settingsButton.onClick.AddListener(OnSettings);
            _emoteButton.onClick.AddListener(OnEmote);
            _tableTap.onClick.AddListener(OnTableTap);
            _trickTap.onClick.AddListener(OnTrickTap);
            _imBackButton.onClick.AddListener(OnImBack);
            _hand.Touched += OnTableTap;
        }

        private void OnDisable()
        {
            _homeButton.onClick.RemoveListener(OnHome);
            _scoresButton.onClick.RemoveListener(OnScores);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _emoteButton.onClick.RemoveListener(OnEmote);
            _tableTap.onClick.RemoveListener(OnTableTap);
            _trickTap.onClick.RemoveListener(OnTrickTap);
            _imBackButton.onClick.RemoveListener(OnImBack);
            _hand.Touched -= OnTableTap;
        }

        private void OnHome() => HomeClicked?.Invoke();

        private void OnScores() => ScoresClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnEmote() => EmoteClicked?.Invoke();

        private void OnTableTap() => TableTapped?.Invoke();

        private void OnTrickTap() => TrickTapped?.Invoke();

        private void OnImBack() => ImBackClicked?.Invoke();

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                AppPaused?.Invoke();
            }
        }
    }
}
