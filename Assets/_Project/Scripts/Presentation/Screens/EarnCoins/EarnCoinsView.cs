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
    /// the button. The clock and the reset line are one group centred in the panel, the clock close
    /// before the first letter whatever the text (<see cref="CentreClockLine"/>).
    /// </summary>
    public sealed class EarnCoinsView : UIScreen
    {
        /// <summary>
        /// Canvas units between the clock's box and the reset line's first letter (the owner, 5 Oct
        /// 2026: "bring it near"). The clock sprite keeps about 2 units clear inside its box, so the
        /// two inks stand about 7 apart, under a quarter of the clock's width.
        /// </summary>
        public const float ClockGap = 5f;

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

        // As built (read before the first change): the watch label's place beside the play triangle.
        private bool _designRead;
        private Vector2 _watchLabelPosition;

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

        /// <summary>The line under the button ("Rewards reset every 5 hours", or the time left), with its clock.</summary>
        public void SetReset(string text)
        {
            SetText(_resetLabel, text);
            CentreClockLine(_resetLabel, _clockIcon);
        }

        /// <summary>
        /// Lays a clock and its line out as one group centred in their common parent: the clock's box,
        /// <see cref="ClockGap"/>, then the letters as drawn (measured, so any text and any language
        /// fit tight), the clock centred on the capitals. Both are anchored to the parent's middle, as
        /// the screen builder places every node, and positions are in the parent's units, which the
        /// canvas scales with the design area, so the gap is the same at every aspect ratio. The view
        /// calls this whenever the line changes; the screen builder calls it for the built prefab.
        /// </summary>
        public static void CentreClockLine(TMP_Text label, RectTransform clock)
        {
            if (!InkSpan(label, out float left, out float right, out TMP_CharacterInfo first))
            {
                return;
            }

            RectTransform labelRect = label.rectTransform;
            float clockWidth = clock.rect.width;
            float groupWidth = clockWidth + ClockGap + (right - left);
            float groupLeft = -groupWidth / 2f;
            float labelX = groupLeft + clockWidth + ClockGap - left;
            if (!Mathf.Approximately(labelRect.anchoredPosition.x, labelX))
            {
                labelRect.anchoredPosition = new Vector2(labelX, labelRect.anchoredPosition.y);
            }

            // The middle of the capitals: the line's baseline plus half the font's cap height.
            float capsMiddle = labelRect.anchoredPosition.y + first.baseLine + (label.font.faceInfo.capLine * first.scale / 2f);
            var clockPosition = new Vector2(groupLeft + (clockWidth / 2f), capsMiddle);
            if (clock.anchoredPosition != clockPosition)
            {
                clock.anchoredPosition = clockPosition;
            }
        }

        /// <summary>
        /// The left and right edges of a label's letters as drawn, in its own space, also while the
        /// screen is hidden (TextMeshPro's character quads carry the distance field's padding, which is
        /// taken off), and its first visible character. False for a label with nothing to draw.
        /// </summary>
        private static bool InkSpan(TMP_Text label, out float left, out float right, out TMP_CharacterInfo first)
        {
            label.ForceMeshUpdate(true, true);
            TMP_TextInfo info = label.textInfo;
            left = float.MaxValue;
            right = float.MinValue;
            first = default;
            bool found = false;
            for (int i = 0; info != null && i < info.characterCount; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible || c.textElement == null || c.textElement.glyph == null)
                {
                    continue;
                }

                float glyphWidth = c.textElement.glyph.metrics.width * c.scale * label.characterHorizontalScale;
                float padding = Mathf.Max(0f, (c.topRight.x - c.bottomLeft.x - glyphWidth) / 2f);
                left = Mathf.Min(left, c.bottomLeft.x + padding);
                right = Mathf.Max(right, c.topRight.x - padding);
                if (!found)
                {
                    first = c;
                    found = true;
                }
            }

            return found;
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
