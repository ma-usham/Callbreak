using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Callbreak.Bootstrap
{
    /// <summary>
    /// The Menu scene's scope (GDD §21 FIG 21.1): every menu screen is a prefab instance in the
    /// scene, registered as itself (for its presenter) and as a <see cref="UIScreen"/> (for the
    /// router). Presenters are entry points, so they start with the scene and are disposed when it
    /// unloads.
    /// </summary>
    public sealed class MenuLifetimeScope : LifetimeScope
    {
        [SerializeField] private UIScreen[] _screens;

        protected override void Configure(IContainerBuilder builder)
        {
            foreach (UIScreen screen in _screens)
            {
                if (screen != null)
                {
                    builder.RegisterComponent(screen).AsSelf();
                }
            }

            builder.Register<ScreenRouter>(Lifetime.Scoped);
            builder.Register<MatchLauncher>(Lifetime.Scoped);

            RegisterPresenter<MainMenuView, MainMenuPresenter>(builder);
            RegisterPresenter<PlayWithFriendsView, PlayWithFriendsPresenter>(builder);
            RegisterPresenter<CreateRoomView, CreateRoomPresenter>(builder);
            RegisterPresenter<JoinRoomView, JoinRoomPresenter>(builder);
            RegisterPresenter<PrivateLobbyView, PrivateLobbyPresenter>(builder);
            RegisterPresenter<MatchmakingView, MatchmakingPresenter>(builder);
            RegisterPresenter<OfflineSetupView, OfflineSetupPresenter>(builder);
            RegisterPresenter<SettingsView, SettingsPresenter>(builder);
            RegisterPresenter<HowToPlayView, HowToPlayPresenter>(builder);
            RegisterPresenter<ProfileView, ProfilePresenter>(builder);
            RegisterPresenter<CardCollectionView, CardCollectionPresenter>(builder);

            // Last, so every presenter is already listening when the first screen opens.
            builder.RegisterEntryPoint<MenuEntryPoint>();
        }

        /// <summary>Registers a screen's presenter only when the scene has that screen.</summary>
        private void RegisterPresenter<TView, TPresenter>(IContainerBuilder builder)
            where TView : UIScreen
        {
            foreach (UIScreen screen in _screens)
            {
                if (screen is TView)
                {
                    builder.RegisterEntryPoint<TPresenter>();
                    return;
                }
            }
        }
    }
}
