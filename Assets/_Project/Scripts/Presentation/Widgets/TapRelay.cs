using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// Raises <see cref="Tapped"/> when its object is tapped, whether or not a Selectable on the
    /// same object takes the tap: the Profile screen's name field, locked while a saved name has
    /// to stay (ADR 041), still answers a tap by pointing at the line under it.
    /// </summary>
    public sealed class TapRelay : MonoBehaviour, IPointerClickHandler
    {
        public event Action Tapped;

        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke();
    }
}
