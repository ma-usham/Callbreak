using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 14: the player's identity and play statistics. The pencil (or the avatar)
    /// opens the preset picker; the display name is edited in the field and saved with Save
    /// Changes; the line under the field shows the note or a validation message. A Save button
    /// that is not interactable shows its disabled look with its label dimmed.
    /// </summary>
    public sealed class ProfileView : UIScreen
    {
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _avatarButton;
        [SerializeField] private UnityEngine.UI.Button _editAvatarButton;
        [SerializeField] private UnityEngine.UI.Button _saveButton;
        [SerializeField] private TMP_Text _saveLabel;
        [SerializeField] private UnityEngine.UI.Image _avatar;
        [SerializeField] private TMP_Text _playerName;
        [SerializeField] private TMP_Text[] _stat;
        [SerializeField] private TMP_InputField _nameField;
        [SerializeField] private ProfileNameMessage _nameMessage;

        public event Action BackClicked;
        public event Action EditAvatarClicked;

        /// <summary>Raised on every edit of the display-name field.</summary>
        public event Action<string> NameChanged;

        public event Action SaveClicked;

        /// <summary>The name shown under the avatar.</summary>
        public void SetName(string playerName)
        {
            SetText(_playerName, playerName);
        }

        /// <summary>The picked avatar preset. A missing sprite keeps the current picture.</summary>
        public void SetAvatar(Sprite avatar)
        {
            if (avatar != null)
            {
                _avatar.sprite = avatar;
            }
        }

        /// <summary>Already formatted values: "48", "19", "40%", "26.4".</summary>
        public void SetStats(string gamesPlayed, string wins, string winRate, string bestScore)
        {
            SetText(_stat[0], gamesPlayed);
            SetText(_stat[1], wins);
            SetText(_stat[2], winRate);
            SetText(_stat[3], bestScore);
        }

        /// <summary>The field's text, without raising <see cref="NameChanged"/>.</summary>
        public void SetNameField(string playerName)
        {
            _nameField.SetTextWithoutNotify(playerName);
        }

        public void SetSaveInteractable(bool interactable)
        {
            _saveButton.interactable = interactable;
            SetAlpha(_saveLabel, interactable ? 1f : DimmedAlpha);
        }

        /// <summary>The line under the field: the note (isError false) or a validation message.</summary>
        public void SetNameMessage(string text, bool isError)
        {
            _nameMessage.Show(text, isError);
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _avatarButton.onClick.AddListener(OnEditAvatar);
            _editAvatarButton.onClick.AddListener(OnEditAvatar);
            _saveButton.onClick.AddListener(OnSave);
            _nameField.onValueChanged.AddListener(OnNameChanged);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _avatarButton.onClick.RemoveListener(OnEditAvatar);
            _editAvatarButton.onClick.RemoveListener(OnEditAvatar);
            _saveButton.onClick.RemoveListener(OnSave);
            _nameField.onValueChanged.RemoveListener(OnNameChanged);
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

        private void OnEditAvatar() => EditAvatarClicked?.Invoke();

        private void OnSave() => SaveClicked?.Invoke();

        private void OnNameChanged(string value) => NameChanged?.Invoke(value);
    }
}
