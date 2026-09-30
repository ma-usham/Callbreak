namespace Callbreak.Application.Ports
{
    /// <summary>Quits the app (Android back on the main menu, GDD §11 screen 00).</summary>
    public interface IAppExit
    {
        void Quit();
    }
}
