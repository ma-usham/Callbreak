using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// Colours a label with one vertical gradient across the whole line (GDD §12: screen titles
    /// carry a burgundy vertical gradient). TextMeshPro's own vertex gradient restarts on every
    /// character, so a lowercase letter would run through the full range in half the height; this
    /// samples the gradient by height instead: <see cref="_capTop"/> at the top of the capitals,
    /// <see cref="_baseline"/> at the baseline, continuing a little beyond both for ascenders and
    /// descenders. It follows the text, the font size and the language at runtime.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TextLineGradient : MonoBehaviour
    {
        private const float Above = 1.35f;      // how far past the capital height the ramp continues
        private const float Below = -0.4f;      // how far below the baseline

        [SerializeField] private Color _capTop = Color.white;
        [SerializeField] private Color _baseline = Color.white;

        private TMP_Text _text;

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            _text.OnPreRenderText += Paint;
            _text.SetVerticesDirty();
        }

        private void OnDisable()
        {
            if (_text == null)
            {
                return;
            }

            _text.OnPreRenderText -= Paint;
            _text.SetVerticesDirty();
        }

        private void Paint(TMP_TextInfo info)
        {
            TMP_FontAsset font = _text.font;
            if (font == null)
            {
                return;
            }

            float capLine = font.faceInfo.capLine;
            if (capLine <= 0f)
            {
                return;
            }

            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }

                float capHeight = capLine * character.scale;
                if (capHeight <= 0f)
                {
                    continue;
                }

                TMP_MeshInfo mesh = info.meshInfo[character.materialReferenceIndex];
                for (int v = 0; v < 4; v++)
                {
                    int index = character.vertexIndex + v;
                    float t = Mathf.Clamp((mesh.vertices[index].y - character.baseLine) / capHeight, Below, Above);
                    Color32 colour = Color.LerpUnclamped(_baseline, _capTop, t);
                    colour.a = mesh.colors32[index].a;
                    mesh.colors32[index] = colour;
                }
            }
        }
    }
}
