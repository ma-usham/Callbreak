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
        private const float BackgroundPixelsPerUnit = 100f;

        private static readonly Vector2 Screen = UiBuildKit.ScreenCentre;

        // The mockup's lettering is narrower than Inter and Source Serif 4 at their natural
        // width, so each label carries a horizontal scale and spacing measured against it.
        // Size, left edge or centre x, baseline y, horizontal scale, character spacing.
        private static readonly LabelSpec PlayOnlineText = new LabelSpec(72f, 703.3f, 657.6f, 0.886f, 0f);
        private static readonly LabelSpec PlayWithFriendsText = new LabelSpec(35.4f, 589.3f, 793.4f, 0.747f, -2f);
        private static readonly LabelSpec PlayOfflineText = new LabelSpec(35.4f, 971.7f, 793.4f, 0.842f, -2f);
        private static readonly LabelSpec TaglineText = new LabelSpec(21.6f, 829.6f, 883.5f, 1f, 26.2f);
        private static readonly LabelSpec GreetingText = new LabelSpec(27f, 206.6f, 82.1f, 1f, 0f);
        private static readonly LabelSpec PlayerNameText = new LabelSpec(39.6f, 205.4f, 117.5f, 0.966f, 0f);

        // Mockup 23's balance, "250" in Inter Bold: its ink 52.3 x 22.9 at 1202.6..1254.9 x 88.4..111.3
        // (the digits' overshoot puts the baseline 0.3 above the ink's foot).
        private static readonly LabelSpec BalanceText = new LabelSpec(35.23f, 1228.65f, 111.02f, 0.933f, 0f);

        // The "Resume match" banner is on no mockup: it is the ivory secondary button, smaller,
        // standing where the footer line is (the two never show together).
        private static readonly Vector2 ResumeBannerCentre = new Vector2(835.75f, 877f);
        private static readonly Vector2 ResumeBannerSize = new Vector2(336f, 64f);
        private const float ResumeLabelSize = 27f;

        // The coin wallet (mockup 23): the ivory pill (the kit's secondary pill, which the wallet is
        // drawn like) measured on its edges, the room the balance may grow into between the coin and
        // the "+", and the ShopUI kit's coin and crimson "+" (ArtSource/Working/ShopUI/notes.md) at the
        // rects its notes give.
        private static readonly Rect WalletPill = new Rect(1107.5f, 64.5f, 235.5f, 69.5f);
        private static readonly Rect WalletCoin = new Rect(1130.53f, 73.43f, 52.55f, 52.55f);
        private const float WalletBalanceRoom = 84f;
        private static readonly Rect WalletPlus = new Rect(1276.6f, 75.4f, 48.4f, 48.4f);
        private const string WalletCoinPath = "Assets/_Project/Art/UI/Icons/Icon_Coin.png";
        private const string WalletPlusPath = "Assets/_Project/Art/UI/Buttons/UI_ButtonPlus_Normal.png";
        private const float WalletShadowDrop = 2f;
        private const float WalletShadowOpacity = 0.4f;

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

        // The Daily Rewards button (the owner, 8 Oct 2026): the gear's disc and shadow with ChatGPT's
        // gift glyph (ArtSource/Working/DailyRewards) and a red dot on its top right rim while today's
        // reward is ready. The glyph is drawn larger than the shop's and the gear's (57 x 61 of ink
        // against 54 x 45 and 54 x 53), so it is shown at DailyRewardsIconScale of its size, which
        // stands it between the two (the owner, 15 Oct 2026: "make it appear good like shop and setting").
        private const string DailyRewardsIconPath = "Icons/Icon_DailyReward.png";
        private const float DailyRewardsIconScale = 0.8f;
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

            GameObject root = BuildHierarchy();
            UiBuildKit.EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

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

        private static GameObject BuildHierarchy()
        {
            TMP_FontAsset serifBold = UiBuildKit.LoadFont("SourceSerif4-Bold");
            TMP_FontAsset sansSemiBold = UiBuildKit.LoadFont("Inter-SemiBold");
            TMP_FontAsset sansBold = UiBuildKit.LoadFont("Inter-Bold");

            Sprite circle = Ui("Buttons/UI_ButtonCircle_Normal.png");
            Sprite circleShadow = Ui("Buttons/UI_Shadow_Circle.png");
            Sprite secondary = Ui("Buttons/UI_ButtonSecondary_Normal.png", 117f);
            Sprite secondaryShadow = Ui("Buttons/UI_Shadow_Secondary.png", 150f);
            Sprite chevron = Ui("Icons/Icon_Chevron.png");

            var rootObject = new GameObject("MainMenu", typeof(RectTransform));
            var root = (RectTransform)rootObject.transform;
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
            Place("Logo", centre, new Vector2(834.75f, 180.25f), Screen, Ui("Ornaments/Logo_Callbreak.png"));

            // The fan and its shadow rock together about the fan's foot, which the Play Online
            // button covers (GDD screen 00: the fan "idles with a gentle sway"). Their own canvas
            // keeps the rest of the screen from being batched again every frame.
            var fanCentre = new Vector2(848f, 479.25f);
            RectTransform fan = UiBuildKit.CreateAt("CardFanGroup", centre, FanFoot, Screen, Vector2.zero);
            fan.gameObject.AddComponent<Canvas>();
            var fanSway = fan.gameObject.AddComponent<IdleSway>();
            PlaceShadow("CardFanShadow", fan, fanCentre, FanFoot, Ui("Ornaments/UI_Shadow_MenuFan.png"), 6f, 0.3f);
            UnityEngine.UI.Image[] fanFaces = BuildFanCards(fan);

            var playOnlineCentre = new Vector2(835.75f, 636.25f);
            Sprite primary = Ui("Buttons/UI_ButtonPrimary_Normal.png", 202f);
            UnityEngine.UI.Button playOnline = CreateButton(
                "PlayOnlineButton", centre, playOnlineCentre, UiBuildKit.NativeSize(primary), primary,
                Ui("Buttons/UI_Shadow_Primary.png", 286f), 1f, 0f, 10f, 0.5f);
            Place("Icon", playOnline.transform, new Vector2(641.25f, 634.75f), playOnlineCentre, Ui("Icons/Icon_Spade_Gold.png"));
            TextMeshProUGUI playOnlineLabel = Label(
                "Label", playOnline.transform, playOnlineCentre, "PLAY ONLINE", serifBold, Color.white, PlayOnlineText, false);
            Localize(playOnlineLabel, "ui.main_menu.play_online");
            playOnlineLabel.enableVertexGradient = true;
            Color labelFoot = UiBuildKit.Hex("#E8D4B8");
            playOnlineLabel.colorGradient = new VertexGradient(Color.white, Color.white, labelFoot, labelFoot);
            playOnlineLabel.fontSharedMaterial = UiBuildKit.LoadShadowPreset(
                serifBold, "ButtonLabel", new Color(0.29f, 0.03f, 0.04f, 0.9f), new Vector2(0.25f, -0.35f), 0.2f);

            Color ink = UiBuildKit.Hex("#0B0C0E");
            Vector2 secondarySize = UiBuildKit.NativeSize(secondary);

            // Play Offline is 4 mockup pixels narrower than Play with Friends; the same sliced
            // sprite serves both.
            var friendsCentre = new Vector2(656.5f, 783.25f);
            UnityEngine.UI.Button playWithFriends = CreateButton(
                "PlayWithFriendsButton", centre, friendsCentre, secondarySize, secondary, secondaryShadow, 1f, 0f, 2f, 0.4f);
            Place("Icon", playWithFriends.transform, new Vector2(544f, 781.25f), friendsCentre, Ui("Icons/Icon_Friends.png"));
            Localize(
                Label("Label", playWithFriends.transform, friendsCentre, "Play with Friends", sansBold, ink, PlayWithFriendsText, false),
                "ui.main_menu.play_with_friends");
            Place("Chevron", playWithFriends.transform, new Vector2(796f, 783.5f), friendsCentre, chevron);

            var offlineCentre = new Vector2(1015.5f, 783.25f);
            float narrower = (335f - 331f) * UiBuildKit.ScaleX;
            UnityEngine.UI.Button playOffline = CreateButton(
                "PlayOfflineButton", centre, offlineCentre, secondarySize - new Vector2(narrower, 0f), secondary, secondaryShadow, 1f, -narrower, 2f, 0.4f);
            Place("Icon", playOffline.transform, new Vector2(924.5f, 782f), offlineCentre, Ui("Icons/Icon_Offline.png"));
            Localize(
                Label("Label", playOffline.transform, offlineCentre, "Play Offline", sansBold, ink, PlayOfflineText, false),
                "ui.main_menu.play_offline");
            Place("Chevron", playOffline.transform, new Vector2(1140.75f, 783.75f), offlineCentre, chevron);

            RectTransform footer = BuildFooter(centre, sansSemiBold);
            UnityEngine.UI.Button resume = BuildResumeBanner(centre, sansBold, ink, chevron);

            // Header, left: who you are. The mockup's ring is plain white, not the ivory button.
            var profileCentre = new Vector2(127.5f, 98f);
            Sprite ring = Ui("Avatars/UI_AvatarRing.png");
            UnityEngine.UI.Button profile = CreateButton(
                "ProfileButton", headerLeft, profileCentre, UiBuildKit.NativeSize(ring), ring, circleShadow, 104f / 92f, 0f, 6f, 0.15f);
            UnityEngine.UI.Image avatar = Place("Avatar", profile.transform, new Vector2(127.25f, 98f), profileCentre, Ui("Avatars/Avatar_Default.png"));
            Localize(
                Label("Greeting", headerLeft, Screen, "Namaste,", sansSemiBold, UiBuildKit.Hex("#9F7558"), GreetingText, false),
                "ui.main_menu.greeting");
            TextMeshProUGUI playerName = Label(
                "PlayerName", headerLeft, Screen, "Player", sansBold, UiBuildKit.Hex("#151617"), PlayerNameText, false);

            // Header, right: the coin wallet, settings and shop, then Daily Rewards and No Ads under the shop
            UnityEngine.UI.Button earnCoins = BuildWallet(headerRight, sansBold, secondary, out TextMeshProUGUI coins);

            var settingsCentre = new Vector2(1402f, 97f);
            UnityEngine.UI.Button settings = CreateButton(
                "SettingsButton", headerRight, settingsCentre, UiBuildKit.NativeSize(circle), circle, circleShadow, 1f, 0f, 4f, 0.2f);
            Place("Icon", settings.transform, new Vector2(1401.1f, 96.85f), settingsCentre, Ui("Icons/Icon_Settings.png"));

            RectTransform divider = UiBuildKit.CreateAt(
                "Divider", headerRight, new Vector2(1474.5f, 98.4f), Screen, UiBuildKit.FromMock(2f, 65.75f));
            UiBuildKit.AddImage(divider, null, false).color = UiBuildKit.Hex("#86604A");

            var shopCentre = new Vector2(1547.5f, 97f);
            UnityEngine.UI.Button shop = CreateButton(
                "ShopButton", headerRight, shopCentre, UiBuildKit.NativeSize(circle), circle, circleShadow, 1f, 0f, 4f, 0.2f);
            Place("Icon", shop.transform, new Vector2(1548.75f, 96f), shopCentre, Ui("Icons/Icon_Shop.png"));

            UnityEngine.UI.Button dailyRewards = BuildDailyRewardsButton(
                headerRight, UnderShop(shopCentre, 1), circle, circleShadow, out GameObject dailyRewardsDot);
            UnityEngine.UI.Button noAds = BuildNoAdsButton(headerRight, UnderShop(shopCentre, 2), circle, circleShadow);

            RectTransform bannerSlot = BuildBannerSlot(safeArea);

            var view = rootObject.AddComponent<MainMenuView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_screenId").intValue = (int)ScreenId.MainMenu;
            serialized.FindProperty("_content").objectReferenceValue = content;
            serialized.FindProperty("_resumeButton").objectReferenceValue = resume;
            serialized.FindProperty("_footer").objectReferenceValue = footer.gameObject;
            serialized.FindProperty("_avatar").objectReferenceValue = avatar;
            serialized.FindProperty("_fanSway").objectReferenceValue = fanSway;
            SerializedProperty faces = serialized.FindProperty("_fanFaces");
            faces.arraySize = fanFaces.Length;
            for (int i = 0; i < fanFaces.Length; i++)
            {
                faces.GetArrayElementAtIndex(i).objectReferenceValue = fanFaces[i];
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

        private static void BuildBackground(RectTransform root)
        {
            var go = new GameObject("Background", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            Stretch(rect);
            UiBuildKit.AddImage(rect, UiBuildKit.LoadSprite("Backgrounds/BG_Menu_Clean.png", BackgroundPixelsPerUnit), false);

            // Backgrounds bleed to the screen edge on every aspect ratio (GDD §12).
            var fitter = go.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = UiBuildKit.CanvasWidth / UiBuildKit.CanvasHeight;
        }

        private static RectTransform BuildFooter(RectTransform centre, TMP_FontAsset font)
        {
            Color line = UiBuildKit.Hex("#A4856A");
            RectTransform footer = UiBuildKit.CreateAt("Footer", centre, Screen, Screen, Vector2.zero);

            RectTransform left = UiBuildKit.CreateAt("LineLeft", footer, new Vector2(553.25f, 876f), Screen, UiBuildKit.FromMock(58.5f, 1.9f));
            UiBuildKit.AddImage(left, null, false).color = line;

            Localize(
                Label("Tagline", footer, Screen, "A GAME THAT BRINGS US CLOSER", font, UiBuildKit.Hex("#9D795D"), TaglineText, true),
                "ui.main_menu.tagline");

            RectTransform right = UiBuildKit.CreateAt("LineRight", footer, new Vector2(1112.5f, 876f), Screen, UiBuildKit.FromMock(63f, 1.9f));
            UiBuildKit.AddImage(right, null, false).color = line;
            return footer;
        }

        /// <summary>
        /// GDD screen 00: "If an unfinished offline match exists, a banner offers 'Resume match'."
        /// Hidden in the prefab; the presenter shows it in the footer line's place.
        /// </summary>
        private static UnityEngine.UI.Button BuildResumeBanner(RectTransform centre, TMP_FontAsset font, Color ink, Sprite chevron)
        {
            UnityEngine.UI.Button banner = DialogBuilder.KitButton(
                "ResumeBanner", centre, ResumeBannerSize, "UI/Buttons/UI_ButtonSecondary_Normal.png", "UI/Buttons/UI_Shadow_Secondary.png", 2f, 0.4f);
            var rect = (RectTransform)banner.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(
                (ResumeBannerCentre.x - Screen.x) * UiBuildKit.ScaleX, -(ResumeBannerCentre.y - Screen.y) * UiBuildKit.ScaleY);

            // The label is centred in the room the chevron leaves.
            Vector2 chevronSize = UiBuildKit.NativeSize(chevron) * 0.8f;
            float chevronInset = 34f;
            var labelObject = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rect, false);
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(chevronInset, 0f);
            labelRect.offsetMax = new Vector2(-(chevronInset + chevronSize.x), 0f);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = ResumeLabelSize;
            label.color = ink;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.text = "Resume match";
            label.characterHorizontalScale = PlayOfflineText.HorizontalScale;
            label.characterSpacing = PlayOfflineText.Spacing;
            Localize(label, "ui.main_menu.resume_match");

            var chevronObject = new GameObject("Chevron", typeof(RectTransform));
            var chevronRect = (RectTransform)chevronObject.transform;
            chevronRect.SetParent(rect, false);
            chevronRect.anchorMin = chevronRect.anchorMax = chevronRect.pivot = new Vector2(1f, 0.5f);
            chevronRect.sizeDelta = chevronSize;
            chevronRect.anchoredPosition = new Vector2(-chevronInset, 0f);
            UiBuildKit.AddImage(chevronRect, chevron, false);

            banner.gameObject.SetActive(false);
            return banner;
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
            Sprite shadow = Ui("Buttons/UI_Shadow_Secondary.png", 150f);
            Vector2 shadowSize = size + ((UiBuildKit.NativeSize(shadow) - UiBuildKit.NativeSize(pill)) * scale);
            RectTransform shadowRect = UiBuildKit.CreateAt("Shadow", wallet, centre + new Vector2(0f, WalletShadowDrop), centre, shadowSize);
            UnityEngine.UI.Image shadowImage = UiBuildKit.AddImage(shadowRect, shadow, false);
            shadowImage.color = new Color(1f, 1f, 1f, WalletShadowOpacity);
            shadowImage.pixelsPerUnitMultiplier = 1f / scale;
            RectTransform pillRect = UiBuildKit.CreateAt("Pill", wallet, centre, centre, size);
            UiBuildKit.AddImage(pillRect, pill, false).pixelsPerUnitMultiplier = 1f / scale;

            RectTransform coin = UiBuildKit.CreateAt("Coin", wallet, WalletCoin.center, centre, UiBuildKit.FromMock(WalletCoin.width, WalletCoin.height));
            UiBuildKit.AddImage(coin, UiBuildKit.LoadSpriteAt(WalletCoinPath, 0f), false);

            balance = Label("Balance", wallet, centre, "250", font, UiBuildKit.Hex("#181C22"), BalanceText, true);
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
                "NoAdsButton", headerRight, centre, UiBuildKit.NativeSize(circle), circle, circleShadow, 1f, 0f, 4f, 0.2f);
            Place("Icon", button.transform, centre, centre, Ui(NoAdsIconPath));
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
            float radius = UiBuildKit.NativeSize(circle).y / UiBuildKit.ScaleY / 2f;
            UnityEngine.UI.Button button = CreateButton(
                "DailyRewardsButton", headerRight, centre, UiBuildKit.NativeSize(circle), circle, circleShadow, 1f, 0f, 4f, 0.2f);
            Sprite icon = Ui(DailyRewardsIconPath);
            RectTransform iconRect = UiBuildKit.CreateAt("Icon", button.transform, centre, centre, UiBuildKit.NativeSize(icon) * DailyRewardsIconScale);
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
                    (RectTransform)noAdsRect.parent, UnderShop(shopCentre, 1), Ui("Buttons/UI_ButtonCircle_Normal.png"), Ui("Buttons/UI_Shadow_Circle.png"), out GameObject dot);
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
        /// The four aces, each a card with its own soft shadow. The prefab shows the default deck's;
        /// the presenter puts in the equipped deck's. Returns the face images indexed by suit.
        /// </summary>
        private static UnityEngine.UI.Image[] BuildFanCards(RectTransform fan)
        {
            CardSpriteProvider cards = TableCardBuilder.EnsureLibrary();
            Sprite shadow = UiBuildKit.LoadSpriteAt(CardShadowPath, 0f);
            var faces = new UnityEngine.UI.Image[4];
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

                RectTransform faceRect = StretchedChild("Face", rect);
                faces[(int)card.Suit] = UiBuildKit.AddImage(faceRect, face, false);
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
        /// and whatever the caller adds. The shadow sprite is padded, so it keeps its own size:
        /// <paramref name="shadowScale"/> reuses the shadow of one shape under a larger face of the
        /// same shape, and <paramref name="shadowWidthDelta"/> follows a sliced face made wider or
        /// narrower.
        /// </summary>
        private static UnityEngine.UI.Button CreateButton(
            string name,
            Transform parent,
            Vector2 centre,
            Vector2 size,
            Sprite face,
            Sprite shadow,
            float shadowScale,
            float shadowWidthDelta,
            float shadowDrop,
            float shadowOpacity)
        {
            RectTransform root = UiBuildKit.CreateAt(name, parent, centre, Screen, size);

            Vector2 shadowSize = UiBuildKit.NativeSize(shadow) * shadowScale;
            shadowSize.x += shadowWidthDelta;
            RectTransform shadowRect = UiBuildKit.CreateAt("Shadow", root, centre + new Vector2(0f, shadowDrop), centre, shadowSize);
            UiBuildKit.AddImage(shadowRect, shadow, false).color = new Color(1f, 1f, 1f, shadowOpacity);

            RectTransform faceRect = UiBuildKit.CreateAt("Face", root, centre, centre, size);
            UnityEngine.UI.Image faceImage = UiBuildKit.AddImage(faceRect, face, true);
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

        /// <summary>Places a sprite pixel for pixel, centred on a mockup point.</summary>
        private static UnityEngine.UI.Image Place(string name, Transform parent, Vector2 centre, Vector2 parentCentre, Sprite sprite)
        {
            RectTransform rect = UiBuildKit.CreateAt(name, parent, centre, parentCentre, UiBuildKit.NativeSize(sprite));
            return UiBuildKit.AddImage(rect, sprite, false);
        }

        private static void PlaceShadow(string name, Transform parent, Vector2 ownerCentre, Vector2 parentCentre, Sprite sprite, float drop, float opacity)
        {
            RectTransform rect = UiBuildKit.CreateAt(
                name, parent, ownerCentre + new Vector2(0f, drop), parentCentre, UiBuildKit.NativeSize(sprite));
            UiBuildKit.AddImage(rect, sprite, false).color = new Color(1f, 1f, 1f, opacity);
        }

        private static TextMeshProUGUI Label(
            string name, Transform parent, Vector2 parentCentre, string text, TMP_FontAsset font, Color color, LabelSpec spec, bool centred)
        {
            TextMeshProUGUI label = UiBuildKit.CreateLabel(
                name, parent, parentCentre, text, font, spec.Size, color, spec.X, spec.Baseline, centred);
            label.characterHorizontalScale = spec.HorizontalScale;
            label.characterSpacing = spec.Spacing;
            return label;
        }

        private readonly struct LabelSpec
        {
            public readonly float Size;
            public readonly float X;
            public readonly float Baseline;
            public readonly float HorizontalScale;
            public readonly float Spacing;

            public LabelSpec(float size, float x, float baseline, float horizontalScale, float spacing)
            {
                Size = size;
                X = x;
                Baseline = baseline;
                HorizontalScale = horizontalScale;
                Spacing = spacing;
            }
        }

        private static Sprite Ui(string path, float sideBorder = 0f)
        {
            return UiBuildKit.LoadSprite($"UI/{path}", UiPixelsPerUnit, new Vector4(sideBorder, 0f, sideBorder, 0f));
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
