using System;
using System.IO;
using System.Text;
using Callbreak.Application.Ports;
using UnityEngine;

namespace Callbreak.Infrastructure.Persistence
{
    /// <summary>
    /// <see cref="ISaveStore"/> as one JSON file per key in the app's data folder (GDD §29), written
    /// with JsonUtility, so values are plain [Serializable] classes with public fields (field
    /// initializers supply defaults for fields a file doesn't have). Saving is atomic: the JSON goes
    /// to <c>key.json.tmp</c>, is flushed to disk, then replaces <c>key.json</c>, so a crash leaves
    /// the old file or the new one, never half of one. A file that can't be parsed is moved aside
    /// to <c>key.json.corrupt</c> and reported as missing. Disk errors are logged, not thrown: a
    /// failed save must not break the game.
    /// </summary>
    public sealed class JsonSaveStore : ISaveStore
    {
        private const string Extension = ".json";
        private const string TempSuffix = ".tmp";
        private const string CorruptSuffix = ".corrupt";

        private readonly string _folder;
        private readonly object _gate = new object();

        public JsonSaveStore(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                throw new ArgumentException("A save folder is required.", nameof(folder));
            }

            _folder = folder;
        }

        /// <summary>The folder the files live in.</summary>
        public string Folder => _folder;

        /// <summary>The store in <c>UnityEngine.Application.persistentDataPath</c>. Call on the main thread.</summary>
        public static JsonSaveStore CreateDefault()
        {
            return new JsonSaveStore(UnityEngine.Application.persistentDataPath);
        }

        public bool TryLoad<T>(string key, out T value) where T : class
        {
            value = null;
            string path = PathOf(key);
            lock (_gate)
            {
                try
                {
                    RecoverInterruptedSave(path);
                    if (!File.Exists(path))
                    {
                        return false;
                    }

                    string json = File.ReadAllText(path, Encoding.UTF8);
                    if (TryParse(json, out value))
                    {
                        return true;
                    }

                    MoveAside(path);
                    return false;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"[JsonSaveStore] Couldn't read '{key}': {e.Message}");
                    value = null;
                    return false;
                }
            }
        }

        public void Save<T>(string key, T value) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            string path = PathOf(key);
            string json = JsonUtility.ToJson(value, true);
            lock (_gate)
            {
                try
                {
                    Directory.CreateDirectory(_folder);
                    string temp = path + TempSuffix;
                    WriteFlushed(temp, json);
                    Replace(temp, path);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Debug.LogError($"[JsonSaveStore] Couldn't save '{key}': {e.Message}");
                }
            }
        }

        public void Delete(string key)
        {
            string path = PathOf(key);
            lock (_gate)
            {
                try
                {
                    File.Delete(path);
                    File.Delete(path + TempSuffix);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"[JsonSaveStore] Couldn't delete '{key}': {e.Message}");
                }
            }
        }

        public bool Exists(string key)
        {
            string path = PathOf(key);
            lock (_gate)
            {
                return File.Exists(path) || File.Exists(path + TempSuffix);
            }
        }

        /// <summary>Keys become file names, so they are limited to letters, digits, '_' and '-'.</summary>
        private string PathOf(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("A save key is required.", nameof(key));
            }

            foreach (char c in key)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok)
                {
                    throw new ArgumentException($"Save key '{key}' may only use letters, digits, '_' and '-'.", nameof(key));
                }
            }

            return Path.Combine(_folder, key + Extension);
        }

        private static bool TryParse<T>(string json, out T value) where T : class
        {
            value = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                // Overwriting a fresh instance keeps the field initializers for anything the file lacks.
                object instance = Activator.CreateInstance(typeof(T), true);
                JsonUtility.FromJsonOverwrite(json, instance);
                value = (T)instance;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void WriteFlushed(string path, string text)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static void Replace(string temp, string path)
        {
            if (!File.Exists(path))
            {
                File.Move(temp, path);
                return;
            }

            try
            {
                File.Replace(temp, path, null);
            }
            catch (Exception e) when (e is NotSupportedException || e is IOException)
            {
                // No atomic replace on this platform or file system: delete, then move. A crash in
                // between leaves only the complete temp file, which the next load moves into place.
                File.Delete(path);
                File.Move(temp, path);
            }
        }

        /// <summary>A save that stopped after removing the old file but before moving the new one in.</summary>
        private static void RecoverInterruptedSave(string path)
        {
            string temp = path + TempSuffix;
            if (!File.Exists(path) && File.Exists(temp))
            {
                File.Move(temp, path);
            }
        }

        private static void MoveAside(string path)
        {
            string aside = path + CorruptSuffix;
            try
            {
                File.Delete(aside);
                File.Move(path, aside);
                Debug.LogWarning($"[JsonSaveStore] {Path.GetFileName(path)} couldn't be read; moved to {Path.GetFileName(aside)} and treated as missing.");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[JsonSaveStore] {Path.GetFileName(path)} couldn't be read or moved aside: {e.Message}");
            }
        }
    }
}
