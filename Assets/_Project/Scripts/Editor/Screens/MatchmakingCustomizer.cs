using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 05 widgets: the search arc spins (the spade inside it is a separate, still node), and
    /// each seat group gets a <see cref="MatchmakingSeat"/> that switches between its filled and
    /// searching looks. The text styles are the fitted mockup labels: a seat keeps its own labels'
    /// style for the look it has on the mockup, and takes the other look's style from a seat that
    /// shows that look there (seat 3 searching, seat 2 filled). A name wider than its seat's column
    /// shrinks instead of running into its neighbour (the mockup's names are short). The filled look
    /// gets the player's portrait over the mockup's black silhouette (ADR 041).
    /// </summary>
    public static class MatchmakingCustomizer
    {
        private const int SeatCount = 4;
        private const int SearchingSeatInMockup = 3;
        private const int FilledReferenceSeat = 2;

        /// <summary>Gap kept between two neighbouring names, mockup pixels.</summary>
        private const float NameGap = 12f;

        /// <summary>A long name shrinks no further than this (GDD §12: nothing smaller than 22 px at 1080p).</summary>
        private const float MinNameSize = 22f;

        /// <summary>
        /// The portrait's share of the filled seat's disc (ADR 041): it covers the silhouette drawn
        /// in UI_Seat_Filled (out to 83% of the radius) and leaves the ivory band inside the ring.
        /// </summary>
        private const float PortraitShare = 0.88f;

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);   // "mirror" on the title divider's left line
            ctx.Nodes["SearchArc"].gameObject.AddComponent<Spinner>();
            float nameWidth = ctx.Node("Seat1").rect[0] - ctx.Node("Seat0").rect[0] - NameGap;

            for (int i = 0; i < SeatCount; i++)
            {
                bool filledInMockup = i != SearchingSeatInMockup;
                int filledRef = filledInMockup ? i : FilledReferenceSeat;
                int searchingRef = filledInMockup ? SearchingSeatInMockup : i;

                var seat = ctx.Nodes[$"Seat{i}"].gameObject.AddComponent<MatchmakingSeat>();
                var so = new SerializedObject(seat);
                so.FindProperty("_filledLook").objectReferenceValue = ctx.Nodes[$"SeatFilled{i}"].gameObject;
                so.FindProperty("_searchingLook").objectReferenceValue = ctx.Nodes[$"SeatSearching{i}"].gameObject;
                so.FindProperty("_avatar").objectReferenceValue = Portrait(ctx.Nodes[$"SeatFilled{i}"]);
                so.FindProperty("_name").objectReferenceValue = Label(ctx, $"SeatName{i}");
                so.FindProperty("_status").objectReferenceValue = Label(ctx, $"SeatStatus{i}");
                SetStyle(so, "_filledName", Label(ctx, $"SeatName{filledRef}"));
                SetStyle(so, "_filledStatus", Label(ctx, $"SeatStatus{filledRef}"));
                SetStyle(so, "_searchingName", Label(ctx, $"SeatName{searchingRef}"));
                SetStyle(so, "_searchingStatus", Label(ctx, $"SeatStatus{searchingRef}"));
                so.ApplyModifiedPropertiesWithoutUndo();

                TMP_Text name = Label(ctx, $"SeatName{i}");
                RectTransform nameRect = name.rectTransform;
                nameRect.sizeDelta = new Vector2(UiBuildKit.FromMock(nameWidth, 0f).x, nameRect.sizeDelta.y);
                name.fontSizeMax = name.fontSize;
                name.fontSizeMin = Mathf.Min(name.fontSize, MinNameSize);
                name.enableAutoSizing = true;
            }
        }

        private static TMP_Text Label(ScreenBuildContext ctx, string id) => ctx.Nodes[id].GetComponent<TMP_Text>();

        /// <summary>The player's portrait on the filled look's disc (a child of its face), centred; the presenter sets the picture.</summary>
        private static Image Portrait(RectTransform filled)
        {
            var face = (RectTransform)filled.Find("Face");
            Rect disc = face.rect;
            float size = PortraitShare * Mathf.Min(disc.width, disc.height);
            var go = new GameObject("Avatar", typeof(RectTransform));
            go.layer = filled.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(face, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            Image portrait = UiBuildKit.AddImage(rect, AssetDatabase.LoadAssetAtPath<Sprite>($"{UiBuildKit.ArtRoot}/UI/Avatars/Avatar_Default.png"), false);
            portrait.preserveAspect = true;
            return portrait;
        }

        private static void SetStyle(SerializedObject so, string field, TMP_Text label)
        {
            so.FindProperty($"{field}._font").objectReferenceValue = label.font;
            so.FindProperty($"{field}._material").objectReferenceValue = label.fontSharedMaterial;
            so.FindProperty($"{field}._size").floatValue = label.fontSize;
            so.FindProperty($"{field}._horizontalScale").floatValue = label.characterHorizontalScale;
            so.FindProperty($"{field}._color").colorValue = label.color;
        }
    }
}
