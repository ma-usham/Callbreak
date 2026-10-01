using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 05: Quick Match search. The crimson arc spins around the still spade while
    /// the presenter reports progress, fills the four seats as players connect and ticks the honest
    /// elapsed time. Cancel withdraws the ticket. A button that is not interactable shows its
    /// disabled look with its label dimmed.
    /// </summary>
    public sealed class MatchmakingView : UIScreen
    {
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _profileButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _shopButton;
        [SerializeField] private UnityEngine.UI.Button _cancelButton;
        [SerializeField] private TMP_Text _cancelLabel;
        [SerializeField] private UnityEngine.UI.Image _avatar;
        [SerializeField] private TMP_Text _playerName;
        [SerializeField] private TMP_Text _statusTitle;
        [SerializeField] private TMP_Text _statusSubtitle;
        [SerializeField] private MatchmakingSeat[] _seat;
        [SerializeField] private TMP_Text _elapsed;

        public event Action BackClicked;
        public event Action ProfileClicked;
        public event Action SettingsClicked;
        public event Action ShopClicked;
        public event Action CancelClicked;

        public int SeatCount => _seat.Length;

        public void SetPlayerName(string playerName)
        {
            SetText(_playerName, playerName);
        }

        /// <summary>The player's avatar preset in the header. A missing sprite keeps the picture it shows (the default one).</summary>
        public void SetAvatar(Sprite avatar)
        {
            if (avatar != null)
            {
                _avatar.sprite = avatar;
            }
        }

        /// <summary>"Finding players..." · "3 of 4 players connected" (or "Table found", an error line...).</summary>
        public void SetStatus(string title, string subtitle)
        {
            SetText(_statusTitle, title);
            SetText(_statusSubtitle, subtitle);
        }

        /// <summary>Seat 0 is the local player. Filled shows the avatar disc, otherwise the dashed searching circle.</summary>
        public void SetSeat(int index, bool filled, string playerName, string status)
        {
            _seat[index].Show(filled, playerName, status);
        }

        /// <summary>With Reduce motion a seat that fills only fades its player in (GDD §14).</summary>
        public void SetReduceMotion(bool on)
        {
            foreach (MatchmakingSeat seat in _seat)
            {
                seat.ReduceMotion = on;
            }
        }

        /// <summary>The elapsed time, already formatted ("00:12").</summary>
        public void SetElapsed(string elapsed)
        {
            SetText(_elapsed, elapsed);
        }

        public void SetCancelInteractable(bool interactable)
        {
            _cancelButton.interactable = interactable;
            SetAlpha(_cancelLabel, interactable ? 1f : DimmedAlpha);
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _profileButton.onClick.AddListener(OnProfile);
            _settingsButton.onClick.AddListener(OnSettings);
            _shopButton.onClick.AddListener(OnShop);
            _cancelButton.onClick.AddListener(OnCancel);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _profileButton.onClick.RemoveListener(OnProfile);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _shopButton.onClick.RemoveListener(OnShop);
            _cancelButton.onClick.RemoveListener(OnCancel);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
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

        private void OnCancel() => CancelClicked?.Invoke();
    }
}
