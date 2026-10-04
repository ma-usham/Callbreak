using System.Collections.Generic;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The backdrop behind the table (the owner, 4 Oct 2026; mockups 20 and 21): the player's equipped
    /// background shows as its scenery behind the table and its foreground in front of it, with the
    /// equipped table between them, all in BG_Table's frame. A background or table this build has no
    /// art for (an unknown id, one the game no longer has, or art not made yet) shows the default one
    /// instead and says so once. The library here has the game's three backgrounds (the owner, 5 Oct
    /// 2026) and its three tables. These tests need the engine (sprites, the log), so they live
    /// outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class TableBackdropTests
    {
        private const string Vista = "himalayan-vista";
        private const string Pokhara = "pokhara-lakeside";
        private const string Monastery = "monastery-garden";
        private const string Ivory = "himalayan-ivory";
        private const string Crimson = "royal-crimson";
        private const string Walnut = "heritage-walnut";

        private static readonly Vector2 Frame = new Vector2(2230f, 1255f);
        private static readonly Rect FlowerBand = new Rect(0f, 905f, 2230f, 350f);
        private static readonly Rect CrimsonTop = new Rect(0f, 280f, 2230f, 975f);

        private readonly List<Object> _made = new List<Object>();
        private readonly List<string> _warnings = new List<string>();
        private CardSpriteProvider _cards;
        private Sprite _vistaBack;
        private Sprite _vistaFront;
        private Sprite _pokharaBack;
        private Sprite _ivory;
        private Sprite _crimson;

        [SetUp]
        public void SetUp()
        {
            _cards = Make<CardSpriteProvider>();
            _vistaBack = MakeSprite("vista back");
            _vistaFront = MakeSprite("vista front");
            _pokharaBack = MakeSprite("pokhara back");
            _ivory = MakeSprite("ivory table");
            _crimson = MakeSprite("crimson table");
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
        public void TheEquippedBackgroundAndTable_ShowTheirOwnLayers()
        {
            TableBackdrop shown = Skin(Library()).Backdrop(Pokhara, Crimson);

            Assert.AreEqual(Pokhara, shown.BackgroundId);
            Assert.AreEqual(Crimson, shown.TableId);
            Assert.AreSame(_pokharaBack, shown.Back.Sprite, "the scenery behind the table");
            Assert.AreSame(_crimson, shown.Table.Sprite, "the table");
            Assert.IsTrue(shown.Front.IsEmpty, "Pokhara Lakeside has no foreground");
            Assert.AreEqual(Frame, shown.Frame, "BG_Table's frame");
            Assert.IsEmpty(_warnings);
        }

        [Test]
        public void TheDefaults_AreTheVistaWithItsForeground_AndTheIvoryTable()
        {
            TableBackdropLibrary library = Library();
            TableBackdrop shown = Skin(library).Backdrop(Vista, Ivory);

            Assert.AreSame(_vistaBack, shown.Back.Sprite);
            Assert.AreSame(_ivory, shown.Table.Sprite);
            Assert.AreSame(_vistaFront, shown.Front.Sprite, "the red flowers in front of the table");
            Assert.AreEqual(Vista, library.DefaultBackgroundId);
            Assert.AreEqual(Ivory, library.DefaultTableId);
            Assert.AreSame(_vistaBack, library.Default.Back.Sprite, "the library's own default is the same");
            Assert.AreSame(_ivory, library.Default.Table.Sprite);
        }

        [Test]
        public void ALayer_KeepsThePartOfTheFrameItCovers()
        {
            TableBackdropLibrary library = Library();
            TableBackdrop defaults = Skin(library).Backdrop(Vista, Crimson);

            Assert.AreEqual(FlowerBand, defaults.Front.Area, "the foreground covers only the bottom band");
            Assert.AreEqual(CrimsonTop, defaults.Table.Area);
            Assert.AreEqual(Rect.zero, defaults.Back.Area, "the scenery covers the whole frame");
        }

        [Test]
        public void AnUnknownBackground_ShowsTheDefault_AndSaysSoOnce()
        {
            TableSkin skin = Skin(Library());

            TableBackdrop first = skin.Backdrop("rooftop-cafe", Crimson);
            TableBackdrop second = skin.Backdrop("rooftop-cafe", Crimson);

            Assert.AreEqual(Vista, first.BackgroundId);
            Assert.AreSame(_vistaBack, first.Back.Sprite);
            Assert.AreSame(_vistaFront, first.Front.Sprite, "the default's foreground comes with it");
            Assert.AreSame(_crimson, first.Table.Sprite, "the table is still the equipped one");
            Assert.AreEqual(first.BackgroundId, second.BackgroundId);
            Assert.AreEqual(1, _warnings.Count, string.Join(" | ", _warnings));
            StringAssert.Contains("rooftop-cafe", _warnings[0]);
        }

        /// <summary>
        /// The owner (5 Oct 2026): the game keeps three backgrounds. Asked for one of the three that
        /// left (still equipped), the table shows the Himalayan Vista instead, with its foreground and
        /// the equipped table, and says so once.
        /// </summary>
        [TestCase("kathmandu-courtyard")]
        [TestCase("village-terrace")]
        [TestCase("vintage-home")]
        public void ARemovedBackground_ShowsTheHimalayanVista_AndSaysSoOnce(string removed)
        {
            TableSkin skin = Skin(Library());

            TableBackdrop first = skin.Backdrop(removed, Crimson);
            TableBackdrop second = skin.Backdrop(removed, Crimson);

            Assert.AreEqual(Vista, first.BackgroundId);
            Assert.AreSame(_vistaBack, first.Back.Sprite, "the Himalayan Vista's scenery");
            Assert.AreSame(_vistaFront, first.Front.Sprite, "and its foreground");
            Assert.AreEqual(Crimson, first.TableId, "the table is still the equipped one");
            Assert.AreSame(_crimson, first.Table.Sprite);
            Assert.AreEqual(Vista, second.BackgroundId);
            Assert.AreEqual(1, _warnings.Count, string.Join(" | ", _warnings));
            StringAssert.Contains("\"" + removed + "\"", _warnings[0]);
        }

        [Test]
        public void ABackgroundWithoutArtYet_ShowsTheDefault_AndSaysSoOnce()
        {
            TableSkin skin = Skin(Library());

            TableBackdrop shown = skin.Backdrop(Monastery, Ivory);
            skin.Backdrop(Monastery, Ivory);

            Assert.AreEqual(Vista, shown.BackgroundId);
            Assert.AreSame(_vistaBack, shown.Back.Sprite);
            Assert.AreEqual(1, _warnings.Count, string.Join(" | ", _warnings));
            StringAssert.Contains(Monastery, _warnings[0]);
        }

        [Test]
        public void AnUnknownTable_ShowsTheDefaultTable_AndSaysSoOnce()
        {
            TableSkin skin = Skin(Library());

            TableBackdrop shown = skin.Backdrop(Pokhara, "glass-top");
            skin.Backdrop(Pokhara, "glass-top");

            Assert.AreEqual(Ivory, shown.TableId);
            Assert.AreSame(_ivory, shown.Table.Sprite);
            Assert.AreSame(_pokharaBack, shown.Back.Sprite, "the background is still the equipped one");
            Assert.AreEqual(1, _warnings.Count, string.Join(" | ", _warnings));
            StringAssert.Contains("glass-top", _warnings[0]);
        }

        [Test]
        public void ATableWithoutArtYet_ShowsTheDefaultTable_AndSaysSoOnce()
        {
            TableSkin skin = Skin(Library());

            TableBackdrop shown = skin.Backdrop(Pokhara, Walnut);
            skin.Backdrop(Vista, Walnut);

            Assert.AreEqual(Ivory, shown.TableId);
            Assert.AreSame(_ivory, shown.Table.Sprite);
            Assert.AreEqual(1, _warnings.Count, string.Join(" | ", _warnings));
            StringAssert.Contains(Walnut, _warnings[0]);
        }

        [Test]
        public void NothingEquipped_ShowsTheDefaults_AndSaysSo()
        {
            TableBackdrop shown = Skin(Library()).Backdrop(null, string.Empty);

            Assert.AreEqual(Vista, shown.BackgroundId);
            Assert.AreEqual(Ivory, shown.TableId);
            Assert.AreSame(_vistaBack, shown.Back.Sprite);
            Assert.AreSame(_ivory, shown.Table.Sprite);
            Assert.AreEqual(2, _warnings.Count, "one for the background, one for the table: " + string.Join(" | ", _warnings));
        }

        /// <summary>
        /// Until the split layers land, BG_Table (scenery, table and flowers in one picture) stands in
        /// for the Himalayan Vista's back and the default table has no layer of its own: the defaults
        /// then show BG_Table alone, as before, and nothing is said about it.
        /// </summary>
        [Test]
        public void TheDefaultTableWithoutALayer_ShowsNoTableLayer_AndSaysNothing()
        {
            TableBackdropLibrary library = Library(withIvoryLayer: false, withVistaFront: false);
            TableSkin skin = Skin(library);

            TableBackdrop defaults = skin.Backdrop(Vista, Ivory);

            Assert.AreSame(_vistaBack, defaults.Back.Sprite);
            Assert.AreEqual(Ivory, defaults.TableId);
            Assert.IsTrue(defaults.Table.IsEmpty, "the table is the one in the back picture");
            Assert.IsTrue(defaults.Front.IsEmpty, "so are the flowers");
            Assert.IsEmpty(_warnings);

            TableBackdrop walnut = skin.Backdrop(Vista, Walnut);
            Assert.AreEqual(Ivory, walnut.TableId);
            Assert.IsTrue(walnut.Table.IsEmpty);
            Assert.AreEqual(1, _warnings.Count, "a table without art still says so");
        }

        [Test]
        public void TheLibrary_FindsArtByItsId()
        {
            TableBackdropLibrary library = Library();

            Assert.AreSame(_pokharaBack, library.Background(Pokhara).Back.Sprite);
            Assert.AreEqual(Monastery, library.Background(Monastery).Id, "listed without art");
            Assert.IsTrue(library.Background(Monastery).Back.IsEmpty);
            Assert.IsNull(library.Background("rooftop-cafe"));
            Assert.IsNull(library.Background(null));
            Assert.AreSame(_crimson, library.Table(Crimson).Table.Sprite);
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

        private TableSkin Skin(TableBackdropLibrary library) => new TableSkin(_cards, null, library);

        private TableBackdropLibrary Library(bool withIvoryLayer = true, bool withVistaFront = true)
        {
            TableBackdropLibrary library = Make<TableBackdropLibrary>();
            library.Set(
                Frame,
                new[]
                {
                    new TableBackdropLibrary.BackgroundArt(Vista, new BackdropLayer(_vistaBack, Rect.zero), withVistaFront ? new BackdropLayer(_vistaFront, FlowerBand) : default),
                    new TableBackdropLibrary.BackgroundArt(Pokhara, new BackdropLayer(_pokharaBack, Rect.zero), default),
                    new TableBackdropLibrary.BackgroundArt(Monastery, default, default),
                },
                new[]
                {
                    new TableBackdropLibrary.TableArt(Ivory, withIvoryLayer ? new BackdropLayer(_ivory, Rect.zero) : default),
                    new TableBackdropLibrary.TableArt(Crimson, new BackdropLayer(_crimson, CrimsonTop)),
                    new TableBackdropLibrary.TableArt(Walnut, default),
                },
                Vista,
                Ivory);
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
