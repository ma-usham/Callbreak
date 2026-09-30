namespace Callbreak.Application.Ports
{
    /// <summary>
    /// Local persistence (GDD §29): one JSON file per key in the app's data folder, written
    /// atomically (temp file, flush, replace). Values are plain [Serializable] classes with
    /// public fields and a schemaVersion.
    /// </summary>
    public interface ISaveStore
    {
        bool TryLoad<T>(string key, out T value) where T : class;

        void Save<T>(string key, T value) where T : class;

        void Delete(string key);

        bool Exists(string key);
    }
}
