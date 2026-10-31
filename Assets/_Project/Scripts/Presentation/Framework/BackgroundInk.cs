using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Table;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// The words and lines a menu draws straight on the equipped background (ADR 064): in their own
    /// colours on a day scene, and light on a night scene (Tihar Lights, Phewa Moonlight), where the
    /// dark ones would sink into the sky. The builder lists them with their day colours and a light
    /// cream for the night; a title drawn in a gradient by day is drawn plain cream at night. Like
    /// <see cref="EquippedBackground"/>, it shows the day colours until <see cref="ICosmeticsShop"/> is
    /// injected, and follows the player's choice.
    /// </summary>
    public sealed class BackgroundInk : MonoBehaviour
    {
        [SerializeField] private TableBackdropLibrary _library;
        [SerializeField] private Graphic[] _targets = new Graphic[0];
        [SerializeField] private Color[] _day = new Color[0];
        [SerializeField] private Color[] _night = new Color[0];
        [SerializeField] private bool[] _dayGradient = new bool[0];

        private ICosmeticsShop _shop;

        /// <summary>Whether the words show their night colours.</summary>
        public bool ShowsNight { get; private set; }

        [Inject]
        public void Construct(ICosmeticsShop shop)
        {
            _shop = shop;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        /// <summary>The words and lines it colours.</summary>
        public Graphic[] Targets => _targets;

        /// <summary>The words and lines it colours, their day and night colours, and which words have a day gradient (editor builders only).</summary>
        public void Set(TableBackdropLibrary library, Graphic[] targets, Color[] day, Color[] night, bool[] dayGradient)
        {
            _library = library;
            _targets = targets;
            _day = day;
            _night = night;
            _dayGradient = dayGradient;
        }

        /// <summary>Shows the day or the night colours (the shop's preview pictures and tests show either at will).</summary>
        public void Show(bool night)
        {
            ShowsNight = night;
            Color[] colours = night ? _night : _day;
            for (int i = 0; i < _targets.Length; i++)
            {
                Graphic target = _targets[i];
                if (target == null || i >= colours.Length)
                {
                    continue;
                }

                if (i < _dayGradient.Length && _dayGradient[i] && target is TMP_Text text && text.enableVertexGradient == night)
                {
                    text.enableVertexGradient = !night;
                }

                if (target.color != colours[i])
                {
                    target.color = colours[i];
                }
            }
        }

        private void OnEnable()
        {
            if (_shop != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            if (_shop != null)
            {
                _shop.Changed -= Refresh;
            }
        }

        private void Subscribe()
        {
            _shop.Changed -= Refresh;
            _shop.Changed += Refresh;
            Refresh();
        }

        private void Refresh() => Show(_library != null && _library.IsNightScene(_shop.Equipped(CosmeticKind.Background)));
    }
}
