using System;
using System.Collections.Generic;
using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The words a menu draws straight on the equipped background (ADR 064): their own colours on a
    /// day scene, light on a night scene (Tihar Lights, Phewa Moonlight), a gradient title plain at
    /// night; they follow the player's choice, and a hidden screen catches up when it shows again.
    /// Here in PlayMode, where the component's enable and disable run.
    /// </summary>
    public sealed class BackgroundInkTests
    {
        private const string Durbar = "durbar-square";
        private const string Tihar = "tihar-lights";
        private static readonly Color DayInk = new Color32(0x57, 0x2E, 0x12, 0xFF);
        private static readonly Color NightInk = new Color32(0xEA, 0xD8, 0xBE, 0xFF);
        private static readonly Color DayTitle = new Color32(0x8B, 0x1A, 0x1A, 0xFF);
        private static readonly Color NightTitle = new Color32(0xF6, 0xDF, 0xA8, 0xFF);

        private readonly List<Object> _made = new List<Object>();
        private TableBackdropLibrary _library;

        [SetUp]
        public void SetUp()
        {
            _library = ScriptableObject.CreateInstance<TableBackdropLibrary>();
            _made.Add(_library);
            _library.Set(
                new[]
                {
                    new TableBackdropLibrary.Picture(Durbar, MakeSprite("durbar")),
                    new TableBackdropLibrary.Picture(Tihar, MakeSprite("tihar"), null, true),
                },
                Array.Empty<TableBackdropLibrary.Picture>(),
                Durbar,
                "green-dhaka");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
            {
                Object.Destroy(made);
            }

            _made.Clear();
        }

        [Test]
        public void OnANightScene_TheWordsTurnLight_AndBackOnADayScene()
        {
            var shop = new Shop { Background = Tihar };
            BackgroundInk ink = Make(out TMP_Text greeting, out TMP_Text title);
            Assert.AreEqual(DayInk, greeting.color, "the day colours until the shop is known");

            ink.Construct(shop);
            Assert.IsTrue(ink.ShowsNight);
            Assert.AreEqual(NightInk, greeting.color, "light words on the night scene");
            Assert.AreEqual(NightTitle, title.color);
            Assert.IsFalse(title.enableVertexGradient, "the title drawn plain at night");

            shop.Equip(CosmeticKind.Background, Durbar);
            Assert.IsFalse(ink.ShowsNight);
            Assert.AreEqual(DayInk, greeting.color, "their own colours on a day scene");
            Assert.AreEqual(DayTitle, title.color);
            Assert.IsTrue(title.enableVertexGradient, "the title's gradient back by day");
        }

        [Test]
        public void AHiddenScreen_CatchesUpWhenItShowsAgain()
        {
            var shop = new Shop { Background = Durbar };
            BackgroundInk ink = Make(out TMP_Text greeting, out _);
            ink.Construct(shop);

            ink.gameObject.SetActive(false);
            shop.Equip(CosmeticKind.Background, Tihar);
            Assert.AreEqual(DayInk, greeting.color, "a hidden screen does not follow");
            Assert.AreEqual(0, shop.Listeners, "nor listens");

            ink.gameObject.SetActive(true);
            Assert.AreEqual(NightInk, greeting.color, "it catches up when it shows again");
            Assert.AreEqual(1, shop.Listeners, "and listens once");
        }

        private BackgroundInk Make(out TMP_Text greeting, out TMP_Text title)
        {
            var root = new GameObject("Screen", typeof(RectTransform));
            _made.Add(root);
            greeting = Label(root, "Greeting", DayInk, false);
            title = Label(root, "Title", DayTitle, true);
            BackgroundInk ink = root.AddComponent<BackgroundInk>();
            ink.Set(_library, new Graphic[] { greeting, title }, new[] { DayInk, DayTitle }, new[] { NightInk, NightTitle }, new[] { false, true });
            return ink;
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

        private static TMP_Text Label(GameObject root, string name, Color colour, bool gradient)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.color = colour;
            label.enableVertexGradient = gradient;
            return label;
        }

        /// <summary>A shop whose player has this background equipped; equipping one tells its listeners.</summary>
        private sealed class Shop : ICosmeticsShop
        {
            private Action _changed;

            public string Background;

            public int Listeners => _changed?.GetInvocationList().Length ?? 0;

            public event Action Changed
            {
                add => _changed += value;
                remove => _changed -= value;
            }

            public IReadOnlyList<CosmeticItem> Catalog(CosmeticKind kind) => CosmeticCatalog.Of(kind);

            public bool IsOwned(CosmeticKind kind, string id) => true;

            public string Equipped(CosmeticKind kind) => kind == CosmeticKind.Background ? Background : CosmeticCatalog.DefaultOf(kind);

            public void Equip(CosmeticKind kind, string id)
            {
                if (kind == CosmeticKind.Background)
                {
                    Background = id;
                    _changed?.Invoke();
                }
            }

            public PurchaseResult Buy(CosmeticKind kind, string id) => PurchaseResult.AlreadyOwned;
        }
    }
}
