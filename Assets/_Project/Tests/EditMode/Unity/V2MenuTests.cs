using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The menu screens, the table, the call panel and the match menu in V2's type (Figma "V2 ·
    /// Screens", Tools/ArtPipeline/V2/screens.py): every layout label that has V2's size is built at
    /// that size, in its font and colour, and its words stand where V2 draws them; the main menu
    /// (built in code) too, against V2's own measures
    /// (screens-v2.json). How to Play shows the deck's ace over its king, and the name screen rings
    /// the chosen portrait in crimson with the check at its top right. The prefabs are the built
    /// ones, so these live outside the folders the dotnet harness compiles. This assembly has no
    /// TextMeshPro reference: labels are read through their component's type.
    /// </summary>
    public sealed class V2MenuTests
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/UI/Screens";
        private const float ScaleX = 1920f / 1672f;
        private const float ScaleY = 1080f / 941f;

        // The phase 3a, 3b and 3c screens whose prefab is built from the layout of the same name. The
        // shop is three layouts in one prefab, its tiles placed by ShopCustomizer, so it is checked by size only.
        private static readonly string[] Screens =
        {
            "PlayWithFriends", "CreateRoom", "JoinRoom", "PrivateLobby", "Matchmaking", "OfflineSetup", "Settings",
            "HowToPlay", "Profile", "SceneLoading", "NamePrompt", "EarnCoins", "PlayOnline", "DailyRewards",
            "Table", "BiddingPanel", "MatchMenu", "RoundResults", "MatchResults", "Scorecard",
        };

        // Labels a customizer sets in a common style after the build (ScreenExtras.UnifyStyle: V2's
        // prize lines differ in size from row to row), so their own size and place are not V2's.
        private static readonly HashSet<string> Unified = new HashSet<string>
        {
            "PlayOnline/Place0", "PlayOnline/Place1", "PlayOnline/Place2", "PlayOnline/Place3",
            "PlayOnline/PrizeAmount0", "PlayOnline/PrizeAmount1", "PlayOnline/PrizeAmount2", "PlayOnline/PrizeAmount3",
        };

        // The results tables, whose cells ("Row...") their customizers make regular: one size per
        // kind of cell, on common columns and lines (ScreenExtras.UnifyStyle, AlignColumn, AlignRow).
        // V2 draws a few at other sizes (a 28 among the 32s).
        private static readonly HashSet<string> UnifiedTables = new HashSet<string> { "RoundResults", "MatchResults", "Scorecard" };

        // Labels a customizer moves after the build, as their views do at runtime: the lobby's seat
        // lines share one baseline (PrivateLobbyCustomizer), the reset line is centred with its
        // clock (EarnCoinsView.CentreClockLine), and the table's seats stand where the owner put them
        // (TableCustomizer, ADR 027), their words centred on their plates and pills.
        private static readonly HashSet<string> Moved = new HashSet<string>
        {
            "PrivateLobby/SeatStatus0", "PrivateLobby/SeatStatus1", "PrivateLobby/SeatStatus2", "PrivateLobby/SeatStatus3",
            "EarnCoins/ResetLabel",
            "Table/SeatName0", "Table/SeatName1", "Table/SeatName2", "Table/SeatName3",
            "Table/SeatStatus0", "Table/SeatStatus1", "Table/SeatStatus2", "Table/SeatStatus3", "Table/SeatTimerText0",
        };

        // Labels whose words show in another object: the name field is empty until typed, and its
        // placeholder shows on the same line.
        private static readonly Dictionary<string, string> ShownBy = new Dictionary<string, string>
        {
            { "NamePrompt/FieldText", "TextArea/Placeholder" },
        };

        // V2's main menu labels (screens-v2.json, "00 Main Menu"), the prefab's objects that show them
        // and the edge each is aligned to (MainMenuBuilder).
        private static readonly (string V2, string Path, string Align)[] MainMenuLabels =
        {
            ("Greeting", "Greeting", "left"),
            ("PlayerName", "PlayerName", "left"),
            ("Tagline", "Tagline", null),
            ("PlayOnlineButton/Label", "PlayOnlineButton/Label", "left"),
            ("PlayWithFriendsButton/Label", "PlayWithFriendsButton/Label", "left"),
            ("PlayOfflineButton/Label", "PlayOfflineButton/Label", "left"),
            ("Wallet/Amount", "Balance", "left"),
        };

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
        public void TheMenuLabels_AreDrawnInV2sType()
        {
            var wrong = new List<string>();
            int checkedLabels = 0;
            foreach (string screen in Screens.Concat(new[] { "Shop" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{screen}.prefab");
                foreach (Node node in V2Labels(screen))
                {
                    Component label = FindLabel(prefab.transform, node.id);
                    if (label == null || IsUnified(screen, node.id))
                    {
                        continue;
                    }

                    checkedLabels++;
                    bool autoSize = Get<bool>(label, "enableAutoSizing");
                    float size = autoSize ? Get<float>(label, "fontSizeMax") : Get<float>(label, "fontSize");
                    if (Mathf.Abs(size - node.size) > 0.01f)
                    {
                        wrong.Add($"{screen}/{node.id}: size {size}, V2 {node.size}");
                    }

                    string font = ((Object)Get<object>(label, "font")).name;
                    if (!font.StartsWith(FontFile(node.font), StringComparison.Ordinal))
                    {
                        wrong.Add($"{screen}/{node.id}: font {font}, V2 {node.font}");
                    }

                    // The shop drawer's category labels come in an active and an inactive look; V2 draws one of them.
                    ColorUtility.TryParseHtmlString(node.color, out Color colour);
                    Color drawn = Get<Color>(label, "color");
                    bool otherColour = Mathf.Abs(drawn.r - colour.r) > 0.01f || Mathf.Abs(drawn.g - colour.g) > 0.01f || Mathf.Abs(drawn.b - colour.b) > 0.01f;
                    if (otherColour && !node.id.StartsWith("CategoryLabel", StringComparison.Ordinal))
                    {
                        wrong.Add($"{screen}/{node.id}: colour #{ColorUtility.ToHtmlStringRGB(drawn)}, V2 {node.color}");
                    }
                }
            }

            Assert.Greater(checkedLabels, 200, "the menus' V2 labels were found");
            Assert.That(wrong, Is.Empty);
        }

        [Test]
        public void TheMenuLabels_StandWhereV2DrawsThem()
        {
            var wrong = new List<string>();
            int checkedLabels = 0;
            foreach (string screen in Screens)
            {
                RectTransform canvas = Canvas();
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{screen}.prefab"), canvas);
                Stretch(instance.transform);
                foreach (Node node in V2Labels(screen).Where(n => string.IsNullOrEmpty(n.measure)))
                {
                    string id = screen + "/" + node.id;
                    Component label = FindLabel(instance.transform, ShownBy.TryGetValue(id, out string shown) ? shown : node.id);
                    if (label == null || IsUnified(screen, node.id) || Moved.Contains(id))
                    {
                        continue;
                    }

                    checkedLabels++;
                    Vector2 drawn = KeptPoint(Ink(canvas, label), node.align);
                    Vector2 v2 = KeptPoint(Rect.MinMaxRect(node.inkBox[0] * ScaleX, node.inkBox[1] * ScaleY, node.inkBox[2] * ScaleX, node.inkBox[3] * ScaleY), node.align);
                    if (Vector2.Distance(drawn, v2) > 2f)
                    {
                        wrong.Add($"{screen}/{node.id}: words at {drawn}, V2 {v2} ({node.align ?? "centre"})");
                    }
                }
            }

            Assert.Greater(checkedLabels, 150, "the menus' V2 labels were found");
            Assert.That(wrong, Is.Empty);
        }

        [Test]
        public void TheMainMenusLabels_StandWhereV2DrawsThem()
        {
            Dictionary<string, Rect> ink = V2Ink("00 Main Menu");
            RectTransform canvas = Canvas();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/MainMenu.prefab"), canvas);
            Stretch(instance.transform);
            foreach ((string v2, string path, string align) in MainMenuLabels)
            {
                Component label = FindLabel(instance.transform, path);
                Assert.IsNotNull(label, path);
                Assert.That(Vector2.Distance(KeptPoint(Ink(canvas, label), align), KeptPoint(ink[v2], align)), Is.LessThan(2f), path);
            }
        }

        [Test]
        public void HowToPlay_ShowsTheDecksAceOverItsKing()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/HowToPlay.prefab");
            Transform cards = prefab.GetComponentsInChildren<Transform>(true).First(t => t.name == "CardsArt");
            Transform king = cards.Find("KingCard"), ace = cards.Find("AceCard");
            Assert.IsNotNull(king, "the king");
            Assert.IsNotNull(ace, "the ace");
            Assert.Greater(ace.GetSiblingIndex(), king.GetSiblingIndex(), "the ace lies over the king");
            Assert.AreEqual("Card_Hearts_K", king.Find("Face").GetComponent<Image>().sprite.name);
            Assert.AreEqual("Card_Spades_A", ace.Find("Face").GetComponent<Image>().sprite.name);
            Assert.IsTrue(king.Find("Base").GetComponent<Image>().sprite.name.StartsWith("Base_", StringComparison.Ordinal), "the deck's base under the face");
            Assert.AreEqual(10f, king.localEulerAngles.z, 0.01f, "the king turned as V2 turns it");
            Assert.AreEqual(352f, ace.localEulerAngles.z, 0.01f, "the ace turned as V2 turns it");
            Assert.IsNull(cards.Find("Face"), "no old picture of the two cards");
        }

        [Test]
        public void TheNameScreen_RingsTheChosenPortraitInCrimson_WithTheCheckAtItsTopRight()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/NamePrompt.prefab");
            Transform avatars = prefab.GetComponentsInChildren<Transform>(true).First(t => t.name == "Avatars");
            Assert.AreEqual(6, avatars.childCount, "the six portraits");
            foreach (Transform choice in avatars)
            {
                Transform picture = choice.Find("Picture"), ring = choice.Find("Selected"), check = choice.Find("Check");
                Assert.Greater(ring.GetSiblingIndex(), picture.GetSiblingIndex(), choice.name + ": the crimson ring lies over the gold one");
                Assert.AreEqual(((RectTransform)picture).sizeDelta, ((RectTransform)ring).sizeDelta, choice.name + ": and is as large");
                Assert.AreEqual("UI_AvatarRing_Chosen", ring.GetComponent<Image>().sprite.name);
                Assert.AreEqual("UI_Check_Badge", check.GetComponent<Image>().sprite.name, choice.name + ": V2's crimson check");
                var anchor = ((RectTransform)check).anchorMin;
                Assert.Greater(anchor.x, 0.5f, choice.name + ": the check on the right");
                Assert.Greater(anchor.y, 0.5f, choice.name + ": at the top");
            }

            Transform portrait = prefab.GetComponentsInChildren<Transform>(true).First(t => t.name == "Portrait");
            Assert.AreEqual(1, portrait.childCount, "the big portrait is the framed picture alone, as V2 draws it");
        }

        private static bool IsUnified(string screen, string id) =>
            Unified.Contains(screen + "/" + id) || (UnifiedTables.Contains(screen) && id.StartsWith("Row", StringComparison.Ordinal));

        private static IEnumerable<Node> V2Labels(string screen)
        {
            Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText($"ArtSource/Screens/{screen}/layout.json"));
            return layout.nodes.Where(n => n.kind == "label" && n.size > 0f && !n.hidden);
        }

        private static Dictionary<string, Rect> V2Ink(string frame)
        {
            // screens-v2.json rows are [path, type, x, y, w, h, {..., "ink": [x, y, w, h]}].
            string json = File.ReadAllText("Tools/ArtPipeline/V2/screens-v2.json");
            int start = json.IndexOf("\"" + frame + "\"", StringComparison.Ordinal);
            int end = json.IndexOf("\n\"", start + 1, StringComparison.Ordinal);
            string rows = json.Substring(start, (end < 0 ? json.Length : end) - start);
            var ink = new Dictionary<string, Rect>();
            foreach (string row in rows.Split(new[] { "],[" }, StringSplitOptions.None))
            {
                int pathEnd = row.IndexOf("\",\"T\"", StringComparison.Ordinal);
                int inkAt = row.IndexOf("\"ink\":[", StringComparison.Ordinal);
                if (pathEnd < 0 || inkAt < 0)
                {
                    continue;
                }

                string path = row.Substring(row.LastIndexOf('"', pathEnd - 1) + 1, pathEnd - row.LastIndexOf('"', pathEnd - 1) - 1);
                string[] v = row.Substring(inkAt + 7, row.IndexOf(']', inkAt) - inkAt - 7).Split(',');
                float F(int i) => float.Parse(v[i], System.Globalization.CultureInfo.InvariantCulture);
                ink[path] = new Rect(F(0), F(1), F(2), F(3));
            }

            return ink;
        }

        /// <summary>A 1920 x 1080 world-space canvas, so a screen lays out exactly as on its design.</summary>
        private RectTransform Canvas()
        {
            var go = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            _made.Add(go);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(1920f, 1080f);
            return rect;
        }

        private static void Stretch(Transform root)
        {
            var rect = (RectTransform)root;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// The box round a label's words in design coordinates (canvas units from the top left): the
        /// union of its visible glyphs' own boxes (a character's quad holds padding round its glyph).
        /// </summary>
        private static Rect Ink(RectTransform canvas, Component label)
        {
            label.GetType().GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) }).Invoke(label, new object[] { true, true });
            object textInfo = Get<object>(label, "textInfo");
            int count = (int)Field(textInfo, "characterCount");
            var characters = (Array)Field(textInfo, "characterInfo");
            float horizontalScale = Get<float>(label, "characterHorizontalScale");
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                object c = characters.GetValue(i);
                object element = Field(c, "textElement");
                if (!(bool)Field(c, "isVisible") || element == null)
                {
                    continue;
                }

                object glyph = element.GetType().GetProperty("glyph").GetValue(element);
                var metrics = (UnityEngine.TextCore.GlyphMetrics)glyph.GetType().GetProperty("metrics").GetValue(glyph);
                float scale = (float)Field(c, "scale");
                var bottomLeft = (Vector3)Field(c, "bottomLeft");
                var topRight = (Vector3)Field(c, "topRight");
                float padX = Mathf.Max(0f, (topRight.x - bottomLeft.x - (metrics.width * scale * horizontalScale)) / 2f);
                float padY = Mathf.Max(0f, (topRight.y - bottomLeft.y - (metrics.height * scale)) / 2f);
                minX = Mathf.Min(minX, bottomLeft.x + padX);
                minY = Mathf.Min(minY, bottomLeft.y + padY);
                maxX = Mathf.Max(maxX, topRight.x - padX);
                maxY = Mathf.Max(maxY, topRight.y - padY);
            }

            Vector2 Design(float x, float y)
            {
                Vector3 local = canvas.InverseTransformPoint(label.transform.TransformPoint(new Vector3(x, y, 0f)));
                return new Vector2(local.x + 960f, 540f - local.y);
            }

            Vector2 bottomLeftInk = Design(minX, minY), topRightInk = Design(maxX, maxY);
            return Rect.MinMaxRect(bottomLeftInk.x, topRightInk.y, topRightInk.x, bottomLeftInk.y);
        }

        /// <summary>
        /// The point of a label's words that the builder places on V2's (UiBuildKit.PlaceLabel): the
        /// edge it is aligned to, or its middle, at half height. V2 draws its serif labels in a serif
        /// a little narrower than the game's Source Serif 4, so their far edges differ.
        /// </summary>
        private static Vector2 KeptPoint(Rect ink, string align) =>
            new Vector2(align == "left" ? ink.xMin : align == "right" ? ink.xMax : ink.center.x, ink.center.y);

        private static object Field(object owner, string name) => owner.GetType().GetField(name).GetValue(owner);

        /// <summary>The label named <paramref name="path"/> (a name, or "Parent/Name") under the root; null when it has none.</summary>
        private static Component FindLabel(Transform root, string path)
        {
            string[] parts = path.Split('/');
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != parts[parts.Length - 1] || (parts.Length > 1 && (t.parent == null || t.parent.name != parts[0])))
                {
                    continue;
                }

                foreach (Component c in t.GetComponents<Component>())
                {
                    if (c != null && c.GetType().GetProperty("textBounds") != null)
                    {
                        return c;
                    }
                }
            }

            return null;
        }

        private static T Get<T>(Component label, string property) => (T)label.GetType().GetProperty(property).GetValue(label);

        private static string FontFile(string role) => role switch
        {
            "SerifBold" => "SourceSerif4-Bold",
            "SerifSemiBold" => "SourceSerif4-SemiBold",
            "SansRegular" => "Inter-Regular",
            "SansMedium" => "Inter-Medium",
            "SansSemiBold" => "Inter-SemiBold",
            "SansBold" => "Inter-Bold",
            _ => "Inter-Medium",
        };

        [Serializable]
        private sealed class Layout
        {
            public Node[] nodes;
        }

        [Serializable]
        private sealed class Node
        {
            public string id;
            public string kind;
            public bool hidden;
            public string font;
            public string align;
            public string color;
            public float size;
            public string measure;
            public float[] inkBox;
        }
    }
}
