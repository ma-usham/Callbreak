using System;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// An on/off switch drawn by two complete images (GDD §12: crimson for "on"). Tapping flips it
    /// and raises <see cref="Changed"/>; presenters set it without an event.
    /// </summary>
    public sealed class ToggleSwitch : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button _button;
        [SerializeField] private GameObject _onGraphic;
        [SerializeField] private GameObject _offGraphic;
        [SerializeField] private bool _isOn = true;

        public event Action<bool> Changed;

        public bool IsOn => _isOn;

        public bool Interactable
        {
            get => _button.interactable;
            set => _button.interactable = value;
        }

        public void SetIsOnWithoutNotify(bool on)
        {
            _isOn = on;
            Render();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(OnClick);
            Render();
        }

        private void OnDisable() => _button.onClick.RemoveListener(OnClick);

        private void OnClick()
        {
            _isOn = !_isOn;
            Render();
            Changed?.Invoke(_isOn);
        }

        private void Render()
        {
            if (_onGraphic != null) _onGraphic.SetActive(_isOn);
            if (_offGraphic != null) _offGraphic.SetActive(!_isOn);
        }
    }
}
