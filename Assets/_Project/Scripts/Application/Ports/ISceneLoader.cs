using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Ports
{
    /// <summary>The three scenes of GDD §21.</summary>
    public enum GameScene
    {
        Boot,
        Menu,
        Table,
    }

    /// <summary>Loads a scene behind the loading curtain (GDD §21 startup sequence, §23 curtain layer).</summary>
    public interface ISceneLoader
    {
        GameScene Current { get; }

        Task LoadAsync(GameScene scene, CancellationToken ct);

        /// <summary>
        /// What the current scene must do before it is replaced, while its objects still exist:
        /// its scope disposes here, so presenters let go of views that are still alive. The next
        /// load runs it once, behind the curtain, and forgets it.
        /// </summary>
        void BeforeNextLoad(Action teardown);
    }
}
