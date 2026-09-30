using System;
using Callbreak.Application.Profile;
using Callbreak.Presentation.Feedback;
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
        private readonly AvatarLibrary _avatars;

        public PlayWithFriendsPresenter(PlayWithFriendsView view, ScreenRouter router, IProfileService profile, AvatarLibrary avatars)
        {
            _view = view;
            _router = router;
            _profile = profile;
            _avatars = avatars;
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

        private void Render()
        {
            _view.SetPlayerName(_profile.DisplayName);
            _view.SetAvatar(_avatars.Get(_profile.AvatarId));
        }

        private void OnBack() => _router.Pop();

        private void OnProfile() => _router.Push(ScreenId.Profile);

        private void OnSettings() => _router.Push(ScreenId.Settings);

        private void OnShop() => _router.Push(ScreenId.CardCollection);

        private void OnCreate() => _router.Push(ScreenId.CreateRoom);

        private void OnJoin() => _router.Push(ScreenId.JoinRoom);
    }
}
