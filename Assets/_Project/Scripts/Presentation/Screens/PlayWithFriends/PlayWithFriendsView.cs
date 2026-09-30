using System;
using Callbreak.Presentation.Framework;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 01: choose between hosting a private table and joining one. Each whole
    /// card is a tap target as well as its crimson button.
    /// </summary>
    public sealed class PlayWithFriendsView : UIScreen
    {
        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _profileButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _shopButton;
        [SerializeField] private UnityEngine.UI.Button _createPanel;
        [SerializeField] private UnityEngine.UI.Button _createButton;
        [SerializeField] private UnityEngine.UI.Button _joinPanel;
        [SerializeField] private UnityEngine.UI.Button _joinButton;
        [SerializeField] private UnityEngine.UI.Image _avatar;
        [SerializeField] private TMP_Text _playerName;

        public event Action BackClicked;
        public event Action ProfileClicked;
        public event Action SettingsClicked;
        public event Action ShopClicked;
        public event Action CreateClicked;
        public event Action JoinClicked;

        public void SetPlayerName(string playerName)
        {
            if (_playerName.text != playerName)
            {
                _playerName.text = playerName;
            }
        }

        /// <summary>The player's avatar preset in the header. A missing sprite keeps the picture it shows (the default one).</summary>
        public void SetAvatar(Sprite avatar)
        {
            if (avatar != null)
            {
                _avatar.sprite = avatar;
            }
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _profileButton.onClick.AddListener(OnProfile);
            _settingsButton.onClick.AddListener(OnSettings);
            _shopButton.onClick.AddListener(OnShop);
            _createPanel.onClick.AddListener(OnCreate);
            _createButton.onClick.AddListener(OnCreate);
            _joinPanel.onClick.AddListener(OnJoin);
            _joinButton.onClick.AddListener(OnJoin);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _profileButton.onClick.RemoveListener(OnProfile);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _shopButton.onClick.RemoveListener(OnShop);
            _createPanel.onClick.RemoveListener(OnCreate);
            _createButton.onClick.RemoveListener(OnCreate);
            _joinPanel.onClick.RemoveListener(OnJoin);
            _joinButton.onClick.RemoveListener(OnJoin);
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnProfile() => ProfileClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnShop() => ShopClicked?.Invoke();

        private void OnCreate() => CreateClicked?.Invoke();

        private void OnJoin() => JoinClicked?.Invoke();
    }
}
