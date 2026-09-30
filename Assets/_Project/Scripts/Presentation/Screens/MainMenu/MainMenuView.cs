using System;
using Callbreak.Presentation.Framework;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 00, the home hub. Shows who the player is and raises one event per entry
    /// point; the presenter decides where each one leads.
    /// </summary>
    public sealed class MainMenuView : UIScreen
    {
        [SerializeField] private UnityEngine.UI.Button _profileButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _shopButton;
        [SerializeField] private UnityEngine.UI.Button _playOnlineButton;
        [SerializeField] private UnityEngine.UI.Button _playWithFriendsButton;
        [SerializeField] private UnityEngine.UI.Button _playOfflineButton;
        [SerializeField] private TMP_Text _playerNameText;
        [SerializeField] private CanvasGroup _playOnlineGroup;
        [SerializeField] private CanvasGroup _playWithFriendsGroup;
        [SerializeField] private float _mutedAlpha = 0.5f;

        public event Action ProfileClicked;
        public event Action SettingsClicked;
        public event Action ShopClicked;
        public event Action PlayOnlineClicked;
        public event Action PlayWithFriendsClicked;
        public event Action PlayOfflineClicked;

        public void SetPlayerName(string playerName)
        {
            if (_playerNameText.text != playerName)
            {
                _playerNameText.text = playerName;
            }
        }

        /// <summary>
        /// With no internet the two online buttons are muted (GDD §11 screen 00). They still take
        /// the tap, so the presenter can say why and point to Play Offline.
        /// </summary>
        public void SetOnlineAvailable(bool available)
        {
            float alpha = available ? 1f : _mutedAlpha;
            _playOnlineGroup.alpha = alpha;
            _playWithFriendsGroup.alpha = alpha;
        }

        private void OnEnable()
        {
            _profileButton.onClick.AddListener(OnProfileClicked);
            _settingsButton.onClick.AddListener(OnSettingsClicked);
            _shopButton.onClick.AddListener(OnShopClicked);
            _playOnlineButton.onClick.AddListener(OnPlayOnlineClicked);
            _playWithFriendsButton.onClick.AddListener(OnPlayWithFriendsClicked);
            _playOfflineButton.onClick.AddListener(OnPlayOfflineClicked);
        }

        private void OnDisable()
        {
            _profileButton.onClick.RemoveListener(OnProfileClicked);
            _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            _shopButton.onClick.RemoveListener(OnShopClicked);
            _playOnlineButton.onClick.RemoveListener(OnPlayOnlineClicked);
            _playWithFriendsButton.onClick.RemoveListener(OnPlayWithFriendsClicked);
            _playOfflineButton.onClick.RemoveListener(OnPlayOfflineClicked);
        }

        private void OnProfileClicked() => ProfileClicked?.Invoke();

        private void OnSettingsClicked() => SettingsClicked?.Invoke();

        private void OnShopClicked() => ShopClicked?.Invoke();

        private void OnPlayOnlineClicked() => PlayOnlineClicked?.Invoke();

        private void OnPlayWithFriendsClicked() => PlayWithFriendsClicked?.Invoke();

        private void OnPlayOfflineClicked() => PlayOfflineClicked?.Invoke();
    }
}
