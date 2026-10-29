using System.Collections.Generic;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The backgrounds and tables the shop sells (the owner, 4 Oct 2026; mockups 20 and 21): the
    /// equipped table fills the screen at the table (V2, 28 Oct 2026), a cloth since ADR 058 (the
    /// dhaka cloths with their border round the screen), and the equipped background shows behind the
    /// menus (PlayMode's EquippedBackgroundTests). A table this build has no art for (an unknown id,
    /// one the game no longer has, or art not made yet) shows the default one instead and says so
    /// once; a background without art shows the default one. The library here has the game's three
    /// backgrounds (the owner, 5 Oct 2026) and its three cloths.
    /// These tests need the engine (sprites, the log), so they live outside the folders the dotnet
    /// harness compiles.
    /// </summary>
    public sealed class TableBackdropTests
    {
        private const string Vista = "himalayan-vista";
        private const string Pokhara = "pokhara-lakeside";
        private const string Monastery = "monastery-garden";
        private const string Green = "green-dhaka";
        private const string Indigo = "indigo-dhaka";
        private const string Dusk = "dusk-himalaya";

        private readonly List<Object> _made = new List<Object>();
        private readonly List<string> _warnings = new List<string>();
        private CardSpriteProvider _cards;
        private Sprite _vista;
        private Sprite _pokhara;
        private Sprite _green;
        private Sprite _indigo;
        private Sprite _border;

        [SetUp]
        public void SetUp()
        {
            _cards = Make<CardSpriteProvider>();
            _vista = MakeSprite("vista");
            _pokhara = MakeSprite("pokhara");
            _green = MakeSprite("green cloth");
            _indigo = MakeSprite("indigo cloth");
            _border = MakeSprite("dhaka border");
            UnityEngine.Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Application.logMessageReceived -= OnLog;
            foreach (Object made in _made)
            {
                Object.DestroyImmediate(made);
            }

            _made.Clear();
            _warnings.Clear();
        }

        [Test]
        public void TheEquippedTable_ShowsItsOwnPicture()
        {
            Assert.AreSame(_indigo, Skin(Library()).Table(Indigo));
            Assert.IsEmpty(_warnings);
        }

        [Test]
        public void TheDefaults_AreTheVistaAndTheGreenDhaka()
        {
            TableBackdropLibrary library = Library();

            Assert.AreEqual(Vista, library.DefaultBackgroundId);
            Assert.AreEqual(Green, library.DefaultTableId);
            Assert.AreSame(_vista, library.BackgroundPicture(Vista, out string background));
            Assert.AreEqual(Vista, background);
            Assert.AreSame(_green, library.TablePicture(Green, out string table));
            Assert.AreEqual(Green, table);
        }

        [Test]
        public void AnUnknownTable_ShowsTheDefaultTable_AndSaysSoOnce()
        {
            TableSkin skin = Skin(Library());

            Sprite first = skin.Table("glass-top");
            Sprite second = skin.Table("glass-top");

            Assert.AreSame(_green, first);
            Assert.AreSame(_green, second);
            Assert.AreEqual(1, _warnings.Count, string.Join(" | ", _warnings));
            StringAssert.Contains("\"glass-top\"", _warnings[0]);
        }

        [Test]
        public void ATableWithoutArtYet_ShowsTheDefaultTable_AndSaysSoOnce()
        {
            TableSkin skin = Skin(Library());

            Sprite shown = skin.Table(Dusk);
            skin.Table(Dusk);

            Assert.AreSame(_green, shown);
            Assert.AreEqual(1, _warnings.Count, string.Join(" | ", _warnings));
            StringAssert.Contains(Dusk, _warnings[0]);
        }

        [Test]
        public void NoTableEquipped_ShowsTheDefault_AndSaysSo()
        {
            Assert.AreSame(_green, Skin(Library()).Table(string.Empty));
            Assert.AreEqual(1, _warnings.Count, string.Join(" | ", _warnings));
        }

        /// <summary>
        /// The owner (5 Oct 2026): the game keeps three backgrounds. Asked for one of the three that
        /// left (still equipped), the menus show the Himalayan Vista instead.
        /// </summary>
        [TestCase("kathmandu-courtyard")]
        [TestCase("village-terrace")]
        [TestCase("vintage-home")]
        [TestCase("rooftop-cafe")]
        [TestCase("")]
        [TestCase(null)]
        public void ABackgroundTheGameDoesNotHave_ShowsTheHimalayanVista(string id)
        {
            Assert.AreSame(_vista, Library().BackgroundPicture(id, out string shown));
            Assert.AreEqual(Vista, shown);
        }

        [Test]
        public void ABackgroundWithoutArtYet_ShowsTheHimalayanVista()
        {
            Assert.AreSame(_vista, Library().BackgroundPicture(Monastery, out string shown));
            Assert.AreEqual(Vista, shown);
        }

        [Test]
        public void TheLibrary_FindsArtByItsId()
        {
            TableBackdropLibrary library = Library();

            Assert.AreSame(_pokhara, library.Background(Pokhara).Sprite);
            Assert.AreSame(_pokhara, library.BackgroundPicture(Pokhara, out _));
            Assert.AreEqual(Monastery, library.Background(Monastery).Id, "listed without art");
            Assert.IsNull(library.Background(Monastery).Sprite);
            Assert.IsNull(library.Background("rooftop-cafe"));
            Assert.IsNull(library.Background(null));
            Assert.AreSame(_indigo, library.Table(Indigo).Sprite);
            Assert.IsNull(library.Table("glass-top"));
            Assert.AreEqual(3, library.BackgroundCount);
            Assert.AreEqual(3, library.TableCount);
        }

        [Test]
        public void ASkinWithoutALibrary_HasNoBackdrop()
        {
            var skin = new TableSkin(_cards, null);

            Assert.IsFalse(skin.HasBackdrops, "an old Table prefab keeps the picture it has");
            Assert.IsTrue(Skin(Library()).HasBackdrops);
        }

        [Test]
        public void TheTableView_ShowsTheTable_AndKeepsItsPictureForNone()
        {
            var go = new GameObject("Table", typeof(RectTransform));
            _made.Add(go);
            TableView view = go.AddComponent<TableView>();
            var picture = new GameObject("Picture", typeof(RectTransform)).AddComponent<Image>();
            _made.Add(picture.gameObject);
            Bind(view, "_backdropTable", picture);

            view.SetTable(_indigo, null);
            Assert.AreSame(_indigo, picture.sprite);
            Assert.IsTrue(picture.enabled);

            view.SetTable(null, null);
            Assert.AreSame(_indigo, picture.sprite, "no picture keeps the one it has");
        }

        [Test]
        public void TheDhakaCloths_HaveTheirBorder_AndAClothWithoutOneHasNone()
        {
            TableBackdropLibrary library = Library();

            Assert.AreSame(_border, library.TableFrame(Green));
            Assert.AreSame(_border, library.TableFrame(Indigo));
            Assert.AreSame(_border, library.TableFrame("glass-top"), "an unknown cloth shows the default one, border and all");
            Assert.AreSame(_border, Skin(library).TableFrame(Dusk), "a cloth without art yet shows the default one");

            Sprite dusk = MakeSprite("dusk");
            library.Set(
                new[] { new TableBackdropLibrary.Picture(Vista, _vista) },
                new[] { new TableBackdropLibrary.Picture(Green, _green, _border), new TableBackdropLibrary.Picture(Dusk, dusk) },
                Vista,
                Green);
            Assert.AreSame(dusk, library.TablePicture(Dusk, out _));
            Assert.IsNull(library.TableFrame(Dusk), "Dusk Himalaya is a scene: no border");
        }

        [Test]
        public void TheTableView_ShowsTheClothsBorder_OrHidesIt()
        {
            var go = new GameObject("Table", typeof(RectTransform));
            _made.Add(go);
            TableView view = go.AddComponent<TableView>();
            var picture = new GameObject("Picture", typeof(RectTransform)).AddComponent<Image>();
            var border = new GameObject("ClothFrame", typeof(RectTransform)).AddComponent<Image>();
            _made.Add(picture.gameObject);
            _made.Add(border.gameObject);
            Bind(view, "_backdropTable", picture);
            Bind(view, "_clothFrame", border);

            view.SetTable(_green, _border);
            Assert.AreSame(_border, border.sprite);
            Assert.IsTrue(border.enabled);
            Assert.AreSame(border, view.ClothFrame);

            view.SetTable(_indigo, null);
            Assert.IsFalse(border.enabled, "a cloth without a border hides it");
        }

        private static void Bind(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private TableSkin Skin(TableBackdropLibrary library) => new TableSkin(_cards, null, library);

        private TableBackdropLibrary Library()
        {
            TableBackdropLibrary library = Make<TableBackdropLibrary>();
            library.Set(
                new[]
                {
                    new TableBackdropLibrary.Picture(Vista, _vista),
                    new TableBackdropLibrary.Picture(Pokhara, _pokhara),
                    new TableBackdropLibrary.Picture(Monastery, null),
                },
                new[]
                {
                    new TableBackdropLibrary.Picture(Green, _green, _border),
                    new TableBackdropLibrary.Picture(Indigo, _indigo, _border),
                    new TableBackdropLibrary.Picture(Dusk, null),
                },
                Vista,
                Green);
            return library;
        }

        private T Make<T>() where T : ScriptableObject
        {
            T made = ScriptableObject.CreateInstance<T>();
            _made.Add(made);
            return made;
        }

        private Sprite MakeSprite(string name)
        {
            var texture = new Texture2D(4, 4) { name = name };
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            _made.Add(sprite);
            _made.Add(texture);
            return sprite;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning)
            {
                _warnings.Add(condition);
            }
        }
    }
}
