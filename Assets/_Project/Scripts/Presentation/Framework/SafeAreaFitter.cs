using UnityEngine;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Keeps a HUD root inside the device safe area (notches, rounded corners), GDD §12. Put it on
    /// a full-stretch RectTransform that holds the corner-anchored design groups; backgrounds stay
    /// outside it so they still bleed to the screen edge.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (_applied != Screen.safeArea || _screen.x != Screen.width || _screen.y != Screen.height)
            {
                Apply();
            }
        }

        private void Apply()
        {
            Rect safe = Screen.safeArea;
            _applied = safe;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
