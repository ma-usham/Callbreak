using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The row pattern shared by the result tables (screens 09, 10 and 16): the gold highlight band
    /// sits on one row (or is hidden), and a row separator shows only where the band's own border
    /// does not already separate the rows. Separator i lies between rows i and i + 1.
    /// </summary>
    internal static class ResultsTableRows
    {
        public static void Highlight(RectTransform band, RectTransform[] rows, GameObject[] separators, int row)
        {
            bool show = rows != null && row >= 0 && row < rows.Length;
            band.gameObject.SetActive(show);
            if (show)
            {
                band.anchoredPosition = new Vector2(band.anchoredPosition.x, rows[row].anchoredPosition.y);
            }

            if (separators == null)
            {
                return;
            }

            for (int i = 0; i < separators.Length; i++)
            {
                separators[i].SetActive(!show || (i != row && i != row - 1));
            }
        }

        public static void SetText(TMP_Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        public static void SetValue(TMP_Text label, string text, bool negative, Color valueColor, Color negativeColor)
        {
            SetText(label, text);
            label.color = negative ? negativeColor : valueColor;
        }
    }
}
