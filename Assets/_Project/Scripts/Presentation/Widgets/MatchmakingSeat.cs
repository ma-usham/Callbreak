using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// One seat circle of the matchmaking screen (GDD §11 screen 05). It has both looks: filled (the
    /// avatar disc on its ivory ring) and searching (the dashed circle with a faint silhouette), and
    /// shows one of them. The name and status colours follow the look ("Ready" green, "Searching"
    /// muted), with the colours measured on the mockup.
    /// </summary>
    public sealed class MatchmakingSeat : MonoBehaviour
    {
        [SerializeField] private GameObject _filledLook;
        [SerializeField] private GameObject _searchingLook;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Color _filledNameColor = Color.black;
        [SerializeField] private Color _filledStatusColor = Color.black;
        [SerializeField] private Color _searchingNameColor = Color.black;
        [SerializeField] private Color _searchingStatusColor = Color.black;

        public void Show(bool filled, string playerName, string status)
        {
            if (_filledLook.activeSelf != filled)
            {
                _filledLook.SetActive(filled);
            }

            if (_searchingLook.activeSelf == filled)
            {
                _searchingLook.SetActive(!filled);
            }

            if (_name.text != playerName)
            {
                _name.text = playerName;
            }

            if (_status.text != status)
            {
                _status.text = status;
            }

            _name.color = filled ? _filledNameColor : _searchingNameColor;
            _status.color = filled ? _filledStatusColor : _searchingStatusColor;
        }
    }
}
