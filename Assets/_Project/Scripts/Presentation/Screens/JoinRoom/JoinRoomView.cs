using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 03: join a friend's table with a six-character room code. Errors give the
    /// code boxes their error look, shake them (not with reduce motion) and replace the footer
    /// line with the message. Not interactable (fewer than six valid characters): the Join
    /// button's disabled look, label and chevron dimmed. Busy (while joining): the button keeps
    /// its look, a spinner takes the chevron's place, and the button, the code boxes and Paste
    /// ignore taps. While the boxes have the keyboard, the content rises so they stay just above
    /// the on-screen keyboard (<see cref="KeyboardFollower"/>), and settles back when it goes.
    /// </summary>
    public sealed class JoinRoomView : UIScreen
    {
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _profileButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _pasteButton;
        [SerializeField] private UnityEngine.UI.Button _joinButton;
        [SerializeField] private TMP_Text _joinButtonLabel;
        [SerializeField] private UnityEngine.UI.Image _joinChevron;
        [SerializeField] private GameObject _joinSpinner;
        [SerializeField] private UnityEngine.UI.Image _avatar;
        [SerializeField] private TMP_Text _playerName;
        [SerializeField] private CodeInput _codeInput;
        [SerializeField] private GameObject _footer;
        [SerializeField] private TMP_Text _errorLabel;

        private bool _joinInteractable = true;
        private bool _busy;
        private KeyboardFollower _keyboard;

        public event Action BackClicked;
        public event Action ProfileClicked;
        public event Action SettingsClicked;
        public event Action<string> CodeChanged;
        public event Action PasteClicked;
        public event Action JoinClicked;

        public void SetPlayerName(string playerName)
        {
            if (_playerName.text != playerName)
            {
                _playerName.text = playerName;
            }
        }

        /// <summary>The player's avatar in the header. A missing sprite keeps the current picture.</summary>
        public void SetAvatar(Sprite avatar)
        {
            if (avatar != null)
            {
                _avatar.sprite = avatar;
            }
        }

        /// <summary>
        /// The on-screen keyboard's height in screen pixels, 0 while it is down (the presenter gives
        /// the device's; without one the screen never moves).
        /// </summary>
        public Func<float> KeyboardHeight
        {
            get => Keyboard.KeyboardHeight;
            set => Keyboard.KeyboardHeight = value;
        }

        /// <summary>How far the content is lifted to keep the code boxes above the keyboard, in canvas units.</summary>
        public float Lift => Keyboard.Lift;

        private KeyboardFollower Keyboard => _keyboard ??= new KeyboardFollower(SlideTarget, (RectTransform)_codeInput.transform, () => _codeInput.IsFocused);

        /// <summary>
        /// Reduce motion (GDD §13): an error no longer shakes the boxes; they only take their error
        /// look. The content moves for the keyboard at once.
        /// </summary>
        public void SetReduceMotion(bool on)
        {
            _codeInput.ReduceMotion = on;
            Keyboard.ReduceMotion = on;
        }

        /// <summary>Fills the boxes (e.g. after Paste or from an invite link); does not raise <see cref="CodeChanged"/>.</summary>
        public void SetCode(string code) => _codeInput.SetCodeWithoutNotify(code);

        public void SetJoinInteractable(bool interactable)
        {
            _joinInteractable = interactable;
            RenderJoinButton();
        }

        public void SetBusy(bool busy)
        {
            _busy = busy;
            _codeInput.Interactable = !busy;
            _pasteButton.targetGraphic.raycastTarget = !busy;
            RenderJoinButton();
        }

        /// <summary>
        /// Gives the boxes their error look, shakes them and shows the message where the footer
        /// line is, in Heading Burgundy.
        /// </summary>
        public void ShowError(string message)
        {
            _errorLabel.text = message;
            _errorLabel.gameObject.SetActive(true);
            _footer.SetActive(false);
            _codeInput.SetError(true);
            _codeInput.Shake();
        }

        /// <summary>Puts the footer line and the boxes' normal look back.</summary>
        public void ClearError()
        {
            _errorLabel.gameObject.SetActive(false);
            _footer.SetActive(true);
            _codeInput.SetError(false);
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _profileButton.onClick.AddListener(OnProfile);
            _settingsButton.onClick.AddListener(OnSettings);
            _pasteButton.onClick.AddListener(OnPaste);
            _joinButton.onClick.AddListener(OnJoin);
            _codeInput.Changed += OnCodeChanged;
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _profileButton.onClick.RemoveListener(OnProfile);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _pasteButton.onClick.RemoveListener(OnPaste);
            _joinButton.onClick.RemoveListener(OnJoin);
            _codeInput.Changed -= OnCodeChanged;
            _keyboard?.Settle();
        }

        /// <summary>The content follows the keyboard, after a screen transition has placed it for the frame.</summary>
        private void LateUpdate() => Keyboard.Tick(Time.unscaledDeltaTime);

        private void RenderJoinButton()
        {
            // Busy wins: the button keeps its look and shows the spinner, whatever the interactable
            // flag says meanwhile, and takes no taps (so no press feedback either) until it is over.
            bool enabledLook = _busy || _joinInteractable;
            _joinButton.interactable = enabledLook;
            _joinButton.targetGraphic.raycastTarget = !_busy;
            float alpha = enabledLook ? 1f : DimmedAlpha;
            SetAlpha(_joinButtonLabel, alpha);
            SetAlpha(_joinChevron, alpha);
            _joinChevron.enabled = !_busy;
            _joinSpinner.SetActive(_busy);
        }

        private static void SetAlpha(UnityEngine.UI.Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnProfile() => ProfileClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnPaste()
        {
            if (!_busy)
            {
                PasteClicked?.Invoke();
            }
        }

        private void OnJoin()
        {
            if (!_busy)
            {
                JoinClicked?.Invoke();
            }
        }

        private void OnCodeChanged(string code) => CodeChanged?.Invoke(code);
    }
}
