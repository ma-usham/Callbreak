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
    /// above its speaker's avatar (ADR 027). Behind everything is the backdrop: the background's
    /// scenery, the table and the scenery's foreground the player equipped in the shop. It raises
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

        // Behind everything else, in BG_Table's frame (the owner, 4 Oct 2026): the background's
        // scenery, the table, and the scenery's foreground, from the art of every background and
        // table this table can show.
        [SerializeField] private TableBackdropLibrary _backdrops;
        [SerializeField] private UnityEngine.UI.Image _backdropBack;
        [SerializeField] private UnityEngine.UI.Image _backdropTable;
        [SerializeField] private UnityEngine.UI.Image _backdropFront;

        // Where the banner ad may show, left of the Scores button (the owner, 14 Oct 2026): an empty
        // rectangle in the HUD that the ad network's own view is placed over.
        [SerializeField] private RectTransform _bannerSlot;

        private readonly Vector3[] _bannerCorners = new Vector3[4];
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

        /// <summary>The art of every background and table this table can show; null on a prefab built before it.</summary>
        public TableBackdropLibrary Backdrops => _backdrops;

        /// <summary>The background's scenery, at the back of everything.</summary>
        public UnityEngine.UI.Image BackdropBack => _backdropBack;

        /// <summary>The table, on the scenery.</summary>
        public UnityEngine.UI.Image BackdropTable => _backdropTable;

        /// <summary>The scenery's foreground, before the table's near edge and behind everything else.</summary>
        public UnityEngine.UI.Image BackdropFront => _backdropFront;

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
        /// The backdrop: the background's scenery, the table on it and the scenery's foreground, each
        /// at its place in the frame (BG_Table's, which the three images' parent spans). A layer
        /// without a picture shows nothing.
        /// </summary>
        public void SetBackdrop(TableBackdrop backdrop)
        {
            ShowLayer(_backdropBack, backdrop.Back, backdrop.Frame);
            ShowLayer(_backdropTable, backdrop.Table, backdrop.Frame);
            ShowLayer(_backdropFront, backdrop.Front, backdrop.Frame);
        }

        /// <summary>
        /// Puts a layer's picture on an image that lies in a frame of <paramref name="frame"/> pixels
        /// spanned by its parent: over the part of it the layer covers (all of it when the layer's area
        /// is empty). Without a picture the image is off.
        /// </summary>
        public static void ShowLayer(UnityEngine.UI.Image image, BackdropLayer layer, Vector2 frame)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = layer.Sprite;
            image.enabled = layer.Sprite != null;
            RectTransform rect = image.rectTransform;
            Rect area = layer.Area;
            if (area.width > 0f && area.height > 0f && frame.x > 0f && frame.y > 0f)
            {
                rect.anchorMin = new Vector2(area.xMin / frame.x, 1f - area.yMax / frame.y);
                rect.anchorMax = new Vector2(area.xMax / frame.x, 1f - area.yMin / frame.y);
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
            }

            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

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

        /// <summary>
        /// Where the banner ad may show, left of the Scores button: the banner slot's rectangle in
        /// screen pixels from the screen's top-left corner, rounded inwards. False when the table has
        /// no slot (a prefab built before it), or it is hidden or empty.
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
            int width = Mathf.FloorToInt(_bannerCorners[2].x) - x;
            int height = Mathf.FloorToInt(Screen.height - _bannerCorners[0].y) - y;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            slot = new BannerSlot(x, y, width, height);
            return true;
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
