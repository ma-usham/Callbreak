using System;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
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
        [SerializeField] private UnityEngine.UI.Button _resumeButton;
        [SerializeField] private GameObject _footer;
        [SerializeField] private UnityEngine.UI.Image _avatar;
        [SerializeField] private TMP_Text _playerNameText;
        [SerializeField] private CanvasGroup _playOnlineGroup;
        [SerializeField] private CanvasGroup _playWithFriendsGroup;
        [SerializeField] private IdleSway _fanSway;

        // The fan's four aces, indexed by suit.
        [SerializeField] private UnityEngine.UI.Image[] _fanFaces;
        [SerializeField] private float _mutedAlpha = 0.5f;

        public event Action ProfileClicked;
        public event Action SettingsClicked;
        public event Action ShopClicked;
        public event Action PlayOnlineClicked;
        public event Action PlayWithFriendsClicked;
        public event Action PlayOfflineClicked;

        /// <summary>The "Resume match" banner was tapped.</summary>
        public event Action ResumeClicked;

        public void SetPlayerName(string playerName)
        {
            if (_playerNameText.text != playerName)
            {
                _playerNameText.text = playerName;
            }
        }

        /// <summary>The picked avatar preset. A missing sprite keeps the current picture.</summary>
        public void SetAvatar(Sprite avatar)
        {
            if (avatar != null && _avatar.sprite != avatar)
            {
                _avatar.sprite = avatar;
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

        /// <summary>
        /// While an unfinished offline match exists, the "Resume match" banner takes the footer
        /// line's place (GDD §11 screen 00).
        /// </summary>
        public void SetResumeAvailable(bool available)
        {
            if (_resumeButton.gameObject.activeSelf != available)
            {
                _resumeButton.gameObject.SetActive(available);
                _footer.SetActive(!available);
            }
        }

        /// <summary>
        /// The ace of <paramref name="suit"/> in the card fan, in the player's deck (GDD §13). A
        /// missing sprite keeps the card that is showing.
        /// </summary>
        public void SetFanFace(Suit suit, Sprite face)
        {
            UnityEngine.UI.Image image = _fanFaces[(int)suit];
            if (face != null && image.sprite != face)
            {
                image.sprite = face;
            }
        }

        /// <summary>With Reduce motion the card fan stands still (GDD §14).</summary>
        public void SetReduceMotion(bool on) => _fanSway.Still = on;

        private void OnEnable()
        {
            _profileButton.onClick.AddListener(OnProfileClicked);
            _settingsButton.onClick.AddListener(OnSettingsClicked);
            _shopButton.onClick.AddListener(OnShopClicked);
            _playOnlineButton.onClick.AddListener(OnPlayOnlineClicked);
            _playWithFriendsButton.onClick.AddListener(OnPlayWithFriendsClicked);
            _playOfflineButton.onClick.AddListener(OnPlayOfflineClicked);
            _resumeButton.onClick.AddListener(OnResumeClicked);
        }

        private void OnDisable()
        {
            _profileButton.onClick.RemoveListener(OnProfileClicked);
            _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            _shopButton.onClick.RemoveListener(OnShopClicked);
            _playOnlineButton.onClick.RemoveListener(OnPlayOnlineClicked);
            _playWithFriendsButton.onClick.RemoveListener(OnPlayWithFriendsClicked);
            _playOfflineButton.onClick.RemoveListener(OnPlayOfflineClicked);
            _resumeButton.onClick.RemoveListener(OnResumeClicked);
        }

        private void OnProfileClicked() => ProfileClicked?.Invoke();

        private void OnSettingsClicked() => SettingsClicked?.Invoke();

        private void OnShopClicked() => ShopClicked?.Invoke();

        private void OnPlayOnlineClicked() => PlayOnlineClicked?.Invoke();

        private void OnPlayWithFriendsClicked() => PlayWithFriendsClicked?.Invoke();

        private void OnPlayOfflineClicked() => PlayOfflineClicked?.Invoke();

        private void OnResumeClicked() => ResumeClicked?.Invoke();
    }
}
