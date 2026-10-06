using Callbreak.Application.Ports;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor
{
    /// <summary>
    /// Builds an <see cref="AvatarPicker"/> row (ADR 041) from the UI kit, one choice per preset in
    /// the avatar library: the kit's crimson "selected" disc (shown behind the chosen one), the white
    /// disc every avatar sits on, the portrait, and the check badge (shown on the chosen one). The
    /// name question at the start and the Profile screen's picker use it.
    /// </summary>
    public static class AvatarPickerBuilder
    {
        public const string LibraryPath = "Assets/_Project/Config/UI/AvatarLibrary.asset";

        // Shares of a choice's size: the white disc, the portrait on it, and the check badge.
        private const float DiscShare = 0.84f;
        private const float PictureShare = 0.78f;
        private const float CheckShare = 0.32f;

        // Where the check badge's centre sits in a choice, from its bottom left.
        private static readonly Vector2 CheckAnchor = new Vector2(0.84f, 0.16f);

        /// <summary>
        /// A row of <paramref name="cell"/>-wide choices, <paramref name="gap"/> apart, under
        /// <paramref name="parent"/>; its pivot is its top centre, for the caller to place.
        /// </summary>
        public static AvatarPicker Build(Transform parent, string name, float cell, float gap)
        {
            var library = AssetDatabase.LoadAssetAtPath<AvatarLibrary>(LibraryPath);
            int count = library != null ? library.Count : 0;
            if (count == 0)
            {
                Debug.LogError($"[AvatarPickerBuilder] No avatars in {LibraryPath}");
            }

            Sprite selectedDisc = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Buttons/UI_ButtonDisc_Selected.png", 0f);
            Sprite whiteDisc = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Avatars/UI_AvatarRing.png", 0f);
            Sprite check = UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/UI/Controls/UI_Check_Badge.png", 0f);

            RectTransform row = Child(name, parent);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(count * cell + Mathf.Max(0, count - 1) * gap, cell);

            var choices = new Button[count];
            var pictures = new Image[count];
            var selected = new GameObject[count];
            var checks = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                RectTransform choice = Child($"Avatar{i}", row);
                choice.anchorMin = choice.anchorMax = new Vector2(0f, 1f);
                choice.pivot = new Vector2(0.5f, 0.5f);
                choice.sizeDelta = new Vector2(cell, cell);
                choice.anchoredPosition = new Vector2(cell / 2f + i * (cell + gap), -cell / 2f);

                // The whole square takes the tap; the hit area itself draws nothing.
                Image hit = UiBuildKit.AddImage(choice, null, true);
                hit.color = new Color(1f, 1f, 1f, 0f);
                var button = choice.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
                choices[i] = button;

                // Pressed like every button of the game, with the tap's sound.
                var feedback = choice.gameObject.AddComponent<ButtonFeedback>();
                var feedbackSo = new SerializedObject(feedback);
                feedbackSo.FindProperty("_selectable").objectReferenceValue = button;
                feedbackSo.FindProperty("_cue").intValue = (int)SoundCue.UiTap;
                feedbackSo.ApplyModifiedPropertiesWithoutUndo();

                Image chosen = Centred("Selected", choice, selectedDisc, cell);
                selected[i] = chosen.gameObject;
                Centred("Disc", choice, whiteDisc, cell * DiscShare);
                Image picture = Centred("Picture", choice, library.Get(i), cell * PictureShare);
                picture.preserveAspect = true;
                pictures[i] = picture;

                RectTransform badge = Child("Check", choice);
                badge.anchorMin = badge.anchorMax = CheckAnchor;
                badge.pivot = new Vector2(0.5f, 0.5f);
                badge.sizeDelta = new Vector2(cell * CheckShare, cell * CheckShare);
                badge.anchoredPosition = Vector2.zero;
                UiBuildKit.AddImage(badge, check, false).preserveAspect = true;
                checks[i] = badge.gameObject;

                // Nothing is chosen until the presenter says what is.
                selected[i].SetActive(false);
                checks[i].SetActive(false);
            }

            var picker = row.gameObject.AddComponent<AvatarPicker>();
            var so = new SerializedObject(picker);
            Fill(so.FindProperty("_choices"), choices);
            Fill(so.FindProperty("_pictures"), pictures);
            Fill(so.FindProperty("_selected"), selected);
            Fill(so.FindProperty("_checks"), checks);
            so.ApplyModifiedPropertiesWithoutUndo();
            return picker;
        }

        private static Image Centred(string name, RectTransform parent, Sprite sprite, float size)
        {
            RectTransform rect = Child(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            return UiBuildKit.AddImage(rect, sprite, false);
        }

        private static void Fill(SerializedProperty array, Object[] items)
        {
            array.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
