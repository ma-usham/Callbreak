using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// Draws every digit of a label in a cell of one width (the font's widest digit), as the score
    /// tables of the mockups do: "4.1" is then as wide as "5.0", and a column of scores lines up.
    /// Inter's own digits are proportional (its 1 is a third narrower than its 4). The line keeps
    /// the edge its alignment pins. It follows the text and the font size at runtime.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TabularFigures : MonoBehaviour
    {
        private TMP_Text _text;
        private TMP_FontAsset _measuredFont;
        private float _widestDigit;
        private float[] _moves = new float[16];

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            _text.OnPreRenderText += Layout;
            _text.SetVerticesDirty();
        }

        private void OnDisable()
        {
            if (_text == null)
            {
                return;
            }

            _text.OnPreRenderText -= Layout;
            _text.SetVerticesDirty();
        }

        private void Layout(TMP_TextInfo info)
        {
            TMP_FontAsset font = _text.font;
            if (font == null)
            {
                return;
            }

            if (font != _measuredFont)
            {
                _measuredFont = font;
                _widestDigit = WidestDigit(font);
            }

            int count = info.characterCount;
            if (_widestDigit <= 0f || count == 0)
            {
                return;
            }

            if (_moves.Length < count)
            {
                _moves = new float[Mathf.NextPowerOfTwo(count)];
            }

            // Each digit is centred in its cell; everything after it moves along by what the cell added.
            float widthScale = _text.characterHorizontalScale;
            float added = 0f;
            for (int i = 0; i < count; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                float extra = 0f;
                if (character.character >= '0' && character.character <= '9' && character.fontAsset == font && character.textElement != null)
                {
                    extra = (_widestDigit - character.textElement.glyph.metrics.horizontalAdvance) * character.scale * widthScale;
                }

                _moves[i] = added + extra / 2f;
                added += extra;
            }

            if (Mathf.Approximately(added, 0f))
            {
                return;
            }

            float back = _text.horizontalAlignment == HorizontalAlignmentOptions.Right ? added
                : _text.horizontalAlignment == HorizontalAlignmentOptions.Left ? 0f
                : added / 2f;
            for (int i = 0; i < count; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }

                float move = _moves[i] - back;
                Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
                for (int v = 0; v < 4; v++)
                {
                    vertices[character.vertexIndex + v].x += move;
                }
            }
        }

        private static float WidestDigit(TMP_FontAsset font)
        {
            float widest = 0f;
            for (char digit = '0'; digit <= '9'; digit++)
            {
                if (font.characterLookupTable.TryGetValue(digit, out TMP_Character character) && character.glyph != null)
                {
                    widest = Mathf.Max(widest, character.glyph.metrics.horizontalAdvance);
                }
            }

            return widest;
        }
    }
}
