using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Mockup 13 for a capture: the menu is an overlay, so a Table instance in mockup 08's state is
    /// put underneath its dim.
    /// </summary>
    public static class MatchMenuPreview
    {
        public static void Prepare(GameObject instance)
        {
            BiddingPreview.TableUnder(instance, TablePreview.Moment.Gameplay);
        }
    }
}
