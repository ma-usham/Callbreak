using System;
using Callbreak.Presentation.Framework;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// Screen 22, Earn Coins (mockup 22; the designer's rewarded-coins-prompts.md): three reward
    /// blocks linked by gold lines, the progress, the crimson Watch ad button and the reset line,
    /// with the wallet at the top right. The block the next ad pays wears the crimson look; a block
    /// whose ad was watched carries a check. Renders what its presenter gives it and reports Back
    /// and Watch ad. A button that is not interactable shows its disabled look with its label and
    /// play triangle dimmed. Without the play triangle (every ad watched) the label is centred on
    /// the button, and the clock stays beside the reset line's text whatever its length.
    /// </summary>
    public sealed class EarnCoinsView : UIScreen
    {
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _watchButton;
        [SerializeField] private TMP_Text _balance;
        [SerializeField] private TMP_Text[] _adTitle;
        [SerializeField] private TMP_Text[] _amount;

        // Each block's crimson look (the next ad's block) and its check (its ad was watched).
        [SerializeField] private GameObject[] _nextLook;
        [SerializeField] private GameObject[] _watchedBadge;

        [SerializeField] private TMP_Text _progress;
        [SerializeField] private TMP_Text _watchLabel;
        [SerializeField] private UnityEngine.UI.Image _playIcon;
        [SerializeField] private TMP_Text _resetLabel;
        [SerializeField] private RectTransform _clockIcon;

        // As built (read before the first change): the watch label's place beside the play
        // triangle, and the reset line's width with the clock's place beside that text.
        private bool _designRead;
        private Vector2 _watchLabelPosition;
        private float _resetDesignWidth;
        private float _clockDesignX;

        public event Action BackClicked;
        public event Action WatchClicked;

        public int BlockCount => _adTitle.Length;

        public void SetBalance(string coins) => SetText(_balance, coins);

        /// <summary>One reward block: its title ("Ad 1"), its amount ("20 coins") and its state.</summary>
        public void SetBlock(int index, string title, string amount, bool next, bool watched)
        {
            SetText(_adTitle[index], title);
            SetText(_amount[index], amount);
            SetActive(_nextLook[index], next);
            SetActive(_watchedBadge[index], watched);
        }

        public void SetProgress(string progress) => SetText(_progress, progress);

        /// <summary>
        /// The Watch ad button: its label, whether the play triangle shows, and whether it takes a
        /// tap (not while an ad is playing, nor once every ad of the set has been watched).
        /// </summary>
        public void SetWatch(string label, bool showPlay, bool interactable)
        {
            ReadDesign();
            SetText(_watchLabel, label);
            if (_playIcon.gameObject.activeSelf != showPlay)
            {
                _playIcon.gameObject.SetActive(showPlay);
            }

            // Beside the triangle as on the mockup; alone, in the middle of the button.
            RectTransform labelRect = _watchLabel.rectTransform;
            Vector2 position = showPlay ? _watchLabelPosition : new Vector2(0f, _watchLabelPosition.y);
            if (labelRect.anchoredPosition != position)
            {
                labelRect.anchoredPosition = position;
            }

            _watchButton.interactable = interactable;
            float alpha = interactable ? 1f : DimmedAlpha;
            SetAlpha(_watchLabel, alpha);
            SetAlpha(_playIcon, alpha);
        }

        /// <summary>
        /// The line under the button ("Rewards reset every 5 hours", or the time left). The text stays
        /// centred where it is; the clock moves with its left edge, so the two stay together.
        /// </summary>
        public void SetReset(string text)
        {
            ReadDesign();
            SetText(_resetLabel, text);
            float x = _clockDesignX - ((TextWidth(_resetLabel) - _resetDesignWidth) / 2f);
            if (!Mathf.Approximately(_clockIcon.anchoredPosition.x, x))
            {
                _clockIcon.anchoredPosition = new Vector2(x, _clockIcon.anchoredPosition.y);
            }
        }

        /// <summary>The width of a label's text as drawn, also while the screen is hidden.</summary>
        private static float TextWidth(TMP_Text label)
        {
            label.ForceMeshUpdate(true, true);
            return label.textInfo != null && label.textInfo.characterCount > 0 ? label.textBounds.size.x : 0f;
        }

        /// <summary>Reads the places the screen was built with, once, before anything has moved.</summary>
        private void ReadDesign()
        {
            if (_designRead)
            {
                return;
            }

            _designRead = true;
            _watchLabelPosition = _watchLabel.rectTransform.anchoredPosition;
            _resetDesignWidth = TextWidth(_resetLabel);
            _clockDesignX = _clockIcon.anchoredPosition.x;
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _watchButton.onClick.AddListener(OnWatch);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _watchButton.onClick.RemoveListener(OnWatch);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void SetAlpha(UnityEngine.UI.Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            if (!Mathf.Approximately(color.a, alpha))
            {
                color.a = alpha;
                graphic.color = color;
            }
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnWatch() => WatchClicked?.Invoke();
    }
}
