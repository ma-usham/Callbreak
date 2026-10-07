using Callbreak.Editor.Screens;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Builds the name screen asked when the game starts (ADR 022) from its layout,
    /// ArtSource/Screens/NamePrompt (the owner's mockup 22, ADR 043), like every other screen; the
    /// screen's parts that no layout node draws come from <see cref="NamePromptCustomizer"/>. The
    /// prefab used to live with the overlays; it moved to the screens with its asset id, so the Menu
    /// scene keeps its instance.
    /// </summary>
    public static class NamePromptBuilder
    {
        public const string ScreenName = "NamePrompt";

        private const string OldPrefabPath = "Assets/_Project/Prefabs/UI/Overlays/NamePrompt.prefab";

        public static string PrefabPath => ScreenLayoutBuilder.PrefabPath(ScreenName);

        [MenuItem("Callbreak/Build/Name Prompt")]
        public static void Build()
        {
            // Once: the prefab moves to the screens folder, keeping its asset id (and the Menu scene's instance).
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OldPrefabPath) != null && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                string error = AssetDatabase.MoveAsset(OldPrefabPath, PrefabPath);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[NamePromptBuilder] Could not move {OldPrefabPath}: {error}");
                }
            }

            ScreenLayoutBuilder.Build(ScreenName);
        }
    }
}
