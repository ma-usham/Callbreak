using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 12, in V2's type. The rules' two cards are the deck's own, turned as V2 draws them
    /// (<see cref="RuleCards"/>). For the first launch (ADR 022) the view gets the
    /// rules panel, which it hides while the player's name is asked, and a dim between the logo and
    /// the panel, the name question's own, for the rules shown that first time.
    /// </summary>
    public static class HowToPlayCustomizer
    {
        // The dialog's dim (DialogBuilder), measured on mockup 13; the name question uses it too.
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.817f);

        // Far enough past the design area to cover a 21:9 or 4:3 screen as well.
        private const float DimOutset = 2400f;

        // V2 (12 How to Play): the rules' two cards are the default deck's own, the ace of spades over
        // the king of hearts, each turned as V2 turns it: its centre (mockup px) and turn (degrees,
        // anticlockwise), at V2's card size (canvas units).
        private static readonly Vector2 CardSize = new Vector2(177.7f, 278.8f);
        private static readonly (Suit Suit, Rank Rank, Vector2 Centre, float Turn)[] RuleCards =
        {
            (Suit.Hearts, Rank.King, new Vector2(1179.1f, 531.9f), 10f),
            (Suit.Spades, Rank.Ace, new Vector2(1118f, 531.9f), -8f),
        };

        // The cards' shadow, as the main menu's fan casts it (canvas units).
        private const string CardShadowPath = "Assets/_Project/Art/UI/Common/UI_Panel_Card_Shadow.png";
        private const float CardShadowSpread = 9f;
        private const float CardShadowDrop = 3f;
        private const float CardShadowOpacity = 0.4f;
        private const float CardShadowSliceScale = 2.6f;

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);
            BuildRuleCards(ctx);
            if (!ctx.Nodes.TryGetValue("Panel", out RectTransform panel))
            {
                ctx.Warnings.Add("Panel: node missing");
                return;
            }

            ctx.Bound["_panel"] = panel.gameObject;

            // Over the background and the logo, under the rules.
            var dimObject = new GameObject("Dim", typeof(RectTransform));
            var dim = (RectTransform)dimObject.transform;
            dim.SetParent(panel.parent, false);
            dim.SetSiblingIndex(panel.GetSiblingIndex());
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = new Vector2(-DimOutset, -DimOutset);
            dim.offsetMax = new Vector2(DimOutset, DimOutset);
            Image image = dimObject.AddComponent<Image>();
            image.color = Dim;
            image.raycastTarget = false;
            dimObject.SetActive(false);
            ctx.Bound["_dim"] = dimObject;
        }

        /// <summary>The rules' two cards (see <see cref="RuleCards"/>) in the CardsArt node, each its deck's base and face over a soft shadow.</summary>
        private static void BuildRuleCards(ScreenBuildContext ctx)
        {
            if (!ctx.Nodes.TryGetValue("CardsArt", out RectTransform art))
            {
                ctx.Warnings.Add("CardsArt: node missing; no rule cards");
                return;
            }

            CardSpriteProvider cards = TableCardBuilder.EnsureLibrary();
            Sprite shadow = UiBuildKit.LoadSpriteAt(CardShadowPath, 0f);
            Vector2 artCentre = ctx.Node("CardsArt").Centre;
            foreach ((Suit suit, Rank rank, Vector2 centre, float turn) in RuleCards)
            {
                RectTransform card = UiBuildKit.CreateAt(rank == Rank.Ace ? "AceCard" : "KingCard", art, centre, artCentre, CardSize);
                card.localRotation = Quaternion.Euler(0f, 0f, turn);

                RectTransform shadowRect = Stretched("Shadow", card);
                shadowRect.offsetMin = new Vector2(-CardShadowSpread, -CardShadowSpread - CardShadowDrop);
                shadowRect.offsetMax = new Vector2(CardShadowSpread, CardShadowSpread - CardShadowDrop);
                Image shadowImage = UiBuildKit.AddImage(shadowRect, shadow, false);
                shadowImage.color = new Color(1f, 1f, 1f, CardShadowOpacity);
                shadowImage.pixelsPerUnitMultiplier = CardShadowSliceScale;

                UiBuildKit.AddImage(Stretched("Base", card), cards.Base(cards.DefaultDeckId), false);
                UiBuildKit.AddImage(Stretched("Face", card), cards.Face(cards.DefaultDeckId, new Card(suit, rank)), false);
            }
        }

        private static RectTransform Stretched(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
