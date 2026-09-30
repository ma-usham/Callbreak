using System;
using System.Collections;
using System.IO;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;
using Callbreak.Bootstrap;
using Callbreak.Infrastructure.Persistence;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// Runs the real game for a test: loads the Boot scene with its saves redirected to an empty
    /// temporary folder (so a run never touches the player's files), waits for the menu, and tears
    /// everything down again afterwards.
    /// </summary>
    public sealed class GameFixture
    {
        public const float StepTimeout = 20f;

        private const string BootScene = "Boot";

        private string _saveFolder;

        public ProjectLifetimeScope Project { get; private set; }

        public IObjectResolver Services => Project.Container;

        public MenuLifetimeScope Menu => UnityEngine.Object.FindFirstObjectByType<MenuLifetimeScope>();

        public TableLifetimeScope Table => UnityEngine.Object.FindFirstObjectByType<TableLifetimeScope>();

        public ScreenRouter MenuRouter => Menu != null && Menu.Container != null ? Menu.Container.Resolve<ScreenRouter>() : null;

        public ScreenRouter TableRouter => Table != null && Table.Container != null ? Table.Container.Resolve<ScreenRouter>() : null;

        public ILoadingCurtain Curtain => Services.Resolve<ILoadingCurtain>();

        public IAppFlow Flow => Services.Resolve<IAppFlow>();

        public IDialogs Dialogs => Services.Resolve<IDialogs>();

        public ISettingsService Settings => Services.Resolve<ISettingsService>();

        /// <summary>
        /// Boots the game and waits until the menu is on screen.
        /// </summary>
        /// <param name="freshInstall">False (the default) starts as a player who has already seen the rules primer.</param>
        /// <param name="seed">Writes further save files before the game starts.</param>
        /// <param name="extra">Extra registrations for the project scope (they win over the game's own).</param>
        public IEnumerator Boot(bool freshInstall = false, Action<ISaveStore> seed = null, Action<IContainerBuilder> extra = null)
        {
            _saveFolder = Path.Combine(UnityEngine.Application.temporaryCachePath, "callbreak-playmode-" + Guid.NewGuid().ToString("N"));
            var saves = new JsonSaveStore(_saveFolder);
            if (!freshInstall)
            {
                new SettingsService(saves).MarkHowToPlaySeen();
            }

            seed?.Invoke(saves);
            using (LifetimeScope.Enqueue(builder =>
            {
                // A type of its own: the container refuses a second registration of the game's own store type.
                builder.RegisterInstance<ISaveStore>(new TemporarySaves(saves));
                extra?.Invoke(builder);
            }))
            {
                // The project scope builds in Awake, inside this load; the later scenes load without the overrides.
                yield return SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
                Project = UnityEngine.Object.FindFirstObjectByType<ProjectLifetimeScope>();
            }

            Assert.IsNotNull(Project, "the Boot scene has the project scope");
            yield return UntilMenu();
        }

        /// <summary>Waits for the Menu scene to be up with its first screen showing and the curtain gone.</summary>
        public IEnumerator UntilMenu()
        {
            yield return Until(
                () => Flow.State == AppState.Menu && MenuRouter != null && MenuRouter.Current != ScreenId.None && !Curtain.IsCovering,
                "the menu to open");
        }

        /// <summary>Waits for the Table scene to be up with the curtain gone.</summary>
        public IEnumerator UntilTable()
        {
            yield return Until(() => Flow.State == AppState.InMatch && Table != null && Table.Container != null && !Curtain.IsCovering, "the table to open");
        }

        /// <summary>Waits, frame by frame, until <paramref name="condition"/> holds; fails after <paramref name="timeout"/> seconds.</summary>
        public static IEnumerator Until(Func<bool> condition, string what, float timeout = StepTimeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail("Timed out after " + timeout + " s waiting for " + what + ".");
                }

                yield return null;
            }
        }

        /// <summary>The button called <paramref name="buttonName"/> on the screen the router shows, tapped.</summary>
        public static void Press(ScreenRouter router, string buttonName)
        {
            UIScreen screen = router.Get<UIScreen>(router.Current);
            Assert.IsNotNull(screen, "a screen is showing");
            Press(screen.transform, buttonName, router.Current.ToString());
        }

        /// <summary>True when <paramref name="root"/> shows a button of that name that can be tapped now.</summary>
        public static bool CanPress(Component root, string buttonName)
        {
            if (root == null)
            {
                return false;
            }

            foreach (Button button in root.GetComponentsInChildren<Button>(false))
            {
                if (button.name == buttonName && button.IsInteractable())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The button called <paramref name="buttonName"/> somewhere under <paramref name="root"/>, tapped.</summary>
        public static void PressIn(Component root, string buttonName)
        {
            Assert.IsNotNull(root, "the view with " + buttonName + " exists");
            Press(root.transform, buttonName, root.name);
        }

        /// <summary>The open dialog's primary ("ConfirmButton") or secondary ("CancelButton") button, tapped.</summary>
        public void PressDialog(bool confirm)
        {
            Assert.IsTrue(Dialogs.IsOpen, "a dialog is open");
            DialogView dialog = Project.GetComponentInChildren<DialogView>(true);
            Press(dialog.transform, confirm ? "ConfirmButton" : "CancelButton", "the dialog");
        }

        public IEnumerator TearDown()
        {
            // An empty scene first, so the game's scenes can unload; then the services that outlive scenes.
            SceneManager.CreateScene("PlayModeTestEmpty-" + Guid.NewGuid().ToString("N"));
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.buildIndex >= 0)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }

            if (Project != null)
            {
                UnityEngine.Object.Destroy(Project.gameObject);
                Project = null;
                yield return null;
            }

            if (!string.IsNullOrEmpty(_saveFolder) && Directory.Exists(_saveFolder))
            {
                Directory.Delete(_saveFolder, true);
            }
        }

        /// <summary>The game's save store, pointed at the test's temporary folder.</summary>
        private sealed class TemporarySaves : ISaveStore
        {
            private readonly JsonSaveStore _inner;

            public TemporarySaves(JsonSaveStore inner)
            {
                _inner = inner;
            }

            public bool TryLoad<T>(string key, out T value) where T : class => _inner.TryLoad(key, out value);

            public void Save<T>(string key, T value) where T : class => _inner.Save(key, value);

            public void Delete(string key) => _inner.Delete(key);

            public bool Exists(string key) => _inner.Exists(key);
        }

        private static void Press(Transform root, string buttonName, string where)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(false))
            {
                if (button.name == buttonName)
                {
                    Assert.IsTrue(button.IsInteractable(), buttonName + " on " + where + " can be tapped");
                    button.onClick.Invoke();
                    return;
                }
            }

            Assert.Fail("No active button " + buttonName + " on " + where + ".");
        }
    }
}
