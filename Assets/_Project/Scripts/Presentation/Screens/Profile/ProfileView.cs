using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 14: the player's identity and play statistics. The pencil (or the avatar)
    /// opens the avatar picker (ADR 041): the six portraits on a panel over the dimmed screen, the
    /// chosen one ringed, closed with Done or a tap beside the panel. The display name is edited in
    /// the field and saved with Save Changes; while a saved name has to stay (ADR 041: 7 days) the
    /// field takes no typing, and a tap on it nudges the line under it, which says how long the name
    /// stays. Otherwise the line shows the note or a validation message. A Save button that is not interactable shows its disabled look with its
    /// label dimmed. While the field has the keyboard, the content rises so the field stays just
    /// above the on-screen keyboard (<see cref="KeyboardFollower"/>), and settles back when it goes.
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
        [SerializeField] private TapRelay _nameFieldTap;
        [SerializeField] private ProfileNameMessage _nameMessage;

        // The avatar picker (ADR 041): the dim and the panel over the screen, the row of
        // portraits, Done, and the dim itself, which closes it too.
        [SerializeField] private CanvasGroup _avatarSheet;
        [SerializeField] private RectTransform _avatarPanel;
        [SerializeField] private AvatarPicker _avatarPicker;
        [SerializeField] private UnityEngine.UI.Button _avatarDone;
        [SerializeField] private UnityEngine.UI.Button _avatarBackdrop;
        [SerializeField] private float _avatarFadeSeconds = 0.18f;

        private KeyboardFollower _keyboard;
        private MotionHandle _sheetFade;
        private MotionHandle _sheetPop;

        public event Action BackClicked;
        public event Action EditAvatarClicked;

        /// <summary>Raised on every edit of the display-name field.</summary>
        public event Action<string> NameChanged;

        public event Action SaveClicked;

        /// <summary>A portrait in the avatar picker was tapped: its preset index.</summary>
        public event Action<int> AvatarPicked;

        /// <summary>Done, or a tap beside the picker's panel.</summary>
        public event Action AvatarPickerCloseClicked;

        /// <summary>The name field was tapped while it takes no typing (ADR 041: the name has to stay).</summary>
        public event Action LockedNameTapped;

        public bool IsAvatarPickerOpen { get; private set; }

        /// <summary>The picker's row of portraits.</summary>
        public AvatarPicker AvatarPicker => _avatarPicker;

        /// <summary>False while the saved name has to stay: the field takes no typing.</summary>
        public bool NameEditable => _nameField.interactable;

        /// <summary>The line under the field is being nudged (it shakes, or its colour is on its way back).</summary>
        public bool NameMessageNudging => _nameMessage.IsNudging;

        /// <summary>
        /// The on-screen keyboard's height in screen pixels, 0 while it is down (the presenter gives
        /// the device's; without one the screen never moves).
        /// </summary>
        public Func<float> KeyboardHeight
        {
            get => Keyboard.KeyboardHeight;
            set => Keyboard.KeyboardHeight = value;
        }

        /// <summary>How far the content is lifted to keep the name field above the keyboard, in canvas units.</summary>
        public float Lift => Keyboard.Lift;

        /// <summary>Reduce motion (GDD §14): the content moves for the keyboard at once.</summary>
        public bool ReduceMotion
        {
            get => Keyboard.ReduceMotion;
            set => Keyboard.ReduceMotion = value;
        }

        private KeyboardFollower Keyboard => _keyboard ??= new KeyboardFollower(SlideTarget, (RectTransform)_nameField.transform, () => _nameField.isFocused);

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

        /// <summary>Whether the name can be typed in (ADR 041: not while the saved name has to stay).</summary>
        public void SetNameEditable(bool editable)
        {
            if (_nameField.interactable == editable)
            {
                return;
            }

            if (!editable)
            {
                _nameField.DeactivateInputField();
            }

            _nameField.interactable = editable;
        }

        /// <summary>Draws the eye to the line under the field: it flashes and shakes (only flashes with Reduce motion).</summary>
        public void NudgeNameMessage() => _nameMessage.Nudge(ReduceMotion);

        /// <summary>Opens the avatar picker over the screen with <paramref name="chosen"/> ringed.</summary>
        public void ShowAvatarPicker(Func<int, Sprite> pictureOf, int chosen)
        {
            _avatarPicker.SetPictures(pictureOf);
            _avatarPicker.Choose(chosen);
            if (IsAvatarPickerOpen)
            {
                return;
            }

            IsAvatarPickerOpen = true;
            _nameField.DeactivateInputField();
            _avatarSheet.gameObject.SetActive(true);
            _avatarSheet.interactable = true;
            _avatarSheet.blocksRaycasts = true;
            AnimateSheet(1f);
        }

        /// <summary>Rings <paramref name="index"/> in the open picker.</summary>
        public void ChooseAvatar(int index) => _avatarPicker.Choose(index);

        public void HideAvatarPicker()
        {
            if (!IsAvatarPickerOpen)
            {
                return;
            }

            IsAvatarPickerOpen = false;
            _avatarSheet.interactable = false;
            _avatarSheet.blocksRaycasts = false;
            AnimateSheet(0f);
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
            _nameFieldTap.Tapped += OnNameFieldTapped;
            _avatarPicker.Picked += OnAvatarPicked;
            _avatarDone.onClick.AddListener(OnAvatarPickerClose);
            _avatarBackdrop.onClick.AddListener(OnAvatarPickerClose);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _avatarButton.onClick.RemoveListener(OnEditAvatar);
            _editAvatarButton.onClick.RemoveListener(OnEditAvatar);
            _saveButton.onClick.RemoveListener(OnSave);
            _nameField.onValueChanged.RemoveListener(OnNameChanged);
            _nameFieldTap.Tapped -= OnNameFieldTapped;
            _avatarPicker.Picked -= OnAvatarPicked;
            _avatarDone.onClick.RemoveListener(OnAvatarPickerClose);
            _avatarBackdrop.onClick.RemoveListener(OnAvatarPickerClose);
            _keyboard?.Settle();

            // A screen that goes away takes its picker with it, at once.
            UiMotion.Stop(ref _sheetFade);
            UiMotion.Stop(ref _sheetPop);
            IsAvatarPickerOpen = false;
            CloseSheet();
        }

        /// <summary>The content follows the keyboard, after a screen transition has placed it for the frame.</summary>
        private void LateUpdate() => Keyboard.Tick(Time.unscaledDeltaTime);

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

        private void OnNameFieldTapped()
        {
            if (!_nameField.interactable)
            {
                LockedNameTapped?.Invoke();
            }
        }

        private void OnAvatarPicked(int index)
        {
            if (IsAvatarPickerOpen)
            {
                AvatarPicked?.Invoke(index);
            }
        }

        private void OnAvatarPickerClose()
        {
            if (IsAvatarPickerOpen)
            {
                AvatarPickerCloseClicked?.Invoke();
            }
        }

        /// <summary>The dim fades and the panel pops, like the dialog's; with Reduce motion the panel only fades.</summary>
        private void AnimateSheet(float target)
        {
            UiMotion.Stop(ref _sheetFade);
            UiMotion.Stop(ref _sheetPop);
            bool closing = target <= 0f;
            _sheetFade = LMotion.Create(_avatarSheet.alpha, target, closing ? UiMotion.Short : _avatarFadeSeconds)
                .WithEase(closing ? Ease.InCubic : Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() =>
                {
                    if (closing)
                    {
                        CloseSheet();
                    }
                })
                .Bind(this, (alpha, view) => view._avatarSheet.alpha = alpha);
            _sheetPop = closing
                ? UiMotion.PopOut(_avatarPanel, null, ReduceMotion, null)
                : UiMotion.PopIn(_avatarPanel, null, ReduceMotion);
        }

        private void CloseSheet()
        {
            _avatarSheet.alpha = 0f;
            _avatarSheet.interactable = false;
            _avatarSheet.blocksRaycasts = false;
            _avatarPanel.localScale = Vector3.one;
            _avatarSheet.gameObject.SetActive(false);
        }
    }
}
