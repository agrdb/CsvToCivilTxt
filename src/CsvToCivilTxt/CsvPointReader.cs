using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CsvToCivilTxt
{
    /// <summary>Lectura de CSV y conversión a formatos de punto de Civil 3D.</summary>
    public static class CsvPointReader
    {
        // ---------- Lectura ----------

        /// <summary>Lee todas las líneas no vacías y las separa en campos.</summary>
        public static List<string[]> ReadRows(string path, char delimiter)
        {
            var rows = new List<string[]>();
            foreach (var line in ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                rows.Add(SplitLine(line, delimiter));
            }
            return rows;
        }

        /// <summary>Lee el archivo detectando UTF-8 (con o sin BOM) y si no, ANSI (tildes de Excel en español).</summary>
        public static string[] ReadLines(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                text = Encoding.Default.GetString(bytes); // Windows-1252
            }
            text = text.TrimStart('﻿');
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        /// <summary>Detecta el separador más probable entre ; , tabulador y espacio.</summary>
        public static char DetectDelimiter(string path)
        {
            var sample = ReadLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).Take(20).ToList();
            if (sample.Count == 0) return ',';

            char[] candidates = { ';', '\t', ',', ' ' };
            char best = ',';
            int bestScore = -1;
            foreach (char c in candidates)
            {
                var counts = sample.Select(l => SplitLine(l, c).Length).ToList();
                int cols = counts[0];
                if (cols < 2) continue;
                // Gana el separador que da el mismo número de columnas (>=2) en más líneas
                int consistent = counts.Count(n => n == cols);
                int score = consistent * 100 + cols;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        /// <summary>Separa una línea respetando comillas dobles.</summary>
        public static string[] SplitLine(string line, char delimiter)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (ch == delimiter && !inQuotes)
                {
                    fields.Add(sb.ToString().Trim());
                    sb.Clear();
                    // Con separador espacio, varios espacios seguidos cuentan como uno
                    if (delimiter == ' ')
                        while (i + 1 < line.Length && line[i + 1] == ' ') i++;
                }
                else sb.Append(ch);
            }
            fields.Add(sb.ToString().Trim());
            return fields.ToArray();
        }

        /// <summary>True si la primera fila parece encabezado (algún campo de coordenada no numérico).</summary>
        public static bool LooksLikeHeader(string[] firstRow)
        {
            int numeric = firstRow.Count(f => TryParseNumber(f, out _));
            return numeric < Math.Min(2, firstRow.Length);
        }

        // ---------- Números ----------

        /// <summary>
        /// Interpreta números con punto o coma decimal: "1234.56", "1234,56", "1.234,56", "1,234.56".
        /// </summary>
        public static bool TryParseNumber(string s, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().Replace(" ", "").Replace(" ", "");

            int lastDot = s.LastIndexOf('.');
            int lastComma = s.LastIndexOf(',');

            if (lastDot >= 0 && lastComma >= 0)
            {
                // El último separador es el decimal; el otro es de miles
                if (lastComma > lastDot) s = s.Replace(".", "").Replace(',', '.');
                else s = s.Replace(",", "");
            }
            else if (lastComma >= 0)
            {
                s = s.Replace(',', '.');
            }

            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        // ---------- Mapeo automático de columnas ----------

        public static readonly Dictionary<string, string[]> HeaderAliases = new Dictionary<string, string[]>
        {
            { "P", new[] { "p", "pt", "point", "punto", "pto", "id", "no", "num", "numero", "número", "number", "nombre", "name",
                           "pt_id", "point_id", "pointid", "point_number", "pointnumber", "pnt", "station", "estacion", "estación" } },
            { "N", new[] { "n", "norte", "north", "northing", "y", "coord_y", "coordy", "y_m", "norte_m", "local_n", "grid_n" } },
            { "E", new[] { "e", "este", "east", "easting", "x", "coord_x", "coordx", "x_m", "este_m", "local_e", "grid_e" } },
            { "Z", new[] { "z", "cota", "elev", "elevacion", "elevación", "elevation", "h", "altura", "alt", "z_m", "cota_m",
                           "ortho_ht", "ortometrica", "ortométrica", "height" } },
            { "D", new[] { "d", "desc", "descripcion", "descripción", "description", "codigo", "código", "code", "cod",
                           "feature", "feature_code", "observacion", "observación", "obs", "tipo", "layer", "capa" } },
        };

        /// <summary>Devuelve el índice de columna que coincide con el campo, o -1.</summary>
        public static int GuessColumn(string[] header, string field)
        {
            var aliases = HeaderAliases[field];
            for (int i = 0; i < header.Length; i++)
            {
                // "Este (m)" -> "este", "Point ID" -> "point_id", "Coord. X" -> "coord_x"
                string h = System.Text.RegularExpressions.Regex.Replace(header[i], @"\(.*?\)|\[.*?\]", "");
                h = h.Trim().ToLowerInvariant().Replace(".", " ").Replace("-", " ").Replace("#", "");
                h = System.Text.RegularExpressions.Regex.Replace(h.Trim(), @"\s+", "_");
                if (aliases.Contains(h)) return i;
            }
            return -1;
        }

        // ---------- Escritura ----------

        public class ConvertOptions
        {
            public char InputDelimiter = ',';
            public bool HasHeader = true;
            public string Format = "PENZD";          // orden de campos de salida
            public char OutputDelimiter = ',';
            public int Decimals = 3;
            public Dictionary<char, int> ColumnMap = new Dictionary<char, int>(); // 'P','N','E','Z','D' -> índice (-1 = no hay)
            public int AutoNumberStart = 1;          // si no hay columna P
            public bool SkipInvalidRows = true;
        }

        public class ConvertResult
        {
            public List<string[]> OutputRows = new List<string[]>();
            public List<string> Warnings = new List<string>();
            public int Skipped;
        }

        /// <summary>Convierte las filas leídas a campos de salida según el formato.</summary>
        public static ConvertResult Convert(List<string[]> rows, ConvertOptions opt)
        {
            var result = new ConvertResult();
            int start = opt.HasHeader ? 1 : 0;
            int autoNumber = opt.AutoNumberStart;
            string numFmt = "F" + opt.Decimals;

            for (int r = start; r < rows.Count; r++)
            {
                string[] row = rows[r];
                int lineNo = r + 1;
                var outFields = new List<string>();
                bool ok = true;

                foreach (char f in opt.Format)
                {
                    int col = opt.ColumnMap.TryGetValue(f, out int c) ? c : -1;
                    string raw = (col >= 0 && col < row.Length) ? row[col] : "";

                    switch (f)
                    {
                        case 'P':
                            outFields.Add(col >= 0 && raw != "" ? CleanText(raw, opt.OutputDelimiter) : (autoNumber++).ToString());
                            break;

                        case 'N':
                        case 'E':
                        case 'Z':
                            if (TryParseNumber(raw, out double v))
                                outFields.Add(v.ToString(numFmt, CultureInfo.InvariantCulture));
                            else
                            {
                                ok = false;
                                result.Warnings.Add($"Línea {lineNo}: valor {f} no numérico \"{raw}\"");
                            }
                            break;

                        case 'D':
                            string d = CleanText(raw, opt.OutputDelimiter);
                            outFields.Add(d == "" ? "SD" : d); // Civil necesita algo en la columna D
                            break;
                    }
                    if (!ok) break;
                }

                if (ok) result.OutputRows.Add(outFields.ToArray());
                else result.Skipped++;
            }
            return result;
        }

        /// <summary>
        /// Civil 3D usa el separador del formato para partir columnas:
        /// con espacio, una descripción "POZO 1" se rompería; se reemplaza por "_".
        /// </summary>
        private static string CleanText(string s, char outDelimiter)
        {
            s = s.Trim().Replace("\"", "");
            if (outDelimiter == ' ') s = string.Join("_", s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            else s = s.Replace(outDelimiter.ToString(), "_");
            return s;
        }

        public static void WriteTxt(string path, IEnumerable<string[]> rows, char outDelimiter)
        {
            // ANSI sin BOM: Civil 3D lee mejor así las tildes y no se confunde con el BOM
            using (var sw = new StreamWriter(path, false, Encoding.Default))
            {
                foreach (var r in rows) sw.WriteLine(string.Join(outDelimiter.ToString(), r));
            }
        }
    }
}
