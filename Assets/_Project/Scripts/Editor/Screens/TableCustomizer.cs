using System.Collections.Generic;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 08, the table. The layout gives the HUD, the seats' plates and pills and the empty
    /// areas for cards; this adds what the game fills in at runtime and what the mockup does not
    /// draw:
    /// the equipped table in place of the plate, filling the screen (the owner's shop of 4 Oct 2026;
    /// V2, 28 Oct 2026);
    /// the layers (table, hand, HUD as nested canvases, GDD §23); the card pool; the hand with its
    /// touch area, pinned to the bottom edge, its cards drawn a little larger than the shared card;
    /// the trick's cross of slots (ADR 060) and the opponents' fans, placed as mockup 07 draws them; a
    /// <see cref="SeatView"/> per seat, with the timer ring, seconds disc and call bubble the
    /// opponents' seats need (derived from the local seat's) and the speech box for quick phrases;
    /// the seats' profiles as the owner arranged them (the side seats lower, the top seat's name
    /// above its avatar, the local seat's below it); the turn prompt, the trick counter, the emote
    /// picker, the "Reconnecting…" cover and the "I'm back" banner (derived from kit pills, panels
    /// and buttons); and each seat's chip for "Away", "Reconnecting…" and "Bot playing" (the kit's
    /// tag pill, hidden until there is something to say; a bot that fills a seat has none, ADR 025).
    /// The status pills hug their words ("3-7" in a short pill, "Choosing bid" in a longer one).
    /// Names get a width: a long one shrinks, then ends in an ellipsis. Controls smaller than
    /// 88 px answer to touches on 88 px (GDD §16) without being drawn any larger.
    /// All mockup measurements are in mockup pixels (1672 × 941), as in the layout.
    /// </summary>
    public static class TableCustomizer
    {
        // The table's pills, timer ring and badge come from the shared kit (ArtSource/Kit/Panels.map.csv).
        private const string TableArt = UiBuildKit.ArtRoot + "/UI/Table";

        // ---- The local hand. Mockup 08 draws its cards unevenly (88 to 114 px apart, 116 to 136 wide);
        // this is the even fan that fits its thirteen card corners best, with the tops of the middle
        // cards at y 716. Since 30 Oct 2026 (ADR 060) it hangs HandDrop lower, and the turn prompt above
        // it with it, so that the trick fits above them.
        private const float HandDrop = 17.4f;                   // 20 canvas units
        private static readonly Vector2 HandCentre = new Vector2(868.6f, 839.05f + HandDrop);
        private const float HandWidthFraction = 0.772f;         // 94.5 px between cards on the mockup; the GDD says 70%
        private const float HandLift = 14.4f;                   // 12.5 mockup px; the GDD says 28

        // The owner asked for slightly bigger cards in the hand (2 Oct 2026): they are drawn 9% larger
        // than the shared card, which the trick, the fans and the deal keep. The hand keeps its width,
        // so they overlap a little more; the tops of the middle cards come up to y 705, and the touch
        // area and the turn prompt (below) went up with them.
        private const float HandCardScale = 1.09f;
        private const float HandTouchTop = 673f + HandDrop;

        // Half the bend mockup 08 draws (a radius of 3740, end cards leaning 10°): the owner asked for
        // a gentler arc, so a full hand's end cards lean 5° and hang 28 px lower (ADR 021).
        private const float HandArcRadius = 7480f;

        // ---- The trick, by relative seat (0 bottom, 1 right, 2 top, 3 left). Mockup 08 drew its cards
        // 128.4 px wide in the table's perspective, 0.64 of their height, close round the middle. Seen
        // from straight above (ADR 059) a card is drawn full height, and the top and the bottom card lay
        // over each other (the owner, 30 Oct 2026: "while playing the card gets overlap. either reduce
        // the card size or make move the player card quite below and move the top player quite upside
        // so that the card fits"; ADR 060). So the four cards are smaller and form a cross round the
        // trick's centre, TrickGap apart and none over another: the top and the bottom card one above
        // the other, the side cards between them. The cross fits between the top seat's fan and the
        // turn prompt with the banner's room made too (TableView brings the top seat and the trick down).
        private static readonly Vector2 TrickCentre = new Vector2(836.2f, 466.5f);
        private const float TrickCardWidth = 86f;               // 98.75 canvas units, 155 high
        private const float TrickGap = 9f;

        // Mockup 08 turns the side cards 5.5° (the GDD says ±6°). Drawn lying in the table's
        // perspective, a turned card looked skewed and larger than the others, so every trick card
        // lies straight (ADR 024), and since ADR 059 it is seen from straight above, upright.
        private static readonly float[] TrickTurns = { 0f, 0f, 0f, 0f };

        // ---- Opponent fans, as the bidding mockup draws them since 2 Oct 2026 (ADR 026; measured on
        // 07-bidding.png). Its card backs are 46 px long; at a real card's shape that is 29 wide.
        private const float FanCardWidth = 29f;

        // A side seat's fan turns about a point behind the profile (from the avatar's centre: away
        // from the table, and down): 13 cards 5.35° apart, from 126° (the bottom card, in front)
        // up to 62°, measured clockwise from straight up on the left seat.
        private static readonly Vector2 SideFanPivot = new Vector2(63.8f, 36.2f);
        private const float SideFanRadius = 158f;
        private const float SideFanBottomAngle = 125.9f;
        private const float SideFanStep = 5.35f;

        // The top seat's hangs below its avatar (its name and status pill are above it since 2 Oct
        // 2026, so nothing covers the fan): it turns about a point 164 px above the avatar's centre,
        // 13 cards 4.77° apart around straight down, the middle ones in front.
        private const float TopFanPivotAbove = 163.7f;
        private const float TopFanRadius = 242f;
        private const float TopFanStep = 4.77f;

        // ---- Derived pieces (not on the mockup): where they sit.
        private static readonly Vector2 PromptCentre = new Vector2(869.3f, 652f + HandDrop);
        private const float PromptHeight = 38f;
        private static readonly Vector2 TrickLabelCentre = new Vector2(836f, 104f);
        // Six phrases, each an 88 px touch target with 16 px between them (GDD §16): 76.8 and 14 mockup px.
        // The panel opens to the left of the emote button, 112 px in from the right edge (ADR 021),
        // and since the side seats came down (ADR 027) it starts below the right seat's status pill.
        private static readonly Rect EmotePanel = new Rect(1112f, 545.6f, 448f, 294.4f);
        private const float BubbleHeight = 33f;

        // While the next quick phrase has to wait, the emote button is covered in this colour, which
        // sweeps away as the wait runs out (ADR 024).
        private static readonly Color EmoteCooldownColor = new Color(0f, 0f, 0f, 0.5f);

        // The "Reconnecting…" cover: the dim over the table, and its words, the speech box's ivory
        // (the loading screen's dark brown would not read on the dim).
        private const float ReconnectingDim = 0.45f;
        private static readonly Color ReconnectingWordsColor = new Color32(0xFD, 0xF5, 0xED, 0xFF);

        // Its art, the Dhaka Ring (the owner's pick, 30 Oct 2026; Figma "Reconnecting A · Dhaka Ring"):
        // a ring of gold diamonds, a bright one running round, turning round a still ivory seal with
        // the crimson spade. Tools/ArtPipeline/NewUI/reconnecting.py draws both at two sprite pixels to
        // a canvas unit; their sizes here are in canvas units. Figma draws the pieces in mockup px: the
        // bright diamond's tip RingReach above the seal's centre, the words' middle RingToWords below.
        private const float RingSize = 288f;
        private const float SealSize = 212f;
        private const float RingReach = 110.1f;
        private const float RingToWords = 152.5f;

        /// <summary>GDD §16 "Touch targets are ≥ 88 px", in canvas units.</summary>
        public const float MinTouch = 88f;

        // ---- The seats as the owner arranged them on 2 Oct 2026 (mockup px).
        // The side seats sit lower, nearer the player: the whole seat moves, fan and all.
        private const float SideSeatDrop = 55f;

        // The top seat's name plate and status pill stand above its avatar, just under the trump
        // line, and its fan hangs below the avatar where nothing covers it (as mockup 19 draws it):
        // the avatar comes down to make room. The trump line is gone (3 Oct 2026), and since 30 Oct
        // 2026 the whole seat stands TopSeatRise higher, so that the trick fits under it (ADR 060).
        private const float TopProfileGap = 5f;                 // from the trump line to the name plate
        private const float TopRingGap = 1f;                    // from the status pill to the avatar's ring
        private const float TopSeatRise = 24.4f;                // 28 canvas units

        // The local seat's name plate and status pill are below its avatar, centred under it at the
        // opponents' widths; the pill keeps its place at the bottom and the avatar, its timer ring
        // and its seconds disc go up to the plate, which lies over the ring's foot as an opponent's.
        private const float LocalProfileX = 90f;

        // The seconds disc sits on a free side of the avatar (from its centre): the outer top corner
        // of a side seat; on the left of the top and the local avatar, whose name plates, chips and
        // speech boxes take the other sides.
        private static readonly Vector2[] DiscOffsets =
        {
            new Vector2(-52f, 0f), new Vector2(29f, -29f), new Vector2(-52f, 0f), new Vector2(-29f, -29f),
        };

        // The call bubble (a call as it is placed, "auto"), from the avatar's centre: right of the
        // local avatar, towards the hand; below a side seat's status pill; left of the top avatar,
        // past its seconds disc (its fan hangs below it, its speech box is on the right).
        private static readonly Vector2[] BubbleOffsets =
        {
            new Vector2(84f, 0f), new Vector2(0f, 123f), new Vector2(-112f, 0f), new Vector2(0f, 123f),
        };

        // ---- The speech box for quick phrases (ADR 027; the owner's reference image, as mockup 18
        // draws it): an ivory box with a golden rim and its tail at the bottom-left corner pointing
        // down at the speaker, the phrase in bold Heading Burgundy. It sits just above the avatar
        // (to the upper right of the top seat's, whose name and pill are above it) and grows away
        // from its tail: rightwards, leftwards on the right seat, whose tail is mirrored.
        // The box is 9-sliced, its soft shadow inside the image: the rim is inset from the image's
        // edges by these margins (canvas units). The tail is an overlay drawn over the box's foot,
        // its bottom-left corner this far from the box image's; its tip ends this far from the rim's
        // bottom-left corner. The image is kept at least 112 wide (the tail's top on the straight
        // part of the rim) and 71 high (the slices at full size).
        private const string ChatBoxArt = TableArt + "/UI_ChatBubble.png";
        private const string ChatTailArt = TableArt + "/UI_ChatBubble_Tail.png";
        private const float ChatRimSide = 11.58f;
        private const float ChatRimTop = 5.08f;
        private const float ChatRimBottom = 17.79f;
        private static readonly Vector2 ChatTailSize = new Vector2(76f, 72f);
        private static readonly Vector2 ChatTailCorner = new Vector2(0.5f, -46.5f);
        private static readonly Vector2 ChatTailTip = new Vector2(2.25f, -48.2f);
        private const float ChatRimHeight = 62f;                // canvas units: mockup 18's box is 56 mockup px high
        private const float ChatFontSize = 30f;                 // canvas units: capitals 19 mockup px high, as mockup 18's
        private const float ChatTextPadding = 52f;              // canvas units from the rim to the words, both sides together
        private const float ChatMinWidth = 140f;                // canvas units, the image
        private static readonly Color ChatTextColor = new Color32(0x7A, 0x10, 0x16, 0xFF);    // Heading Burgundy (GDD §12)

        // The tail's tip stops this far outside the avatar's timer ring (mockup px), up and 30° over
        // to the box's side, so that the tail, which leans 30°, points at the avatar's centre. The
        // top seat's box stands right of its status pill, its rim's foot this far above the avatar's
        // centre, its tail reaching down towards the avatar (and clear of the fan below it).
        private const float ChatTipGap = 8f;
        private const float ChatTipTurn = 30f;
        private const float ChatBesideGap = 8f;
        private const float ChatBesideRise = 30f;

        // The local seat's box leans less and stands a little higher, so that its rim keeps clear of
        // the tops of the hand's first cards (y 737 to 728 there); its tail then points a little left
        // of the avatar's centre.
        private const float LocalChatTipGap = 18f;
        private const float LocalChatTipTurn = 20f;

        // ---- The seat chip ("Away", "Reconnecting…", "Bot playing"): the kit's tag pill on the
        // top edge of the avatar (the bottom edge of the top seat's, whose name and pill are above).
        private const float ChipHeight = 24f;
        private const float ChipTextScale = 0.8f;
        private const float ChipSpinnerSize = 15f;

        // ---- The round line under the logo (the owner, 28-29 Oct 2026, ADR 057), canvas units: on the
        // left, centred under the CALLBREAK logo this far below it, on the seats' status pill so it
        // reads over the scenery; the mockup's place at the top centre and its ornaments go.
        private const float RoundUnderLogoGap = 6f;

        // ---- The banner ad at the top of the table (the owner, 26 Oct 2026, ADR 051; ADR 057),
        // canvas units. While a banner may come, its slot spans the top of the safe area from this far
        // right of the logo and the round line to the right edge, as tall as the largest standard
        // banner (320 x 50 dp is 150 units on a 360 dp-tall phone) and a little more; the banner sits
        // in its top-right corner. TableView brings the top seat and the trick down to clear it.
        private const float BannerGap = 48f;
        private const float BannerSlotHeight = 155f;

        // ---- Scores and Settings under Home (the owner, 28 Oct 2026, ADR 057), canvas units: the
        // left column, each button centred on Home's middle, this far below the button above. The
        // buttons show their icons only (the owner, the same day: no "Scores" and "Settings" words).
        // The round line's pill is as wide as its words and this much more.
        private const float ColumnGap = 22f;
        private const float LabelPillPadding = 30f;

        // ---- The status pill hugs its words (the owner, 3 Oct 2026: the pill drawn for "Bid 3 • Won 1"
        // was too big for "3-7"). It keeps its centre under the name plate and its height. The words
        // keep about the margins mockup 08 draws round "Bid 3 • Won 1" (22 canvas units a side there,
        // 23 here, which keeps them clear of the round ends). The narrowest pill holds the widest
        // one-digit line, "4-4", so "0-3" and "3-7" are as wide; "Choosing bid" or "10-8" widen it.
        private const float StatusPadding = 46f;                // canvas units, both sides together
        private const float StatusMinWidth = 84f;               // canvas units

        // ---- Names: up to 16 characters (GDD §11 screen 14) on a plate that holds about ten. A longer
        // name shrinks to 60% and then ends in an ellipsis; the width is the plate without its caps.
        private const float OpponentNameWidth = 108f;
        private const float LocalNameWidth = 100f;

        public static void Customize(ScreenBuildContext ctx)
        {
            BuildBackdrop(ctx);

            // The edge bands only shape the plate (see the layout); they are not part of the screen.
            foreach (string id in new[] { "EdgeTop", "EdgeBottom", "EdgeLeft", "EdgeRight", "HandArea" })
            {
                if (ctx.Nodes.TryGetValue(id, out RectTransform node))
                {
                    Object.DestroyImmediate(node.gameObject);
                    ctx.Nodes.Remove(id);
                }
            }

            // The owner took "Trump ♠" off the table (3 Oct 2026): spades are always trump. The round
            // line stays, and the top seat keeps its place under where the trump line was.
            foreach (string id in new[] { "TrumpLabel", "TrumpSuit" })
            {
                if (ctx.Nodes.TryGetValue(id, out RectTransform node))
                {
                    Object.DestroyImmediate(node.gameObject);
                    ctx.Nodes.Remove(id);
                }
            }

            RectTransform centre = ctx.Groups["Centre"];
            TMP_Text statusStyle = ctx.Nodes["SeatStatus3"].GetComponent<TMP_Text>();
            TMP_Text nameStyle = ctx.Nodes["SeatName3"].GetComponent<TMP_Text>();

            // A tap on the table itself closes the emote panel and skips no animation (ADR 038);
            // everything else sits above this.
            RectTransform tap = Stretched("TableTap", ctx.Content);
            tap.SetAsFirstSibling();
            var tapImage = tap.gameObject.AddComponent<Image>();
            tapImage.color = new Color(1f, 1f, 1f, 0f);
            var tapButton = tap.gameObject.AddComponent<Button>();
            tapButton.transition = Selectable.Transition.None;
            tapButton.targetGraphic = tapImage;
            NoNavigation(tapButton);
            ctx.Bound["_tableTap"] = tapButton;

            CardViewPool pool = BuildPool(ctx);
            ctx.Bound["_cards"] = pool;

            BuildTrickArea(ctx);
            for (int seat = 0; seat < TableView.SeatCount; seat++)
            {
                BuildSeat(ctx, seat, pool, nameStyle, statusStyle);
            }

            RectTransform handLayer = BuildHand(ctx, pool, centre);
            BuildPrompt(ctx, handLayer, statusStyle);
            BuildTrickLabel(ctx);
            StackUnderHome(ctx);
            PlaceRoundUnderLogo(ctx);
            BuildBannerRoom(ctx);
            BuildEmotePicker(ctx, statusStyle);
            BuildEmoteCooldown(ctx);
            BuildAwayCatcher(ctx);
            BuildReconnecting(ctx);

            // The emote button is 83 px across; the other HUD buttons are 88 or more already.
            foreach (string id in new[] { "HomeButton", "ScoresButton", "SettingsButton", "EmoteButton" })
            {
                MinimumTouch(ctx.Nodes[id].GetComponent<Button>().targetGraphic, MinTouch, MinTouch);
            }

            // Last, so that the pieces above that borrow the name's type are not limited with it.
            for (int seat = 0; seat < TableView.SeatCount; seat++)
            {
                ScreenExtras.LimitWidth(ctx, $"SeatName{seat}", seat == 0 ? LocalNameWidth : OpponentNameWidth);
            }

            // Table, hand and HUD rebuild at different rates, so each gets its own canvas (GDD §23).
            NestedCanvas(centre);
            NestedCanvas(handLayer);
            NestedCanvas(ctx.SafeArea);
        }

        /// <summary>
        /// The backdrop (the owner, 4 Oct 2026), behind everything else, in the layout background's
        /// frame and place (the screen plus margins for 21:9 and 4:3): the equipped table, filling it
        /// (V2, 28 Oct 2026), a cloth since ADR 058, with the cloth's border over it round the screen.
        /// The prefab shows the default cloth; the presenter puts the player's own on when a match
        /// opens. The art is TableBackdropBuilder's library.
        /// </summary>
        private static void BuildBackdrop(ScreenBuildContext ctx)
        {
            RectTransform frame = ctx.Background;
            if (frame == null)
            {
                ctx.Warnings.Add("the layout has no background: the table has no backdrop");
                return;
            }

            // The layout's plate gave the frame; the table takes the place of its picture.
            Image plate = frame.GetComponent<Image>();
            if (plate != null)
            {
                Object.DestroyImmediate(plate);
            }

            CanvasRenderer drawn = frame.GetComponent<CanvasRenderer>();
            if (drawn != null)
            {
                Object.DestroyImmediate(drawn);
            }

            TableBackdropLibrary library = TableBackdropBuilder.EnsureLibrary();
            var table = Stretched("Table", frame).gameObject.AddComponent<Image>();
            table.raycastTarget = false;
            table.sprite = library.TablePicture(library.DefaultTableId, out _);
            ctx.Bound["_backdrops"] = library;
            ctx.Bound["_backdropTable"] = table;

            // The cloth's border round the screen's edges (ADR 058), right over the cloth: its corners
            // stay put and its edges repeat, so it hugs the screen on every aspect ratio.
            RectTransform border = Stretched("ClothFrame", frame.parent);
            border.SetSiblingIndex(frame.GetSiblingIndex() + 1);
            var clothFrame = border.gameObject.AddComponent<Image>();
            clothFrame.raycastTarget = false;
            clothFrame.type = Image.Type.Tiled;
            clothFrame.fillCenter = false;
            clothFrame.sprite = library.TableFrame(library.DefaultTableId);
            clothFrame.enabled = clothFrame.sprite != null;
            ctx.Bound["_clothFrame"] = clothFrame;
        }

        private static CardViewPool BuildPool(ScreenBuildContext ctx)
        {
            RectTransform poolRect = Stretched("CardPool", ctx.Content);
            poolRect.SetSiblingIndex(1);
            RectTransform storage = Stretched("Storage", poolRect);
            storage.gameObject.SetActive(false);
            var pool = poolRect.gameObject.AddComponent<CardViewPool>();
            var so = new SerializedObject(pool);
            so.FindProperty("_prefab").objectReferenceValue = TableCardBuilder.EnsureCardPrefab();
            so.FindProperty("_storage").objectReferenceValue = storage;
            so.ApplyModifiedPropertiesWithoutUndo();
            return pool;
        }

        /// <summary>
        /// The trick (ADR 060): the area is centred on the trick's centre, where the deck also lies
        /// while a round is dealt, and its four slots form a cross round it, the cards a gap apart.
        /// </summary>
        private static void BuildTrickArea(ScreenBuildContext ctx)
        {
            MoveNode(ctx, "TrickArea", TrickCentre);
            RectTransform area = ctx.Nodes["TrickArea"];
            var trick = area.gameObject.AddComponent<TrickAreaView>();
            var so = new SerializedObject(trick);
            so.FindProperty("_area").objectReferenceValue = area;

            // Canvas units: the card as the trick draws it, and the gap.
            float scale = TrickCardWidth * UiBuildKit.ScaleX / TableCardBuilder.CardWidth;
            var card = new Vector2(TableCardBuilder.CardWidth, TableCardBuilder.CardHeight) * scale;
            float gap = TrickGap * UiBuildKit.ScaleX;
            float across = card.x + gap, down = (card.y + gap) / 2f;
            Vector2[] slots = { new Vector2(0f, -down), new Vector2(across, 0f), new Vector2(0f, down), new Vector2(-across, 0f) };

            SerializedProperty positions = so.FindProperty("_slotPositions");
            SerializedProperty rotations = so.FindProperty("_slotRotations");
            positions.arraySize = rotations.arraySize = TrickAreaView.Slots;
            for (int i = 0; i < TrickAreaView.Slots; i++)
            {
                positions.GetArrayElementAtIndex(i).vector2Value = slots[i];
                rotations.GetArrayElementAtIndex(i).floatValue = TrickTurns[i];
            }

            so.FindProperty("_cardScale").floatValue = scale;
            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_trick"] = trick;

            // A tap on the trick area is a tap on the table: it no longer peeks at the previous trick
            // (GDD screen 08; ADR 038). Cards lie above it.
            RectTransform tap = Stretched("Tap", area);
            tap.SetAsFirstSibling();
            var tapImage = tap.gameObject.AddComponent<Image>();
            tapImage.color = new Color(1f, 1f, 1f, 0f);
            var tapButton = tap.gameObject.AddComponent<Button>();
            tapButton.transition = Selectable.Transition.None;
            tapButton.targetGraphic = tapImage;
            NoNavigation(tapButton);
            ctx.Bound["_trickTap"] = tapButton;
        }

        /// <summary>
        /// The hand's layer sits between the table and the HUD and reaches the bottom edge of the
        /// screen (cards run off it), while its width follows the safe area.
        /// </summary>
        private static RectTransform BuildHand(ScreenBuildContext ctx, CardViewPool pool, RectTransform centre)
        {
            RectTransform layer = Stretched("HandLayer", ctx.Content);
            layer.SetSiblingIndex(centre.GetSiblingIndex() + 1);

            // Touch area: the bottom strip of the screen, as tall as the hand with a lifted card.
            var touchObject = new GameObject("Hand", typeof(RectTransform));
            var touch = (RectTransform)touchObject.transform;
            touch.SetParent(layer, false);
            touch.anchorMin = new Vector2(0f, 0f);
            touch.anchorMax = new Vector2(1f, 0f);
            touch.pivot = new Vector2(0.5f, 0f);
            touch.offsetMin = Vector2.zero;
            touch.offsetMax = new Vector2(0f, (UiBuildKit.MockHeight - HandTouchTop) * UiBuildKit.ScaleY);
            var touchImage = touchObject.AddComponent<Image>();
            touchImage.color = new Color(1f, 1f, 1f, 0f);
            touchImage.raycastTarget = true;

            // Cards hang from a point at the centre of the middle card, measured from the bottom edge.
            var cardsObject = new GameObject("Cards", typeof(RectTransform));
            var cards = (RectTransform)cardsObject.transform;
            cards.SetParent(layer, false);
            cards.anchorMin = cards.anchorMax = new Vector2(0.5f, 0f);
            cards.pivot = new Vector2(0.5f, 0.5f);
            cards.sizeDelta = Vector2.zero;
            cards.anchoredPosition = new Vector2(
                (HandCentre.x - UiBuildKit.MockWidth / 2f) * UiBuildKit.ScaleX,
                (UiBuildKit.MockHeight - HandCentre.y) * UiBuildKit.ScaleY);

            // The layout spaces the cards as they are drawn, the hand's scale larger than the shared card.
            var hand = touchObject.AddComponent<HandView>();
            var so = new SerializedObject(hand);
            so.FindProperty("_cardsRoot").objectReferenceValue = cards;
            so.FindProperty("_widthReference").objectReferenceValue = ctx.SafeArea;
            so.FindProperty("_pool").objectReferenceValue = pool;
            so.FindProperty("_cardScale").floatValue = HandCardScale;
            float cardWidth = HandCardScale * TableCardBuilder.CardWidth;
            SerializedProperty layout = so.FindProperty("_layout");
            layout.FindPropertyRelative("CardWidth").floatValue = cardWidth;
            layout.FindPropertyRelative("MinWidthFraction").floatValue = HandWidthFraction;
            layout.FindPropertyRelative("MaxWidthFraction").floatValue = 0.86f;
            layout.FindPropertyRelative("MinStrip").floatValue = HandLayout.HardMinStrip;
            layout.FindPropertyRelative("MaxStrip").floatValue = 0.8f * cardWidth;
            layout.FindPropertyRelative("Lift").floatValue = HandLift;
            layout.FindPropertyRelative("HoverLift").floatValue = 18f;
            layout.FindPropertyRelative("ArcRadius").floatValue = HandArcRadius;
            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_hand"] = hand;
            return layer;
        }

        private static void BuildSeat(ScreenBuildContext ctx, int seat, CardViewPool pool, TMP_Text nameStyle, TMP_Text statusStyle)
        {
            RectTransform root = ctx.Nodes[$"Seat{seat}"];
            Vector2 seatCentre = ctx.CentreOf($"Seat{seat}");
            RectTransform ring = ctx.Nodes[$"SeatRing{seat}"];
            LayoutNode ringNode = ctx.Node($"SeatRing{seat}");
            bool local = seat == 0;

            // The profile as the owner arranged it; everything below is placed from its avatar.
            Vector2 avatarCentre = ArrangeProfile(ctx, seat);

            // The turn ring: the local seat's is on the layout; the others get the same ring, scaled
            // to their avatar (a gold ring around the active seat, GDD §11 T rules).
            LayoutNode localRing = ctx.Node("SeatRing0"), localTimer = ctx.Node("SeatTimerRing0");
            float timerSize = ringNode.rect[2] * localTimer.rect[2] / localRing.rect[2];
            RectTransform timerRing;
            if (local)
            {
                timerRing = ctx.Nodes["SeatTimerRing0"];
            }
            else
            {
                timerRing = UiBuildKit.CreateAt($"SeatTimerRing{seat}", root, avatarCentre, seatCentre, UiBuildKit.FromMock(timerSize, timerSize));
                timerRing.SetSiblingIndex(ring.GetSiblingIndex());
                RectTransform face = Stretched("Face", timerRing);
                UiBuildKit.AddImage(face, UiBuildKit.LoadSpriteAt($"{TableArt}/UI_TimerRing.png", 0f), false);
            }

            Image ringImage = timerRing.Find("Face").GetComponent<Image>();
            ringImage.type = Image.Type.Filled;
            ringImage.fillMethod = Image.FillMethod.Radial360;
            ringImage.fillOrigin = (int)Image.Origin360.Top;
            ringImage.fillClockwise = false;
            ringImage.fillAmount = 1f;
            ringImage.enabled = local;      // the mockups show the local seat's turn

            // Seconds disc: the local seat's is on the layout (moved with its avatar); the others get a
            // copy at a free corner.
            RectTransform disc = ctx.Nodes["SeatTimerDisc0"];
            if (!local)
            {
                disc = Object.Instantiate(disc, root);
                disc.name = $"SeatTimerDisc{seat}";
                disc.anchoredPosition = FromMock(avatarCentre + DiscOffsets[seat] - seatCentre);
                disc.gameObject.SetActive(false);
            }

            TMP_Text timerText = disc.GetComponentInChildren<TMP_Text>(true);

            // Bubble for a call as it is placed: the name plate's white pill.
            Vector2 bubbleCentre = avatarCentre + BubbleOffsets[seat];
            RectTransform bubble = UiBuildKit.CreateAt("Bubble", root, bubbleCentre, seatCentre, UiBuildKit.FromMock(90f, BubbleHeight));
            Sliced(bubble, LoadTableSprite("UI_NamePlate"));
            TextMeshProUGUI bubbleText = Label("Text", bubble, nameStyle, "4");
            bubble.gameObject.SetActive(false);

            // The chip: how the seat's player is connected (GDD §8; a fill-in bot has none, ADR 025).
            // It sizes itself to its text; the spinner shows only with "Reconnecting…". ("BOT" is
            // only the sample the prefab is built with.)
            float ringEdge = ringNode.rect[3] / 2f - 2f;
            Vector2 chipCentre = avatarCentre + new Vector2(0f, seat == 2 ? ringEdge : -ringEdge);
            RectTransform chip = UiBuildKit.CreateAt("Chip", root, chipCentre, seatCentre, UiBuildKit.FromMock(56f, ChipHeight));
            Sliced(chip, LoadTableSprite("UI_Pill_Tag"));
            TextMeshProUGUI chipText = Label("Text", chip, statusStyle, "BOT");
            chipText.fontSize = statusStyle.fontSize * ChipTextScale;
            float spinnerSize = ChipSpinnerSize * UiBuildKit.ScaleX;
            GameObject chipSpinner = BuildSpinner(chip, spinnerSize, 12f + spinnerSize / 2f, chipText.color);
            chipSpinner.SetActive(false);
            chip.gameObject.SetActive(false);

            // The speech box for the seat's quick phrases, over its other parts.
            (RectTransform chat, TextMeshProUGUI chatText) = BuildChat(root, seat, seatCentre, ChatRimCorner(ctx, seat, avatarCentre, timerSize / 2f));
            RectTransform statusPill = BuildStatusPill(ctx, seat);

            var view = root.gameObject.AddComponent<SeatView>();
            var so = new SerializedObject(view);
            so.FindProperty("_anchor").objectReferenceValue = ring;
            so.FindProperty("_avatar").objectReferenceValue = ctx.Nodes[$"SeatAvatar{seat}"].Find("Face").GetComponent<Image>();
            so.FindProperty("_name").objectReferenceValue = ctx.Nodes[$"SeatName{seat}"].GetComponent<TMP_Text>();
            so.FindProperty("_status").objectReferenceValue = ctx.Nodes[$"SeatStatus{seat}"].GetComponent<TMP_Text>();
            so.FindProperty("_statusPill").objectReferenceValue = statusPill.Find("Face").GetComponent<Image>();
            so.FindProperty("_statusPillRoot").objectReferenceValue = statusPill;
            so.FindProperty("_statusPadding").floatValue = StatusPadding;
            so.FindProperty("_statusMinWidth").floatValue = StatusMinWidth;
            so.FindProperty("_timerRing").objectReferenceValue = ringImage;
            so.FindProperty("_timerDisc").objectReferenceValue = disc.gameObject;
            so.FindProperty("_timerText").objectReferenceValue = timerText;
            so.FindProperty("_bubble").objectReferenceValue = bubble;
            so.FindProperty("_bubbleText").objectReferenceValue = bubbleText;
            so.FindProperty("_bubblePadding").floatValue = 40f;
            so.FindProperty("_bubbleMinWidth").floatValue = 64f * UiBuildKit.ScaleX;
            so.FindProperty("_chip").objectReferenceValue = chip;
            so.FindProperty("_chipText").objectReferenceValue = chipText;
            so.FindProperty("_chipSpinner").objectReferenceValue = chipSpinner;
            so.FindProperty("_chipPadding").floatValue = 26f;
            so.FindProperty("_chipSpinnerSpace").floatValue = spinnerSize + 6f;
            so.FindProperty("_chat").objectReferenceValue = chat;
            so.FindProperty("_chatText").objectReferenceValue = chatText;
            so.FindProperty("_chatPadding").floatValue = ChatTextPadding + 2f * ChatRimSide;
            so.FindProperty("_chatMinWidth").floatValue = ChatMinWidth;

            // The kit's ring sprite is orange (#F6960B); the last five seconds multiply it towards
            // crimson (#861018, GDD §12); a met call tints the sand pill gold.
            so.FindProperty("_ringColor").colorValue = Color.white;
            so.FindProperty("_ringWarningColor").colorValue = new Color(0x86 / 246f, 0x10 / 150f, 1f, 1f);
            so.FindProperty("_pillColor").colorValue = Color.white;
            so.FindProperty("_pillMetColor").colorValue = new Color(1f, 0.96f, 0.62f, 1f);

            if (!local)
            {
                // The avatar's button: a tap on an opponent muted their quick phrases (GDD §8) until
                // the owner removed the mute (5 Oct 2026, ADR 038). SeatView now keeps it from taking
                // a tap, so a tap on an avatar does nothing.
                Image ringFace = ring.Find("Face").GetComponent<Image>();
                ringFace.raycastTarget = true;
                var button = ring.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = ringFace;
                NoNavigation(button);
                so.FindProperty("_avatarButton").objectReferenceValue = button;
                so.FindProperty("_fan").objectReferenceValue = BuildFan(ctx, seat, pool, avatarCentre);
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // A side seat sits lower, nearer the player: everything above is under its root and moves with it.
            if (seat == 1 || seat == 3)
            {
                root.anchoredPosition += FromMock(new Vector2(0f, SideSeatDrop));
            }
        }

        /// <summary>
        /// Puts the parts of a seat's profile where the owner arranged them (2 Oct 2026) and returns
        /// the avatar's centre (mockup px). A side seat keeps the layout's (the whole seat moves down
        /// later). The top seat's name plate and status pill stand above its avatar, just under the
        /// trump line, and its avatar comes down below them (mockup 19). The local seat's plate and
        /// pill are centred below its avatar at the opponents' widths, their words centred in them,
        /// the pill keeping its place at the bottom; the avatar, its timer ring and its seconds disc
        /// go up so that the plate lies over the ring's foot as an opponent's does.
        /// </summary>
        private static Vector2 ArrangeProfile(ScreenBuildContext ctx, int seat)
        {
            LayoutNode ring = ctx.Node($"SeatRing{seat}"), plate = ctx.Node($"SeatPlate{seat}"), pill = ctx.Node($"SeatPill{seat}");
            if (seat == 2)
            {
                LayoutNode trump = ctx.Node("TrumpLabel"), suit = ctx.Node("TrumpSuit");
                float top = Mathf.Max(trump.inkBox[3], suit.rect[1] + suit.rect[3]) + TopProfileGap - TopSeatRise;
                float pillTop = top + plate.rect[3];
                MoveNode(ctx, "SeatPlate2", new Vector2(plate.Centre.x, top + plate.rect[3] / 2f));
                MoveNode(ctx, "SeatPill2", new Vector2(pill.Centre.x, pillTop + pill.rect[3] / 2f));
                var topAvatar = new Vector2(ring.Centre.x, pillTop + pill.rect[3] + TopRingGap + ring.rect[3] / 2f);
                MoveNode(ctx, "SeatRing2", topAvatar);
                return topAvatar;
            }

            if (seat != 0)
            {
                return ring.Centre;
            }

            // How far an opponent's name plate lies over the foot of its avatar's ring.
            LayoutNode sideRing = ctx.Node("SeatRing3"), sidePlate = ctx.Node("SeatPlate3"), sidePill = ctx.Node("SeatPill3");
            float cover = sideRing.rect[1] + sideRing.rect[3] - sidePlate.rect[1];
            MoveNode(ctx, "SeatPlate0", new Vector2(LocalProfileX, plate.Centre.y), sidePlate.rect[2]);
            MoveNode(ctx, "SeatPill0", new Vector2(LocalProfileX, pill.Centre.y), sidePill.rect[2]);
            CentreLabel(ctx, "SeatName0", "SeatPlate0");
            CentreLabel(ctx, "SeatStatus0", "SeatPill0");
            var avatar = new Vector2(LocalProfileX, plate.rect[1] + cover - ring.rect[3] / 2f);
            MoveNode(ctx, "SeatRing0", avatar);
            MoveNode(ctx, "SeatTimerRing0", avatar);
            MoveNode(ctx, "SeatTimerDisc0", avatar + DiscOffsets[0]);

            // The plate and the pill lie over the ring, and the seconds disc over everything.
            ctx.Nodes["SeatPlate0"].SetAsLastSibling();
            ctx.Nodes["SeatPill0"].SetAsLastSibling();
            ctx.Nodes["SeatTimerDisc0"].SetAsLastSibling();
            return avatar;
        }

        /// <summary>
        /// Moves a layout node, its children with it, so that its centre is at <paramref name="centre"/>
        /// (mockup px); a <paramref name="width"/> (mockup px) also resizes it and its art.
        /// </summary>
        private static void MoveNode(ScreenBuildContext ctx, string id, Vector2 centre, float width = 0f)
        {
            LayoutNode node = ctx.Node(id);
            RectTransform rect = ctx.Nodes[id];
            Vector2 parentCentre = string.IsNullOrEmpty(node.parent) ? UiBuildKit.ScreenCentre : ctx.CentreOf(node.parent);
            rect.anchoredPosition = FromMock(centre - parentCentre);
            if (width <= 0f)
            {
                return;
            }

            rect.sizeDelta = new Vector2(width * UiBuildKit.ScaleX, rect.sizeDelta.y);
            foreach (string part in new[] { "Face", "Shadow" })
            {
                if (rect.Find(part) is RectTransform art)
                {
                    art.sizeDelta = new Vector2(width * UiBuildKit.ScaleX, art.sizeDelta.y);
                }
            }
        }

        /// <summary>Centres a label's words across the plate it is on (its baseline stays).</summary>
        private static void CentreLabel(ScreenBuildContext ctx, string labelId, string plateId)
        {
            LayoutNode label = ctx.Node(labelId);
            float off = (label.inkBox[0] + label.inkBox[2]) / 2f - ctx.CentreOf(plateId).x;
            ctx.Nodes[labelId].anchoredPosition -= new Vector2(off * UiBuildKit.ScaleX, 0f);
        }

        /// <summary>
        /// A seat's status pill, readied to hug its words (<see cref="HugWords"/>; the seat's view
        /// sets its width as they change) and sized here to the words it is built with.
        /// </summary>
        private static RectTransform BuildStatusPill(ScreenBuildContext ctx, int seat)
        {
            RectTransform pill = ctx.Nodes[$"SeatPill{seat}"];
            TMP_Text words = ctx.Nodes[$"SeatStatus{seat}"].GetComponent<TMP_Text>();
            if (words.rectTransform.parent != pill)
            {
                ctx.Warnings.Add($"SeatStatus{seat} is not on SeatPill{seat}: its words do not follow the pill's width");
            }

            HugWords(pill, words);
            float width = Mathf.Max(StatusMinWidth, Mathf.Ceil(words.GetPreferredValues(words.text).x) + StatusPadding);
            pill.sizeDelta = new Vector2(width, pill.sizeDelta.y);
            return pill;
        }

        /// <summary>
        /// Readies a pill to hug its words, where its view then sets only the pill's width: its art
        /// (every other child: the face, a shadow or a rim) follows the width with the margins it has
        /// now, and the words, when they are on the pill, span it from side to side, centred, on
        /// their baseline. The pill keeps its centre and its height.
        /// </summary>
        public static void HugWords(RectTransform pill, TMP_Text words)
        {
            RectTransform area = words.rectTransform;
            foreach (RectTransform part in pill)
            {
                if (part != area)
                {
                    FollowWidth(part);
                }
            }

            if (area.parent != pill)
            {
                return;
            }

            area.anchorMin = new Vector2(0f, area.anchorMin.y);
            area.anchorMax = new Vector2(1f, area.anchorMax.y);
            area.offsetMin = new Vector2(0f, area.offsetMin.y);
            area.offsetMax = new Vector2(0f, area.offsetMax.y);
        }

        /// <summary>
        /// From now on <paramref name="part"/> follows its parent's width: its anchors span the
        /// parent from side to side, with the margins it has now; its height and where it sits
        /// up and down stay as they are.
        /// </summary>
        private static void FollowWidth(RectTransform part)
        {
            var parent = (RectTransform)part.parent;
            float left = part.localPosition.x + part.rect.xMin - parent.rect.xMin;
            float right = part.localPosition.x + part.rect.xMax - parent.rect.xMax;
            part.anchorMin = new Vector2(0f, part.anchorMin.y);
            part.anchorMax = new Vector2(1f, part.anchorMax.y);
            part.offsetMin = new Vector2(left, part.offsetMin.y);
            part.offsetMax = new Vector2(right, part.offsetMax.y);
        }

        /// <summary>
        /// Where a seat's speech box goes: the corner of its rim by the tail (the bottom-left; the
        /// bottom-right on the right seat), in mockup px. Above a side avatar the tail's tip stops
        /// just outside the timer ring, up and over to the box's side, so that the leaning tail
        /// points at the avatar's centre; the local seat's stops a little further out and turned
        /// less, which keeps its box off the hand. The top seat's box stands right of its status
        /// pill, its foot a little above the avatar's centre and its tail reaching down towards the
        /// avatar.
        /// </summary>
        private static Vector2 ChatRimCorner(ScreenBuildContext ctx, int seat, Vector2 avatarCentre, float timerRadius)
        {
            if (seat == 2)
            {
                LayoutNode pill = ctx.Node("SeatPill2");
                return new Vector2(pill.rect[0] + pill.rect[2] + ChatBesideGap, avatarCentre.y - ChatBesideRise);
            }

            float side = seat == 1 ? -1f : 1f;
            float turn = (seat == 0 ? LocalChatTipTurn : ChatTipTurn) * Mathf.Deg2Rad;
            float reach = timerRadius + (seat == 0 ? LocalChatTipGap : ChatTipGap);
            Vector2 tip = avatarCentre + new Vector2(side * reach * Mathf.Sin(turn), -reach * Mathf.Cos(turn));
            return tip - new Vector2(side * ChatTailTip.x / UiBuildKit.ScaleX, -ChatTailTip.y / UiBuildKit.ScaleY);
        }

        /// <summary>
        /// A seat's speech box for quick phrases (ADR 027), hidden. The box art's image is the rim
        /// grown by its shadow's margins; its corner by the tail is the pivot, so the box grows away
        /// from the tail as the phrase gets longer (leftwards on the right seat, whose tail is
        /// mirrored) and pops out from there. The tail lies over the box's foot; the phrase is
        /// centred inside the rim.
        /// </summary>
        private static (RectTransform box, TextMeshProUGUI text) BuildChat(RectTransform root, int seat, Vector2 seatCentre, Vector2 rimCorner)
        {
            bool mirrored = seat == 1;
            float side = mirrored ? -1f : 1f;
            Vector2 corner = rimCorner - new Vector2(side * ChatRimSide / UiBuildKit.ScaleX, -ChatRimBottom / UiBuildKit.ScaleY);

            var boxObject = new GameObject("Chat", typeof(RectTransform));
            var box = (RectTransform)boxObject.transform;
            box.SetParent(root, false);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(mirrored ? 1f : 0f, 0f);
            box.anchoredPosition = FromMock(corner - seatCentre);
            box.sizeDelta = new Vector2(ChatMinWidth, ChatRimHeight + ChatRimTop + ChatRimBottom);

            // No part of it takes touches: the local box lies over the top of the hand's first cards
            // for its seconds, and a throw must still reach them. (Its pop and fade use this group.)
            var group = boxObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            // The box: sliced at its native scale, which the tail's placement is measured against.
            RectTransform face = Stretched("Box", box);
            Image boxImage = UiBuildKit.AddImage(face, UiBuildKit.LoadSpriteAt(ChatBoxArt, 0f), false);
            boxImage.type = Image.Type.Sliced;
            boxImage.fillCenter = true;
            boxImage.pixelsPerUnitMultiplier = 1f;
            boxImage.raycastTarget = false;

            var tailObject = new GameObject("Tail", typeof(RectTransform));
            var tail = (RectTransform)tailObject.transform;
            tail.SetParent(box, false);
            tail.anchorMin = tail.anchorMax = new Vector2(mirrored ? 1f : 0f, 0f);
            tail.pivot = Vector2.zero;
            tail.sizeDelta = ChatTailSize;
            tail.anchoredPosition = new Vector2(side * ChatTailCorner.x, ChatTailCorner.y);
            tail.localScale = new Vector3(side, 1f, 1f);
            Image tailImage = UiBuildKit.AddImage(tail, UiBuildKit.LoadSpriteAt(ChatTailArt, 0f), false);
            tailImage.raycastTarget = false;

            RectTransform textRect = Stretched("Text", box);
            textRect.offsetMin = new Vector2(ChatRimSide, ChatRimBottom);
            textRect.offsetMax = new Vector2(-ChatRimSide, -ChatRimTop);
            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = UiBuildKit.LoadFontRole("SansBold");
            text.fontSize = ChatFontSize;
            text.color = ChatTextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = "Well played!";
            float width = Mathf.Ceil(text.GetPreferredValues(text.text).x) + ChatTextPadding + 2f * ChatRimSide;
            box.sizeDelta = new Vector2(Mathf.Max(ChatMinWidth, width), box.sizeDelta.y);

            boxObject.SetActive(false);
            return (box, text);
        }

        /// <summary>
        /// The face-down fan of an opponent, as the bidding mockup draws it (ADR 026): beside a side
        /// seat's profile, on the table side, a gentle vertical arc of cards pointing away from the
        /// profile; below the top seat's avatar (its name and status pill are above it), a shallow arc
        /// of cards hanging down, half to each side. Fans lie under the avatar, the name and the pill.
        /// </summary>
        private static OpponentHandView BuildFan(ScreenBuildContext ctx, int seat, CardViewPool pool, Vector2 avatarCentre)
        {
            RectTransform area = ctx.Nodes[$"SeatFan{seat}"];
            Vector2 areaCentre = ctx.CentreOf($"SeatFan{seat}");
            var sides = new List<(Vector2 pivot, float radius, float start, float step, float spread)>();
            if (seat == 2)
            {
                // Cards alternate between the halves from the middle out, so the middle ones lie on top.
                Vector2 pivot = avatarCentre + new Vector2(0f, -TopFanPivotAbove);
                sides.Add((pivot, TopFanRadius, 180f + (TopFanStep / 2f), TopFanStep, 6f * TopFanStep));
                sides.Add((pivot, TopFanRadius, 180f - (TopFanStep / 2f), -TopFanStep, 6f * TopFanStep));
            }
            else
            {
                // Seat 3 is on the left (its cards point right), seat 1 its mirror image.
                float side = seat == 3 ? 1f : -1f;
                Vector2 pivot = avatarCentre + new Vector2(-SideFanPivot.x * side, SideFanPivot.y);
                sides.Add((pivot, SideFanRadius, SideFanBottomAngle * side, -SideFanStep * side, 12f * SideFanStep));
            }

            var fan = area.gameObject.AddComponent<OpponentHandView>();
            var so = new SerializedObject(fan);
            so.FindProperty("_area").objectReferenceValue = area;
            so.FindProperty("_pool").objectReferenceValue = pool;
            so.FindProperty("_cardScale").floatValue = FanCardWidth * UiBuildKit.ScaleX / TableCardBuilder.CardWidth;
            SerializedProperty list = so.FindProperty("_sides");
            list.arraySize = sides.Count;
            for (int i = 0; i < sides.Count; i++)
            {
                SerializedProperty s = list.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("_pivot").vector2Value = FromMock(sides[i].pivot - areaCentre);
                s.FindPropertyRelative("_radius").floatValue = sides[i].radius * UiBuildKit.ScaleX;
                s.FindPropertyRelative("_startAngle").floatValue = sides[i].start;
                s.FindPropertyRelative("_stepAngle").floatValue = sides[i].step;
                s.FindPropertyRelative("_maxSpread").floatValue = sides[i].spread;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return fan;
        }

        /// <summary>The turn prompt: the status pill's sand pill above the hand, sized to its text at runtime.</summary>
        private static void BuildPrompt(ScreenBuildContext ctx, RectTransform handLayer, TMP_Text style)
        {
            var holder = new GameObject("Prompt", typeof(RectTransform));
            var holderRect = (RectTransform)holder.transform;
            holderRect.SetParent(handLayer, false);
            holderRect.anchorMin = holderRect.anchorMax = new Vector2(0.5f, 0f);
            holderRect.pivot = new Vector2(0.5f, 0.5f);
            holderRect.sizeDelta = Vector2.zero;
            holderRect.anchoredPosition = new Vector2(
                (PromptCentre.x - UiBuildKit.MockWidth / 2f) * UiBuildKit.ScaleX,
                (UiBuildKit.MockHeight - PromptCentre.y) * UiBuildKit.ScaleY);

            var pillObject = new GameObject("Pill", typeof(RectTransform));
            var pill = (RectTransform)pillObject.transform;
            pill.SetParent(holderRect, false);
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0.5f, 0.5f);
            pill.sizeDelta = UiBuildKit.FromMock(280f, PromptHeight);
            Image pillImage = Sliced(pill, LoadTableSprite("UI_StatusPill"));
            TextMeshProUGUI label = Label("Text", pill, style, "Your turn · Lead any card");
            label.richText = true;

            // The prompt names cards and suits ("Beat Q♥"); the fonts have no suit symbols, so they
            // are drawn from the suit sprites (each sprite answers to its Unicode character).
            label.spriteAsset = SuitSpriteBuilder.Ensure();
            pillObject.SetActive(false);

            var prompt = holder.AddComponent<TablePromptView>();
            var so = new SerializedObject(prompt);
            so.FindProperty("_pill").objectReferenceValue = pill;
            so.FindProperty("_pillImage").objectReferenceValue = pillImage;
            so.FindProperty("_label").objectReferenceValue = label;
            so.FindProperty("_padding").floatValue = 56f;
            so.FindProperty("_minWidth").floatValue = 180f;
            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_prompt"] = prompt;
        }

        /// <summary>"Trick 6 / 13" under the round line (GDD screen 08, callout 1). The mockup does not draw it, so it starts hidden.</summary>
        private static void BuildTrickLabel(ScreenBuildContext ctx)
        {
            TMP_Text style = ctx.Nodes["ScoresLabel"].GetComponent<TMP_Text>();
            RectTransform top = ctx.Groups["Top"];
            RectTransform rect = UiBuildKit.CreateAt("TrickLabel", top, TrickLabelCentre, UiBuildKit.ScreenCentre, new Vector2(320f, 40f));
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            CopyStyle(style, label);
            label.alignment = TextAlignmentOptions.Center;
            label.text = "Trick 6 / 13";
            rect.gameObject.SetActive(false);
            ctx.Bound["_trickLabel"] = label;
        }

        /// <summary>
        /// Scores and Settings under Home (the owner, 28 Oct 2026, ADR 057): the two buttons move from
        /// the top-right corner into Home's group, one above the other in Home's column
        /// (<see cref="ColumnGap"/> apart), as icons only: the mockup's "Scores" and "Settings" words
        /// under them go. So does the divider between them, drawn for buttons side by side.
        /// </summary>
        private static void StackUnderHome(ScreenBuildContext ctx)
        {
            RectTransform left = ctx.Groups["HeaderLeft"];
            RectTransform home = ctx.Nodes["HomeButton"];
            float column = home.anchoredPosition.x;
            float above = home.anchoredPosition.y - (home.rect.height / 2f);
            foreach (string id in new[] { "Scores", "Settings" })
            {
                RectTransform button = ctx.Nodes[id + "Button"];
                button.SetParent(left, false);
                float y = above - ColumnGap - (button.rect.height / 2f);
                button.anchoredPosition = new Vector2(column, y);
                above = y - (button.rect.height / 2f);

                RectTransform label = ctx.Nodes[id + "Label"];
                ctx.Nodes.Remove(id + "Label");
                Object.DestroyImmediate(label.gameObject);
            }

            RectTransform divider = ctx.Nodes["HeaderDivider"];
            ctx.Nodes.Remove("HeaderDivider");
            Object.DestroyImmediate(divider.gameObject);
        }

        /// <summary>
        /// The round line under the logo (the owner, 29 Oct 2026, ADR 057): the mockup's "ROUND 1 / 5"
        /// moves from the top centre into the logo's group, centred under the CALLBREAK logo
        /// <see cref="RoundUnderLogoGap"/> below it, on the seats' status pill (TableView keeps the pill
        /// as wide as the words). Its two ornaments, drawn to flank it at the top, go.
        /// </summary>
        private static void PlaceRoundUnderLogo(ScreenBuildContext ctx)
        {
            RectTransform left = ctx.Groups["HeaderLeft"];
            RectTransform logo = ctx.Nodes["Logo"];
            RectTransform round = ctx.Nodes["RoundLabel"];
            TMP_Text words = round.GetComponent<TMP_Text>();
            words.horizontalAlignment = HorizontalAlignmentOptions.Center;
            words.verticalAlignment = VerticalAlignmentOptions.Middle;

            Vector2 size = words.GetPreferredValues(words.text);
            float pillHeight = Mathf.Max(UiBuildKit.FromMock(0f, ctx.Node("SeatPill3").rect[3]).y, size.y + 8f);
            float logoBottom = logo.anchoredPosition.y - (logo.rect.height / 2f);
            var centre = new Vector2(logo.anchoredPosition.x, logoBottom - RoundUnderLogoGap - (pillHeight / 2f));

            round.SetParent(left, false);
            round.anchoredPosition = centre;

            var pill = (RectTransform)new GameObject("RoundPill", typeof(RectTransform)).transform;
            pill.SetParent(left, false);
            pill.SetSiblingIndex(round.GetSiblingIndex());
            pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0.5f, 0.5f);
            pill.sizeDelta = new Vector2(size.x + LabelPillPadding, pillHeight);
            pill.anchoredPosition = centre;
            Sliced(pill, LoadTableSprite("UI_StatusPill"));
            ctx.Bound["_roundPill"] = pill;

            foreach (string id in new[] { "RoundOrnamentLeft", "RoundOrnamentRight" })
            {
                RectTransform ornament = ctx.Nodes[id];
                ctx.Nodes.Remove(id);
                Object.DestroyImmediate(ornament.gameObject);
            }
        }

        /// <summary>
        /// The table's room for the banner ad (ADR 051, 057), off until TableView makes it: an empty slot
        /// across the top of the safe area from <see cref="BannerGap"/> past the logo and the round line
        /// under it to the right edge, pinned to the left side so it keeps its distance on 21:9 and 4:3
        /// screens. The banner sits in its top-right corner.
        /// </summary>
        private static void BuildBannerRoom(ScreenBuildContext ctx)
        {
            RectTransform safe = ctx.SafeArea;

            // Measured from the safe area's left side: where the logo, or the round line's pill, ends.
            float right = 0f;
            var corners = new Vector3[4];
            foreach (RectTransform piece in new[] { ctx.Nodes["Logo"], (RectTransform)ctx.Bound["_roundPill"] })
            {
                piece.GetWorldCorners(corners);
                right = Mathf.Max(right, safe.InverseTransformPoint(corners[2]).x - safe.rect.xMin);
            }

            var slot = (RectTransform)new GameObject("BannerSlot", typeof(RectTransform)).transform;
            slot.SetParent(safe, false);
            slot.anchorMin = new Vector2(0f, 1f);
            slot.anchorMax = new Vector2(1f, 1f);
            slot.pivot = new Vector2(0.5f, 1f);
            slot.offsetMin = new Vector2(right + BannerGap, -BannerSlotHeight);
            slot.offsetMax = Vector2.zero;
            slot.gameObject.SetActive(false);
            ctx.Bound["_bannerSlot"] = slot;
        }

        /// <summary>
        /// The emote picker: a kit card panel beside the emote button with six secondary buttons,
        /// two columns of three, over a catcher that closes it.
        /// </summary>
        private static void BuildEmotePicker(ScreenBuildContext ctx, TMP_Text style)
        {
            RectTransform holder = Stretched("EmotePicker", ctx.SafeArea);
            RectTransform panelRoot = Stretched("Panel", holder);

            RectTransform dismissRect = Stretched("Dismiss", panelRoot);
            var dismissImage = dismissRect.gameObject.AddComponent<Image>();
            dismissImage.color = new Color(1f, 1f, 1f, 0f);
            var dismiss = dismissRect.gameObject.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.targetGraphic = dismissImage;
            NoNavigation(dismiss);

            // Pinned to the bottom-right corner like the emote button it belongs to.
            var card = new GameObject("Card", typeof(RectTransform));
            var cardRect = (RectTransform)card.transform;
            cardRect.SetParent(panelRoot, false);
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(1f, 0f);
            cardRect.anchoredPosition = new Vector2(
                -(UiBuildKit.MockWidth - EmotePanel.xMax) * UiBuildKit.ScaleX,
                (UiBuildKit.MockHeight - EmotePanel.yMax) * UiBuildKit.ScaleY);
            cardRect.sizeDelta = UiBuildKit.FromMock(EmotePanel.width, EmotePanel.height);

            Sprite panelShadow = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Common/UI_Panel_Card_Shadow.png", 0f);
            RectTransform shadowRect = Stretched("Shadow", cardRect);
            shadowRect.offsetMin = new Vector2(-14f, -16f);
            shadowRect.offsetMax = new Vector2(14f, 12f);
            UiBuildKit.AddImage(shadowRect, panelShadow, false);
            RectTransform faceRect = Stretched("Face", cardRect);
            Image face = UiBuildKit.AddImage(faceRect, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Card.png", 0f), true);
            face.raycastTarget = true;      // taps on the panel itself do not close it

            Sprite buttonSprite = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Buttons/UI_ButtonSecondary_Normal.png", 0f);
            const int columns = 2, rows = 3;
            const float pad = 18f, gap = 14f;
            float width = (EmotePanel.width - 2f * pad - gap) / columns, height = (EmotePanel.height - 2f * pad - (rows - 1) * gap) / rows;
            string[] samples = { "Namaste!", "Well played!", "Nice call!", "Oops!", "Thinking…", "Good game!" };
            var buttons = new Button[columns * rows];
            var labels = new TMP_Text[columns * rows];
            for (int i = 0; i < buttons.Length; i++)
            {
                int column = i % columns, row = i / columns;
                var go = new GameObject($"Emote{i}", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(cardRect, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = UiBuildKit.FromMock(width, height);
                rect.anchoredPosition = new Vector2(
                    (pad + column * (width + gap) + width / 2f) * UiBuildKit.ScaleX,
                    -(pad + row * (height + gap) + height / 2f) * UiBuildKit.ScaleY);
                RectTransform buttonFace = Stretched("Face", rect);
                Image image = Sliced(buttonFace, buttonSprite);
                image.raycastTarget = true;
                var button = go.AddComponent<Button>();
                button.targetGraphic = image;
                ColorBlock colors = button.colors;
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
                colors.selectedColor = Color.white;
                colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;
                NoNavigation(button);
                var feedback = go.AddComponent<ButtonFeedback>();
                var feedbackSo = new SerializedObject(feedback);
                feedbackSo.FindProperty("_selectable").objectReferenceValue = button;
                feedbackSo.ApplyModifiedPropertiesWithoutUndo();
                buttons[i] = button;
                labels[i] = Label("Text", rect, style, samples[i]);
            }

            panelRoot.gameObject.SetActive(false);
            var picker = holder.gameObject.AddComponent<EmotePickerView>();
            var so = new SerializedObject(picker);
            so.FindProperty("_panel").objectReferenceValue = panelRoot.gameObject;
            so.FindProperty("_dismiss").objectReferenceValue = dismiss;
            SerializedProperty buttonList = so.FindProperty("_buttons"), labelList = so.FindProperty("_labels");
            buttonList.arraySize = labelList.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttonList.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                labelList.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_emotes"] = picker;
        }

        /// <summary>
        /// The wait before the next quick phrase (ADR 024): the button's own disc, tinted, over the
        /// button and its icon, filled round from twelve o'clock. As the wait runs out it sweeps
        /// away clockwise. Hidden while a phrase may be sent; it takes no touches.
        /// </summary>
        private static void BuildEmoteCooldown(ScreenBuildContext ctx)
        {
            RectTransform button = ctx.Nodes["EmoteButton"];
            var face = (RectTransform)button.Find("Face");
            var coverObject = new GameObject("Cooldown", typeof(RectTransform));
            var cover = (RectTransform)coverObject.transform;
            cover.SetParent(button, false);
            cover.SetAsLastSibling();
            cover.anchorMin = face.anchorMin;
            cover.anchorMax = face.anchorMax;
            cover.pivot = face.pivot;
            cover.anchoredPosition = face.anchoredPosition;
            cover.sizeDelta = face.sizeDelta;
            var image = coverObject.AddComponent<Image>();
            image.sprite = face.GetComponent<Image>().sprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = false;
            image.fillAmount = 1f;
            image.color = EmoteCooldownColor;
            image.raycastTarget = false;
            image.enabled = false;
            ctx.Bound["_emoteCooldown"] = image;
        }

        /// <summary>
        /// While the local seat is Away (GDD §8: after two timeouts a bot plays it), the whole table
        /// is the "I'm back" button (the owner, 3 Oct 2026, in place of the banner): a transparent
        /// catcher over the table, the hand and the HUD, hidden until then. The first touch only
        /// brings the player back; it is neither played as a card nor taken by anything under it.
        /// </summary>
        private static void BuildAwayCatcher(ScreenBuildContext ctx)
        {
            RectTransform rect = Stretched("AwayCatcher", ctx.Content);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            NoNavigation(button);
            rect.gameObject.SetActive(false);
            ctx.Bound["_imBackButton"] = button;
        }

        /// <summary>
        /// "Reconnecting…" while the session re-joins (GDD §8): a light dim over the whole table, which
        /// stays visible but takes no input, and the Dhaka Ring in the middle of the screen: its gold
        /// diamonds turning round the still seal, on unscaled time (standing still with Reduce motion),
        /// and the words below in the loading screen's serif (mockup 17), light on the dim.
        /// </summary>
        private static void BuildReconnecting(ScreenBuildContext ctx)
        {
            GameObject cover = BuildReconnectingCover(ctx.Content, out Spinner ring);
            ctx.Bound["_reconnecting"] = cover;
            ctx.Bound["_reconnectingRing"] = ring;
        }

        /// <summary>
        /// Gives the saved table prefab the "Reconnecting…" cover with the Dhaka Ring (the owner, 30 Oct
        /// 2026), made anew where the old one was and bound to the view; everything else in the prefab
        /// stays as it is.
        /// </summary>
        public static string UseDhakaRing()
        {
            string path = ScreenLayoutBuilder.PrefabPath("Table");
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            // TextMeshPro only lays text out under a Canvas, and the words are fitted by measuring
            // their layout, so the cover is built inside a temporary one, as ScreenLayoutBuilder does.
            var canvas = new GameObject("LayoutBuildCanvas", typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvas, root.scene);
            root.transform.SetParent(canvas.transform, false);
            try
            {
                var view = new SerializedObject(root.GetComponent<TableView>());
                var old = view.FindProperty("_reconnecting").objectReferenceValue as GameObject;
                if (old == null)
                {
                    return $"FAILED: {path} has no Reconnecting cover";
                }

                var parent = (RectTransform)old.transform.parent;
                int index = old.transform.GetSiblingIndex();
                Object.DestroyImmediate(old);
                GameObject cover = BuildReconnectingCover(parent, out Spinner ring);
                cover.transform.SetSiblingIndex(index);
                view.FindProperty("_reconnecting").objectReferenceValue = cover;
                view.FindProperty("_reconnectingRing").objectReferenceValue = ring;
                view.ApplyModifiedPropertiesWithoutUndo();

                root.transform.SetParent(null, false);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                return saved ? $"saved {path} with the Dhaka Ring" : $"FAILED to save {path}";
            }
            finally
            {
                root.transform.SetParent(null, false);
                Object.DestroyImmediate(canvas);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject BuildReconnectingCover(RectTransform content, out Spinner spinner)
        {
            RectTransform cover = Stretched("Reconnecting", content);
            var dim = cover.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, ReconnectingDim);
            dim.raycastTarget = true;

            // The ring, its seal and the words, centred together on the screen, as Figma lays them out.
            ScreenLayout loading = ScreenLayout.Load("SceneLoading");
            LayoutNode wordsNode = NodeOf(loading, "Loading");
            float[] ink = wordsNode.inkBox;
            float wordsHalf = (ink[3] - ink[1]) / 2f;
            var centre = new Vector2(UiBuildKit.ScreenCentre.x, UiBuildKit.ScreenCentre.y - (RingToWords + wordsHalf - RingReach) / 2f);

            RectTransform ring = UiBuildKit.CreateAt("Ring", cover, centre, UiBuildKit.ScreenCentre, Vector2.one * RingSize);
            UiBuildKit.AddImage(ring, LoadReconnectingSprite("Ring"), false);
            spinner = ring.gameObject.AddComponent<Spinner>();
            var spinnerSo = new SerializedObject(spinner);
            spinnerSo.FindProperty("_unscaledTime").boolValue = true;
            spinnerSo.FindProperty("_clockwise").boolValue = false;
            spinnerSo.ApplyModifiedPropertiesWithoutUndo();

            RectTransform seal = UiBuildKit.CreateAt("Seal", cover, centre, UiBuildKit.ScreenCentre, Vector2.one * SealSize);
            UiBuildKit.AddImage(seal, LoadReconnectingSprite("Seal"), false);

            // Fitted to the loading line, so the words come out at its size, then given their own.
            float wordsY = centre.y + RingToWords;
            float wordsX = centre.x - (ink[0] + ink[2]) / 2f;
            float[] inkHere = { ink[0] + wordsX, wordsY - wordsHalf, ink[2] + wordsX, wordsY + wordsHalf };
            TMP_FontAsset serif = UiBuildKit.LoadFontRole(wordsNode.font);
            TextMeshProUGUI label = UiBuildKit.FitLabel(
                "Text", cover, UiBuildKit.ScreenCentre, wordsNode.text, serif, inkHere, "centre", false, ReconnectingWordsColor, ReconnectingWordsColor);
            label.text = "Reconnecting…";
            var localized = label.gameObject.AddComponent<LocalizedText>();
            var so = new SerializedObject(localized);
            so.FindProperty("_key").stringValue = "ui.seat.reconnecting";
            so.ApplyModifiedPropertiesWithoutUndo();

            cover.gameObject.SetActive(false);
            return cover.gameObject;
        }

        /// <summary>The Dhaka Ring's sprites, uncompressed (the owner wants no softening).</summary>
        private static Sprite LoadReconnectingSprite(string name)
        {
            string path = $"{TableArt}/UI_Reconnecting_{name}.png";
            Sprite sprite = UiBuildKit.LoadSpriteAt(path, 0f);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            return sprite;
        }

        private static LayoutNode NodeOf(ScreenLayout layout, string id)
        {
            foreach (LayoutNode node in layout.nodes)
            {
                if (node.id == id)
                {
                    return node;
                }
            }

            throw new System.InvalidOperationException($"{layout.name} has no node {id}");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// GDD §16 "Touch targets are ≥ 88 px": grows what a control answers to, not what it draws,
        /// to at least <paramref name="minWidth"/> by <paramref name="minHeight"/> canvas units.
        /// Missing width is added equally left and right. Of the missing height,
        /// <paramref name="upShare"/> goes above the control and the rest below, so a control with
        /// a close neighbour on one side grows away from it. The art is not touched.
        /// </summary>
        public static void MinimumTouch(Graphic target, float minWidth, float minHeight, float upShare = 0.5f)
        {
            if (target == null)
            {
                return;
            }

            target.raycastTarget = true;
            Vector2 size = target.rectTransform.rect.size;
            float moreX = Mathf.Max(0f, minWidth - size.x), moreY = Mathf.Max(0f, minHeight - size.y);

            // Padding shrinks the area a graphic answers to; negative padding grows it. Left, bottom, right, top.
            target.raycastPadding = new Vector4(-moreX / 2f, -moreY * (1f - upShare), -moreX / 2f, -moreY * upShare);
        }

        /// <summary>
        /// A busy spinner at the left end of a pill: the kit's ring, tinted like the pill's text,
        /// drawn as a three-quarter arc that turns while it is shown (as on Create Room).
        /// </summary>
        private static GameObject BuildSpinner(RectTransform pill, float size, float centreX, Color color)
        {
            var spinner = new GameObject("Spinner", typeof(RectTransform));
            var rect = (RectTransform)spinner.transform;
            rect.SetParent(pill, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(centreX, 0f);
            Image ring = UiBuildKit.AddImage(rect, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Ring_Spinner.png", 0f), false);
            ring.color = color;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0.75f;
            spinner.AddComponent<Spinner>();
            return spinner;
        }

        private static Sprite LoadTableSprite(string name) => UiBuildKit.LoadSpriteAt($"{TableArt}/{name}.png", 0f);

        /// <summary>A mockup-pixel offset (y down) as a canvas offset (y up).</summary>
        private static Vector2 FromMock(Vector2 offset) => new Vector2(offset.x * UiBuildKit.ScaleX, -offset.y * UiBuildKit.ScaleY);

        private static RectTransform Stretched(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>A horizontally sliced pill that keeps its end caps round at this rect's height.</summary>
        private static Image Sliced(RectTransform rect, Sprite sprite)
        {
            Image image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }

            image.sprite = sprite;
            image.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                image.type = Image.Type.Sliced;
                float height = Mathf.Max(1f, rect.rect.height > 0f ? rect.rect.height : rect.sizeDelta.y);
                image.pixelsPerUnitMultiplier = UiBuildKit.NativeSize(sprite).y / height;
            }

            return image;
        }

        private static TextMeshProUGUI Label(string name, RectTransform parent, TMP_Text style, string text)
        {
            RectTransform rect = Stretched(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            CopyStyle(style, label);
            label.alignment = TextAlignmentOptions.Center;
            label.text = text;
            return label;
        }

        private static void CopyStyle(TMP_Text from, TextMeshProUGUI to)
        {
            to.font = from.font;
            to.fontSharedMaterial = from.fontSharedMaterial;
            to.fontSize = from.fontSize;
            to.characterHorizontalScale = from.characterHorizontalScale;
            to.color = from.color;
            to.raycastTarget = false;
            to.textWrappingMode = TextWrappingModes.NoWrap;
            to.overflowMode = TextOverflowModes.Overflow;
        }

        private static void NoNavigation(Selectable selectable)
        {
            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.None;
            selectable.navigation = navigation;
        }

        private static void NestedCanvas(RectTransform rect)
        {
            if (rect.GetComponent<Canvas>() == null)
            {
                rect.gameObject.AddComponent<Canvas>();
            }

            if (rect.GetComponent<GraphicRaycaster>() == null)
            {
                rect.gameObject.AddComponent<GraphicRaycaster>();
            }
        }
    }
}
