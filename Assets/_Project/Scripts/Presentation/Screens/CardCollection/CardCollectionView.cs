using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 15: choose the deck you play with. Each option shows the deck's back, its
    /// name and its status ("Equipped" / "Owned"); the chosen option shows the crimson outline and
    /// filled radio. Tapping a deck (another one, or the chosen one again) turns its picture over
    /// to the deck's ace of spades for a moment and back (<see cref="PeekFace"/>, timed by
    /// <see cref="DeckPeek"/>), so the faces can be seen before choosing. A Use button that is not
    /// interactable shows its disabled look with its label dimmed.
    /// </summary>
    public sealed class CardCollectionView : UIScreen
    {
        private const float DimmedAlpha = 0.5f;
        private const int NoDeck = -1;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _collectionButton;
        [SerializeField] private UnityEngine.UI.Button _useButton;
        [SerializeField] private TMP_Text _useLabel;
        [SerializeField] private SelectableGroup _decks;
        [SerializeField] private TMP_Text[] _deckName;
        [SerializeField] private TMP_Text[] _deckStatus;
        [SerializeField] private UnityEngine.UI.Image[] _deckPreview;

        // What turns when a deck shows its face: the picture together with its shadow.
        [SerializeField] private RectTransform[] _deckCard;

        private MotionHandle _peekMotion;
        private int _peeking = NoDeck;
        private Sprite _peekFace;
        private Sprite _peekBack;
        private bool _peekWithoutFlip;
        private float _peekTime;

        public event Action BackClicked;
        public event Action CollectionClicked;

        /// <summary>Raised when the player taps a deck option: another deck, or the chosen one again.</summary>
        public event Action<int> DeckSelected;

        public event Action UseClicked;

        public int DeckCount => _deckName.Length;

        /// <summary>Shows the selection without raising <see cref="DeckSelected"/>.</summary>
        public void SetSelected(int index)
        {
            _decks.SetSelectedWithoutNotify(index);
        }

        /// <summary>A deck's name ("Classic Crimson") and status ("Equipped" / "Owned").</summary>
        public void SetDeck(int index, string deckName, string status)
        {
            SetText(_deckName[index], deckName);
            SetText(_deckStatus[index], status);
        }

        /// <summary>
        /// The picture a deck option rests on: the deck's back. A missing sprite keeps the current
        /// picture. A deck that is showing its face keeps doing so and turns back to this picture.
        /// </summary>
        public void SetPreview(int index, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            if (index == _peeking)
            {
                bool showsBack = _deckPreview[index].sprite == _peekBack;
                _peekBack = sprite;
                if (!showsBack)
                {
                    return;
                }
            }

            _deckPreview[index].sprite = sprite;
        }

        /// <summary>
        /// Lets the player see a deck's faces: the option's picture turns over to
        /// <paramref name="face"/> (it narrows to its edge, changes side and opens again), stays
        /// for <see cref="DeckPeek.FaceHoldSeconds"/> and turns back to <paramref name="back"/>.
        /// Another deck that was still showing its face is put back on its back at once; the same
        /// deck, asked again while it peeks, keeps its face up for another hold without a jump
        /// (<see cref="DeckPeek.TimeAfterTapAgain"/>). With <paramref name="reduceMotion"/> the
        /// picture changes side without turning.
        /// </summary>
        public void PeekFace(int index, Sprite face, Sprite back, bool reduceMotion)
        {
            if (index < 0 || index >= _deckPreview.Length || face == null)
            {
                EndPeek();
                return;
            }

            bool again = index == _peeking && reduceMotion == _peekWithoutFlip;
            float from = again ? DeckPeek.TimeAfterTapAgain(_peekTime, reduceMotion) : 0f;
            if (again)
            {
                StopPeekMotion();
            }
            else
            {
                EndPeek();
                _peeking = index;
                _peekWithoutFlip = reduceMotion;
                _peekBack = _deckPreview[index].sprite;
            }

            _peekFace = face;
            if (back != null)
            {
                _peekBack = back;
            }

            ApplyPeek(from);

            float seconds = DeckPeek.Seconds(reduceMotion);
            _peekMotion = LMotion.Create(from, seconds, seconds - from)
                .WithOnComplete(OnPeekComplete)
                .Bind(this, (time, view) => view.ApplyPeek(time));
        }

        public void SetUseInteractable(bool interactable)
        {
            _useButton.interactable = interactable;
            SetAlpha(_useLabel, interactable ? 1f : DimmedAlpha);
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _collectionButton.onClick.AddListener(OnCollection);
            _useButton.onClick.AddListener(OnUse);
            _decks.Tapped += OnDeckSelected;
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _collectionButton.onClick.RemoveListener(OnCollection);
            _useButton.onClick.RemoveListener(OnUse);
            _decks.Tapped -= OnDeckSelected;

            // A screen that opens again shows every deck by its back.
            EndPeek();
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

        private void ApplyPeek(float time)
        {
            if (_peeking == NoDeck)
            {
                return;
            }

            _peekTime = time;
            DeckPeek.Pose(time, _peekWithoutFlip, out bool showsFace, out float width);
            Sprite side = showsFace ? _peekFace : _peekBack;
            UnityEngine.UI.Image picture = _deckPreview[_peeking];
            if (picture.sprite != side)
            {
                picture.sprite = side;
            }

            _deckCard[_peeking].localScale = new Vector3(width, 1f, 1f);
        }

        private void OnPeekComplete()
        {
            // This runs inside the motion's own completion: forget the handle rather than cancel it.
            _peekMotion = default;
            RestOnBack();
        }

        /// <summary>Stops a peek where it is and puts that deck back on its back.</summary>
        private void EndPeek()
        {
            StopPeekMotion();
            RestOnBack();
        }

        private void StopPeekMotion()
        {
            if (_peekMotion.IsActive())
            {
                _peekMotion.Cancel();
            }

            _peekMotion = default;
        }

        private void RestOnBack()
        {
            if (_peeking == NoDeck)
            {
                return;
            }

            // Also reached while the screen is being torn down, when its children may be gone already.
            UnityEngine.UI.Image picture = _deckPreview[_peeking];
            if (picture != null)
            {
                picture.sprite = _peekBack;
            }

            RectTransform card = _deckCard[_peeking];
            if (card != null)
            {
                card.localScale = Vector3.one;
            }

            _peeking = NoDeck;
            _peekFace = null;
            _peekBack = null;
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnCollection() => CollectionClicked?.Invoke();

        private void OnUse() => UseClicked?.Invoke();

        private void OnDeckSelected(int index) => DeckSelected?.Invoke(index);
    }
}
