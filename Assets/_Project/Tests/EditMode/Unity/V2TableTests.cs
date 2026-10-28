using System;
using System.IO;
using System.Linq;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The table's pieces as V2 draws them (Figma "V2 · Screens": 08 Gameplay, its table states, 07
    /// Bidding, 13 Match Menu and Overlay · Reconnecting; Tools/ArtPipeline/V2/pieces.py): the prompt
    /// is the dark strip with ivory words and suits, a call shows in the crimson tag pill, the emote
    /// picker has V2's size and pill buttons, "Reconnecting…" is a panel with V2's spinner, and the
    /// call panel and the match menu dim the table. V2's translucent darks are set for the game's
    /// linear-light blending (TableCustomizer.V2DimOpacity). The prefabs are the built ones; this
    /// assembly has no TextMeshPro reference, so labels are read through their component's type.
    /// </summary>
    public sealed class V2TableTests
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/UI/Screens";

        // V2's black at 60% and #17140F at 80%, as they look over the table blended in linear light.
        private const float DimOpacity = 0.855f;
        private const float StripOpacity = 0.937f;

        private static readonly Color Ink = new Color32(0x17, 0x14, 0x0F, 0xFF);
        private static readonly Color Ivory = new Color32(0xFF, 0xFC, 0xF7, 0xFF);
        private static readonly Color Gold = new Color32(0xF3, 0xDF, 0xA8, 0xFF);
        private static readonly Color Crimson = new Color32(0x8E, 0x0E, 0x19, 0xFF);

        [Test]
        public void ThePrompt_IsV2sDarkStrip_WithIvoryWordsAndSuits()
        {
            Transform pill = Find(Prefab("Table"), "Prompt/Pill");
            var face = pill.GetComponent<Image>();
            Assert.AreEqual("UI_PromptStrip", face.sprite.name);
            AssertColour(Ink, face.color, "the strip is V2's ink");
            Assert.AreEqual(StripOpacity, face.color.a, 0.005f, "at V2's 80%, blended in linear light");
            Assert.AreEqual(62f, ((RectTransform)pill).sizeDelta.y, 0.01f, "62 high");
            Assert.AreEqual("UI_PromptStrip_Rim", pill.Find("Rim").GetComponent<Image>().sprite.name, "with its gold rim over it");

            Component words = Label(pill.Find("Text"));
            AssertType(words, "Inter-SemiBold", 28f, Ivory);
            Assert.AreEqual("SuitSprites_Light", ((Object)Get<object>(words, "spriteAsset")).name, "suits in ivory too");

            var prompt = new SerializedObject(Prefab("Table").GetComponentInChildren<TablePromptView>(true));
            AssertColour(face.color, prompt.FindProperty("_pillColor").colorValue, "the prompt keeps the strip's colour");
            Color refuse = prompt.FindProperty("_refuseColor").colorValue;
            Assert.Greater(refuse.r, 0.5f, "and flashes crimson for a card that cannot be played");
            Assert.Less(refuse.g, 0.2f);
        }

        [Test]
        public void TheIvorySuits_AreIvory()
        {
            var sheet = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(sheet.LoadImage(File.ReadAllBytes("Assets/_Project/Art/UI/Table/UI_SuitSprites_Light.png")));
                Color32[] ink = sheet.GetPixels32().Where(p => p.a > 200).ToArray();
                Assert.Greater(ink.Length, 1000, "the four suits are drawn");
                Assert.IsTrue(ink.All(p => p.r > 240 && p.g > 240 && p.b > 235), "every suit in ivory");
            }
            finally
            {
                Object.DestroyImmediate(sheet);
            }
        }

        [Test]
        public void ACall_ShowsInV2sTagPill()
        {
            for (int seat = 0; seat < 4; seat++)
            {
                Transform bubble = Find(Prefab("Table"), $"Seat{seat}/Bubble");
                Assert.AreEqual("UI_Pill_Tag", bubble.GetComponent<Image>().sprite.name, $"seat {seat}: the crimson tag pill");
                Assert.AreEqual(42f, ((RectTransform)bubble).sizeDelta.y, 0.01f, $"seat {seat}: 42 high");
                Component words = Label(bubble.Find("Text"));
                AssertType(words, "Inter-SemiBold", 20f, Gold);
                Assert.AreEqual(22f, Get<float>(words, "characterSpacing"), 0.01f, $"seat {seat}: letter-spaced as V2's tag");
            }
        }

        [Test]
        public void TheEmotePicker_HasV2sSize_AndPillButtons()
        {
            var card = (RectTransform)Find(Prefab("Table"), "EmotePicker/Panel/Card");
            Assert.AreEqual(new Vector2(556f, 296f), card.sizeDelta);
            for (int i = 0; i < 6; i++)
            {
                var phrase = (RectTransform)card.Find($"Emote{i}");
                Assert.AreEqual(new Vector2(250f, 76f), phrase.sizeDelta, $"phrase {i}");
                AssertType(Label(phrase.Find("Text")), "Inter-Bold", 28f, Ink);
            }
        }

        [Test]
        public void Reconnecting_IsV2sPanel_WithItsSpinner()
        {
            Transform cover = Find(Prefab("Table"), "Reconnecting");
            Color dim = cover.GetComponent<Image>().color;
            Assert.AreEqual(DimOpacity, dim.a, 0.005f, "the table dimmed as V2 dims it");
            Assert.AreEqual(0f, dim.r + dim.g + dim.b, 0.001f, "in black");

            var panel = (RectTransform)cover.Find("Panel");
            Assert.AreEqual("UI_Panel_Modal_Menu", panel.GetComponent<Image>().sprite.name, "V2's plain panel");
            Assert.AreEqual(new Vector2(520f, 280f), panel.sizeDelta);

            var ring = (RectTransform)cover.Find("Ring");
            Assert.AreEqual("UI_Spinner", ring.GetComponent<Image>().sprite.name, "V2's spinner");
            Assert.AreEqual(new Vector2(64f, 64f), ring.sizeDelta);
            Assert.IsNotNull(ring.GetComponent<Callbreak.Presentation.Widgets.Spinner>(), "and it turns");
            Assert.Greater(ring.GetSiblingIndex(), panel.GetSiblingIndex(), "on the panel");

            AssertType(Label(cover.Find("Text")), "SourceSerif4-Bold", 44f, Crimson);
            Assert.IsNull(cover.Find("Spade"), "V2 has no spade in it");
        }

        [Test]
        public void TheCallPanel_DimsTheTable_AndGoesWithIt()
        {
            Transform panel = Find(Prefab("BiddingPanel"), "Panel");
            Transform dim = panel.GetChild(0);
            Assert.AreEqual("Dim", dim.name, "the dim is the panel's first part, under its shadow and face");
            var image = dim.GetComponent<Image>();
            Assert.AreEqual(DimOpacity, image.color.a, 0.005f);
            Assert.IsFalse(image.raycastTarget, "it takes no touches: the HUD answers as before");
            Vector2 size = ((RectTransform)dim).sizeDelta;
            Assert.GreaterOrEqual(size.x, 2560f + 48f, "it covers the backdrop's frame as the panel slides");
            Assert.GreaterOrEqual(size.y, 1440f + 48f);
        }

        [Test]
        public void TheMatchMenu_DimsTheTableAsV2Does()
        {
            Assert.AreEqual(DimOpacity, Find(Prefab("MatchMenu"), "Dim").GetComponent<Image>().color.a, 0.005f);
        }

        [Test]
        public void TheModeTag_HugsItsWords()
        {
            Transform pill = Find(Prefab("MatchMenu"), "ModePill");
            Component words = Label(pill.Find("ModeLabel"));
            var preferred = (Vector2)words.GetType().GetMethod("GetPreferredValues", new[] { typeof(string) }).Invoke(words, new object[] { Get<string>(words, "text") });
            Assert.Greater(((RectTransform)pill).sizeDelta.x, preferred.x, "its gold words stay on the crimson");
        }

        [Test]
        public void TheTurnRing_WarnsInRed()
        {
            // V2's ring runs from gold at the top to crimson at the foot; the warning multiplies it.
            SeatView seat = Prefab("Table").GetComponentInChildren<SeatView>(true);
            Color warning = new SerializedObject(seat).FindProperty("_ringWarningColor").colorValue;
            Color top = Gold * warning;
            Assert.Greater(top.r, 2f * top.g, "the gold top turns red, not purple");
            Assert.Greater(top.r, 2f * top.b);
        }

        [Test]
        public void TheNamePlate_IsV2sDarkPlate()
        {
            var plate = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(plate.LoadImage(File.ReadAllBytes("Assets/_Project/Art/UI/Table/UI_NamePlate.png")));
                Color middle = plate.GetPixel(plate.width / 2, plate.height / 2);
                AssertColour(Ink, middle, "V2's ink");
                Assert.AreEqual(StripOpacity, middle.a, 0.01f, "at V2's 80%, blended in linear light");
            }
            finally
            {
                Object.DestroyImmediate(plate);
            }
        }

        private static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");

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

        private static void AssertType(Component label, string font, float size, Color colour)
        {
            string name = label.transform.parent.name + "/" + label.name;
            StringAssert.StartsWith(font, ((Object)Get<object>(label, "font")).name, name);
            Assert.AreEqual(size, Get<float>(label, "fontSize"), 0.01f, name);
            AssertColour(colour, Get<Color>(label, "color"), name);
        }

        private static void AssertColour(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f, message);
            Assert.AreEqual(expected.g, actual.g, 0.01f, message);
            Assert.AreEqual(expected.b, actual.b, 0.01f, message);
        }

        private static T Get<T>(Component label, string property) => (T)label.GetType().GetProperty(property).GetValue(label);
    }
}
