using Callbreak.Presentation.Widgets;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 06: the three difficulty cards form one <see cref="SelectableGroup"/>. Each card is a
    /// tap target; its selected look (the crimson outline and check badge cut from the Normal card)
    /// shows only on the chosen card. The plain kit card underneath is every card's unselected look.
    /// Normal is the default (GDD §11, the mockup's state).
    /// </summary>
    public static class OfflineSetupCustomizer
    {
        private static readonly string[] Cards = { "CardEasy", "CardNormal", "CardHard" };
        private const int DefaultDifficulty = 1;

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);   // "mirror" on the title divider's left line
            var group =ctx.Nodes["Difficulty"].gameObject.AddComponent<SelectableGroup>();
            var so = new SerializedObject(group);
            SerializedProperty options = so.FindProperty("_options");
            SerializedProperty looks = so.FindProperty("_selectedLooks");
            options.arraySize = Cards.Length;
            looks.arraySize = Cards.Length;
            for (int i = 0; i < Cards.Length; i++)
            {
                options.GetArrayElementAtIndex(i).objectReferenceValue = ctx.Nodes[Cards[i]].GetComponent<UnityEngine.UI.Button>();
                looks.GetArrayElementAtIndex(i).objectReferenceValue = ctx.Nodes[$"Selected{i}"].gameObject;
                ctx.Nodes[$"Selected{i}"].gameObject.SetActive(i == DefaultDifficulty);
            }

            so.FindProperty("_unselectedLooks").arraySize = 0;
            so.FindProperty("_labels").arraySize = 0;
            so.FindProperty("_selectedIndex").intValue = DefaultDifficulty;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
