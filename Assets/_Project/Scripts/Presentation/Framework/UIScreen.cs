using System;
using UnityEngine;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Base of every screen view (GDD §23). A screen is one prefab: its own background plate, its
    /// design groups and its widgets. The <see cref="ScreenRouter"/> shows and hides screens; the
    /// view only raises intent events and renders what its presenter gives it.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [SerializeField] private ScreenId _screenId = ScreenId.None;

        // Everything but the background: screens that share a background slide only this, so the
        // scenery stays still while the content moves.
        [SerializeField] private RectTransform _content;

        private CanvasGroup _canvasGroup;
        private RectTransform _rect;

        public ScreenId Id => _screenId;

        public CanvasGroup CanvasGroup => _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();

        public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        /// <summary>What the screen transition slides: the content, or the whole screen.</summary>
        public RectTransform SlideTarget => _content != null ? _content : Rect;

        /// <summary>
        /// Raised when Android back (or Escape) reaches this screen while it is on top. With no
        /// subscriber the router simply pops the screen.
        /// </summary>
        public event Action BackRequested;

        /// <summary>Called by the router; returns true when a presenter handled back itself.</summary>
        public bool HandleBack()
        {
            if (BackRequested == null)
            {
                return false;
            }

            BackRequested.Invoke();
            return true;
        }

        /// <summary>Called when the screen becomes the visible top screen.</summary>
        public virtual void OnShown()
        {
        }

        /// <summary>Called when the screen stops being visible.</summary>
        public virtual void OnHidden()
        {
        }
    }
}
