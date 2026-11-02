using System.IO;
using Callbreak.Infrastructure.Persistence;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Callbreak &gt; Clear Player Data (the owner, 2 Nov 2026: "clear my data once again tell me how can i
    /// clear my player data"): deletes every save of the game played in the Editor, the wallet, profile,
    /// daily rewards, settings, a paused match, the offers claimed and the sign-in, so the next Play
    /// starts as a new player. The saves are the game's own <see cref="JsonSaveStore"/> files in
    /// persistentDataPath (a half-written save and one set aside as corrupt too); Unity's own files
    /// there are left alone. Not while the game is playing, which would save again at once. A phone's
    /// data is cleared in its settings (Apps &gt; Callbreak &gt; Storage &gt; Clear data).
    /// </summary>
    public static class PlayerDataMenu
    {
        private const string MenuPath = "Callbreak/Clear Player Data";
        private const string Saves = "*.json";
        private const string SetAside = "*.json.corrupt";

        [MenuItem(MenuPath)]
        private static void Clear()
        {
            JsonSaveStore store = JsonSaveStore.CreateDefault();
            string[] saves = Directory.Exists(store.Folder) ? Directory.GetFiles(store.Folder, Saves) : new string[0];
            string[] setAside = Directory.Exists(store.Folder) ? Directory.GetFiles(store.Folder, SetAside) : new string[0];
            if (saves.Length + setAside.Length == 0)
            {
                EditorUtility.DisplayDialog("Clear Player Data", "There is no player data to clear in\n" + store.Folder, "OK");
                return;
            }

            string message = $"Delete the {saves.Length} saves of the game played in the Editor (wallet, profile, daily rewards, "
                + "settings, a paused match, offers, sign-in)? The next Play starts as a new player.\n\n" + store.Folder;
            if (!EditorUtility.DisplayDialog("Clear Player Data", message, "Delete", "Cancel"))
            {
                return;
            }

            foreach (string path in saves)
            {
                store.Delete(Path.GetFileNameWithoutExtension(path));
            }

            foreach (string path in setAside)
            {
                File.Delete(path);
            }

            Debug.Log($"[Callbreak] Cleared player data: {saves.Length} saves in {store.Folder}");
        }

        [MenuItem(MenuPath, true)]
        private static bool ClearValidate()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
