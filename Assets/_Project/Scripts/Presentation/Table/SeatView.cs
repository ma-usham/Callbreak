using System;
using Callbreak.Presentation.Framework;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// One seat at the table (GDD §11 screens 07 and 08): avatar in its ring, name plate, status
    /// pill, and while it is the seat's turn the gold ring with the timer arc and the seconds disc.
    /// Opponents also have the fan of face-down cards behind the avatar. A small bubble over the
    /// seat shows a call as it is placed and the quick phrases (emotes). A small chip at the
    /// avatar says how the player is connected ("Away", "Reconnecting…" with a spinner, "Bot
    /// playing") or, on an online table, that the seat is a fill-in bot ("BOT"); the mockups do
    /// not draw it, so it is made of the kit's tag pill and hidden until there is something to say.
    /// The chip pops in, and a bubble shrinks and fades away when its time is up (ADR 023).
    /// </summary>
    public sealed class SeatView : MonoBehaviour
    {
        [SerializeField] private RectTransform _anchor;
        [SerializeField] private Image _avatar;
        [SerializeField] private Button _avatarButton;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Image _statusPill;
        [SerializeField] private Image _timerRing;
        [SerializeField] private GameObject _timerDisc;
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private OpponentHandView _fan;
        [SerializeField] private RectTransform _bubble;
        [SerializeField] private TMP_Text _bubbleText;
        [SerializeField] private RectTransform _chip;
        [SerializeField] private TMP_Text _chipText;
        [SerializeField] private GameObject _chipSpinner;
        [SerializeField] private Color _pillColor = Color.white;
        [SerializeField] private Color _pillMetColor = new Color(1f, 0.88f, 0.55f, 1f);
        [SerializeField] private Color _ringColor = new Color(0.976f, 0.647f, 0.11f, 1f);
        [SerializeField] private Color _ringWarningColor = new Color(0.525f, 0.063f, 0.094f, 1f);
        [SerializeField] private float _bubblePadding = 44f;
        [SerializeField] private float _bubbleMinWidth = 76f;
        [SerializeField] private float _chipPadding = 26f;
        [SerializeField] private float _chipSpinnerSpace = 26f;

        private MotionHandle _pulseMotion;
        private MotionHandle _chipMotion;
        private SpeechBubble _speech;
        private int _shownSeconds = int.MinValue;
        private bool _met;
        private bool _muted;
        private bool _chipBusy;

        /// <summary>The opponent tapped: the presenter mutes or unmutes this seat's emotes.</summary>
        public event Action AvatarClicked;

        /// <summary>The avatar's centre: where dealt and swept cards fly to and played cards come from.</summary>
        public RectTransform Anchor => _anchor;

        /// <summary>The fan of face-down cards; null for the local seat.</summary>
        public OpponentHandView Fan => _fan;

        /// <summary>With Reduce motion the chip only fades in (GDD §14).</summary>
        public bool ReduceMotion { get; set; }

        private SpeechBubble Speech => _speech ??= new SpeechBubble(_bubble, _bubbleText, _bubblePadding, _bubbleMinWidth);

        public void SetName(string playerName)
        {
            SetText(_name, playerName);
        }

        public void SetAvatar(Sprite avatar)
        {
            if (avatar != null && _avatar.sprite != avatar)
            {
                _avatar.sprite = avatar;
            }
        }

        /// <summary>The status pill: "Waiting", "Bid 3 · Won 1", …; gold once the call is met.</summary>
        public void SetStatus(string status, bool met)
        {
            SetText(_status, status);
            if (_met != met || _statusPill.color != (met ? _pillMetColor : _pillColor))
            {
                _met = met;
                _statusPill.color = met ? _pillMetColor : _pillColor;
            }
        }

        /// <summary>The chip's text while it shows; null while the seat has no chip.</summary>
        public string Chip => _chip != null && _chip.gameObject.activeSelf ? _chipText.text : null;

        /// <summary>The chip shows its spinner.</summary>
        public bool ChipIsBusy => _chip != null && _chip.gameObject.activeSelf && _chipSpinner.activeSelf;

        /// <summary>
        /// The chip at the avatar (GDD §8 "What others see", §9 "a small BOT tag"): "Away",
        /// "Reconnecting…" (<paramref name="busy"/>: with the spinner), "Bot playing" or "BOT".
        /// Null or empty hides it. The pill hugs its text.
        /// </summary>
        public void SetChip(string text, bool busy)
        {
            if (_chip == null)
            {
                // A Table prefab built before the chip existed: it has nothing to show it with.
                return;
            }

            bool show = !string.IsNullOrEmpty(text);
            if (_chip.gameObject.activeSelf != show)
            {
                // It pops in, and goes at once.
                Cancel(ref _chipMotion);
                _chip.localScale = Vector3.one;
                UiMotion.GroupOf(_chip.gameObject).alpha = 1f;
                _chip.gameObject.SetActive(show);
                if (show)
                {
                    _chipMotion = UiMotion.PopIn(_chip, UiMotion.GroupOf(_chip.gameObject), ReduceMotion, 0.8f, UiMotion.Short);
                }
            }

            if (!show || (_chipText.text == text && _chipBusy == busy && _chipSpinner.activeSelf == busy))
            {
                return;
            }

            _chipBusy = busy;
            _chipText.text = text;
            if (_chipSpinner.activeSelf != busy)
            {
                _chipSpinner.SetActive(busy);
            }

            // The spinner sits at the left end; the text is centred in what is left.
            float spinner = busy ? _chipSpinnerSpace : 0f;
            _chipText.rectTransform.offsetMin = new Vector2(spinner, 0f);
            float width = Mathf.Ceil(_chipText.GetPreferredValues(text).x) + _chipPadding + spinner;
            _chip.sizeDelta = new Vector2(width, _chip.sizeDelta.y);
        }

        /// <summary>The gold ring of the seat whose turn it is; a full ring when no clock is running.</summary>
        public void SetTurn(bool active)
        {
            if (_timerRing.enabled != active)
            {
                _timerRing.enabled = active;
            }

            if (!active)
            {
                ClearTimer();
            }
        }

        /// <summary>
        /// The turn clock: the arc left, the whole seconds left, and the crimson warning of the last
        /// five seconds with its 1 Hz pulse (<paramref name="pulse"/> 0-1 within the second; pass 0
        /// with Reduce motion, which changes the colour only).
        /// </summary>
        public void SetTimer(float fraction, int seconds, bool warning, float pulse)
        {
            if (!_timerRing.enabled)
            {
                _timerRing.enabled = true;
            }

            _timerRing.fillAmount = Mathf.Clamp01(fraction);
            Color color = warning ? _ringWarningColor : _ringColor;
            if (_timerRing.color != color)
            {
                _timerRing.color = color;
            }

            float scale = warning ? 1f + 0.05f * Mathf.Sin(pulse * Mathf.PI) : 1f;
            _timerRing.rectTransform.localScale = new Vector3(scale, scale, 1f);
            if (!_timerDisc.activeSelf)
            {
                _timerDisc.SetActive(true);
            }

            if (_shownSeconds != seconds)
            {
                _shownSeconds = seconds;
                _timerText.SetText("{0}", seconds);
            }
        }

        /// <summary>No clock (offline, or not this seat's turn): the seconds disc goes and the ring is whole.</summary>
        public void ClearTimer()
        {
            _shownSeconds = int.MinValue;
            if (_timerDisc.activeSelf)
            {
                _timerDisc.SetActive(false);
            }

            _timerRing.fillAmount = 1f;
            if (_timerRing.color != _ringColor)
            {
                _timerRing.color = _ringColor;
            }

            _timerRing.rectTransform.localScale = Vector3.one;
        }

        /// <summary>Pops a bubble over the seat: the call just placed, or another player's quick phrase.</summary>
        public void ShowBubble(string text, float seconds, bool reduceMotion) => Speech.Show(text, seconds, reduceMotion);

        public void HideBubble() => Speech.Hide();

        /// <summary>"Won +1": a short pulse on the status pill (GDD §14; the number alone with Reduce motion).</summary>
        public void PulseStatus(bool reduceMotion)
        {
            Cancel(ref _pulseMotion);
            RectTransform pill = _statusPill.rectTransform;
            pill.localScale = Vector3.one;
            if (reduceMotion)
            {
                return;
            }

            _pulseMotion = LMotion.Create(0f, 1f, 0.3f)
                .WithEase(Ease.OutQuad)
                .Bind(pill, (t, target) =>
                {
                    float scale = 1f + 0.1f * Mathf.Sin(t * Mathf.PI);
                    target.localScale = new Vector3(scale, scale, 1f);
                });
        }

        /// <summary>A muted opponent's avatar is dimmed; their quick phrases are not shown.</summary>
        public void SetMuted(bool muted)
        {
            if (_muted == muted)
            {
                return;
            }

            _muted = muted;
            Color color = _avatar.color;
            color.a = muted ? 0.45f : 1f;
            _avatar.color = color;
        }

        private void OnEnable()
        {
            if (_avatarButton != null)
            {
                _avatarButton.onClick.AddListener(OnAvatarClicked);
            }
        }

        private void OnDisable()
        {
            if (_avatarButton != null)
            {
                _avatarButton.onClick.RemoveListener(OnAvatarClicked);
            }

            _speech?.Stop();
            Cancel(ref _pulseMotion);
            UiMotion.Finish(ref _chipMotion);

            // When the scene unloads the pill may already be gone.
            if (_statusPill != null)
            {
                _statusPill.rectTransform.localScale = Vector3.one;
            }
        }

        private void OnAvatarClicked() => AvatarClicked?.Invoke();

        private static void SetText(TMP_Text label, string text)
        {
            text ??= string.Empty;
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private static void Cancel(ref MotionHandle handle)
        {
            if (handle.IsActive())
            {
                handle.Cancel();
            }

            handle = default;
        }
    }
}
