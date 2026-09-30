using UnityEngine;

namespace Callbreak.Application.Config
{
    /// <summary>
    /// The string tables the game ships (GDD §15): UI, Rules and Emotes, each a CSV with the header
    /// <c>key,en,ne</c> under Assets/_Project/Localization. Infrastructure turns them into
    /// <see cref="Localization.StringTables"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Callbreak/Config/Localization", fileName = "LocalizationConfig")]
    public sealed class LocalizationConfig : ScriptableObject
    {
        [SerializeField] private TextAsset _ui;
        [SerializeField] private TextAsset _rules;
        [SerializeField] private TextAsset _emotes;

        public TextAsset Ui => _ui;

        public TextAsset Rules => _rules;

        public TextAsset Emotes => _emotes;
    }
}
