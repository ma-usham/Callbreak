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
    }
}
