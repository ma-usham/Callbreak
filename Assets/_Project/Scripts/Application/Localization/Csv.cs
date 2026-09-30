using System;
using System.Collections.Generic;
using System.Text;

namespace Callbreak.Application.Localization
{
    /// <summary>
    /// RFC 4180 CSV as spreadsheet tools write it: comma-separated fields, double-quoted when they
    /// hold commas, quotes (doubled) or line breaks; CRLF or LF rows; an optional UTF-8 byte-order
    /// mark. Line breaks inside a quoted field are read as "\n" whatever the file uses, so a table
    /// reads the same after git has converted its line endings.
    /// </summary>
    public static class Csv
    {
        /// <summary>
        /// Every row with its fields in order. Blank lines come back as a row with one empty field.
        /// Throws <see cref="FormatException"/> for a quoted field that never closes, naming the line
        /// where it opened (it would otherwise swallow the rest of the file).
        /// </summary>
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(text))
            {
                return rows;
            }

            var row = new List<string>();
            var field = new StringBuilder();
            int i = text[0] == '﻿' ? 1 : 0;
            int line = 1;
            bool rowHasContent = false;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '"' && field.Length == 0 && !FieldStartedUnquoted(text, i))
                {
                    int openedAt = line;
                    i++;
                    bool closed = false;
                    while (i < text.Length)
                    {
                        char q = text[i];
                        if (q == '"')
                        {
                            if (i + 1 < text.Length && text[i + 1] == '"')
                            {
                                field.Append('"');
                                i += 2;
                                continue;
                            }

                            i++;
                            closed = true;
                            break;
                        }

                        if (q == '\r' || q == '\n')
                        {
                            field.Append('\n');
                            line++;
                            i += q == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                            continue;
                        }

                        field.Append(q);
                        i++;
                    }

                    if (!closed)
                    {
                        throw new FormatException($"CSV line {openedAt}: a quoted field is never closed.");
                    }

                    rowHasContent = true;
                    continue;
                }

                if (c == ',')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rowHasContent = true;
                    i++;
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row.ToArray());
                    row.Clear();
                    rowHasContent = false;
                    line++;
                    i += c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                    continue;
                }

                field.Append(c);
                rowHasContent = true;
                i++;
            }

            if (rowHasContent || field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }

            return rows;
        }

        /// <summary>Writes rows, quoting only the fields that need it, each row ended by <paramref name="newline"/>.</summary>
        public static string Write(IEnumerable<IReadOnlyList<string>> rows, string newline = "\r\n")
        {
            var sb = new StringBuilder();
            foreach (IReadOnlyList<string> row in rows)
            {
                for (int f = 0; f < row.Count; f++)
                {
                    if (f > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(Escape(row[f]));
                }

                sb.Append(newline);
            }

            return sb.ToString();
        }

        /// <summary>The field as it appears in a file: quoted, with quotes doubled, when it holds a comma, quote, line break or edge space.</summary>
        public static string Escape(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return string.Empty;
            }

            bool quote = field[0] == ' ' || field[field.Length - 1] == ' ';
            foreach (char c in field)
            {
                if (c == ',' || c == '"' || c == '\r' || c == '\n')
                {
                    quote = true;
                    break;
                }
            }

            return quote ? "\"" + field.Replace("\"", "\"\"") + "\"" : field;
        }

        // A quote only opens a quoted field at the very start of the field; one in the middle of
        // unquoted text (5" screen) is kept as a character.
        private static bool FieldStartedUnquoted(string text, int index)
        {
            return index > 0 && text[index - 1] != ',' && text[index - 1] != '\n' && text[index - 1] != '\r' && text[index - 1] != '﻿';
        }
    }
}
