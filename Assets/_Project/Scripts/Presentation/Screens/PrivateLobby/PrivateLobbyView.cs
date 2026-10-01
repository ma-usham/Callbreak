using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 04: the private lobby. Title = room name, subtitle = lobby state, the room
    /// code with a copy button, Invite Friends, four seats (seat 0 is the host) and the primary
    /// button. The host sees Start Game; a guest sees the same button as a Ready toggle and
    /// "Waiting for host…" in the footer's place. An empty seat shows a dimmed avatar, its name
    /// line in muted brown and no ready dot; a player who takes it pops in (ADR 023). Holding a
    /// seat raises <see cref="SeatLongPressed"/> (the host removes a player that way); the
    /// presenter decides whether it applies.
    /// </summary>
    public sealed class PrivateLobbyView : UIScreen
    {
        public const int SeatCount = 4;

        private const float EmptySeatAvatarAlpha = 0.35f;
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _copyButton;
        [SerializeField] private UnityEngine.UI.Button _inviteButton;
        [SerializeField] private UnityEngine.UI.Button _startButton;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _subtitle;
        [SerializeField] private TMP_Text _code;
        [SerializeField] private TMP_Text _footer;
        [SerializeField] private GameObject _waitingLabel;
        [SerializeField] private TMP_Text _startLabel;
        [SerializeField] private UnityEngine.UI.Image _startIcon;
        [SerializeField] private TMP_Text _readyLabel;
        [SerializeField] private CanvasGroup[] _seatAvatar;
        [SerializeField] private UnityEngine.UI.Image[] _seatPicture;
        [SerializeField] private LongPressArea[] _seatHold;
        [SerializeField] private Sprite _defaultAvatar;
        [SerializeField] private GameObject[] _seatDot;
        [SerializeField] private TMP_Text[] _seatName;
        [SerializeField] private TMP_Text[] _seatStatus;

        // Colours measured on the mockup: names, "Host", "Ready"; muted brown is the Host brown.
        [SerializeField] private Color _nameColor = new Color32(0x1D, 0x20, 0x20, 0xFF);
        [SerializeField] private Color _mutedColor = new Color32(0x84, 0x60, 0x42, 0xFF);
        [SerializeField] private Color _readyColor = new Color32(0x2E, 0x73, 0x35, 0xFF);

        // What each seat last showed: -1 nothing yet, 0 empty, 1 a player.
        private readonly int[] _seatShown = { -1, -1, -1, -1 };
        private readonly MotionHandle[] _seatMotion = new MotionHandle[SeatCount];
        private Action[] _seatHoldHandlers;
        private bool _isHost = true;
        private bool _startInteractable = true;
        private bool _reduceMotion;

        public event Action BackClicked;
        public event Action SettingsClicked;
        public event Action CopyCodeClicked;
        public event Action InviteClicked;
        public event Action StartClicked;
        public event Action ReadyClicked;

        /// <summary>A seat (0..3) was held down; only the host acts on it (GDD: long-press to remove a player).</summary>
        public event Action<int> SeatLongPressed;

        /// <summary>The room name.</summary>
        public void SetTitle(string title) => SetText(_title, title);

        /// <summary>The lobby state line ("WAITING FOR FRIENDS · 2 / 4", "EVERYONE IS HERE").</summary>
        public void SetSubtitle(string subtitle) => SetText(_subtitle, subtitle);

        public void SetCode(string code) => SetText(_code, code);

        /// <summary>"4 / 4 players · 5 rounds · 30 sec per turn".</summary>
        public void SetFooter(string footer) => SetText(_footer, footer);

        /// <param name="index">Seat 0..3; seat 0 is the host.</param>
        /// <param name="showReadyDot">The green dot on the avatar (never shown on an empty seat).</param>
        /// <param name="occupied">False: dimmed avatar, the name line in muted brown, no dot.</param>
        public void SetSeat(int index, string name, string status, SeatStatusStyle style, bool showReadyDot, bool occupied)
        {
            SetText(_seatName[index], name);
            _seatName[index].color = occupied ? _nameColor : _mutedColor;
            SetText(_seatStatus[index], status);
            _seatStatus[index].color = style == SeatStatusStyle.Ready ? _readyColor : _mutedColor;
            _seatDot[index].SetActive(showReadyDot && occupied);
            bool joined = occupied && _seatShown[index] == 0 && isActiveAndEnabled;
            _seatShown[index] = occupied ? 1 : 0;
            UiMotion.Finish(ref _seatMotion[index]);
            _seatAvatar[index].alpha = occupied ? 1f : EmptySeatAvatarAlpha;
            if (joined)
            {
                CanvasGroup avatar = _seatAvatar[index];
                _seatMotion[index] = UiMotion.PopIn(avatar.transform, avatar, _reduceMotion, 0.8f);
            }
        }

        /// <summary>With Reduce motion a player who takes a seat only fades in (GDD §14).</summary>
        public void SetReduceMotion(bool on) => _reduceMotion = on;

        /// <summary>The seat's avatar picture (players pick their avatar); null shows the default silhouette.</summary>
        public void SetSeatAvatar(int index, Sprite avatar)
        {
            _seatPicture[index].sprite = avatar != null ? avatar : _defaultAvatar;
        }

        /// <summary>Host: Start Game. Guest: the same button as a Ready toggle, and "Waiting for host…" in the footer's place.</summary>
        public void SetHostMode(bool isHost)
        {
            _isHost = isHost;
            _startLabel.gameObject.SetActive(isHost);
            _startIcon.gameObject.SetActive(isHost);
            _readyLabel.gameObject.SetActive(!isHost);
            _footer.gameObject.SetActive(isHost);
            _waitingLabel.SetActive(!isHost);
            RenderPrimaryButton();
        }

        public void SetStartInteractable(bool interactable)
        {
            _startInteractable = interactable;
            RenderPrimaryButton();
        }

        /// <summary>The guest's button label: "Ready" or "Not ready".</summary>
        public void SetReadyLabel(string label) => SetText(_readyLabel, label);

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _settingsButton.onClick.AddListener(OnSettings);
            _copyButton.onClick.AddListener(OnCopy);
            _inviteButton.onClick.AddListener(OnInvite);
            _startButton.onClick.AddListener(OnPrimary);

            _seatHoldHandlers = new Action[_seatHold.Length];
            for (int i = 0; i < _seatHold.Length; i++)
            {
                int seat = i;
                _seatHoldHandlers[i] = () => SeatLongPressed?.Invoke(seat);
                _seatHold[i].Held += _seatHoldHandlers[i];
            }
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _copyButton.onClick.RemoveListener(OnCopy);
            _inviteButton.onClick.RemoveListener(OnInvite);
            _startButton.onClick.RemoveListener(OnPrimary);

            for (int i = 0; i < _seatHold.Length && _seatHoldHandlers != null; i++)
            {
                _seatHold[i].Held -= _seatHoldHandlers[i];
            }

            for (int i = 0; i < _seatMotion.Length; i++)
            {
                UiMotion.Finish(ref _seatMotion[i]);
            }
        }

        private void RenderPrimaryButton()
        {
            bool interactable = !_isHost || _startInteractable;
            _startButton.interactable = interactable;
            SetAlpha(_startLabel, interactable ? 1f : DimmedAlpha);
            SetAlpha(_startIcon, interactable ? 1f : DimmedAlpha);
        }

        private static void SetAlpha(UnityEngine.UI.Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label.text != value)
            {
                label.text = value;
            }
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnCopy() => CopyCodeClicked?.Invoke();

        private void OnInvite() => InviteClicked?.Invoke();

        private void OnPrimary()
        {
            if (_isHost)
            {
                StartClicked?.Invoke();
            }
            else
            {
                ReadyClicked?.Invoke();
            }
        }
    }
}
