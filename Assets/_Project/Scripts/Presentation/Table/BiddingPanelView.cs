using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// GDD §11 screen 07, "Make your call": the call panel in the middle of the table. It shows only
    /// on the local seat's call turn: chips from the rule set's lowest to highest call with nothing
    /// chosen, and a Confirm button that stays disabled until a chip is chosen. Once the call is
    /// placed the panel slides away. Before that turn the centre of the table reads "Asha is
    /// calling…". A call that is refused puts its reason where the subtitle is. With the beginner
    /// assist on, a faint dot marks the suggested call. The table itself (HUD, seats, hand) is the
    /// <see cref="TableView"/> underneath.
    /// </summary>
    public sealed class BiddingPanelView : UIScreen
    {
        // GDD §14 "Screen change": fade and a 24 px slide, 220 ms OutCubic; fade only with Reduce motion.
        private const float SlideSeconds = 0.22f;
        private const float SlideDistance = 24f;

        [SerializeField] private GameObject _panel;
        [SerializeField] private CanvasGroup _panelGroup;
        [SerializeField] private BidChipRow _chips;
        [SerializeField] private RectTransform[] _chip = Array.Empty<RectTransform>();
        [SerializeField] private RectTransform _hint;
        [SerializeField] private UnityEngine.UI.Button _confirmButton;
        [SerializeField] private TMP_Text _confirmLabel;
        [SerializeField] private GameObject _subtitle;
        [SerializeField] private TMP_Text _reason;
        [SerializeField] private TMP_Text _callingText;

        private MotionHandle _panelMotion;
        private Vector2 _restPosition;
        private float _slideDistance;
        private bool _leaving;
        private int _minCall = 1;
        private int _hintedCall;

        /// <summary>A chip was tapped: the call it stands for.</summary>
        public event Action<int> CallChosen;

        /// <summary>Confirm was pressed; the chosen call is <see cref="SelectedCall"/>.</summary>
        public event Action ConfirmClicked;

        /// <summary>The chosen call, or 0.</summary>
        public int SelectedCall => _chips.Selected;

        /// <summary>The panel is up and takes input (not hidden, and not on its way out).</summary>
        public bool IsPanelShown => _panel.activeSelf && !_leaving;

        /// <summary>The refusal line while it shows; otherwise null.</summary>
        public string Reason => _reason != null && _reason.gameObject.activeSelf ? _reason.text : null;

        /// <summary>The call the beginner assist's dot is on; 0 while it is hidden.</summary>
        public int HintedCall => _hint != null && _hint.gameObject.activeSelf ? _hintedCall : 0;

        /// <summary>The chips to offer: RuleSet.MinCall to RuleSet.MaxCall.</summary>
        public void SetCallRange(int minCall, int maxCall)
        {
            _minCall = minCall;
            _chips.SetRange(minCall, maxCall);
        }

        /// <summary>Shows the panel for the local seat's call turn, or hides it, at once.</summary>
        public void ShowPanel(bool show)
        {
            StopSlide();
            if (_panel.activeSelf != show)
            {
                _panel.SetActive(show);
            }
        }

        /// <summary>
        /// The call has been placed: the panel slides away (GDD screen 07) and takes no more
        /// touches while it goes. With Reduce motion it only fades.
        /// </summary>
        public void SlideAway(bool reduceMotion)
        {
            if (!_panel.activeSelf || _leaving)
            {
                return;
            }

            // (A panel built before it could slide has no group to fade: it just goes.)
            if (!gameObject.activeInHierarchy || _panelGroup == null)
            {
                _panel.SetActive(false);
                return;
            }

            _leaving = true;
            _restPosition = ((RectTransform)_panel.transform).anchoredPosition;
            _slideDistance = reduceMotion ? 0f : SlideDistance;
            _panelGroup.blocksRaycasts = false;
            _panelMotion = LMotion.Create(0f, 1f, SlideSeconds)
                .WithEase(Ease.OutCubic)
                .WithOnComplete(EndSlide)
                .Bind(this, (t, view) => view.ApplySlide(t));
        }

        /// <summary>The chosen chip (0 for none).</summary>
        public void SetSelected(int call)
        {
            _chips.Select(call);
        }

        /// <summary>"Confirm" or "Confirm 4", and whether it can be pressed.</summary>
        public void SetConfirm(string label, bool enabled)
        {
            label ??= string.Empty;
            if (_confirmLabel.text != label)
            {
                _confirmLabel.text = label;
            }

            if (_confirmButton.interactable != enabled)
            {
                _confirmButton.interactable = enabled;
            }
        }

        /// <summary>False once the call has been sent: the choice is final (GDD screen 07, callout 7).</summary>
        public void SetChipsEnabled(bool enabled)
        {
            _chips.SetInteractable(enabled);
        }

        /// <summary>
        /// Why the call was not taken (GDD screen 07 "On rejection, the reason is shown"), in place
        /// of the subtitle; null or empty brings the subtitle back.
        /// </summary>
        public void SetReason(string text)
        {
            if (_reason == null || _subtitle == null)
            {
                return;
            }

            bool show = !string.IsNullOrEmpty(text);
            if (_reason.gameObject.activeSelf != show)
            {
                _reason.gameObject.SetActive(show);
            }

            if (_subtitle.activeSelf == show)
            {
                _subtitle.SetActive(!show);
            }

            if (show && _reason.text != text)
            {
                _reason.text = text;
            }
        }

        /// <summary>
        /// The beginner assist (GDD screen 07): a faint dot on the chip of the suggested call; 0,
        /// or a call the row does not offer, hides it.
        /// </summary>
        public void SetHint(int call)
        {
            if (_hint == null)
            {
                return;
            }

            int index = call - _minCall;
            bool show = call != 0 && index >= 0 && index < _chip.Length && _chip[index].gameObject.activeSelf;
            if (_hint.gameObject.activeSelf != show)
            {
                _hint.gameObject.SetActive(show);
            }

            if (show)
            {
                // Built under one chip at the right height; it only moves along the row.
                _hintedCall = call;
                _hint.anchoredPosition = new Vector2(_chip[index].anchoredPosition.x, _hint.anchoredPosition.y);
            }
        }

        /// <summary>"Asha is calling…" in the middle of the table; null or empty hides it.</summary>
        public void SetCalling(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (_callingText.gameObject.activeSelf != show)
            {
                _callingText.gameObject.SetActive(show);
            }

            if (show && _callingText.text != text)
            {
                _callingText.text = text;
            }
        }

        private void ApplySlide(float t)
        {
            _panelGroup.alpha = 1f - t;
            ((RectTransform)_panel.transform).anchoredPosition = _restPosition + new Vector2(0f, -_slideDistance * t);
        }

        private void EndSlide()
        {
            _panelMotion = default;
            RestorePanel();
            _panel.SetActive(false);
        }

        private void StopSlide()
        {
            if (_panelMotion.IsActive())
            {
                _panelMotion.Cancel();
            }

            _panelMotion = default;
            RestorePanel();
        }

        /// <summary>Puts the panel back as it is shown: in place, opaque and touchable.</summary>
        private void RestorePanel()
        {
            if (!_leaving)
            {
                return;
            }

            _leaving = false;

            // When the scene unloads the panel may already be gone.
            if (_panelGroup != null)
            {
                _panelGroup.alpha = 1f;
                _panelGroup.blocksRaycasts = true;
            }

            if (_panel != null)
            {
                ((RectTransform)_panel.transform).anchoredPosition = _restPosition;
            }
        }

        private void OnEnable()
        {
            _chips.Chosen += OnChosen;
            _confirmButton.onClick.AddListener(OnConfirm);
        }

        private void OnDisable()
        {
            _chips.Chosen -= OnChosen;
            _confirmButton.onClick.RemoveListener(OnConfirm);
            StopSlide();
        }

        private void OnChosen(int call) => CallChosen?.Invoke(call);

        private void OnConfirm() => ConfirmClicked?.Invoke();
    }
}
