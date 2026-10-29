using System.IO;
using Callbreak.Application.Ports;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Callbreak.Editor
{
    /// <summary>
    /// Builds the Main menu prefab and the Menu scene (GDD §11 screen 00). Every position below
    /// is a centre measured on 00-main-menu.png in mockup pixels; the parts were fitted to the
    /// mockup by re-composing it from the exported sprites (ArtSource/Prompts/MainMenu_Batch.md).
    /// The coin wallet left of the settings disc is mockup 23's (23-coinsection.png, which is
    /// mockup 00 with the wallet added; everything else stands where it did). The column under the
    /// shop disc, Daily Rewards and then No Ads, is on no mockup (see <see cref="HeaderColumnStep"/>).
    /// Re-running overwrites the prefab, so layout changes belong here until the screen is signed
    /// off. Like the screens built from layouts, its buttons carry press feedback, its fixed
    /// labels carry string-table keys, and its corner groups follow the device's safe area.
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Screens/MainMenu.prefab";
        public const string ScenePath = "Assets/_Project/Scenes/Menu.unity";

        private const string TextMeshProSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const float UiPixelsPerUnit = 200f;

        private static readonly Vector2 Screen = UiBuildKit.ScreenCentre;

        // V2's type (Figma "V2 · Screens", 00 Main Menu): font size, colour, letter spacing, and the
        // box round the glyphs V2 renders (canvas units: x, y, width, height), on which the label's
        // ink is placed by its alignment's edge (Tools/ArtPipeline/V2/screens-v2.json).
        private static readonly LabelSpec PlayOnlineText = new LabelSpec(44f, "#FFFCF7", new Rect(871.5f, 715.9f, 259.8f, 34.4f), "left");
        private static readonly LabelSpec PlayWithFriendsText = new LabelSpec(28f, "#17140F", new Rect(642.7f, 887.9f, 231.1f, 27.3f), "left");
        private static readonly LabelSpec PlayOfflineText = new LabelSpec(28f, "#17140F", new Rect(1092.9f, 887.9f, 154.4f, 27.3f), "left");
        private static readonly LabelSpec TaglineText = new LabelSpec(20f, "#F3DFA8", new Rect(726.5f, 998.3f, 452f, 15f), "centre", 22f);
        private static readonly LabelSpec GreetingText = new LabelSpec(28f, "#6E4F37", new Rect(239f, 72.6f, 124.5f, 25.3f), "left");
        private static readonly LabelSpec PlayerNameText = new LabelSpec(32f, "#17140F", new Rect(238f, 107.7f, 95.2f, 29.8f), "left");

        // The balance starts right of the coin, as V2 draws it, and a longer one shrinks to stay
        // left of the "+".
        private static readonly LabelSpec BalanceText = new LabelSpec(32f, "#17140F", new Rect(1349.5f, 100.5f, 59.5f, 24.1f), "left");

        // V2's art, each where V2 draws it: the picture's own ink box lands on V2's (Tools/ArtPipeline/V2,
        // screens.py's icon pass): centre in mockup pixels, size in canvas units.
        private static readonly Rect LogoArt = new Rect(836.19f, 159.62f, 580.3f, 230.4f);
        private static readonly Rect PlayOnlineIconArt = new Rect(712.68f, 636.26f, 47.9f, 51.9f);
        private static readonly Rect FriendsIconArt = new Rect(530.38f, 784.16f, 39.6f, 27.4f);
        private static readonly Rect FriendsChevronArt = new Rect(786.75f, 783.64f, 12.4f, 20.4f);
        private static readonly Rect OfflineIconArt = new Rect(922.3f, 784.51f, 31.1f, 27.3f);
        private static readonly Rect OfflineChevronArt = new Rect(1112.62f, 783.64f, 12.4f, 20.4f);
        private static readonly Rect SettingsIconArt = new Rect(1402.05f, 97.06f, 42.6f, 41.8f);
        private static readonly Rect ShopIconArt = new Rect(1547.65f, 97.5f, 44.2f, 36.9f);
        private static readonly Vector2 DailyRewardsIconSize = new Vector2(40.4f, 43.2f);
        private static readonly Vector2 NoAdsIconSize = new Vector2(41.9f, 41.9f);

        // The coin wallet (mockup 23): the ivory pill (the kit's secondary pill, which the wallet is
        // drawn like) measured on its edges, the room the balance may grow into between the coin and
        // the "+", and the ShopUI kit's coin and crimson "+" (ArtSource/Working/ShopUI/notes.md) at the
        // rects its notes give.
        private static readonly Rect WalletPill = new Rect(1107.5f, 64.5f, 235.5f, 69.5f);
        private static readonly Rect WalletCoin = new Rect(1140.62f - (52f / UiBuildKit.ScaleX / 2f), 97.67f - (52f / UiBuildKit.ScaleY / 2f), 52f / UiBuildKit.ScaleX, 52f / UiBuildKit.ScaleY);
        private const float WalletBalanceRoom = 84f;
        private static readonly Rect WalletPlus = new Rect(1276.6f, 75.4f, 48.4f, 48.4f);
        private const string WalletCoinPath = "Assets/_Project/Art/UI/Icons/Icon_Coin.png";
        private const string WalletPlusPath = "Assets/_Project/Art/UI/Buttons/UI_ButtonPlus_Normal.png";
        private const float WalletShadowDrop = CommonKit.SecondaryShadowDrop / UiBuildKit.ScaleY;
        private const float WalletShadowOpacity = 1f;

        // The buttons keep the sizes mockup 00 drew them at (canvas units); the V2 kit's sprites
        // are sliced or scaled to them.
        private static readonly Vector2 HeaderButtonSize = new Vector2(104f, 104f);
        private static readonly Vector2 PlayOnlineSize = new Vector2(720f, 132f);
        private static readonly Vector2 SecondaryButtonSize = new Vector2(400f, 92f);

        // The portrait fills the profile ring's opening; the portraits are 512 px (ADR 041).
        private static readonly Vector2 ProfileAvatarSize = new Vector2(111f, 111f);

        // The "+" takes a touch a little beyond its disc (GDD: touch targets of about 88 px).
        private const float WalletPlusHitGrowth = 15f;

        // The column under the shop disc is on no mockup. The owner moved No Ads under the shop
        // (5 Oct 2026) and then put Daily Rewards between the two at the shop's size (14 Oct 2026):
        // each disc stands HeaderColumnStep below the one above it, the step the owner gave No Ads.
        // Daily Rewards stays where it is when No Ads has been bought and its button is gone.
        private const float HeaderColumnStep = 128.67f;

        // The No Ads button (the owner, 5 Oct 2026): the gear's disc and shadow with the No Ads glyph
        // (ChatGPT's, ArtSource/Working/MainMenu/chatgpt/NoAds_prompts.md) centred on it.
        private const string NoAdsIconPath = "Icons/Icon_NoAds.png";

        // The Daily Rewards button (the owner, 8 Oct 2026): the gear's disc and shadow with the gift
        // glyph, at V2's size, and a red dot on its top right rim while today's reward is ready (ADR 046:
        // a dot, where V2 draws a count).
        private const string DailyRewardsIconPath = "Icons/Icon_DailyReward.png";
        private const string DailyRewardsDotPath = "Controls/UI_Dot_Alert.png";
        private const float DailyRewardsDotSize = 20f;

        // The banner ad in the bottom-left corner of the main menu (the owner, 27 Oct 2026, ADR 052): an
        // empty slot in the safe area's bottom-left corner, along the bottom of the screen, that the ad
        // network's own view is placed over. A standard banner (320 x 50 dp) is about 960 x 150 canvas
        // units on a phone 360 dp tall, so the slot is this big (canvas units); MainMenuView draws the
        // centre column smaller while it is on, so the play buttons clear it.
        private static readonly Vector2 BannerSlotSize = new Vector2(1000f, 180f);

        // Prize coins arriving (the owner, 9 Oct 2026): a Stake Match prize, won at the table, flies
        // into the wallet from the middle of the screen, over the card fan, out of an area this big.
        private static readonly Vector2 PrizeCoinsFromSize = new Vector2(240f, 140f);

        // The fan: the four aces of the player's deck (GDD §13), laid as mockup 00 draws its four
        // cards. For each, the middle of its top edge and its tilt (mockup pixels; degrees,
        // anticlockwise positive), measured on the mockup's card edges: the two left cards by their
        // top-left corners, the two right ones by their top-right corners. The cards hang from
        // those edges; their feet are behind the Play Online button. The mockup paints its side
        // cards narrower than the middle one; real cards are all one size, the width of the one the
        // mockup shows whole, and they are stacked so that every ace shows its index: back to
        // front hearts, diamonds, spades, clubs.
        private static readonly FanCard[] FanCards =
        {
            new FanCard(Suit.Hearts, 633.1f, 383.5f, 22.94f),
            new FanCard(Suit.Diamonds, 940.5f, 366.3f, -15.6f),
            new FanCard(Suit.Spades, 772f, 340.9f, 7.7f),
            new FanCard(Suit.Clubs, 1062.9f, 419.5f, -27.6f),
        };

        private const float FanCardWidth = 171.6f;
        private const float FanCardAspect = 1.57f;
        private static readonly Vector2 FanFoot = new Vector2(848f, 628.25f);
        private const string CardShadowPath = "Assets/_Project/Art/UI/Common/UI_Panel_Card_Shadow.png";
        private const float CardShadowSpread = 9f;
        private const float CardShadowDrop = 3f;
        private const float CardShadowOpacity = 0.4f;
        private const float CardShadowSliceScale = 2.6f;

        [MenuItem("Callbreak/Build/Main Menu")]
        public static void Build()
        {
            if (!File.Exists(TextMeshProSettingsPath))
            {
                // The fonts need TextMeshPro's shaders. The import finishes after this call
                // returns, so the build continues from the completion callback.
                AssetDatabase.importPackageCompleted += ContinueAfterTextMeshProImport;
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                return;
            }

            // TextMeshPro only lays text out under a Canvas, and the labels are placed by measuring
            // their glyphs, so the menu is built inside a temporary one (as the layout builder does).
            var canvasObject = new GameObject("MainMenuBuildCanvas", typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject prefab;
            try
            {
                GameObject root = BuildHierarchy(canvasObject.transform);
                UiBuildKit.EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }

            // The scene only hosts an instance of the prefab, so it is created once and then left
            // alone: rebuilding it would overwrite a scene someone has open.
            if (!File.Exists(ScenePath))
            {
                BuildScene(prefab);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MainMenuBuilder] Built {PrefabPath}; scene at {ScenePath}");
        }

        private static void ContinueAfterTextMeshProImport(string packageName)
        {
            AssetDatabase.importPackageCompleted -= ContinueAfterTextMeshProImport;
            Build();
        }

        private static GameObject BuildHierarchy(Transform buildParent)
        {
            TMP_FontAsset serifBold = UiBuildKit.LoadFont("SourceSerif4-Bold");
            TMP_FontAsset sansSemiBold = UiBuildKit.LoadFont("Inter-SemiBold");
            TMP_FontAsset sansBold = UiBuildKit.LoadFont("Inter-Bold");

            Sprite circle = Kit("Buttons/UI_ButtonCircle_Normal.png");
            Sprite circleShadow = Kit("Buttons/UI_Shadow_Circle.png");
            Sprite secondary = Kit("Buttons/UI_ButtonSecondary_Normal.png");
            Sprite secondaryShadow = Kit("Buttons/UI_Shadow_Secondary.png");
            float circleDrop = CommonKit.CircleShadowDrop / UiBuildKit.ScaleY;
            float secondaryDrop = CommonKit.SecondaryShadowDrop / UiBuildKit.ScaleY;
            Sprite chevron = Ui("Icons/Icon_Chevron.png");

            var rootObject = new GameObject("MainMenu", typeof(RectTransform));
            var root = (RectTransform)rootObject.transform;
            root.SetParent(buildParent, false);
            Stretch(root);

            BuildBackground(root);

            // Everything but the background sits under Content, which is what a screen change
            // slides; the corner groups follow the device's safe area (GDD §12).
            RectTransform content = StretchedChild("Content", root);
            RectTransform centre = UiBuildKit.CreateDesignGroup("Centre", content, UiBuildKit.Attach.Centre);
            RectTransform safeArea = StretchedChild("SafeArea", content);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform headerLeft = UiBuildKit.CreateDesignGroup("HeaderLeft", safeArea, UiBuildKit.Attach.TopLeft);
            RectTransform headerRight = UiBuildKit.CreateDesignGroup("HeaderRight", safeArea, UiBuildKit.Attach.TopRight);

            // Centre column, back to front
            PlaceArt("Logo", centre, LogoArt, Screen, Ui("Ornaments/Logo_Callbreak.png"));

            // The fan and its shadow rock together about the fan's foot, which the Play Online
            // button covers (GDD screen 00: the fan "idles with a gentle sway"). Their own canvas
            // keeps the rest of the screen from being batched again every frame.
            var fanCentre = new Vector2(848f, 479.25f);
            RectTransform fan = UiBuildKit.CreateAt("CardFanGroup", centre, FanFoot, Screen, Vector2.zero);
            fan.gameObject.AddComponent<Canvas>();
            var fanSway = fan.gameObject.AddComponent<IdleSway>();
            PlaceShadow("CardFanShadow", fan, fanCentre, FanFoot, Ui("Ornaments/UI_Shadow_MenuFan.png"), 6f, 0.3f);
            UnityEngine.UI.Image[] fanFaces = BuildFanCards(fan, out UnityEngine.UI.Image[] fanBases);

            var playOnlineCentre = new Vector2(835.75f, 636.25f);
            UnityEngine.UI.Button playOnline = CreateButton(
                "PlayOnlineButton", centre, playOnlineCentre, PlayOnlineSize, Kit("Controls/UI_Button_Crimson.png"),
                Kit("Controls/UI_Button_Crimson_Shadow.png"), CommonKit.HeroShadowDrop / UiBuildKit.ScaleY, 1f);
            PlaceArt("Icon", playOnline.transform, PlayOnlineIconArt, playOnlineCentre, Ui("Icons/Icon_Spade_Gold.png"));
            TextMeshProUGUI playOnlineLabel = Label("Label", playOnline.transform, playOnlineCentre, "PLAY ONLINE", serifBold, PlayOnlineText);
            Localize(playOnlineLabel, "ui.main_menu.play_online");
            playOnlineLabel.fontSharedMaterial = UiBuildKit.LoadShadowPreset(
                serifBold, "ButtonLabel", new Color(0.29f, 0.03f, 0.04f, 0.9f), new Vector2(0.25f, -0.35f), 0.2f);

            Color ink = UiBuildKit.Hex("#17140F");

            var friendsCentre = new Vector2(656.5f, 783.25f);
            UnityEngine.UI.Button playWithFriends = CreateButton(
                "PlayWithFriendsButton", centre, friendsCentre, SecondaryButtonSize, secondary, secondaryShadow, secondaryDrop, 1f);
            PlaceArt("Icon", playWithFriends.transform, FriendsIconArt, friendsCentre, Ui("Icons/Icon_Friends.png"));
            Localize(
                Label("Label", playWithFriends.transform, friendsCentre, "Play with Friends", sansBold, PlayWithFriendsText),
                "ui.main_menu.play_with_friends");
            PlaceArt("Chevron", playWithFriends.transform, FriendsChevronArt, friendsCentre, chevron);

            var offlineCentre = new Vector2(1015.5f, 783.25f);
            UnityEngine.UI.Button playOffline = CreateButton(
                "PlayOfflineButton", centre, offlineCentre, SecondaryButtonSize, secondary, secondaryShadow, secondaryDrop, 1f);
            PlaceArt("Icon", playOffline.transform, OfflineIconArt, offlineCentre, Ui("Icons/Icon_Offline.png"));
            Localize(
                Label("Label", playOffline.transform, offlineCentre, "Play Offline", sansBold, PlayOfflineText),
                "ui.main_menu.play_offline");
            PlaceArt("Chevron", playOffline.transform, OfflineChevronArt, offlineCentre, chevron);

            RectTransform footer = BuildFooter(centre, sansSemiBold);

            // Header, left: who you are. The mockup's ring is plain white, not the ivory button.
            var profileCentre = new Vector2(127.5f, 98f);
            Sprite ring = Ui("Avatars/UI_AvatarRing.png");
            UnityEngine.UI.Button profile = CreateButton(
                "ProfileButton", headerLeft, profileCentre, UiBuildKit.NativeSize(ring), ring, circleShadow, circleDrop, 1f, circle);
            RectTransform avatarRect = UiBuildKit.CreateAt("Avatar", profile.transform, new Vector2(127.25f, 98f), profileCentre, ProfileAvatarSize);
            UnityEngine.UI.Image avatar = UiBuildKit.AddImage(avatarRect, Ui("Avatars/Avatar_Default.png"), false);
            Localize(Label("Greeting", headerLeft, Screen, "Namaste,", sansSemiBold, GreetingText), "ui.main_menu.greeting");
            TextMeshProUGUI playerName = Label("PlayerName", headerLeft, Screen, "Player", sansBold, PlayerNameText);

            // Header, right: the coin wallet, settings and shop, then Daily Rewards and No Ads under the shop
            UnityEngine.UI.Button earnCoins = BuildWallet(headerRight, sansBold, secondary, out TextMeshProUGUI coins);

            var settingsCentre = new Vector2(1402f, 97f);
            UnityEngine.UI.Button settings = CreateButton(
                "SettingsButton", headerRight, settingsCentre, HeaderButtonSize, circle, circleShadow, circleDrop, 1f);
            PlaceArt("Icon", settings.transform, SettingsIconArt, settingsCentre, Ui("Icons/Icon_Settings.png"));

            RectTransform divider = UiBuildKit.CreateAt(
                "Divider", headerRight, new Vector2(1474.5f, 98.4f), Screen, UiBuildKit.FromMock(2f, 65.75f));
            UiBuildKit.AddImage(divider, null, false).color = UiBuildKit.Hex("#86604A");

            var shopCentre = new Vector2(1547.5f, 97f);
            UnityEngine.UI.Button shop = CreateButton(
                "ShopButton", headerRight, shopCentre, HeaderButtonSize, circle, circleShadow, circleDrop, 1f);
            PlaceArt("Icon", shop.transform, ShopIconArt, shopCentre, Ui("Icons/Icon_Shop.png"));

            UnityEngine.UI.Button dailyRewards = BuildDailyRewardsButton(
                headerRight, UnderShop(shopCentre, 1), circle, circleShadow, out GameObject dailyRewardsDot);
            UnityEngine.UI.Button noAds = BuildNoAdsButton(headerRight, UnderShop(shopCentre, 2), circle, circleShadow);

            RectTransform bannerSlot = BuildBannerSlot(safeArea);

            var view = rootObject.AddComponent<MainMenuView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_screenId").intValue = (int)ScreenId.MainMenu;
            serialized.FindProperty("_content").objectReferenceValue = content;
            serialized.FindProperty("_footer").objectReferenceValue = footer.gameObject;
            serialized.FindProperty("_avatar").objectReferenceValue = avatar;
            serialized.FindProperty("_fanSway").objectReferenceValue = fanSway;
            SerializedProperty faces = serialized.FindProperty("_fanFaces");
            faces.arraySize = fanFaces.Length;
            for (int i = 0; i < fanFaces.Length; i++)
            {
                faces.GetArrayElementAtIndex(i).objectReferenceValue = fanFaces[i];
            }

            SerializedProperty bases = serialized.FindProperty("_fanBases");
            bases.arraySize = fanBases.Length;
            for (int i = 0; i < fanBases.Length; i++)
            {
                bases.GetArrayElementAtIndex(i).objectReferenceValue = fanBases[i];
            }

            serialized.FindProperty("_profileButton").objectReferenceValue = profile;
            serialized.FindProperty("_settingsButton").objectReferenceValue = settings;
            serialized.FindProperty("_shopButton").objectReferenceValue = shop;
            serialized.FindProperty("_coinsText").objectReferenceValue = coins;
            serialized.FindProperty("_earnCoinsButton").objectReferenceValue = earnCoins;
            serialized.FindProperty("_noAdsButton").objectReferenceValue = noAds;
            serialized.FindProperty("_dailyRewardsButton").objectReferenceValue = dailyRewards;
            serialized.FindProperty("_dailyRewardsDot").objectReferenceValue = dailyRewardsDot;
            serialized.FindProperty("_centre").objectReferenceValue = centre;
            serialized.FindProperty("_bannerSlot").objectReferenceValue = bannerSlot;
            BindCoinFlight(serialized, root, content, coins.transform.parent.Find("Coin"));
            serialized.FindProperty("_playOnlineButton").objectReferenceValue = playOnline;
            serialized.FindProperty("_playWithFriendsButton").objectReferenceValue = playWithFriends;
            serialized.FindProperty("_playOfflineButton").objectReferenceValue = playOffline;
            serialized.FindProperty("_playerNameText").objectReferenceValue = playerName;
            serialized.FindProperty("_playOnlineGroup").objectReferenceValue = playOnline.gameObject.AddComponent<CanvasGroup>();
            serialized.FindProperty("_playWithFriendsGroup").objectReferenceValue = playWithFriends.gameObject.AddComponent<CanvasGroup>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return rootObject;
        }

        /// <summary>
        /// The player's equipped background (V2), framed as every menu screen frames it: the screen
        /// plus margins for 21:9 and 4:3, so it bleeds to the screen edge on every aspect ratio
        /// (GDD §12) and the screens open onto each other without a jump.
        /// </summary>
        private static void BuildBackground(RectTransform root)
        {
            RectTransform rect = UiBuildKit.CreateAt(
                "Background", root, Screen, Screen, UiBuildKit.FromMock(TableBackdropBuilder.FrameWidth, TableBackdropBuilder.FrameHeight));
            Screens.ScreenLayoutBuilder.AddEquippedBackground(rect);
        }

        private static RectTransform BuildFooter(RectTransform centre, TMP_FontAsset font)
        {
            Color line = UiBuildKit.Hex("#A4856A");
            RectTransform footer = UiBuildKit.CreateAt("Footer", centre, Screen, Screen, Vector2.zero);

            RectTransform left = UiBuildKit.CreateAt("LineLeft", footer, new Vector2(553.25f, 876f), Screen, UiBuildKit.FromMock(58.5f, 1.9f));
            UiBuildKit.AddImage(left, null, false).color = line;

            Localize(Label("Tagline", footer, Screen, "A GAME THAT BRINGS US CLOSER", font, TaglineText), "ui.main_menu.tagline");

            RectTransform right = UiBuildKit.CreateAt("LineRight", footer, new Vector2(1112.5f, 876f), Screen, UiBuildKit.FromMock(63f, 1.9f));
            UiBuildKit.AddImage(right, null, false).color = line;
            return footer;
        }

        /// <summary>
        /// Mockup 23's coin wallet: the ivory pill holding the coin, the balance and the crimson "+"
        /// that opens Earn Coins. The pill and its shadow are sliced at the scale that gives the pill
        /// its height, as the layout builder draws sliced pills. The balance is centred between the
        /// coin and the "+", and a longer one shrinks to stay between them. Returns the "+".
        /// </summary>
        private static UnityEngine.UI.Button BuildWallet(RectTransform headerRight, TMP_FontAsset font, Sprite pill, out TextMeshProUGUI balance)
        {
            Vector2 centre = WalletPill.center;
            Vector2 size = UiBuildKit.FromMock(WalletPill.width, WalletPill.height);
            RectTransform wallet = UiBuildKit.CreateAt("Wallet", headerRight, centre, Screen, size);

            float scale = size.y / UiBuildKit.NativeSize(pill).y;
            Sprite shadow = Kit("Buttons/UI_Shadow_Secondary.png");
            Vector2 shadowSize = size + ((UiBuildKit.NativeSize(shadow) - UiBuildKit.NativeSize(pill)) * scale);
            RectTransform shadowRect = UiBuildKit.CreateAt("Shadow", wallet, centre + new Vector2(0f, WalletShadowDrop), centre, shadowSize);
            UnityEngine.UI.Image shadowImage = UiBuildKit.AddImage(shadowRect, shadow, false);
            shadowImage.color = new Color(1f, 1f, 1f, WalletShadowOpacity);
            shadowImage.pixelsPerUnitMultiplier = 1f / scale;
            RectTransform pillRect = UiBuildKit.CreateAt("Pill", wallet, centre, centre, size);
            UiBuildKit.AddImage(pillRect, pill, false).pixelsPerUnitMultiplier = 1f / scale;

            RectTransform coin = UiBuildKit.CreateAt("Coin", wallet, WalletCoin.center, centre, UiBuildKit.FromMock(WalletCoin.width, WalletCoin.height));
            UiBuildKit.AddImage(coin, UiBuildKit.LoadSpriteAt(WalletCoinPath, 0f), false);

            balance = Label("Balance", wallet, centre, "250", font, BalanceText);
            RectTransform balanceRect = balance.rectTransform;
            balanceRect.sizeDelta = new Vector2(WalletBalanceRoom * UiBuildKit.ScaleX, balanceRect.sizeDelta.y);
            balance.fontSizeMax = balance.fontSize;
            balance.fontSizeMin = balance.fontSize * 0.6f;
            balance.enableAutoSizing = true;
            balance.overflowMode = TextOverflowModes.Ellipsis;

            Vector2 plusCentre = WalletPlus.center;
            Vector2 plusSize = UiBuildKit.FromMock(WalletPlus.width, WalletPlus.height);
            RectTransform plus = UiBuildKit.CreateAt("EarnCoinsButton", wallet, plusCentre, centre, plusSize);
            RectTransform face = UiBuildKit.CreateAt("Face", plus, plusCentre, plusCentre, plusSize);
            UnityEngine.UI.Image faceImage = UiBuildKit.AddImage(face, UiBuildKit.LoadSpriteAt(WalletPlusPath, 0f), true);
            faceImage.raycastPadding = -Vector4.one * WalletPlusHitGrowth;
            return MakeButton(plus, faceImage);
        }

        /// <summary>
        /// The No Ads button: the gear's disc, shadow and press feedback, with the No Ads glyph
        /// centred on it. Built hidden; the presenter shows it while No Ads can be bought.
        /// </summary>
        private static UnityEngine.UI.Button BuildNoAdsButton(RectTransform headerRight, Vector2 centre, Sprite circle, Sprite circleShadow)
        {
            UnityEngine.UI.Button button = CreateButton(
                "NoAdsButton", headerRight, centre, HeaderButtonSize, circle, circleShadow, CommonKit.CircleShadowDrop / UiBuildKit.ScaleY, 1f);
            Sprite icon = Ui(NoAdsIconPath);
            UiBuildKit.AddImage(UiBuildKit.CreateAt("Icon", button.transform, centre, centre, NoAdsIconSize), icon, false);
            button.gameObject.SetActive(false);
            return button;
        }

        /// <summary>The centre of the disc <paramref name="place"/> steps down the column under the shop (see <see cref="HeaderColumnStep"/>).</summary>
        private static Vector2 UnderShop(Vector2 shopCentre, int place)
        {
            return new Vector2(shopCentre.x, shopCentre.y + (place * HeaderColumnStep));
        }

        /// <summary>
        /// The Daily Rewards button (see <see cref="DailyRewardsIconPath"/>): the gear's disc, shadow and
        /// press feedback with the gift glyph, and its red dot, built hidden: the presenter shows the dot
        /// while today's reward is ready.
        /// </summary>
        private static UnityEngine.UI.Button BuildDailyRewardsButton(
            RectTransform headerRight, Vector2 centre, Sprite circle, Sprite circleShadow, out GameObject dot)
        {
            float radius = HeaderButtonSize.y / UiBuildKit.ScaleY / 2f;
            UnityEngine.UI.Button button = CreateButton(
                "DailyRewardsButton", headerRight, centre, HeaderButtonSize, circle, circleShadow, CommonKit.CircleShadowDrop / UiBuildKit.ScaleY, 1f);
            Sprite icon = Ui(DailyRewardsIconPath);
            RectTransform iconRect = UiBuildKit.CreateAt("Icon", button.transform, centre, centre, DailyRewardsIconSize);
            UiBuildKit.AddImage(iconRect, icon, false);

            // On the rim, up and to the right, as a badge sits on an app icon.
            float offset = radius * 0.72f;
            RectTransform dotRect = UiBuildKit.CreateAt(
                "Dot", button.transform, centre + new Vector2(offset, -offset), centre, UiBuildKit.FromMock(DailyRewardsDotSize, DailyRewardsDotSize));
            UiBuildKit.AddImage(dotRect, Ui(DailyRewardsDotPath), false);
            dot = dotRect.gameObject;
            dot.SetActive(false);
            return button;
        }

        /// <summary>
        /// The banner ad's slot in the safe area's bottom-left corner (see <see cref="BannerSlotSize"/>),
        /// built off: the menu turns it on while it makes room for a banner.
        /// </summary>
        private static RectTransform BuildBannerSlot(RectTransform safeArea)
        {
            RectTransform corner = UiBuildKit.CreateDesignGroup("BottomLeft", safeArea, UiBuildKit.Attach.BottomLeft);
            var centre = new Vector2(
                BannerSlotSize.x / 2f / UiBuildKit.ScaleX,
                (UiBuildKit.CanvasHeight - (BannerSlotSize.y / 2f)) / UiBuildKit.ScaleY);
            RectTransform slot = UiBuildKit.CreateAt("BannerSlot", corner, centre, Screen, BannerSlotSize);
            slot.gameObject.SetActive(false);
            return slot;
        }

        /// <summary>
        /// Takes the "Resume match" banner out of the saved main menu (the owner, 28 Oct 2026, ADR 057:
        /// an unfinished offline match is offered back by the resume dialog instead); everything else
        /// in the prefab stays as it is.
        /// </summary>
        public static string RemoveResumeBanner()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                int removed = 0;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == "ResumeBanner")
                    {
                        Object.DestroyImmediate(t.gameObject);
                        removed++;
                    }
                }

                if (removed == 0)
                {
                    return "the main menu has no Resume banner";
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                return saved ? $"saved {PrefabPath} without the Resume banner" : $"FAILED to save {PrefabPath}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Gives the aces of the saved main menu's fan the shared faces of the default deck and their
        /// premultiplied material (ADR 054); everything else in the prefab stays as it is. The
        /// presenter puts in the equipped deck's aces when the menu opens.
        /// </summary>
        public static string UseSharedFanFaces()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                CardSpriteProvider cards = TableCardBuilder.EnsureLibrary();
                Material material = TableCardBuilder.EnsureFaceMaterial();
                SerializedProperty faces = new SerializedObject(root.GetComponent<MainMenuView>()).FindProperty("_fanFaces");
                int changed = 0;
                for (int i = 0; i < faces.arraySize; i++)
                {
                    var face = faces.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.UI.Image;
                    if (face == null)
                    {
                        return $"FAILED: the fan has no face {i}";
                    }

                    // The fan's faces are indexed by suit (BuildFanCards).
                    Sprite ace = cards.Face(cards.DefaultDeckId, new Card((Suit)i, Rank.Ace));
                    if (face.sprite != ace || face.material != material)
                    {
                        face.sprite = ace;
                        face.material = material;
                        changed++;
                    }
                }

                if (changed == 0)
                {
                    return "the main menu's fan already shows the shared faces";
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                return saved ? $"saved {PrefabPath}: {changed} fan faces shared and premultiplied" : $"FAILED to save {PrefabPath}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Adds the deck's base under each ace of the saved main menu's fan and binds them to the view
        /// (ADR 053); everything else in the prefab stays as it is. A fan that has its bases is left alone.
        /// </summary>
        public static string AddFanBases()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<MainMenuView>());
                SerializedProperty faces = serialized.FindProperty("_fanFaces");
                SerializedProperty bases = serialized.FindProperty("_fanBases");
                if (bases.arraySize == faces.arraySize && bases.arraySize > 0 && bases.GetArrayElementAtIndex(0).objectReferenceValue != null)
                {
                    return "the main menu's fan already has its bases";
                }

                CardSpriteProvider cards = TableCardBuilder.EnsureLibrary();
                bases.arraySize = faces.arraySize;
                for (int i = 0; i < faces.arraySize; i++)
                {
                    var face = faces.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.UI.Image;
                    if (face == null)
                    {
                        return $"FAILED: the fan has no face {i}";
                    }

                    RectTransform baseRect = StretchedChild("Base", face.transform.parent);
                    baseRect.SetSiblingIndex(face.transform.GetSiblingIndex());
                    bases.GetArrayElementAtIndex(i).objectReferenceValue = UiBuildKit.AddImage(baseRect, cards.Base(cards.DefaultDeckId), false);
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                return saved ? $"saved {PrefabPath} with the fan's bases" : $"FAILED to save {PrefabPath}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Adds the banner ad's slot (<see cref="BuildBannerSlot"/>) to the saved main menu prefab and
        /// binds it, with the centre column the menu draws smaller to make room, to the view; everything
        /// else in the prefab stays as it is (<see cref="Build"/> makes it anew and drops the changes
        /// saved into it since). A prefab that already has the slot is left alone.
        /// </summary>
        public static string AddBannerSlot()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<MainMenuView>();
                var serialized = new SerializedObject(view);
                if (serialized.FindProperty("_bannerSlot").objectReferenceValue != null)
                {
                    return "the main menu already has its banner slot";
                }

                var safeArea = root.transform.Find("Content/SafeArea") as RectTransform;
                var centre = root.transform.Find("Content/Centre") as RectTransform;
                if (safeArea == null || centre == null)
                {
                    return "FAILED: the main menu has no Content/SafeArea or Content/Centre";
                }

                serialized.FindProperty("_centre").objectReferenceValue = centre;
                serialized.FindProperty("_bannerSlot").objectReferenceValue = BuildBannerSlot(safeArea);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                return saved ? $"saved {PrefabPath} with the banner slot" : $"FAILED to save {PrefabPath}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Prize coins arriving: the <see cref="CoinFlight"/> layer over the whole menu, last, and the
        /// area in the middle of the screen the coins come from, bound to the view with the wallet's coin.
        /// </summary>
        private static void BindCoinFlight(SerializedObject view, RectTransform root, RectTransform content, Transform walletCoin)
        {
            RectTransform from = UiBuildKit.CreateAt("PrizeCoinsFrom", content, Screen, Screen, PrizeCoinsFromSize);
            var layer = new GameObject("CoinFlight", typeof(RectTransform));
            var rect = (RectTransform)layer.transform;
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            var flight = layer.AddComponent<CoinFlight>();
            var serializedFlight = new SerializedObject(flight);
            serializedFlight.FindProperty("_coin").objectReferenceValue = UiBuildKit.LoadSpriteAt(WalletCoinPath, 0f);
            serializedFlight.ApplyModifiedPropertiesWithoutUndo();

            view.FindProperty("_coinFlight").objectReferenceValue = flight;
            view.FindProperty("_walletCoin").objectReferenceValue = walletCoin;
            view.FindProperty("_prizeCoinsFrom").objectReferenceValue = from;
        }

        /// <summary>
        /// Adds the prize coins' flight (<see cref="BindCoinFlight"/>) to the saved main menu prefab and
        /// binds it to the view; everything else in the prefab stays as it is. A prefab that already has
        /// it is left alone.
        /// </summary>
        public static string AddCoinFlight()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<MainMenuView>();
                var serialized = new SerializedObject(view);
                if (serialized.FindProperty("_coinFlight").objectReferenceValue != null)
                {
                    return "the main menu already has its coin flight";
                }

                var coins = serialized.FindProperty("_coinsText").objectReferenceValue as TMP_Text;
                Transform walletCoin = coins != null ? coins.transform.parent.Find("Coin") : null;
                var content = serialized.FindProperty("_content").objectReferenceValue as RectTransform;
                if (walletCoin == null || content == null)
                {
                    return "FAILED: the main menu has no wallet coin or no content to fly over";
                }

                BindCoinFlight(serialized, (RectTransform)root.transform, content, walletCoin);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                return saved ? $"saved {PrefabPath} with the coin flight" : $"FAILED to save {PrefabPath}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Lays out the column under the shop disc in the saved main menu prefab: the Daily Rewards
        /// button (<see cref="BuildDailyRewardsButton"/>, made anew at the shop's size and bound to the
        /// view) one step under the shop, and the No Ads button, as it is, one step under that.
        /// Everything else in the prefab stays as it is (<see cref="Build"/> makes it anew and drops
        /// the changes saved into it since). Running it again lays the column out the same way.
        /// </summary>
        public static string StackColumnUnderShop()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<MainMenuView>();
                var serialized = new SerializedObject(view);
                var shop = serialized.FindProperty("_shopButton").objectReferenceValue as UnityEngine.UI.Button;
                var noAds = serialized.FindProperty("_noAdsButton").objectReferenceValue as UnityEngine.UI.Button;
                if (shop == null || noAds == null)
                {
                    return "FAILED: the main menu has no shop or no No Ads button";
                }

                var oldDailyRewards = serialized.FindProperty("_dailyRewardsButton").objectReferenceValue as UnityEngine.UI.Button;
                if (oldDailyRewards != null)
                {
                    Object.DestroyImmediate(oldDailyRewards.gameObject);
                }

                // The header buttons are placed against the mockup's centre (CreateButton).
                var shopRect = (RectTransform)shop.transform;
                Vector2 position = shopRect.anchoredPosition;
                var shopCentre = new Vector2(Screen.x + (position.x / UiBuildKit.ScaleX), Screen.y - (position.y / UiBuildKit.ScaleY));
                var noAdsRect = (RectTransform)noAds.transform;
                Vector2 noAdsCentre = UnderShop(shopCentre, 2);
                noAdsRect.anchoredPosition = new Vector2((noAdsCentre.x - Screen.x) * UiBuildKit.ScaleX, -(noAdsCentre.y - Screen.y) * UiBuildKit.ScaleY);

                UnityEngine.UI.Button button = BuildDailyRewardsButton(
                    (RectTransform)noAdsRect.parent, UnderShop(shopCentre, 1), Kit("Buttons/UI_ButtonCircle_Normal.png"), Kit("Buttons/UI_Shadow_Circle.png"), out GameObject dot);
                button.transform.SetSiblingIndex(noAdsRect.GetSiblingIndex());
                serialized.FindProperty("_dailyRewardsButton").objectReferenceValue = button;
                serialized.FindProperty("_dailyRewardsDot").objectReferenceValue = dot;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                return saved ? $"saved {PrefabPath} with Daily Rewards and No Ads under the shop" : $"FAILED to save {PrefabPath}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The four aces, each a card with its own soft shadow, its deck's base and its face (ADR
        /// 053), the face with the premultiplied material of the shared faces (ADR 054). The prefab shows the default deck's; the presenter puts in the equipped deck's.
        /// Returns the face images, and the bases under them, indexed by suit.
        /// </summary>
        private static UnityEngine.UI.Image[] BuildFanCards(RectTransform fan, out UnityEngine.UI.Image[] bases)
        {
            CardSpriteProvider cards = TableCardBuilder.EnsureLibrary();
            Sprite shadow = UiBuildKit.LoadSpriteAt(CardShadowPath, 0f);
            var faces = new UnityEngine.UI.Image[4];
            bases = new UnityEngine.UI.Image[4];
            foreach (FanCard card in FanCards)
            {
                Sprite face = cards.Face(cards.DefaultDeckId, new Card(card.Suit, Rank.Ace));
                float height = FanCardWidth * (face != null ? face.rect.height / face.rect.width : FanCardAspect);
                Vector2 size = UiBuildKit.FromMock(FanCardWidth, height);

                // The card hangs from its top edge: its centre is half a card down its own axis.
                float tilt = card.Tilt * Mathf.Deg2Rad;
                var cardCentre = new Vector2(card.X + height / 2f * Mathf.Sin(tilt), card.Y + height / 2f * Mathf.Cos(tilt));
                RectTransform rect = UiBuildKit.CreateAt("Ace" + card.Suit, fan, cardCentre, FanFoot, size);
                rect.localRotation = Quaternion.Euler(0f, 0f, card.Tilt);

                RectTransform shadowRect = StretchedChild("Shadow", rect);
                shadowRect.offsetMin = new Vector2(-CardShadowSpread, -CardShadowSpread - CardShadowDrop);
                shadowRect.offsetMax = new Vector2(CardShadowSpread, CardShadowSpread - CardShadowDrop);
                UnityEngine.UI.Image shadowImage = UiBuildKit.AddImage(shadowRect, shadow, false);
                shadowImage.color = new Color(1f, 1f, 1f, CardShadowOpacity);
                shadowImage.pixelsPerUnitMultiplier = CardShadowSliceScale;

                RectTransform baseRect = StretchedChild("Base", rect);
                bases[(int)card.Suit] = UiBuildKit.AddImage(baseRect, cards.Base(cards.DefaultDeckId), false);

                RectTransform faceRect = StretchedChild("Face", rect);
                faces[(int)card.Suit] = UiBuildKit.AddImage(faceRect, face, false);
                faces[(int)card.Suit].material = TableCardBuilder.EnsureFaceMaterial();
            }

            return faces;
        }

        private readonly struct FanCard
        {
            public readonly Suit Suit;
            public readonly float X;
            public readonly float Y;
            public readonly float Tilt;

            public FanCard(Suit suit, float x, float y, float tilt)
            {
                Suit = suit;
                X = x;
                Y = y;
                Tilt = tilt;
            }
        }

        /// <summary>Binds a fixed label to its string-table key; the prefab keeps the English it was fitted with.</summary>
        private static void Localize(TMP_Text label, string key)
        {
            var localized = label.gameObject.AddComponent<LocalizedText>();
            var so = new SerializedObject(localized);
            so.FindProperty("_key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform StretchedChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        /// <summary>
        /// A button is a root with the Button component and, back to front, its shadow, its face
        /// and whatever the caller adds. The face is scaled as a whole to <paramref name="size"/>'s
        /// height (a pill sliced only across keeps its round ends), and the shadow, a padded copy
        /// of <paramref name="shadowFace"/>'s shape (the face itself when null), with it; a sliced
        /// face made wider or narrower takes its shadow along. <paramref name="shadowDrop"/> is in
        /// mockup pixels.
        /// </summary>
        private static UnityEngine.UI.Button CreateButton(
            string name,
            Transform parent,
            Vector2 centre,
            Vector2 size,
            Sprite face,
            Sprite shadow,
            float shadowDrop,
            float shadowOpacity,
            Sprite shadowFace = null)
        {
            RectTransform root = UiBuildKit.CreateAt(name, parent, centre, Screen, size);

            Vector2 shapeSize = UiBuildKit.NativeSize(shadowFace != null ? shadowFace : face);
            float scale = size.y / shapeSize.y;
            Vector2 shadowSize = size + ((UiBuildKit.NativeSize(shadow) - shapeSize) * scale);
            RectTransform shadowRect = UiBuildKit.CreateAt("Shadow", root, centre + new Vector2(0f, shadowDrop), centre, shadowSize);
            UnityEngine.UI.Image shadowImage = UiBuildKit.AddImage(shadowRect, shadow, false);
            shadowImage.color = new Color(1f, 1f, 1f, shadowOpacity);
            shadowImage.pixelsPerUnitMultiplier = 1f / scale;

            RectTransform faceRect = UiBuildKit.CreateAt("Face", root, centre, centre, size);
            UnityEngine.UI.Image faceImage = UiBuildKit.AddImage(faceRect, face, true);
            faceImage.pixelsPerUnitMultiplier = UiBuildKit.NativeSize(face).y / size.y;
            return MakeButton(root, faceImage);
        }

        /// <summary>Makes <paramref name="root"/> a button that <paramref name="face"/> draws: its states and its press feedback.</summary>
        private static UnityEngine.UI.Button MakeButton(RectTransform root, UnityEngine.UI.Image face)
        {
            var button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = face;

            // GDD §12: pressed is 8% darker; disabled is muted at half opacity.
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            // After the Button, so a presenter that answers the press with its own cue is heard first.
            var feedback = root.gameObject.AddComponent<ButtonFeedback>();
            var so = new SerializedObject(feedback);
            so.FindProperty("_selectable").objectReferenceValue = button;
            so.FindProperty("_cue").intValue = (int)SoundCue.UiTap;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        private static void PlaceShadow(string name, Transform parent, Vector2 ownerCentre, Vector2 parentCentre, Sprite sprite, float drop, float opacity)
        {
            RectTransform rect = UiBuildKit.CreateAt(
                name, parent, ownerCentre + new Vector2(0f, drop), parentCentre, UiBuildKit.NativeSize(sprite));
            UiBuildKit.AddImage(rect, sprite, false).color = new Color(1f, 1f, 1f, opacity);
        }

        /// <summary>Places a sprite at a size (canvas units), centred on a mockup point: <paramref name="art"/>'s position is the centre, its size the size.</summary>
        private static UnityEngine.UI.Image PlaceArt(string name, Transform parent, Rect art, Vector2 parentCentre, Sprite sprite)
        {
            RectTransform rect = UiBuildKit.CreateAt(name, parent, art.position, parentCentre, art.size);
            return UiBuildKit.AddImage(rect, sprite, false);
        }

        /// <summary>A label in V2's type, its ink on V2's (see <see cref="LabelSpec"/>).</summary>
        private static TextMeshProUGUI Label(string name, Transform parent, Vector2 parentCentre, string text, TMP_FontAsset font, LabelSpec spec)
        {
            float[] inkBox =
            {
                spec.Ink.xMin / UiBuildKit.ScaleX, spec.Ink.yMin / UiBuildKit.ScaleY, spec.Ink.xMax / UiBuildKit.ScaleX, spec.Ink.yMax / UiBuildKit.ScaleY,
            };
            return UiBuildKit.PlaceLabel(name, parent, parentCentre, text, null, font, spec.Size, spec.Spacing, inkBox, spec.Align, UiBuildKit.Hex(spec.Color));
        }

        /// <summary>A label's V2 type: size and colour, the box round its glyphs in canvas units, the edge it keeps, letter spacing.</summary>
        private readonly struct LabelSpec
        {
            public readonly float Size;
            public readonly string Color;
            public readonly Rect Ink;
            public readonly string Align;
            public readonly float Spacing;

            public LabelSpec(float size, string color, Rect ink, string align, float spacing = 0f)
            {
                Size = size;
                Color = color;
                Ink = ink;
                Align = align;
                Spacing = spacing;
            }
        }

        private static Sprite Ui(string path, float sideBorder = 0f)
        {
            return UiBuildKit.LoadSprite($"UI/{path}", UiPixelsPerUnit, new Vector4(sideBorder, 0f, sideBorder, 0f));
        }

        /// <summary>A V2 kit sprite, as ArtSource/Kit/V2.json imports it (KitImporter): its settings are left alone.</summary>
        private static Sprite Kit(string path)
        {
            return UiBuildKit.LoadSprite($"UI/{path}", 0f);
        }

        private static void BuildScene(GameObject prefab)
        {
            UiBuildKit.EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiBuildKit.Hex("#F7EFE5");
            camera.cullingMask = 0;
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            var canvasObject = new GameObject(
                "MenuCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiBuildKit.CanvasWidth, UiBuildKit.CanvasHeight);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(canvasObject.transform, false);

            var eventSystem = new GameObject(
                "EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
