using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 04: gives every seat one name style and one status style (fitted on seat 1, "Asha" /
    /// "Ready"), centred under its avatar; lets the view dim an empty seat's avatar (CanvasGroup)
    /// and hear a long press on it (<see cref="LongPressArea"/>); and adds the guest state's two labels: "Ready" on the primary button (the Start Game label
    /// re-set and centred, no spade) and "Waiting for host…" in the footer's place.
    /// </summary>
    public static class PrivateLobbyCustomizer
    {
        private const int SeatCount = 4;
        private const int ReferenceSeat = 1;
        private const string WaitingKey = "ui.status.waiting_for_host";      // already in UI.csv
        private const string WaitingText = "Waiting for host…";
        private const float TitleGap = 20f;     // mockup px kept free between the title and the logo
        private const float CodeGap = 8f;       // ... between the code and the copy glyph
        private const float SeatGap = 8f;       // ... between a name and its seat separators

        public static void Customize(ScreenBuildContext ctx)
        {
            BuildSeats(ctx);
            BuildGuestLabels(ctx);
            BoundDynamicLabels(ctx);
        }

        /// <summary>
        /// Room names, codes and player names vary in width: the title stays clear of the logo, the
        /// code of the copy glyph, and each name inside its seat column.
        /// </summary>
        private static void BoundDynamicLabels(ScreenBuildContext ctx)
        {
            if (ctx.Nodes.TryGetValue("Title", out RectTransform title) && ctx.Nodes.ContainsKey("Logo"))
            {
                float[] logo = ctx.Node("Logo").rect, ink = ctx.Node("Title").inkBox;
                float centre = (ink[0] + ink[2]) / 2f;
                Bound(title.GetComponent<TMP_Text>(), 2f * (centre - (logo[0] + logo[2]) - TitleGap), 0.6f);
            }

            if (ctx.Nodes.TryGetValue("Code", out RectTransform code) && ctx.Nodes.ContainsKey("CopyIcon"))
            {
                float room = ctx.Node("CopyIcon").spriteRect[0] - ctx.Node("Code").inkBox[0] - CodeGap;
                Bound(code.GetComponent<TMP_Text>(), room, 0.7f);
            }

            if (ctx.Nodes.ContainsKey("SeatAvatar0") && ctx.Nodes.ContainsKey("SeatAvatar1"))
            {
                float column = ctx.CentreOf("SeatAvatar1").x - ctx.CentreOf("SeatAvatar0").x - 2f * SeatGap;
                for (int i = 0; i < SeatCount; i++)
                {
                    if (ctx.Nodes.TryGetValue($"SeatName{i}", out RectTransform name))
                    {
                        Bound(name.GetComponent<TMP_Text>(), column, 0.7f);
                    }
                }
            }
        }

        private static void BuildSeats(ScreenBuildContext ctx)
        {
            for (int i = 0; i < SeatCount; i++)
            {
                if (!ctx.Nodes.TryGetValue($"SeatAvatar{i}", out RectTransform avatar))
                {
                    ctx.Warnings.Add($"SeatAvatar{i}: missing");
                    continue;
                }

                // The ring is the seat's hit area for the long press; the group dims an empty seat.
                avatar.gameObject.AddComponent<CanvasGroup>();
                Image ring = avatar.Find("Face")?.GetComponent<Image>();
                if (ring != null)
                {
                    ring.raycastTarget = true;
                }

                ctx.Bound[$"_seatHold{i}"] = avatar.gameObject.AddComponent<LongPressArea>();
            }

            if (ctx.Nodes.TryGetValue("SeatPicture0", out RectTransform picture))
            {
                Image face = picture.Find("Face")?.GetComponent<Image>();
                if (face != null)
                {
                    ctx.Bound["_defaultAvatar"] = face.sprite;
                }
            }

            NormalizeSeatLabels(ctx, "SeatName");
            NormalizeSeatLabels(ctx, "SeatStatus");
        }

        /// <summary>
        /// Every seat's label gets the reference seat's fitted size, width scale, baseline and a
        /// plain colour, and is centred under its own avatar, so any name renders alike.
        /// </summary>
        private static void NormalizeSeatLabels(ScreenBuildContext ctx, string prefix)
        {
            if (!ctx.Nodes.TryGetValue($"{prefix}{ReferenceSeat}", out RectTransform referenceRect))
            {
                ctx.Warnings.Add($"{prefix}{ReferenceSeat}: missing");
                return;
            }

            var reference = referenceRect.GetComponent<TMP_Text>();
            float referenceSeatX = ctx.CentreOf($"SeatAvatar{ReferenceSeat}").x;
            for (int i = 0; i < SeatCount; i++)
            {
                if (!ctx.Nodes.TryGetValue($"{prefix}{i}", out RectTransform rect) || !ctx.Nodes.ContainsKey($"SeatAvatar{i}"))
                {
                    ctx.Warnings.Add($"{prefix}{i}: missing");
                    continue;
                }

                var label = rect.GetComponent<TMP_Text>();
                Color plain = label.enableVertexGradient ? label.colorGradient.bottomLeft : label.color;
                if (i != ReferenceSeat)
                {
                    rect.pivot = referenceRect.pivot;
                    rect.sizeDelta = referenceRect.sizeDelta;
                    float seatX = ctx.CentreOf($"SeatAvatar{i}").x;
                    rect.anchoredPosition = referenceRect.anchoredPosition + new Vector2((seatX - referenceSeatX) * UiBuildKit.ScaleX, 0f);
                    label.fontSize = reference.fontSize;
                    label.characterHorizontalScale = reference.characterHorizontalScale;
                    label.characterSpacing = reference.characterSpacing;
                    label.alignment = reference.alignment;
                }

                label.enableVertexGradient = false;
                label.color = new Color(plain.r, plain.g, plain.b, 1f);
            }
        }

        private static void BuildGuestLabels(ScreenBuildContext ctx)
        {
            if (ctx.Nodes.TryGetValue("StartLabel", out RectTransform startLabel))
            {
                var readyObject = Object.Instantiate(startLabel.gameObject, startLabel.parent, false);
                readyObject.name = "ReadyLabel";
                readyObject.transform.SetSiblingIndex(startLabel.GetSiblingIndex() + 1);
                RemoveLocalization(readyObject);
                var ready = readyObject.GetComponent<TMP_Text>();
                ready.text = "Ready";                      // design-time sample; the presenter sets Ready / Not ready
                var readyRect = (RectTransform)readyObject.transform;
                readyRect.pivot = new Vector2(0.5f, readyRect.pivot.y);
                readyRect.anchoredPosition = new Vector2(0f, readyRect.anchoredPosition.y);   // centred on the button (no spade)
                ready.alignment = TextAlignmentOptions.Baseline;
                readyObject.SetActive(false);
                ctx.Bound["_readyLabel"] = ready;
            }
            else
            {
                ctx.Warnings.Add("StartLabel: missing; no guest Ready label");
            }

            if (ctx.Nodes.TryGetValue("Footer", out RectTransform footer))
            {
                var waitingObject = Object.Instantiate(footer.gameObject, footer.parent, false);
                waitingObject.name = "WaitingLabel";
                waitingObject.transform.SetSiblingIndex(footer.GetSiblingIndex() + 1);
                RemoveLocalization(waitingObject);
                waitingObject.GetComponent<TMP_Text>().text = WaitingText;
                var localized = waitingObject.AddComponent<LocalizedText>();
                var so = new SerializedObject(localized);
                so.FindProperty("_key").stringValue = WaitingKey;
                so.ApplyModifiedPropertiesWithoutUndo();
                waitingObject.SetActive(false);
                ctx.Bound["_waitingLabel"] = waitingObject;
            }
            else
            {
                ctx.Warnings.Add("Footer: missing; no Waiting for host label");
            }
        }

        /// <summary>
        /// Caps a dynamic label at a width (mockup px): longer text first shrinks down to
        /// <paramref name="minScale"/> of the fitted size, then ends in an ellipsis. Text that
        /// fits keeps the fitted size, so the mockup sample is unchanged.
        /// </summary>
        private static void Bound(TMP_Text label, float widthMock, float minScale)
        {
            var rect = (RectTransform)label.transform;
            rect.sizeDelta = new Vector2(widthMock * UiBuildKit.ScaleX, rect.sizeDelta.y);
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = label.fontSize * minScale;
            label.enableAutoSizing = minScale < 1f;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void RemoveLocalization(GameObject gameObject)
        {
            var localized = gameObject.GetComponent<LocalizedText>();
            if (localized != null)
            {
                Object.DestroyImmediate(localized);
            }
        }
    }
}
