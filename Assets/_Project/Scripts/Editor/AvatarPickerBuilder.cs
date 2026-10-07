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
    /// Builds an <see cref="AvatarPicker"/> from the UI kit, one choice per preset in the avatar
    /// library, in one of two looks:
    /// <list type="bullet">
    /// <item>plain (ADR 041, the Profile screen's picker): the kit's crimson "selected" disc behind
    /// the chosen one, the white disc every avatar sits on, the portrait, and the crimson check
    /// badge on the chosen one, in one row;</item>
    /// <item>framed (ADR 043, the name screen at the start, as its mockup draws it): each portrait in
    /// its own gold ring, the chosen one hugged by a thin crimson ring and wearing the gold check
    /// badge, in rows of a given length, a shorter last row centred.</item>
    /// </list>
    /// </summary>
    public static class AvatarPickerBuilder
    {
        public const string LibraryPath = "Assets/_Project/Config/UI/AvatarLibrary.asset";

        /// <summary>The framed portrait's share of a choice: it ends where the crimson ring's inner edge begins.</summary>
        public const float FramedPictureShare = 0.94f;

        /// <summary>The gold check badge's share of a framed choice, and where its centre sits (from the bottom left).</summary>
        public const float FramedCheckShare = 0.22f;

        public static readonly Vector2 FramedCheckAnchor = new Vector2(0.82f, 0.16f);

        private static readonly Look Plain = new Look
        {
            Selected = "UI/Buttons/UI_ButtonDisc_Selected.png",
            Disc = "UI/Avatars/UI_AvatarRing.png",
            Check = "UI/Controls/UI_Check_Badge.png",
            DiscShare = 0.84f,
            PictureShare = 0.78f,
            CheckShare = 0.32f,
            CheckAnchor = new Vector2(0.84f, 0.16f),
            Framed = false,
        };

        private static readonly Look Framed = new Look
        {
            Selected = "UI/Avatars/UI_AvatarRing_Chosen.png",
            Disc = null,
            Check = "UI/Controls/UI_Check_Badge_Gold.png",
            DiscShare = 0f,
            PictureShare = FramedPictureShare,
            CheckShare = FramedCheckShare,
            CheckAnchor = FramedCheckAnchor,
            Framed = true,
        };

        /// <summary>
        /// The plain look in one row of <paramref name="cell"/>-wide choices, <paramref name="gap"/>
        /// apart, under <paramref name="parent"/>; its pivot is its top centre, for the caller to place.
        /// </summary>
        public static AvatarPicker Build(Transform parent, string name, float cell, float gap)
        {
            return Build(parent, name, cell, gap, 0f, int.MaxValue, Plain);
        }

        /// <summary>
        /// The framed look in rows of <paramref name="columns"/> choices (<paramref name="gap"/> apart,
        /// rows <paramref name="rowGap"/> apart), a shorter last row centred under the others; its
        /// pivot is its top centre.
        /// </summary>
        public static AvatarPicker BuildFramedGrid(Transform parent, string name, float cell, float gap, float rowGap, int columns)
        {
            return Build(parent, name, cell, gap, rowGap, columns, Framed);
        }

        /// <summary>
        /// One framed portrait shown as chosen, for good: the crimson ring, the portrait in its gold
        /// ring and the gold check badge, <paramref name="size"/> wide, centred on its parent's
        /// <paramref name="centre"/> (canvas units from the parent's centre). Returns the portrait,
        /// whose picture the view changes.
        /// </summary>
        public static Image BuildChosenFrame(Transform parent, string name, float size, Vector2 centre)
        {
            var library = AssetDatabase.LoadAssetAtPath<AvatarLibrary>(LibraryPath);
            RectTransform root = Child(name, parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(size, size);
            root.anchoredPosition = centre;
            Centred("Ring", root, KitSprite(Framed.Selected), size);
            Image picture = Centred("Picture", root, library != null ? library.Framed(0) : null, size * FramedPictureShare);
            picture.preserveAspect = true;
            Badge(root, Framed, size);
            return picture;
        }

        private static AvatarPicker Build(Transform parent, string name, float cell, float gap, float rowGap, int columns, Look look)
        {
            var library = AssetDatabase.LoadAssetAtPath<AvatarLibrary>(LibraryPath);
            int count = library != null ? library.Count : 0;
            if (count == 0)
            {
                Debug.LogError($"[AvatarPickerBuilder] No avatars in {LibraryPath}");
            }

            Sprite selectedSprite = KitSprite(look.Selected);
            Sprite discSprite = look.Disc != null ? KitSprite(look.Disc) : null;

            int perRow = Mathf.Max(1, Mathf.Min(columns, count));
            int rows = count == 0 ? 0 : (count + perRow - 1) / perRow;
            float width = perRow * cell + (perRow - 1) * gap;

            RectTransform root = Child(name, parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(width, rows * cell + Mathf.Max(0, rows - 1) * rowGap);

            var choices = new Button[count];
            var pictures = new Image[count];
            var selected = new GameObject[count];
            var checks = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                int row = i / perRow, column = i % perRow;
                int inRow = Mathf.Min(perRow, count - row * perRow);
                float rowStart = (width - (inRow * cell + (inRow - 1) * gap)) / 2f;

                RectTransform choice = Child($"Avatar{i}", root);
                choice.anchorMin = choice.anchorMax = new Vector2(0f, 1f);
                choice.pivot = new Vector2(0.5f, 0.5f);
                choice.sizeDelta = new Vector2(cell, cell);
                choice.anchoredPosition = new Vector2(rowStart + cell / 2f + column * (cell + gap), -(cell / 2f + row * (cell + rowGap)));

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

                Image chosen = Centred("Selected", choice, selectedSprite, cell);
                selected[i] = chosen.gameObject;
                if (discSprite != null)
                {
                    Centred("Disc", choice, discSprite, cell * look.DiscShare);
                }

                Sprite portrait = library == null ? null : look.Framed ? library.Framed(i) : library.Get(i);
                Image picture = Centred("Picture", choice, portrait, cell * look.PictureShare);
                picture.preserveAspect = true;
                pictures[i] = picture;
                checks[i] = Badge(choice, look, cell);

                // Nothing is chosen until the presenter says what is.
                selected[i].SetActive(false);
                checks[i].SetActive(false);
            }

            var picker = root.gameObject.AddComponent<AvatarPicker>();
            var so = new SerializedObject(picker);
            Fill(so.FindProperty("_choices"), choices);
            Fill(so.FindProperty("_pictures"), pictures);
            Fill(so.FindProperty("_selected"), selected);
            Fill(so.FindProperty("_checks"), checks);
            so.ApplyModifiedPropertiesWithoutUndo();
            return picker;
        }

        private static GameObject Badge(RectTransform owner, Look look, float size)
        {
            RectTransform badge = Child("Check", owner);
            badge.anchorMin = badge.anchorMax = look.CheckAnchor;
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.sizeDelta = new Vector2(size * look.CheckShare, size * look.CheckShare);
            badge.anchoredPosition = Vector2.zero;
            UiBuildKit.AddImage(badge, KitSprite(look.Check), false).preserveAspect = true;
            return badge.gameObject;
        }

        private static Sprite KitSprite(string relativePath) => UiBuildKit.LoadSpriteAt($"{UiBuildKit.ArtRoot}/{relativePath}", 0f);

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

        /// <summary>The sprites and proportions of one picker look.</summary>
        private sealed class Look
        {
            public string Selected;
            public string Disc;
            public string Check;
            public float DiscShare;
            public float PictureShare;
            public float CheckShare;
            public Vector2 CheckAnchor;
            public bool Framed;
        }
    }
}
