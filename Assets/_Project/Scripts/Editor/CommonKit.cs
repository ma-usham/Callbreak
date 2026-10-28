using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Import settings for the shared UI kit's older pieces (Assets/_Project/Art/UI/Common):
    /// pixels per unit and 9-slice borders, so every screen can reuse the same pieces at any size.
    /// Screens refer to these sprites with "use" in their layout. Borders are (left, bottom, right,
    /// top) in sprite pixels. The V2 panels, buttons and controls are set by KitImporter from
    /// ArtSource/Kit/V2.json.
    /// </summary>
    public static class CommonKit
    {
        /// <summary>A sprite cut at mockup resolution shows at its mockup size on the 1080p canvas.</summary>
        public const float Ppu1x = 100f * 1672f / 1920f;

        /// <summary>Cut at twice the mockup resolution.</summary>
        public const float Ppu2x = 2f * Ppu1x;

        /// <summary>The main menu's @2x exports (Designer Handbook recipe E).</summary>
        public const float PpuMenu = 200f;

        // The V2 kit's shadows (Tools/ArtPipeline/V2/kit.json, rendered by kit.py), in canvas units:
        // how far a shadow sprite reaches beyond the face it was rendered for, and how far below the
        // face it lies. Their colour and strength are in the sprites, so they are drawn at full opacity.

        /// <summary>UI/Common/UI_Panel_Large_Shadow.png around a panel, sliced at its native size.</summary>
        public const float PanelShadowSpread = 62f;

        public const float PanelShadowDrop = 12f;

        /// <summary>UI/Buttons/UI_Shadow_Circle.png reaches this far around a disc of <see cref="CircleShadowDiameter"/>.</summary>
        public const float CircleShadowSpread = 17f;

        public const float CircleShadowDiameter = 96f;

        public const float CircleShadowDrop = 4f;

        /// <summary>UI/Buttons/UI_Shadow_Secondary.png under the ivory pill (and the box's shadow under the box).</summary>
        public const float SecondaryShadowDrop = 4f;

        /// <summary>UI/Controls/UI_Button_Crimson_Shadow.png (and _Box_Shadow) under the crimson button.</summary>
        public const float HeroShadowDrop = 8f;

        [MenuItem("Callbreak/Build/Common Kit Import Settings")]
        public static void Apply()
        {
            Set("UI/Common/UI_Panel_Large.png", Ppu1x, new Vector4(160, 110, 160, 170));
            Set("UI/Common/UI_Panel_Card.png", Ppu1x, new Vector4(26, 26, 26, 26));
            Set("UI/Common/UI_Panel_Card_Shadow.png", Ppu1x, new Vector4(35, 35, 35, 35));
            Set("UI/Common/Deco_TitleDivider.png", Ppu2x, Vector4.zero);
            Set("UI/Common/Icon_Back.png", Ppu2x, Vector4.zero);
            Set("UI/Common/Icon_Home.png", Ppu2x, Vector4.zero);
            Set("UI/Common/Icon_Scorecard.png", Ppu2x, Vector4.zero);
            Set("UI/Common/Icon_Close.png", Ppu2x, Vector4.zero);
            Set("UI/Common/Icon_Chevron_Light.png", PpuMenu, Vector4.zero);
            Set("UI/Common/Logo_Callbreak_NoFlag.png", PpuMenu, Vector4.zero);

            // The panels, the buttons and their shadows are the V2 kit's: ArtSource/Kit/V2.json
            // (KitImporter) sets them.
            AssetDatabase.SaveAssets();
            Debug.Log("[CommonKit] import settings applied");
        }

        private static void Set(string relative, float ppu, Vector4 border)
        {
            UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{relative}", ppu, border);
        }
    }
}
