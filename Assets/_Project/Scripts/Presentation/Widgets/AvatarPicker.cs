using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// The avatar presets side by side to pick from (ADR 041), in the name question at the start and
    /// in the Profile screen's picker. Each choice is a portrait on the white disc every avatar sits
    /// on; the chosen one is framed by the kit's crimson "selected" disc and wears its check badge.
    /// A tap raises <see cref="Picked"/>; the presenter saves the pick and calls <see cref="Choose"/>.
    /// </summary>
    public sealed class AvatarPicker : MonoBehaviour
    {
        [SerializeField] private Button[] _choices;
        [SerializeField] private Image[] _pictures;

        // The crimson disc behind a chosen portrait, and the check badge over it.
        [SerializeField] private GameObject[] _selected;
        [SerializeField] private GameObject[] _checks;

        private UnityAction[] _handlers;

        /// <summary>A choice was tapped: its preset index.</summary>
        public event Action<int> Picked;

        public int Count => _choices != null ? _choices.Length : 0;

        /// <summary>The preset shown as chosen; -1 when none is.</summary>
        public int Chosen { get; private set; } = -1;

        /// <summary>The portraits by preset index; a null keeps the picture a choice has.</summary>
        public void SetPictures(Func<int, Sprite> pictureOf)
        {
            for (int i = 0; i < _pictures.Length; i++)
            {
                Sprite picture = pictureOf(i);
                if (picture != null && _pictures[i].sprite != picture)
                {
                    _pictures[i].sprite = picture;
                }
            }
        }

        /// <summary>Shows <paramref name="index"/> as chosen; an index outside the presets shows none.</summary>
        public void Choose(int index)
        {
            Chosen = index >= 0 && index < Count ? index : -1;
            for (int i = 0; i < Count; i++)
            {
                Show(_selected[i], i == Chosen);
                Show(_checks[i], i == Chosen);
            }
        }

        /// <summary>The button of choice <paramref name="index"/> (tests and tools press it).</summary>
        public Button ChoiceAt(int index) => _choices[index];

        private static void Show(GameObject part, bool on)
        {
            if (part.activeSelf != on)
            {
                part.SetActive(on);
            }
        }

        private void OnEnable()
        {
            _handlers ??= new UnityAction[_choices.Length];
            for (int i = 0; i < _choices.Length; i++)
            {
                int index = i;
                _handlers[i] ??= () => Picked?.Invoke(index);
                _choices[i].onClick.AddListener(_handlers[i]);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _choices.Length; i++)
            {
                _choices[i].onClick.RemoveListener(_handlers[i]);
            }
        }
    }
}
