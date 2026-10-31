using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// A page that opens over a running match (the scorecard, the round and match results, and Settings
    /// and How to Play from the match menu). It has no picture of its own, so the game stays in sight
    /// behind it (the owner, 31 Oct 2026: "the score board should be open with no bg there the game play
    /// scene will be the background"; ADR 058's addendum), under a dim that keeps the page readable and
    /// the taps off the table. The dim is off until the table's scope shows it: Settings and How to Play
    /// open over the menus' background too. Over the table the words the page draws straight on its
    /// ground are drawn light (<see cref="BackgroundInk.KeepLight"/>).
    /// </summary>
    public sealed class OverTheTable : MonoBehaviour
    {
        [SerializeField] private Image _dim;
        [SerializeField] private BackgroundInk _ink;

        /// <summary>The dim between the table and the page.</summary>
        public Image Dim => _dim;

        /// <summary>Whether the page stands over the table (its dim shows).</summary>
        public bool Shown => _dim != null && _dim.gameObject.activeSelf;

        /// <summary>Shows the dim and draws the page's words light, for as long as the page lives.</summary>
        public void Show()
        {
            if (_dim != null && !_dim.gameObject.activeSelf)
            {
                _dim.gameObject.SetActive(true);
            }

            if (_ink != null)
            {
                _ink.KeepLight();
            }
        }

        /// <summary>Its dim and the ink it lightens (editor builders only).</summary>
        public void Set(Image dim, BackgroundInk ink)
        {
            _dim = dim;
            _ink = ink;
        }
    }
}
