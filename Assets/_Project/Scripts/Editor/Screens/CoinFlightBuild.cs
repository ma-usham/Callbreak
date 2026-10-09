using Callbreak.Presentation.Widgets;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Coins collected (the owner, 8 Oct 2026): the <see cref="CoinFlight"/> layer of a screen with a
    /// wallet (Daily Rewards, Earn Coins). It covers the whole screen and comes last, so the coins fly
    /// over everything; the coins are the wallet's own gold spade coin (UI/Icons/Icon_Coin.png).
    /// </summary>
    internal static class CoinFlightBuild
    {
        private const string CoinSprite = "Assets/_Project/Art/UI/Icons/Icon_Coin.png";

        /// <summary>Adds the layer and binds it to the view's <c>_coinFlight</c>.</summary>
        public static CoinFlight Add(ScreenBuildContext ctx)
        {
            var layer = new GameObject("CoinFlight", typeof(RectTransform));
            var rect = (RectTransform)layer.transform;
            rect.SetParent(ctx.Root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var flight = layer.AddComponent<CoinFlight>();
            var serialized = new SerializedObject(flight);
            serialized.FindProperty("_coin").objectReferenceValue = UiBuildKit.LoadSpriteAt(CoinSprite, 0f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_coinFlight"] = flight;
            return flight;
        }
    }
}
