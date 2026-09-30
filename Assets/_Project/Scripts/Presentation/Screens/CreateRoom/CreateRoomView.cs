using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 02: configure a private table (room name, time per turn) and open it. The
    /// Players and Match length tiles are fixed in v1 but their values are rendered, not baked.
    /// Not interactable (the name is not valid yet): the button's disabled look, label and chevron
    /// dimmed. Busy (the room is being created): the button keeps its look, a spinner takes the
    /// chevron's place and taps are ignored, so a double tap never creates two rooms.
    /// </summary>
    public sealed class CreateRoomView : UIScreen
    {
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _profileButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _shopButton;
        [SerializeField] private UnityEngine.UI.Button _createButton;
        [SerializeField] private TMP_Text _createButtonLabel;
        [SerializeField] private UnityEngine.UI.Image _createChevron;
        [SerializeField] private GameObject _createSpinner;
        [SerializeField] private TMP_Text _playerName;
        [SerializeField] private TMP_InputField _roomNameField;
        [SerializeField] private TMP_Text _playersValue;
        [SerializeField] private TMP_Text _matchValue;
        [SerializeField] private SelectableGroup _turnSelector;

        private bool _createInteractable = true;
        private bool _busy;

        public event Action BackClicked;
        public event Action ProfileClicked;
        public event Action SettingsClicked;
        public event Action ShopClicked;
        public event Action CreateClicked;
        public event Action<string> RoomNameChanged;
        public event Action<int> TurnSecondsChanged;

        public void SetPlayerName(string playerName) => SetText(_playerName, playerName);

        /// <summary>The field's text ("{Name}'s Table" by default); does not raise <see cref="RoomNameChanged"/>.</summary>
        public void SetRoomName(string roomName)
        {
            if (_roomNameField.text != roomName)
            {
                _roomNameField.SetTextWithoutNotify(roomName);
            }
        }

        /// <summary>Selects the 15 sec or 30 sec option; does not raise <see cref="TurnSecondsChanged"/>.</summary>
        public void SetTurnSeconds(int seconds) => _turnSelector.SetSelectedWithoutNotify(OptionFor(seconds));

        public void SetCreateInteractable(bool interactable)
        {
            _createInteractable = interactable;
            RenderCreateButton();
        }

        public void SetBusy(bool busy)
        {
            _busy = busy;
            RenderCreateButton();
        }

        /// <summary>The Players tile's value ("4 players").</summary>
        public void SetPlayersValue(string value) => SetText(_playersValue, value);

        /// <summary>The Match length tile's value ("5 rounds").</summary>
        public void SetMatchLengthValue(string value) => SetText(_matchValue, value);

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _profileButton.onClick.AddListener(OnProfile);
            _settingsButton.onClick.AddListener(OnSettings);
            _shopButton.onClick.AddListener(OnShop);
            _createButton.onClick.AddListener(OnCreate);
            _roomNameField.onValueChanged.AddListener(OnRoomNameChanged);
            _turnSelector.Changed += OnTurnOptionChanged;
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _profileButton.onClick.RemoveListener(OnProfile);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _shopButton.onClick.RemoveListener(OnShop);
            _createButton.onClick.RemoveListener(OnCreate);
            _roomNameField.onValueChanged.RemoveListener(OnRoomNameChanged);
            _turnSelector.Changed -= OnTurnOptionChanged;
        }

        private void RenderCreateButton()
        {
            // Busy wins: the button keeps its look and shows the spinner, whatever the interactable
            // flag says meanwhile, and takes no taps (so no press feedback either) until it is over.
            bool enabledLook = _busy || _createInteractable;
            _createButton.interactable = enabledLook;
            _createButton.targetGraphic.raycastTarget = !_busy;
            float alpha = enabledLook ? 1f : DimmedAlpha;
            SetAlpha(_createButtonLabel, alpha);
            SetAlpha(_createChevron, alpha);
            _createChevron.enabled = !_busy;
            _createSpinner.SetActive(_busy);
        }

        // The two options drawn on the mockup: 15 sec | 30 sec.
        private static int OptionFor(int seconds) => seconds <= 15 ? 0 : 1;

        private static int SecondsFor(int option) => option == 0 ? 15 : 30;

        private static void SetText(TMP_Text label, string value)
        {
            if (label.text != value)
            {
                label.text = value;
            }
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

        private void OnShop() => ShopClicked?.Invoke();

        private void OnCreate()
        {
            if (!_busy)
            {
                CreateClicked?.Invoke();
            }
        }

        private void OnRoomNameChanged(string roomName) => RoomNameChanged?.Invoke(roomName);

        private void OnTurnOptionChanged(int option) => TurnSecondsChanged?.Invoke(SecondsFor(option));
    }
}
