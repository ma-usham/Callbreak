using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The messages as V2 draws them (Figma "V2 · Screens": Dialogs, Messages &amp; Errors, Overlay ·
    /// Confirm Dialog and Overlay · Toast; Tools/ArtPipeline/V2/pieces.py): the dialog's 800-wide
    /// panel with its divider and its buttons side by side, the dark toast pill, a refused name's
    /// field in its error look, the error lines in V2's crimson, and the results screens without
    /// the chevrons V2's crimson buttons do not have. The prefabs are the built ones; this assembly
    /// has no TextMeshPro reference, so labels are read through their component's type.
    /// </summary>
    public sealed class V2MessageTests
    {
        private const string ScreenFolder = "Assets/_Project/Prefabs/UI/Screens";
        private const string OverlayFolder = "Assets/_Project/Prefabs/UI/Overlays";

        // V2's black at 60%, as it looks blended in linear light (TableCustomizer.V2DimOpacity).
        private const float DimOpacity = 0.855f;

        private static readonly Color Ink = new Color32(0x17, 0x14, 0x0F, 0xFF);
        private static readonly Color Ivory = new Color32(0xFF, 0xFC, 0xF7, 0xFF);
        private static readonly Color Crimson = new Color32(0x8E, 0x0E, 0x19, 0xFF);
        private static readonly Color Brown = new Color32(0x6E, 0x4F, 0x37, 0xFF);
        private static readonly Color ErrorCrimson = new Color32(0xA3, 0x12, 0x1F, 0xFF);

        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
            {
                Object.DestroyImmediate(made);
            }

            _made.Clear();
        }

        [Test]
        public void TheDialog_IsV2s_Panel_Type_AndButtons()
        {
            GameObject dialog = Prefab(OverlayFolder, "Dialog");
            Color dim = Find(dialog, "Backdrop").GetComponent<Image>().color;
            Assert.AreEqual(DimOpacity, dim.a, 0.005f, "the dim under V2's overlays");

            var panel = (RectTransform)Find(dialog, "Panel");
            Assert.AreEqual(800f, panel.sizeDelta.x, 0.01f, "V2's 800-wide panel");
            AssertType(Label(Find(dialog, "Panel/Title")), "SourceSerif4-Bold", 44f, Crimson);
            AssertType(Label(Find(dialog, "Panel/Body")), "Inter-SemiBold", 28f, Brown);
            Assert.AreEqual("Deco_TitleDivider", Find(dialog, "Panel/Divider").GetComponent<Image>().sprite.name, "the ornament under the title");

            Transform confirm = Find(dialog, "Panel/ConfirmButton"), cancel = Find(dialog, "Panel/CancelButton");
            Assert.AreEqual("UI_ButtonPrimary_Normal", confirm.Find("Face").GetComponent<Image>().sprite.name, "the crimson primary pill");
            Assert.AreEqual(96f, ((RectTransform)confirm).sizeDelta.y, 0.01f);
            AssertType(Label(confirm.Find("Label")), "SourceSerif4-Bold", 34f, Ivory);
            Assert.AreEqual("UI_ButtonSecondary_Normal", cancel.Find("Face").GetComponent<Image>().sprite.name, "the ivory secondary pill");
            Assert.AreEqual(88f, ((RectTransform)cancel).sizeDelta.y, 0.01f);
            AssertType(Label(cancel.Find("Label")), "Inter-Bold", 28f, Ink);
        }

        [Test]
        public void TheDialog_StacksItsParts_AndSetsItsButtonsSideBySide()
        {
            GameObject dialog = Instance(OverlayFolder, "Dialog");
            DialogView view = dialog.GetComponent<DialogView>();
            SetText(view, "_title", "Leave match?");
            SetText(view, "_body", "A bot will take your seat and it counts as a loss.");
            SetText(view, "_confirmLabel", "Leave Match");
            SetText(view, "_cancelLabel", "Cancel");
            Layout(view, true, true);

            var panel = (RectTransform)Find(dialog, "Panel");
            float Top(string path) => -((RectTransform)Find(dialog, path)).anchoredPosition.y;
            float Bottom(string path) => Top(path) + ((RectTransform)Find(dialog, path)).sizeDelta.y;
            Assert.AreEqual(56f, Top("Panel/Title"), 0.01f, "V2's top margin");
            Assert.AreEqual(28f, Top("Panel/Divider") - Bottom("Panel/Title"), 0.01f, "the divider 28 under the title");
            Assert.AreEqual(28f, Top("Panel/Body") - Bottom("Panel/Divider"), 0.01f, "the body 28 under it");

            var confirm = (RectTransform)Find(dialog, "Panel/ConfirmButton");
            var cancel = (RectTransform)Find(dialog, "Panel/CancelButton");
            Assert.AreEqual(28f, Top("Panel/ConfirmButton") - Bottom("Panel/Body"), 0.01f, "the buttons 28 under the body");
            Assert.AreEqual(Top("Panel/ConfirmButton") + confirm.sizeDelta.y / 2f, Top("Panel/CancelButton") + cancel.sizeDelta.y / 2f, 0.01f, "on one line");
            Assert.AreEqual(panel.sizeDelta.y, Bottom("Panel/ConfirmButton") + 48f, 0.01f, "V2's bottom margin");

            float cancelRight = cancel.anchoredPosition.x + cancel.sizeDelta.x / 2f;
            float confirmLeft = confirm.anchoredPosition.x - confirm.sizeDelta.x / 2f;
            Assert.AreEqual(24f, confirmLeft - cancelRight, 0.01f, "Cancel left of the primary button, 24 apart");
            Assert.AreEqual(0f, (cancel.anchoredPosition.x - cancel.sizeDelta.x / 2f + confirm.anchoredPosition.x + confirm.sizeDelta.x / 2f) / 2f, 0.01f, "the row centred");
            Assert.GreaterOrEqual(confirm.sizeDelta.x, 280f, "the primary button's least width");
            Assert.GreaterOrEqual(cancel.sizeDelta.x, 200f, "the secondary one's");

            SetText(view, "_title", "Update required");
            SetText(view, "_confirmLabel", "Update");
            Layout(view, true, false);
            Assert.AreEqual(0f, confirm.anchoredPosition.x, 0.01f, "a lone button is centred");
        }

        [Test]
        public void TheToast_IsV2sDarkPill()
        {
            GameObject toast = Prefab(OverlayFolder, "Toast");
            Assert.AreEqual("UI_Toast", Find(toast, "Pill/Face").GetComponent<Image>().sprite.name);
            AssertType(Label(Find(toast, "Pill/Label")), "Inter-SemiBold", 28f, Ivory);
            var view = new SerializedObject(toast.GetComponent<ToastView>());
            Assert.AreEqual(74f, view.FindProperty("_minHeight").floatValue, 0.01f, "V2's 74-high pill");
            Assert.AreEqual(new Vector2(32f, 18f), view.FindProperty("_padding").vector2Value, "V2's margins round its words");

            Color middle = Pixel("Assets/_Project/Art/UI/Common/UI_Toast.png", 0.5f, 0.5f);
            AssertColour(Ink, middle, "V2's ink");
            Assert.AreEqual(0.975f, middle.a, 0.01f, "at V2's 90%, blended in linear light");
        }

        [Test]
        public void ARefusedName_ShowsTheFieldInV2sErrorLook()
        {
            GameObject prompt = Instance(ScreenFolder, "NamePrompt");
            var look = (FieldErrorLook)Get<Object>(prompt.GetComponent<NamePromptView>(), "_fieldLook");
            Assert.IsNotNull(look, "the name screen's field has its error look");
            Image face = look.transform.Find("Face").GetComponent<Image>();
            string normal = face.sprite.name;
            look.SetError(true);
            Assert.AreEqual("UI_Field_Error", face.sprite.name, "a refused name: the crimson rim");
            look.SetError(false);
            Assert.AreEqual(normal, face.sprite.name, "and back");

            var message = Prefab(ScreenFolder, "Profile").GetComponentInChildren<ProfileNameMessage>(true);
            Assert.IsNotNull(Get<Object>(message, "_fieldLook"), "the profile's field has it too");
        }

        [Test]
        public void TheErrorLines_AreV2sCrimson()
        {
            AssertColour(ErrorCrimson, Get<Color>(Prefab(ScreenFolder, "NamePrompt").GetComponent<NamePromptView>(), "_errorColor"), "the name screen's");
            AssertColour(ErrorCrimson, Get<Color>(Prefab(ScreenFolder, "Profile").GetComponentInChildren<ProfileNameMessage>(true), "_errorColor"), "the profile's");
            AssertType(Label(Find(Prefab(ScreenFolder, "JoinRoom"), "ErrorLabel")), "Inter-SemiBold", 28f, ErrorCrimson);
        }

        [Test]
        public void TheResultsButtons_HaveNoChevron_AsV2DrawsThem()
        {
            Assert.IsFalse(Find(Prefab(ScreenFolder, "RoundResults"), "NextChevron").gameObject.activeSelf, "Next Round");
            Assert.IsFalse(Find(Prefab(ScreenFolder, "MatchResults"), "PlayAgainChevron").gameObject.activeSelf, "Play Again");
            Assert.IsFalse(Find(Prefab(ScreenFolder, "Scorecard"), "PrimaryChevron").gameObject.activeSelf, "Back to Results");
            Assert.IsTrue(Find(Prefab(ScreenFolder, "MatchResults"), "MainMenuChevron").gameObject.activeSelf, "Main Menu keeps its chevron, as V2's secondary box does");
        }

        private static GameObject Prefab(string folder, string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.prefab");

        /// <summary>An instance under a 1920 x 1080 world-space canvas, so its text lays out.</summary>
        private GameObject Instance(string folder, string name)
        {
            var canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            _made.Add(canvas);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920f, 1080f);
            return (GameObject)PrefabUtility.InstantiatePrefab(Prefab(folder, name), canvas.transform);
        }

        /// <summary>The object at <paramref name="path"/> ("Parent/Child", from any depth) under the root.</summary>
        private static Transform Find(GameObject root, string path)
        {
            string[] parts = path.Split('/');
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != parts[0])
                {
                    continue;
                }

                Transform found = t;
                for (int i = 1; i < parts.Length && found != null; i++)
                {
                    found = found.Find(parts[i]);
                }

                if (found != null)
                {
                    return found;
                }
            }

            Assert.Fail($"{root.name} has no {path}");
            return null;
        }

        private static Component Label(Transform t)
        {
            Component label = t.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "TextMeshProUGUI");
            Assert.IsNotNull(label, $"{t.name} is a label");
            return label;
        }

        private static void SetText(DialogView view, string field, string text)
        {
            var label = Get<Component>(view, field);
            label.GetType().GetProperty("text").SetValue(label, text);
        }

        /// <summary>The dialog's own layout, without the fade that showing it starts.</summary>
        private static void Layout(DialogView view, bool hasBody, bool hasCancel) =>
            typeof(DialogView).GetMethod("Layout", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { hasBody, hasCancel });

        private static T Get<T>(object owner, string field) =>
            (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);

        private static void AssertType(Component label, string font, float size, Color colour)
        {
            string name = label.transform.parent.name + "/" + label.name;
            StringAssert.StartsWith(font, ((Object)label.GetType().GetProperty("font").GetValue(label)).name, name);
            Assert.AreEqual(size, (float)label.GetType().GetProperty("fontSize").GetValue(label), 0.01f, name);
            AssertColour(colour, (Color)label.GetType().GetProperty("color").GetValue(label), name);
        }

        private static void AssertColour(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f, message);
            Assert.AreEqual(expected.g, actual.g, 0.01f, message);
            Assert.AreEqual(expected.b, actual.b, 0.01f, message);
        }

        private static Color Pixel(string path, float u, float v)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)), path);
                return texture.GetPixel((int)(texture.width * u), (int)(texture.height * v));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
