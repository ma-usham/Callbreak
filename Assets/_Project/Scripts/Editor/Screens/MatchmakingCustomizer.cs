using Callbreak.Presentation.Widgets;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 05 widgets: the search arc spins (the spade inside it is a separate, still node), and
    /// each seat group gets a <see cref="MatchmakingSeat"/> that switches between its filled and
    /// searching looks. Seat colours are the ones measured on the mockup: a seat keeps its own
    /// colours for the look it has there, and takes the other look's colours from a seat that
    /// shows that look (seat 3 searching, seat 2 filled).
    /// </summary>
    public static class MatchmakingCustomizer
    {
        private const int SeatCount = 4;
        private const int SearchingSeatInMockup = 3;
        private const int FilledReferenceSeat = 2;

        public static void Customize(ScreenBuildContext ctx)
        {
            ctx.Nodes["SearchArc"].gameObject.AddComponent<Spinner>();

            for (int i = 0; i < SeatCount; i++)
            {
                bool filledInMockup = i != SearchingSeatInMockup;
                int filledRef = filledInMockup ? i : FilledReferenceSeat;
                int searchingRef = filledInMockup ? SearchingSeatInMockup : i;

                var seat = ctx.Nodes[$"Seat{i}"].gameObject.AddComponent<MatchmakingSeat>();
                var so = new SerializedObject(seat);
                so.FindProperty("_filledLook").objectReferenceValue = ctx.Nodes[$"SeatFilled{i}"].gameObject;
                so.FindProperty("_searchingLook").objectReferenceValue = ctx.Nodes[$"SeatSearching{i}"].gameObject;
                so.FindProperty("_name").objectReferenceValue = ctx.Nodes[$"SeatName{i}"].GetComponent<TMPro.TMP_Text>();
                so.FindProperty("_status").objectReferenceValue = ctx.Nodes[$"SeatStatus{i}"].GetComponent<TMPro.TMP_Text>();
                so.FindProperty("_filledNameColor").colorValue = LabelColor(ctx, $"SeatName{filledRef}");
                so.FindProperty("_filledStatusColor").colorValue = LabelColor(ctx, $"SeatStatus{filledRef}");
                so.FindProperty("_searchingNameColor").colorValue = LabelColor(ctx, $"SeatName{searchingRef}");
                so.FindProperty("_searchingStatusColor").colorValue = LabelColor(ctx, $"SeatStatus{searchingRef}");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Color LabelColor(ScreenBuildContext ctx, string id)
        {
            LayoutNode node = ctx.Node(id);
            return UiBuildKit.Hex(string.IsNullOrEmpty(node.color) ? "#000000" : node.color);
        }
    }
}
