namespace Callbreak.Application.Ports
{
    /// <summary>
    /// Product analytics (GDD §15 event list). Never personal data: player ids only, never names or
    /// hands. Parameters are key/value pairs.
    /// </summary>
    public interface IAnalytics
    {
        void Track(string eventName, params (string key, object value)[] parameters);
    }
}
