using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Import settings for the shared UI kit (Assets/_Project/Art/UI/Common and the main menu's
    /// buttons): pixels per unit and 9-slice borders, so every screen can reuse the same panels,
    /// buttons and icons at any size. Screens refer to these sprites with "use" in their layout.
    /// Borders are (left, bottom, right, top) in sprite pixels.
    /// </summary>
    public static class CommonKit
    {
        /// <summary>A sprite cut at mockup resolution shows at its mockup size on the 1080p canvas.</summary>
        public const float Ppu1x = 100f * 1672f / 1920f;

        /// <summary>Cut at twice the mockup resolution.</summary>
        public const float Ppu2x = 2f * Ppu1x;

        /// <summary>The main menu's @2x exports (Designer Handbook recipe E).</summary>
        public const float PpuMenu = 200f;

        [MenuItem("Callbreak/Build/Common Kit Import Settings")]
        public static void Apply()
        {
            Set("UI/Common/UI_Panel_Large.png", Ppu1x, new Vector4(160, 110, 160, 170));
            Set("UI/Common/UI_Panel_Large_Shadow.png", Ppu1x, new Vector4(52, 52, 52, 52));
            Set("UI/Common/UI_Panel_Card.png", Ppu1x, new Vector4(26, 26, 26, 26));
            Set("UI/Common/UI_Panel_Card_Shadow.png", Ppu1x, new Vector4(35, 35, 35, 35));
            Set("UI/Common/Deco_TitleDivider.png", Ppu2x, Vector4.zero);
            Set("UI/Common/Icon_Back.png", Ppu2x, Vector4.zero);
            Set("UI/Common/Icon_Home.png", Ppu2x, Vector4.zero);
            Set("UI/Common/Icon_Scorecard.png", Ppu2x, Vector4.zero);
            Set("UI/Common/Icon_Close.png", Ppu2x, Vector4.zero);
            Set("UI/Common/Icon_Chevron_Light.png", PpuMenu, Vector4.zero);
            Set("UI/Common/Logo_Callbreak_NoFlag.png", PpuMenu, Vector4.zero);

            // The main menu's pieces, reused everywhere (their borders were set by the main menu build).
            Set("UI/Buttons/UI_ButtonPrimary_Normal.png", PpuMenu, new Vector4(202, 0, 202, 0));
            Set("UI/Buttons/UI_Shadow_Primary.png", PpuMenu, new Vector4(286, 0, 286, 0));
            Set("UI/Buttons/UI_ButtonSecondary_Normal.png", PpuMenu, new Vector4(117, 0, 117, 0));
            Set("UI/Buttons/UI_Shadow_Secondary.png", PpuMenu, new Vector4(150, 0, 150, 0));

            AssetDatabase.SaveAssets();
            Debug.Log("[CommonKit] import settings applied");
        }

        private static void Set(string relative, float ppu, Vector4 border)
        {
            UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{relative}", ppu, border);
        }
    }
}
