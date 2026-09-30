using System;
using Callbreak.Application.Profile;
using Callbreak.Presentation.Framework;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>GDD §11 screen 01: host a private table or join one.</summary>
    public sealed class PlayWithFriendsPresenter : IStartable, IDisposable
    {
        private readonly PlayWithFriendsView _view;
        private readonly ScreenRouter _router;
        private readonly IProfileService _profile;

        public PlayWithFriendsPresenter(PlayWithFriendsView view, ScreenRouter router, IProfileService profile)
        {
            _view = view;
            _router = router;
            _profile = profile;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.ProfileClicked += OnProfile;
            _view.SettingsClicked += OnSettings;
            _view.ShopClicked += OnShop;
            _view.CreateClicked += OnCreate;
            _view.JoinClicked += OnJoin;
            _profile.Changed += Render;
            Render();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.ProfileClicked -= OnProfile;
            _view.SettingsClicked -= OnSettings;
            _view.ShopClicked -= OnShop;
            _view.CreateClicked -= OnCreate;
            _view.JoinClicked -= OnJoin;
            _profile.Changed -= Render;
        }

        private void Render() => _view.SetPlayerName(_profile.DisplayName);

        private void OnBack() => _router.Pop();

        private void OnProfile() => _router.Push(ScreenId.Profile);

        private void OnSettings() => _router.Push(ScreenId.Settings);

        private void OnShop() => _router.Push(ScreenId.CardCollection);

        private void OnCreate() => _router.Push(ScreenId.CreateRoom);

        private void OnJoin() => _router.Push(ScreenId.JoinRoom);
    }
}
