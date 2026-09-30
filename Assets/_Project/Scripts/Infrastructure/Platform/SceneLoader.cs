using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// <see cref="ISceneLoader"/> over <see cref="SceneManager"/> (GDD §21): the curtain covers the
    /// screen, the scene replaces the current one, and the curtain opens a frame after the new
    /// scene's scope has built, so no half-laid-out screen is ever visible.
    /// </summary>
    public sealed class SceneLoader : ISceneLoader
    {
        private static readonly string[] SceneNames = { "Boot", "Menu", "Table" };

        private readonly ILoadingCurtain _curtain;
        private Action _teardown;
        private bool _loading;

        public SceneLoader(ILoadingCurtain curtain)
        {
            _curtain = curtain ?? throw new ArgumentNullException(nameof(curtain));
            Current = FromName(SceneManager.GetActiveScene().name);
        }

        public GameScene Current { get; private set; }

        public void BeforeNextLoad(Action teardown)
        {
            _teardown = teardown;
        }

        public async Task LoadAsync(GameScene scene, CancellationToken ct)
        {
            // A load asked for while another is still finishing waits its turn.
            while (_loading)
            {
                await Awaitable.NextFrameAsync(ct);
            }

            _loading = true;
            try
            {
                await _curtain.CoverAsync(ct);
                TearDownCurrentScene();
                AsyncOperation load = SceneManager.LoadSceneAsync(SceneNames[(int)scene], LoadSceneMode.Single);
                while (!load.isDone)
                {
                    await Awaitable.NextFrameAsync(ct);
                }

                Current = scene;
                await Awaitable.NextFrameAsync(ct);
                await _curtain.UncoverAsync(ct);
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>A scene that fails to tidy up must not leave the player behind the curtain.</summary>
        private void TearDownCurrentScene()
        {
            Action teardown = _teardown;
            _teardown = null;
            if (teardown == null)
            {
                return;
            }

            try
            {
                teardown();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static GameScene FromName(string name)
        {
            for (int i = 0; i < SceneNames.Length; i++)
            {
                if (SceneNames[i] == name)
                {
                    return (GameScene)i;
                }
            }

            return GameScene.Boot;
        }
    }
}
