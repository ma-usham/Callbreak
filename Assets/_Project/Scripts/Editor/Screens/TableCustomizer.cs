using System.Collections.Generic;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
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
    /// the layers (table, hand, HUD as nested canvases, GDD §23); the card pool; the hand with its
    /// touch area, pinned to the bottom edge; the trick slots and the opponents' fans, placed as
    /// mockup 08 draws them; a <see cref="SeatView"/> per seat, with the timer ring, seconds disc
    /// and bubble the opponents' seats need (derived from the local seat's); the turn prompt, the
    /// trick counter and the emote picker (derived from kit pills, panels and buttons).
    /// All mockup measurements are in mockup pixels (1672 × 941), as in the layout.
    /// </summary>
    public static class TableCustomizer
    {
        private const string TableArt = UiBuildKit.ArtRoot + "/Screens/Table";

        // ---- The local hand. Mockup 08 draws its cards unevenly (88 to 114 px apart, 116 to 136 wide);
        // this is the even fan that fits its thirteen card corners best, with the tops of the middle
        // cards at y 716.
        private static readonly Vector2 HandCentre = new Vector2(868.6f, 839.05f);
        private const float HandTouchTop = 684f;
        private const float HandWidthFraction = 0.772f;         // 94.5 px between cards on the mockup; the GDD says 70%
        private const float HandLift = 14.4f;                   // 12.5 mockup px; the GDD says 28
        private const float HandArcRadius = 3740f;

        // ---- Trick slots by relative seat (0 bottom, 1 right, 2 top, 3 left): centre and turn.
        private static readonly Vector2[] TrickSlots =
        {
            new Vector2(836.2f, 558f), new Vector2(976.3f, 481.3f), new Vector2(836.2f, 404f), new Vector2(697.7f, 480.5f),
        };

        private static readonly float[] TrickTurns = { 0f, 5.5f, 0f, -5.6f };
        private const float TrickCardWidth = 128.4f;            // bottom edge; the top edge is 10% narrower

        // ---- Opponent fans: a card is 42 px wide on the mockup.
        private const float FanCardWidth = 42f;

        // ---- Derived pieces (not on the mockup): where they sit.
        private static readonly Vector2 PromptCentre = new Vector2(869.3f, 662f);
        private const float PromptHeight = 38f;
        private static readonly Vector2 TrickLabelCentre = new Vector2(836f, 104f);
        private static readonly Rect EmotePanel = new Rect(112f, 600f, 448f, 216f);
        private const float BubbleHeight = 33f;

        public static void Customize(ScreenBuildContext ctx)
        {
            // The edge bands only shape the plate (see the layout); they are not part of the screen.
            foreach (string id in new[] { "EdgeTop", "EdgeBottom", "EdgeLeft", "EdgeRight", "HandArea" })
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
            UiBuildKit.EnsureCharacters(statusStyle.font, "✓");

            // A tap on the table itself skips a long animation; everything else sits above this.
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
            var skin = ctx.Root.gameObject.AddComponent<TableSkin>();
            var skinSo = new SerializedObject(skin);
            skinSo.FindProperty("_cards").objectReferenceValue = TableCardBuilder.EnsureLibrary();
            SerializedProperty avatars = skinSo.FindProperty("_avatars");
            avatars.arraySize = 1;
            avatars.GetArrayElementAtIndex(0).objectReferenceValue = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Avatars/Avatar_Default.png", 0f);
            skinSo.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_skin"] = skin;
            ctx.Bound["_cards"] = pool;

            BuildTrickArea(ctx);
            for (int seat = 0; seat < TableView.SeatCount; seat++)
            {
                BuildSeat(ctx, seat, pool, nameStyle);
            }

            RectTransform handLayer = BuildHand(ctx, pool, centre);
            BuildPrompt(ctx, handLayer, statusStyle);
            BuildTrickLabel(ctx);
            BuildEmotePicker(ctx, statusStyle);

            // Table, hand and HUD rebuild at different rates, so each gets its own canvas (GDD §23).
            NestedCanvas(centre);
            NestedCanvas(handLayer);
            NestedCanvas(ctx.SafeArea);
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

        private static void BuildTrickArea(ScreenBuildContext ctx)
        {
            RectTransform area = ctx.Nodes["TrickArea"];
            Vector2 centre = ctx.CentreOf("TrickArea");
            var trick = area.gameObject.AddComponent<TrickAreaView>();
            var so = new SerializedObject(trick);
            so.FindProperty("_area").objectReferenceValue = area;
            SerializedProperty positions = so.FindProperty("_slotPositions");
            SerializedProperty rotations = so.FindProperty("_slotRotations");
            positions.arraySize = rotations.arraySize = TrickAreaView.Slots;
            for (int i = 0; i < TrickAreaView.Slots; i++)
            {
                positions.GetArrayElementAtIndex(i).vector2Value = FromMock(TrickSlots[i] - centre);
                rotations.GetArrayElementAtIndex(i).floatValue = TrickTurns[i];
            }

            so.FindProperty("_cardScale").floatValue = TrickCardWidth * UiBuildKit.ScaleX / TableCardBuilder.CardWidth;
            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_trick"] = trick;
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

            var hand = touchObject.AddComponent<HandView>();
            var so = new SerializedObject(hand);
            so.FindProperty("_cardsRoot").objectReferenceValue = cards;
            so.FindProperty("_widthReference").objectReferenceValue = ctx.SafeArea;
            so.FindProperty("_pool").objectReferenceValue = pool;
            so.FindProperty("_sprites").objectReferenceValue = TableCardBuilder.EnsureLibrary();
            SerializedProperty layout = so.FindProperty("_layout");
            layout.FindPropertyRelative("CardWidth").floatValue = TableCardBuilder.CardWidth;
            layout.FindPropertyRelative("MinWidthFraction").floatValue = HandWidthFraction;
            layout.FindPropertyRelative("MaxWidthFraction").floatValue = 0.86f;
            layout.FindPropertyRelative("MinStrip").floatValue = HandLayout.HardMinStrip;
            layout.FindPropertyRelative("MaxStrip").floatValue = 0.8f * TableCardBuilder.CardWidth;
            layout.FindPropertyRelative("Lift").floatValue = HandLift;
            layout.FindPropertyRelative("HoverLift").floatValue = 18f;
            layout.FindPropertyRelative("ArcRadius").floatValue = HandArcRadius;
            so.ApplyModifiedPropertiesWithoutUndo();
            ctx.Bound["_hand"] = hand;
            return layer;
        }

        private static void BuildSeat(ScreenBuildContext ctx, int seat, CardViewPool pool, TMP_Text nameStyle)
        {
            RectTransform root = ctx.Nodes[$"Seat{seat}"];
            Vector2 seatCentre = ctx.CentreOf($"Seat{seat}");
            RectTransform ring = ctx.Nodes[$"SeatRing{seat}"];
            LayoutNode ringNode = ctx.Node($"SeatRing{seat}");
            Vector2 avatarCentre = ringNode.Centre;
            bool local = seat == 0;

            // The turn ring: the local seat's is on the layout; the others get the same ring, scaled
            // to their avatar (a gold ring around the active seat, GDD §11 T rules).
            RectTransform timerRing;
            if (local)
            {
                timerRing = ctx.Nodes["SeatTimerRing0"];
            }
            else
            {
                LayoutNode localRing = ctx.Node("SeatRing0"), localTimer = ctx.Node("SeatTimerRing0");
                float size = ringNode.rect[2] * localTimer.rect[2] / localRing.rect[2];
                timerRing = UiBuildKit.CreateAt($"SeatTimerRing{seat}", root, avatarCentre, seatCentre, UiBuildKit.FromMock(size, size));
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

            // Seconds disc: the local seat's is on the layout; the others get a copy at a free corner.
            RectTransform disc = ctx.Nodes["SeatTimerDisc0"];
            if (!local)
            {
                LayoutNode discNode = ctx.Node("SeatTimerDisc0");
                Vector2 offset = seat == 2 ? new Vector2(0f, -40f) : new Vector2(seat == 3 ? -29f : 29f, -29f);
                disc = Object.Instantiate(disc, root);
                disc.name = $"SeatTimerDisc{seat}";
                disc.anchoredPosition = FromMock(avatarCentre + offset - seatCentre);
                disc.gameObject.SetActive(false);
            }

            TMP_Text timerText = disc.GetComponentInChildren<TMP_Text>(true);

            // Bubble for a call or a quick phrase: the name plate's white pill, below the status pill
            // (beside the emote button for the local seat).
            Vector2 bubbleCentre = local ? avatarCentre + new Vector2(118f, -76f) : avatarCentre + new Vector2(0f, seat == 2 ? 120f : 123f);
            RectTransform bubble = UiBuildKit.CreateAt("Bubble", root, bubbleCentre, seatCentre, UiBuildKit.FromMock(90f, BubbleHeight));
            Sliced(bubble, LoadTableSprite("UI_NamePlate"));
            TextMeshProUGUI bubbleText = Label("Text", bubble, nameStyle, "4");
            bubble.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<SeatView>();
            var so = new SerializedObject(view);
            so.FindProperty("_anchor").objectReferenceValue = ring;
            so.FindProperty("_avatar").objectReferenceValue = ctx.Nodes[$"SeatAvatar{seat}"].Find("Face").GetComponent<Image>();
            so.FindProperty("_name").objectReferenceValue = ctx.Nodes[$"SeatName{seat}"].GetComponent<TMP_Text>();
            so.FindProperty("_status").objectReferenceValue = ctx.Nodes[$"SeatStatus{seat}"].GetComponent<TMP_Text>();
            so.FindProperty("_statusPill").objectReferenceValue = ctx.Nodes[$"SeatPill{seat}"].Find("Face").GetComponent<Image>();
            so.FindProperty("_timerRing").objectReferenceValue = ringImage;
            so.FindProperty("_timerDisc").objectReferenceValue = disc.gameObject;
            so.FindProperty("_timerText").objectReferenceValue = timerText;
            so.FindProperty("_bubble").objectReferenceValue = bubble;
            so.FindProperty("_bubbleText").objectReferenceValue = bubbleText;
            so.FindProperty("_bubblePadding").floatValue = 40f;
            so.FindProperty("_bubbleMinWidth").floatValue = 64f * UiBuildKit.ScaleX;

            // The ring sprite is the mockup's orange; the last five seconds multiply it to crimson
            // (#861018, GDD §12); a met call tints the sand pill gold.
            so.FindProperty("_ringColor").colorValue = Color.white;
            so.FindProperty("_ringWarningColor").colorValue = new Color(0x86 / 249f, 0x10 / 165f, 0x18 / 28f, 1f);
            so.FindProperty("_pillColor").colorValue = Color.white;
            so.FindProperty("_pillMetColor").colorValue = new Color(1f, 0.96f, 0.62f, 1f);

            if (!local)
            {
                // Tapping an opponent mutes their quick phrases (GDD §8).
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
        }

        /// <summary>
        /// The face-down fan behind an opponent's avatar, as mockup 08 draws it: to the table side of
        /// the side seats, and to both sides of the top seat.
        /// </summary>
        private static OpponentHandView BuildFan(ScreenBuildContext ctx, int seat, CardViewPool pool, Vector2 avatarCentre)
        {
            RectTransform area = ctx.Nodes[$"SeatFan{seat}"];
            Vector2 areaCentre = ctx.CentreOf($"SeatFan{seat}");
            var sides = new List<(Vector2 pivot, float radius, float start, float step, float spread)>();
            if (seat == 2)
            {
                sides.Add((avatarCentre + new Vector2(-27f, 171f), 180f, -3f, -7f, 27f));
                sides.Add((avatarCentre + new Vector2(27f, 171f), 180f, 3f, 7f, 27f));
            }
            else
            {
                float side = seat == 3 ? 1f : -1f;
                sides.Add((avatarCentre + new Vector2(36.5f * side, 196f), 214f, 0f, 6f * side, 30f));
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

            // Pinned to the bottom-left corner like the emote button it belongs to.
            var card = new GameObject("Card", typeof(RectTransform));
            var cardRect = (RectTransform)card.transform;
            cardRect.SetParent(panelRoot, false);
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0f, 0f);
            cardRect.anchoredPosition = new Vector2(EmotePanel.x * UiBuildKit.ScaleX, (UiBuildKit.MockHeight - EmotePanel.yMax) * UiBuildKit.ScaleY);
            cardRect.sizeDelta = UiBuildKit.FromMock(EmotePanel.width, EmotePanel.height);

            Sprite panelShadow = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Common/UI_Panel_Card_Shadow.png", 0f);
            RectTransform shadowRect = Stretched("Shadow", cardRect);
            shadowRect.offsetMin = new Vector2(-14f, -16f);
            shadowRect.offsetMax = new Vector2(14f, 12f);
            UiBuildKit.AddImage(shadowRect, panelShadow, false);
            RectTransform faceRect = Stretched("Face", cardRect);
            Image face = UiBuildKit.AddImage(faceRect, UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Common/UI_Panel_Card.png", 0f), true);
            face.raycastTarget = true;      // taps on the panel itself do not close it

            Sprite buttonSprite = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Buttons/UI_ButtonSecondary_Normal.png", 0f);
            const int columns = 2, rows = 3;
            const float pad = 18f, gap = 12f;
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

        // ------------------------------------------------------------------ helpers

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
