using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Table;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// The background the player equipped in the shop, behind a menu screen (V2, the owner, 28 Oct
    /// 2026: the tables fill the screen, so the scenery shows behind the menus instead). The prefab
    /// keeps the default background's picture, so it renders before anything is injected (and in
    /// editor captures); once <see cref="ICosmeticsShop"/> is injected it shows the equipped one, and
    /// changes as soon as the player equips another. A background without art shows the default.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class EquippedBackground : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private TableBackdropLibrary _library;

        private ICosmeticsShop _shop;

        /// <summary>The picture it shows.</summary>
        public Sprite Shown => _image != null ? _image.sprite : null;

        /// <summary>Where its pictures come from.</summary>
        public TableBackdropLibrary Library => _library;

        [Inject]
        public void Construct(ICosmeticsShop shop)
        {
            _shop = shop;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable()
        {
            if (_shop != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            if (_shop != null)
            {
                _shop.Changed -= Refresh;
            }
        }

        private void Subscribe()
        {
            _shop.Changed -= Refresh;
            _shop.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            if (_image == null || _library == null)
            {
                return;
            }

            Sprite picture = _library.BackgroundPicture(_shop.Equipped(CosmeticKind.Background), out _);
            if (picture != null && _image.sprite != picture)
            {
                _image.sprite = picture;
            }
        }
    }
}
