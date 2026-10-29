using System;
using Callbreak.Application.Economy;
using Callbreak.Presentation.Framework;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// GDD §11 screen 08, the table: the HUD (home, the round, scores and settings; no trump line,
    /// spades are always trump), the four seats, the trick area, the local hand, the emote button,
    /// the turn prompt and the emote picker. The hand keeps one order, so there is no sort button
    /// (ADR 021), and the emote button
    /// sits in the bottom-right corner where mockup 08 draws that button; it shows the wait before
    /// the next quick phrase (ADR 024). Every phrase, the player's own too, shows in the speech box
    /// above its speaker's avatar (ADR 027). Behind everything is the table the player equipped in
    /// the shop, filling the screen (V2, the owner, 28 Oct 2026). It raises
    /// one event per intent and hands out its parts; the presenters and the event playback decide
    /// what they show. Seats are indexed by relative position: 0 bottom (you), 1 right, 2 top, 3 left.
    /// </summary>
    public sealed class TableView : UIScreen
    {
        public const int SeatCount = 4;

        [SerializeField] private UnityEngine.UI.Button _homeButton;
        [SerializeField] private UnityEngine.UI.Button _scoresButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _emoteButton;
        [SerializeField] private UnityEngine.UI.Button _tableTap;

        // The trick area's own catcher, from when a tap there showed the previous trick (GDD screen
        // 08). The owner removed that peek (5 Oct 2026, ADR 038): a tap there is a tap on the table.
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
        [SerializeField] private Widgets.Spinner _reconnectingRing;

        // While the local seat is Away, the whole table is the "I'm back" button: a transparent
        // catcher over everything, so the touch is neither played as a card nor taken by the hand.
        [SerializeField] private UnityEngine.UI.Button _imBackButton;

        // The emote button shows the wait before the next quick phrase (ADR 024).
        [SerializeField] private UnityEngine.UI.Image _emoteCooldown;

        // The GDD asks for a small "Trick 6 / 13" under the round line; mockup 08 does not draw it.
        [SerializeField] private bool _showTrickCounter;

        // Behind everything else, in the backdrop frame (the screen plus margins for 21:9 and 4:3):
        // the equipped table (the owner, 4 Oct 2026; full screen in V2, 28 Oct 2026), from the art of
        // every table this table can show.
        [SerializeField] private TableBackdropLibrary _backdrops;
        [SerializeField] private UnityEngine.UI.Image _backdropTable;

        // The cloth's border, tiled round the screen's edges over the cloth (ADR 058); off for a cloth without one.
        [SerializeField] private UnityEngine.UI.Image _clothFrame;

        // The round line stands on the left under the CALLBREAK logo, on a status pill as wide as its
        // words and this much more (the owner, 29 Oct 2026, ADR 057).
        [SerializeField] private RectTransform _roundPill;
        [SerializeField] private float _roundPillPadding = 30f;

        // The banner ad at the top of the table (the owner, 14 and 26 Oct 2026, ADR 051; ADR 057).
        // While a banner may come, the table makes room for it: the top seat and the trick come down
        // a little (canvas units), so the banner covers nothing of the game. The slot is an empty
        // rectangle across the top of the HUD, from right of the logo and the round line to the right
        // edge, that the ad network's own view is placed over; it is off while the table makes no room.
        // The banner keeps the gap from the round line's pill, however long its words are, and sits in
        // the slot's top-right corner, where no button is (Scores and Settings stand under Home).
        [SerializeField] private RectTransform _bannerSlot;
        [SerializeField] private float _bannerSeatDrop = 52f;
        [SerializeField] private float _bannerTrickDrop = 25f;
        [SerializeField] private float _bannerGap = 48f;

        private readonly Vector3[] _bannerCorners = new Vector3[4];
        private readonly Vector3[] _pillCorners = new Vector3[4];
        private bool _bannerRoom;
        private Vector2 _topSeatHome;
        private Vector2 _trickHome;
        private MotionHandle _coverMotion;
        private bool _reduceMotion;

        /// <summary>Home: opens the match menu; it never leaves the table at once (GDD screen 07, callout 2).</summary>
        public event Action HomeClicked;

        public event Action ScoresClicked;

        public event Action SettingsClicked;

        public event Action EmoteClicked;

        /// <summary>
        /// A tap on the table itself, beside every control, the trick area included (it closes the
        /// emote panel; it skips no animation and shows no earlier trick).
        /// </summary>
        public event Action TableTapped;

        /// <summary>The table was touched while the local seat is Away: the player is back (GDD §8, in place of the "I'm back" banner).</summary>
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

        /// <summary>The art of every table (and background) this table can show; null on a prefab built before it.</summary>
        public TableBackdropLibrary Backdrops => _backdrops;

        /// <summary>The table, filling the backdrop frame behind everything else.</summary>
        public UnityEngine.UI.Image BackdropTable => _backdropTable;

        /// <summary>The cloth's border round the screen's edges, over the table; disabled when the cloth has none.</summary>
        public UnityEngine.UI.Image ClothFrame => _clothFrame;

        /// <summary>
        /// GDD §14 Reduce motion, for the table's own parts: the turn prompt, the emote picker and
        /// the seat chips only fade, the "Reconnecting…" ring stands still, and the opponents' cards
        /// jump to their places in the fans instead of gliding (the hand has its own switch).
        /// </summary>
        public bool ReduceMotion
        {
            get => _reduceMotion;
            set
            {
                _reduceMotion = value;
                _prompt.ReduceMotion = value;
                _emotes.ReduceMotion = value;
                if (_reconnectingRing != null)
                {
                    _reconnectingRing.Still = value;
                }

                foreach (SeatView seat in _seat)
                {
                    seat.ReduceMotion = value;
                    if (seat.Fan != null)
                    {
                        seat.Fan.ReduceMotion = value;
                    }
                }
            }
        }

        /// <summary>How much of the emote button the wait still fills, 0 to 1 (0: a phrase may be sent).</summary>
        public float EmoteCooldown => _emoteCooldown != null && _emoteCooldown.enabled ? _emoteCooldown.fillAmount : 0f;

        /// <summary>The "Reconnecting…" cover is over the table.</summary>
        public bool IsReconnecting => _reconnecting.activeSelf;

        /// <summary>
        /// The equipped table, over the whole frame (a null keeps the picture it has), and its border
        /// round the screen's edges (a null hides the border: the cloth has none).
        /// </summary>
        public void SetTable(Sprite table, Sprite frame)
        {
            if (table != null && _backdropTable != null)
            {
                _backdropTable.sprite = table;
                _backdropTable.enabled = true;
            }

            if (_clothFrame != null)
            {
                _clothFrame.sprite = frame;
                _clothFrame.enabled = frame != null;
            }
        }

        /// <summary>"ROUND 1 / 5".</summary>
        public void SetRound(string text)
        {
            text ??= string.Empty;
            if (_roundLabel.text != text)
            {
                _roundLabel.text = text;
                FitRoundPill();
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
        /// underneath and takes no input, and the loading screen's ring turns round its spade above
        /// the words. It fades in, and goes at once when the table is back.
        /// </summary>
        public void SetReconnecting(bool reconnecting)
        {
            if (_reconnecting.activeSelf == reconnecting)
            {
                return;
            }

            UiMotion.Stop(ref _coverMotion);
            CanvasGroup cover = UiMotion.GroupOf(_reconnecting);
            cover.alpha = 1f;
            _reconnecting.SetActive(reconnecting);
            if (reconnecting)
            {
                cover.alpha = 0f;
                _coverMotion = UiMotion.Fade(cover, 1f);
            }
        }

        /// <summary>
        /// The local seat is marked Away after two timeouts and a bot plays its turns (GDD §8): until
        /// the player is back, any touch on the table says so (the owner, 3 Oct 2026, in place of
        /// the "I'm back" banner). The catcher takes that touch alone, and goes once the seat is back.
        /// </summary>
        public void SetAway(bool away)
        {
            GameObject catcher = _imBackButton.gameObject;
            if (catcher.activeSelf != away)
            {
                catcher.SetActive(away);
            }
        }

        /// <summary>Whether the table is waiting for a touch to bring the Away player back.</summary>
        public bool IsAway => _imBackButton.gameObject.activeSelf;

        /// <summary>The table has made room at its top for the banner ad (<see cref="SetBannerRoom"/>).</summary>
        public bool HasBannerRoom => _bannerRoom;

        /// <summary>
        /// Makes room at the top of the table for the banner ad, or gives it back (ADR 051, 057): the
        /// slot, the top seat and the trick as the fields above describe; the logo and the round line
        /// stay where they are. Called when the table opens, before anything is dealt. A prefab built
        /// before the slot keeps its look.
        /// </summary>
        public void SetBannerRoom(bool room)
        {
            if (_bannerSlot == null || room == _bannerRoom)
            {
                return;
            }

            _bannerRoom = room;
            _bannerSlot.gameObject.SetActive(room);

            // Seat 2 is the top seat (relative positions: 0 bottom, 1 right, 2 top, 3 left).
            var topSeat = (RectTransform)_seat[2].transform;
            var trick = (RectTransform)_trick.transform;
            if (room)
            {
                _topSeatHome = topSeat.anchoredPosition;
                _trickHome = trick.anchoredPosition;
                topSeat.anchoredPosition = _topSeatHome + (Vector2.down * _bannerSeatDrop);
                trick.anchoredPosition = _trickHome + (Vector2.down * _bannerTrickDrop);
            }
            else
            {
                topSeat.anchoredPosition = _topSeatHome;
                trick.anchoredPosition = _trickHome;
            }
        }

        /// <summary>
        /// Where the banner ad may show, at the top of the table: the banner slot's rectangle in screen
        /// pixels from the screen's top-left corner, rounded inwards, starting the gap past the round
        /// line's words, with its right edge as the line to centre the banner on, which puts the banner in
        /// the top-right corner, away from every button (the owner, 28 Oct 2026, ADR 057). False when the
        /// table has no slot (a prefab built before it), or makes no room for a banner, or the slot is
        /// empty.
        /// </summary>
        public bool TryGetBannerSlot(out BannerSlot slot)
        {
            slot = default;
            if (_bannerSlot == null || !_bannerSlot.gameObject.activeInHierarchy)
            {
                return false;
            }

            // The table's canvases are screen-space overlays: world space is screen pixels, from the
            // bottom-left corner.
            _bannerSlot.GetWorldCorners(_bannerCorners);
            int x = Mathf.CeilToInt(_bannerCorners[0].x);
            int y = Mathf.CeilToInt(Screen.height - _bannerCorners[2].y);
            int right = Mathf.FloorToInt(_bannerCorners[2].x);

            // "ROUND 10 / 10", or longer words in another language, reach further right than the slot
            // was built for: the slot starts the gap past their pill.
            if (_roundPill != null && _roundPill.gameObject.activeInHierarchy)
            {
                _roundPill.GetWorldCorners(_pillCorners);
                x = Mathf.Max(x, Mathf.CeilToInt(_pillCorners[2].x + (_bannerGap * _roundPill.lossyScale.x)));
            }

            int width = right - x;
            int height = Mathf.FloorToInt(Screen.height - _bannerCorners[0].y) - y;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            slot = new BannerSlot(x, y, width, height, right);
            return true;
        }

        /// <summary>The round line's pill keeps the width of its words (ADR 057).</summary>
        private void FitRoundPill()
        {
            if (_roundPill == null)
            {
                return;
            }

            float width = _roundLabel.GetPreferredValues(_roundLabel.text).x + _roundPillPadding;
            _roundPill.sizeDelta = new Vector2(width, _roundPill.sizeDelta.y);
        }

        /// <summary>
        /// The wait before the next quick phrase (ADR 024): the button is covered by the wait's
        /// colour, which sweeps away clockwise as <paramref name="fraction"/> falls from 1 to 0.
        /// </summary>
        public void SetEmoteCooldown(float fraction)
        {
            // (A Table prefab built before the cooldown existed has nothing to show it with.)
            if (_emoteCooldown == null)
            {
                return;
            }

            bool waiting = fraction > 0f;
            if (_emoteCooldown.enabled != waiting)
            {
                _emoteCooldown.enabled = waiting;
            }

            if (waiting && _emoteCooldown.fillAmount != fraction)
            {
                _emoteCooldown.fillAmount = fraction;
            }
        }

        private void OnEnable()
        {
            _homeButton.onClick.AddListener(OnHome);
            _scoresButton.onClick.AddListener(OnScores);
            _settingsButton.onClick.AddListener(OnSettings);
            _emoteButton.onClick.AddListener(OnEmote);
            _tableTap.onClick.AddListener(OnTableTap);
            _trickTap.onClick.AddListener(OnTableTap);
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
            _trickTap.onClick.RemoveListener(OnTableTap);
            _imBackButton.onClick.RemoveListener(OnImBack);
            _hand.Touched -= OnTableTap;
            UiMotion.Finish(ref _coverMotion);
        }

        private void OnHome() => HomeClicked?.Invoke();

        private void OnScores() => ScoresClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnEmote() => EmoteClicked?.Invoke();

        private void OnTableTap() => TableTapped?.Invoke();

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
