using System;
using System.Collections.Generic;
using System.Reflection;
using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The background behind the menus (V2, the owner, 28 Oct 2026): the one the player equipped in
    /// the shop, from the moment the shop is known, and a new one as soon as it is equipped; a screen
    /// that is hidden catches up when it shows again. A background the game has no art for shows the
    /// Durbar Square. Here in PlayMode, where the component's enable and disable run.
    /// </summary>
    public sealed class EquippedBackgroundTests
    {
        private const string Durbar = "durbar-square";
        private const string Pokhara = "pokhara-lakeside";
        private const string Monastery = "monastery-garden";

        private readonly List<Object> _made = new List<Object>();
        private Sprite _durbar;
        private Sprite _pokhara;
        private TableBackdropLibrary _library;

        [SetUp]
        public void SetUp()
        {
            _durbar = MakeSprite("durbar");
            _pokhara = MakeSprite("pokhara");
            _library = ScriptableObject.CreateInstance<TableBackdropLibrary>();
            _made.Add(_library);
            _library.Set(
                new[]
                {
                    new TableBackdropLibrary.Picture(Durbar, _durbar),
                    new TableBackdropLibrary.Picture(Pokhara, _pokhara),
                    new TableBackdropLibrary.Picture(Monastery, null),
                },
                Array.Empty<TableBackdropLibrary.Picture>(),
                Durbar,
                "himalayan-ivory");
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
        public void ItShowsTheEquippedBackground_AndFollowsTheShop()
        {
            var shop = new Shop { Background = Pokhara };
            EquippedBackground background = Make(out Image image);
            Assert.AreSame(_durbar, background.Shown, "the prefab's picture until the shop is known");

            background.Construct(shop);
            Assert.AreSame(_pokhara, image.sprite, "the equipped background, once the shop is known");

            shop.Equip(CosmeticKind.Background, Durbar);
            Assert.AreSame(_durbar, image.sprite, "a new background shows at once");
        }

        [Test]
        public void AHiddenScreen_CatchesUpWhenItShowsAgain()
        {
            var shop = new Shop { Background = Durbar };
            EquippedBackground background = Make(out Image image);
            background.Construct(shop);

            background.gameObject.SetActive(false);
            shop.Equip(CosmeticKind.Background, Pokhara);
            Assert.AreSame(_durbar, image.sprite, "a hidden screen does not follow");
            Assert.AreEqual(0, shop.Listeners, "nor listens");

            background.gameObject.SetActive(true);
            Assert.AreSame(_pokhara, image.sprite, "it catches up when it shows again");
            Assert.AreEqual(1, shop.Listeners, "and listens once");
        }

        [Test]
        public void InjectedWhileHidden_ItShowsTheEquippedBackgroundWhenItShows()
        {
            var shop = new Shop { Background = Pokhara };
            EquippedBackground background = Make(out Image image);
            background.gameObject.SetActive(false);

            background.Construct(shop);
            Assert.AreEqual(0, shop.Listeners);
            background.gameObject.SetActive(true);

            Assert.AreSame(_pokhara, image.sprite);
        }

        [TestCase("kathmandu-courtyard")]
        [TestCase(Monastery)]
        [TestCase(null)]
        public void ABackgroundWithoutArt_ShowsDurbarSquare(string id)
        {
            EquippedBackground background = Make(out Image image);
            image.sprite = _pokhara;

            background.Construct(new Shop { Background = id });

            Assert.AreSame(_durbar, image.sprite);
        }

        private EquippedBackground Make(out Image image)
        {
            var go = new GameObject("Background", typeof(RectTransform));
            _made.Add(go);
            image = go.AddComponent<Image>();
            image.sprite = _durbar;
            EquippedBackground background = go.AddComponent<EquippedBackground>();
            Bind(background, "_image", image);
            Bind(background, "_library", _library);
            return background;
        }

        private static void Bind(EquippedBackground background, string field, object value) =>
            typeof(EquippedBackground).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(background, value);

        private Sprite MakeSprite(string name)
        {
            var texture = new Texture2D(4, 4) { name = name };
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            _made.Add(sprite);
            _made.Add(texture);
            return sprite;
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
