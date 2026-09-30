using Callbreak.Presentation.Table;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Mockup 07 for a capture: the call panel is its own prefab, so a Table instance in the state
    /// the mockup shows (the dealt hand, "Waiting" and "Choosing bid", timer 20) is put underneath.
    /// </summary>
    public static class BiddingPreview
    {
        public static void Prepare(GameObject instance)
        {
            TableUnder(instance, TablePreview.Moment.Bidding);
        }

        /// <summary>Puts a Table instance in <paramref name="moment"/> under an overlay's instance, behind its content.</summary>
        public static TableView TableUnder(GameObject overlay, TablePreview.Moment moment)
        {
            string path = ScreenLayoutBuilder.PrefabPath("Table");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[BiddingPreview] No Table prefab at {path}; build the Table screen first.");
                return null;
            }

            var table = (GameObject)PrefabUtility.InstantiatePrefab(prefab, overlay.transform);
            table.transform.SetAsFirstSibling();
            var view = table.GetComponent<TableView>();
            TablePreview.Apply(view, moment);
            return view;
        }
    }
}
