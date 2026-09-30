using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 15: the three deck options form one <see cref="SelectableGroup"/> (selected look =
    /// crimson outline + filled radio, unselected look = empty radio; the plain kit card is every
    /// option's base). The deck pictures keep a card's proportions when the presenter swaps in an
    /// ace. The footer line and the button carry the GDD's copy update; they are fitted on the
    /// wording the mockup shows, so they keep its size, width scale, baseline and centre while
    /// their width follows the new text.
    /// </summary>
    public static class CardCollectionCustomizer
    {
        private const int DeckCount = 3;
        private const int DefaultDeck = 0;

        // What the mockup says where the GDD (§11 screen 15, "Copy update") re-worded the copy.
        private const string MockupFooter = "Choose a card back for your next match.";
        private const string MockupUseLabel = "Use Selected Style";

        public static void Customize(ScreenBuildContext ctx)
        {
            var group = ctx.Nodes["Decks"].gameObject.AddComponent<SelectableGroup>();
            var so = new SerializedObject(group);
            SerializedProperty options = so.FindProperty("_options");
            SerializedProperty selected = so.FindProperty("_selectedLooks");
            SerializedProperty unselected = so.FindProperty("_unselectedLooks");
            options.arraySize = DeckCount;
            selected.arraySize = DeckCount;
            unselected.arraySize = DeckCount;
            for (int i = 0; i < DeckCount; i++)
            {
                options.GetArrayElementAtIndex(i).objectReferenceValue = ctx.Nodes[$"DeckOption{i}"].GetComponent<Button>();
                selected.GetArrayElementAtIndex(i).objectReferenceValue = ctx.Nodes[$"Selected{i}"].gameObject;
                unselected.GetArrayElementAtIndex(i).objectReferenceValue = ctx.Nodes[$"Unselected{i}"].gameObject;
                ctx.Nodes[$"Selected{i}"].gameObject.SetActive(i == DefaultDeck);
                ctx.Nodes[$"Unselected{i}"].gameObject.SetActive(i != DefaultDeck);

                Image preview = ctx.Nodes[$"DeckPreview{i}"].Find("Face").GetComponent<Image>();
                preview.preserveAspect = true;
            }

            so.FindProperty("_labels").arraySize = 0;
            so.FindProperty("_selectedIndex").intValue = DefaultDeck;
            so.ApplyModifiedPropertiesWithoutUndo();

            FitOnMockupWording(ctx, "Footer", MockupFooter);
            FitOnMockupWording(ctx, "UseLabel", MockupUseLabel);
        }

        /// <summary>
        /// Gives a label the size, width scale and baseline the mockup's wording has in its ink box,
        /// and centres the label's own (re-worded) text where the mockup's text is centred.
        /// </summary>
        private static void FitOnMockupWording(ScreenBuildContext ctx, string id, string mockupWording)
        {
            LayoutNode node = ctx.Node(id);
            if (!ctx.Nodes.TryGetValue(id, out RectTransform rect) || node.inkBox == null || node.inkBox.Length != 4)
            {
                ctx.Warnings.Add($"{id}: label missing; copy update not fitted");
                return;
            }

            var label = rect.GetComponent<TMP_Text>();
            Vector2 parentCentre = string.IsNullOrEmpty(node.parent) ? UiBuildKit.ScreenCentre : ctx.CentreOf(node.parent);
            TextMeshProUGUI reference = UiBuildKit.FitLabel(
                id + "MockupWording", rect.parent, parentCentre, mockupWording, label.font, node.inkBox, "centre", node.tracked, label.color, label.color);
            var referenceRect = (RectTransform)reference.transform;
            bool measured = UiBuildKit.InkBounds(reference, out Rect referenceInk, out _);
            float referenceCentreX = referenceRect.anchoredPosition.x + referenceInk.center.x;

            label.fontSize = reference.fontSize;
            label.characterHorizontalScale = reference.characterHorizontalScale;
            label.characterSpacing = reference.characterSpacing;
            rect.pivot = referenceRect.pivot;
            rect.sizeDelta = referenceRect.sizeDelta;
            rect.anchoredPosition = referenceRect.anchoredPosition;
            Object.DestroyImmediate(reference.gameObject);

            if (measured && UiBuildKit.InkBounds(label, out Rect ink, out _))
            {
                rect.anchoredPosition += new Vector2(referenceCentreX - (rect.anchoredPosition.x + ink.center.x), 0f);
            }
        }
    }
}
