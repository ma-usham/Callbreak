using System;
using Callbreak.Presentation.Framework;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// One seat circle of the matchmaking screen (GDD §11 screen 05). It has both looks: filled (the
    /// avatar disc on its ivory ring) and searching (the dashed circle with a faint silhouette), and
    /// shows one of them. The name and status follow the look: the mockup sets a filled seat's name
    /// heavier and its status in green ("Ready"), a searching seat's name lighter and its status in a
    /// muted brown ("Searching"). Each look's text style is taken from a mockup label that shows it.
    /// A seat that fills while the screen is up pops its player in (ADR 023).
    /// </summary>
    public sealed class MatchmakingSeat : MonoBehaviour
    {
        [SerializeField] private GameObject _filledLook;
        [SerializeField] private GameObject _searchingLook;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private TextStyle _filledName;
        [SerializeField] private TextStyle _filledStatus;
        [SerializeField] private TextStyle _searchingName;
        [SerializeField] private TextStyle _searchingStatus;

        private MotionHandle _motion;

        /// <summary>With Reduce motion a seat that fills only fades its player in (GDD §14).</summary>
        public bool ReduceMotion { get; set; }

        /// <summary>A player sits here (the avatar disc rather than the dashed circle).</summary>
        public bool IsFilled => _filledLook.activeSelf;

        /// <summary>The line under the name: "Ready", "Searching".</summary>
        public string Status => _status.text;

        public void Show(bool filled, string playerName, string status)
        {
            if (_filledLook.activeSelf != filled)
            {
                bool fills = filled && gameObject.activeInHierarchy;
                UiMotion.Finish(ref _motion);
                _filledLook.SetActive(filled);
                if (fills)
                {
                    _motion = UiMotion.PopIn(_filledLook.transform, UiMotion.GroupOf(_filledLook), ReduceMotion, 0.8f);
                }
            }

            if (_searchingLook.activeSelf == filled)
            {
                _searchingLook.SetActive(!filled);
            }

            (filled ? _filledName : _searchingName).ApplyTo(_name);
            (filled ? _filledStatus : _searchingStatus).ApplyTo(_status);
            if (_name.text != playerName)
            {
                _name.text = playerName;
            }

            if (_status.text != status)
            {
                _status.text = status;
            }
        }

        private void OnDisable() => UiMotion.Finish(ref _motion);

        /// <summary>How one label looks in one state: typeface, size, width and colour.</summary>
        [Serializable]
        private struct TextStyle
        {
            [SerializeField] private TMP_FontAsset _font;
            [SerializeField] private Material _material;
            [SerializeField] private float _size;
            [SerializeField] private float _horizontalScale;
            [SerializeField] private Color _color;

            public void ApplyTo(TMP_Text label)
            {
                if (_font != null && label.font != _font)
                {
                    label.font = _font;
                }

                if (_material != null && label.fontSharedMaterial != _material)
                {
                    label.fontSharedMaterial = _material;
                }

                if (_size > 0f)
                {
                    label.fontSize = _size;
                    label.fontSizeMax = _size;   // a name that shrinks to fit starts from this size
                }

                if (_horizontalScale > 0f)
                {
                    label.characterHorizontalScale = _horizontalScale;
                }

                label.color = _color;
            }
        }
    }
}
