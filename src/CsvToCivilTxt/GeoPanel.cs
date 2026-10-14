using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CsvToCivilTxt
{
    /// <summary>
    /// Pestaña 2: CSV con latitud/longitud → proyección a coordenadas planas → TXT para Civil 3D.
    /// Método independiente de la pestaña 1 (que recibe coordenadas ya planas).
    /// </summary>
    public class GeoPanel : UserControl
    {
        private readonly ListBox lstFiles = new ListBox();
        private readonly ComboBox cboInDelim = new ComboBox();
        private readonly CheckBox chkHeader = new CheckBox();
        private readonly Dictionary<string, ComboBox> colCombos = new Dictionary<string, ComboBox>();
        private readonly ComboBox cboCrs = new ComboBox();
        private readonly CheckBox chkGeoid = new CheckBox();
        private readonly NumericUpDown numGeoid = new NumericUpDown();
        private readonly ComboBox cboFormat = new ComboBox();
        private readonly ComboBox cboOutDelim = new ComboBox();
        private readonly NumericUpDown numDecimals = new NumericUpDown();
        private readonly NumericUpDown numAutoStart = new NumericUpDown();
        private readonly CheckBox chkSameFolder = new CheckBox();
        private readonly TextBox txtOutFolder = new TextBox();
        private readonly Button btnOutFolder = new Button();
        private readonly DataGridView gridPreview = new DataGridView();
        private readonly TextBox txtLog = new TextBox();
        private readonly Label lblStatus = new Label();
        private readonly Button btnConvert = new Button();

        private List<string[]> _rows = new List<string[]>();
        private bool _loading;

        // Campos de entrada de esta pestaña
        private static readonly string[] Fields = { "P", "LAT", "LON", "H", "D" };
        private static readonly string[] FieldLabels = { "P  Número punto", "Latitud", "Longitud", "Altura / cota", "D  Descripción" };

        private static readonly Dictionary<string, string[]> Aliases = new Dictionary<string, string[]>
        {
            { "P",   CsvPointReader.HeaderAliases["P"] },
            { "LAT", new[] { "lat", "latitud", "latitude", "phi", "lat_dd", "latitud_dd", "y_geo" } },
            { "LON", new[] { "lon", "long", "lng", "longitud", "longitude", "lambda", "lon_dd", "longitud_dd", "x_geo" } },
            { "H",   new[] { "h", "altura", "alt", "altitude", "elev", "elevacion", "elevación", "elevation", "height",
                             "ellip_ht", "ellipsoidal_height", "altura_elipsoidal", "ortho_ht", "z", "cota" } },
            { "D",   CsvPointReader.HeaderAliases["D"] },
        };

        private static readonly string[] InDelimNames = { "Automático", "Punto y coma ( ; )", "Coma ( , )", "Tabulador", "Espacio" };
        private static readonly char[] InDelimChars = { '\0', ';', ',', '\t', ' ' };

        public GeoPanel()
        {
            Font = new Font("Segoe UI", 9F);
            BuildLayout();
            UpdateEnabledState();
        }

        // =====================================================================
        // Interfaz
        // =====================================================================
        private void BuildLayout()
        {
            var left = new FlowLayoutPanel
            {
                Dock = DockStyle.Left, Width = 340, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(8)
            };

            // 1. Archivos
            var gFiles = Group("1. CSV con latitud / longitud", 160);
            lstFiles.SetBounds(10, 22, 300, 90);
            lstFiles.HorizontalScrollbar = true;
            lstFiles.SelectedIndexChanged += (s, e) => LoadSelectedFile();
            gFiles.Controls.Add(lstFiles);
            gFiles.Controls.Add(MakeButton("Agregar...", 10, 120, 95, (s, e) => BrowseFiles()));
            gFiles.Controls.Add(MakeButton("Quitar", 112, 120, 95, (s, e) =>
            {
                if (lstFiles.SelectedIndex >= 0) lstFiles.Items.RemoveAt(lstFiles.SelectedIndex);
                if (lstFiles.Items.Count > 0) lstFiles.SelectedIndex = 0; else ClearData();
                UpdateEnabledState();
            }));
            gFiles.Controls.Add(MakeButton("Limpiar", 214, 120, 95, (s, e) => { lstFiles.Items.Clear(); ClearData(); UpdateEnabledState(); }));

            // 2. Lectura
            var gRead = Group("2. Lectura del CSV", 80);
            gRead.Controls.Add(MakeLabel("Separador:", 10, 25));
            cboInDelim.SetBounds(90, 22, 220, 23);
            cboInDelim.DropDownStyle = ComboBoxStyle.DropDownList;
            cboInDelim.Items.AddRange(InDelimNames);
            cboInDelim.SelectedIndex = 0;
            cboInDelim.SelectedIndexChanged += (s, e) => LoadSelectedFile();
            chkHeader.SetBounds(10, 52, 300, 20);
            chkHeader.Text = "La primera fila es encabezado";
            chkHeader.CheckedChanged += (s, e) => { if (!_loading) { FillColumnCombos(); RefreshPreview(); } };
            gRead.Controls.AddRange(new Control[] { cboInDelim, chkHeader });

            // 3. Columnas
            var gCols = Group("3. Columnas (decimal o GMS: 4°36'12.5\"N)", 175);
            for (int i = 0; i < Fields.Length; i++)
            {
                int y = 22 + i * 29;
                gCols.Controls.Add(MakeLabel(FieldLabels[i], 10, y + 3));
                var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                cb.SetBounds(130, y, 180, 23);
                cb.SelectedIndexChanged += (s, e) => { if (!_loading) RefreshPreview(); };
                colCombos[Fields[i]] = cb;
                gCols.Controls.Add(cb);
            }

            // 4. Proyección
            var gProj = Group("4. Sistema de coordenadas planas destino", 140);
            cboCrs.SetBounds(10, 22, 300, 23);
            cboCrs.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCrs.Items.AddRange(ProjectedCrs.All.Cast<object>().ToArray());
            cboCrs.SelectedIndex = 0; // Origen Nacional
            cboCrs.SelectedIndexChanged += (s, e) => RefreshPreview();

            chkGeoid.SetBounds(10, 54, 300, 20);
            chkGeoid.Text = "Altura elipsoidal → cota: restar ondulación N";
            chkGeoid.CheckedChanged += (s, e) => { numGeoid.Enabled = chkGeoid.Checked; RefreshPreview(); };
            gProj.Controls.Add(MakeLabel("N geoidal (m):", 28, 82));
            numGeoid.SetBounds(130, 79, 90, 23);
            numGeoid.Minimum = -150; numGeoid.Maximum = 150; numGeoid.DecimalPlaces = 3; numGeoid.Increment = 0.1M;
            numGeoid.Enabled = false;
            numGeoid.ValueChanged += (s, e) => RefreshPreview();
            var lblGeoidHint = MakeLabel("Cota = h − N  (N del modelo geoidal del sitio)", 28, 108);
            lblGeoidHint.ForeColor = SystemColors.GrayText;
            gProj.Controls.AddRange(new Control[] { cboCrs, chkGeoid, numGeoid, lblGeoidHint });

            // 5. Salida
            var gOut = Group("5. Formato de salida (igual al de Civil 3D)", 205);
            gOut.Controls.Add(MakeLabel("Formato:", 10, 25));
            cboFormat.SetBounds(130, 22, 180, 23);
            cboFormat.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFormat.Items.AddRange(new object[] { "PENZD", "PNEZD", "PENZ", "PNEZ", "ENZ", "NEZ" });
            cboFormat.SelectedIndex = 0;
            cboFormat.SelectedIndexChanged += (s, e) => RefreshPreview();

            gOut.Controls.Add(MakeLabel("Separador:", 10, 54));
            cboOutDelim.SetBounds(130, 51, 180, 23);
            cboOutDelim.DropDownStyle = ComboBoxStyle.DropDownList;
            cboOutDelim.Items.AddRange(new object[] { "comma delimited ( , )", "space delimited (espacio)" });
            cboOutDelim.SelectedIndex = 0;
            cboOutDelim.SelectedIndexChanged += (s, e) => RefreshPreview();

            gOut.Controls.Add(MakeLabel("Decimales:", 10, 83));
            numDecimals.SetBounds(130, 80, 60, 23);
            numDecimals.Minimum = 0; numDecimals.Maximum = 8; numDecimals.Value = 3;
            numDecimals.ValueChanged += (s, e) => RefreshPreview();

            gOut.Controls.Add(MakeLabel("Si no hay P, numerar desde:", 10, 112));
            numAutoStart.SetBounds(200, 109, 110, 23);
            numAutoStart.Minimum = 1; numAutoStart.Maximum = 99999999; numAutoStart.Value = 1;
            numAutoStart.ValueChanged += (s, e) => RefreshPreview();

            chkSameFolder.SetBounds(10, 140, 300, 20);
            chkSameFolder.Text = "Guardar junto a cada CSV";
            chkSameFolder.Checked = true;
            chkSameFolder.CheckedChanged += (s, e) => UpdateEnabledState();
            txtOutFolder.SetBounds(10, 167, 255, 23);
            btnOutFolder.SetBounds(270, 166, 40, 25);
            btnOutFolder.Text = "...";
            btnOutFolder.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog { Description = "Carpeta de salida para los TXT" })
                    if (fbd.ShowDialog(this) == DialogResult.OK) txtOutFolder.Text = fbd.SelectedPath;
            };
            gOut.Controls.AddRange(new Control[] { cboFormat, cboOutDelim, numDecimals, numAutoStart, chkSameFolder, txtOutFolder, btnOutFolder });

            left.Controls.AddRange(new Control[] { gFiles, gRead, gCols, gProj, gOut });

            // Barra inferior
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8) };
            btnConvert.Text = "Proyectar y exportar TXT";
            btnConvert.Font = new Font(Font, FontStyle.Bold);
            btnConvert.Dock = DockStyle.Right;
            btnConvert.Width = 200;
            btnConvert.Click += (s, e) => ConvertAll();
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Text = "Agrega CSV con latitud y longitud.";
            bottom.Controls.Add(lblStatus);
            bottom.Controls.Add(btnConvert);

            // Derecha: vista previa + registro
            gridPreview.Dock = DockStyle.Fill;
            gridPreview.ReadOnly = true;
            gridPreview.AllowUserToAddRows = false;
            gridPreview.AllowUserToDeleteRows = false;
            gridPreview.RowHeadersVisible = false;
            gridPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            gridPreview.BackgroundColor = SystemColors.Window;
            gridPreview.DefaultCellStyle.Font = new Font("Consolas", 9F);

            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Dock = DockStyle.Fill;
            txtLog.Font = new Font("Consolas", 8.5F);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            split.Panel1.Controls.Add(gridPreview);
            split.Panel2.Controls.Add(txtLog);
            split.Panel2.Padding = new Padding(0, 4, 0, 0);
            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 8, 8, 0) };
            right.Controls.Add(split);

            Controls.Add(right);
            Controls.Add(left);
            Controls.Add(bottom);

            Load += (s, e) => { try { split.SplitterDistance = (int)(split.Height * 0.72); } catch { } };
        }

        private static GroupBox Group(string text, int height) =>
            new GroupBox { Text = text, Width = 320, Height = height, Margin = new Padding(0, 0, 0, 8) };

        private static Label MakeLabel(string text, int x, int y) =>
            new Label { Text = text, Location = new Point(x, y), AutoSize = true };

        private static Button MakeButton(string text, int x, int y, int w, EventHandler click)
        {
            var b = new Button { Text = text };
            b.SetBounds(x, y, w, 27);
            b.Click += click;
            return b;
        }

        private void UpdateEnabledState()
        {
            txtOutFolder.Enabled = btnOutFolder.Enabled = !chkSameFolder.Checked;
            btnConvert.Enabled = lstFiles.Items.Count > 0;
        }

        // =====================================================================
        // Archivos y columnas
        // =====================================================================
        private void BrowseFiles()
        {
            using (var ofd = new OpenFileDialog
            {
                Title = "Selecciona los CSV con coordenadas geográficas",
                Filter = "Archivos de puntos (*.csv;*.txt)|*.csv;*.txt|Todos (*.*)|*.*",
                Multiselect = true
            })
            {
                if (ofd.ShowDialog(this) == DialogResult.OK) AddFiles(ofd.FileNames);
            }
        }

        public void AddFiles(IEnumerable<string> paths)
        {
            foreach (var p in paths.Where(File.Exists))
                if (!lstFiles.Items.Contains(p)) lstFiles.Items.Add(p);
            if (lstFiles.SelectedIndex < 0 && lstFiles.Items.Count > 0) lstFiles.SelectedIndex = 0;
            UpdateEnabledState();
        }

        private void ClearData()
        {
            _rows = new List<string[]>();
            gridPreview.Columns.Clear();
            foreach (var cb in colCombos.Values) cb.Items.Clear();
            lblStatus.Text = "Agrega CSV con latitud y longitud.";
        }

        private char InDelimiter(string path)
        {
            char c = InDelimChars[cboInDelim.SelectedIndex];
            return c == '\0' ? CsvPointReader.DetectDelimiter(path) : c;
        }

        private char OutDelimiter() => cboOutDelim.SelectedIndex == 1 ? ' ' : ',';

        private static string Normalize(string h)
        {
            h = Regex.Replace(h, @"\(.*?\)|\[.*?\]", "");
            h = h.Trim().ToLowerInvariant().Replace(".", " ").Replace("-", " ").Replace("#", "");
            return Regex.Replace(h.Trim(), @"\s+", "_");
        }

        private static int Guess(string[] header, string field)
        {
            for (int i = 0; i < header.Length; i++)
                if (Aliases[field].Contains(Normalize(header[i]))) return i;
            return -1;
        }

        /// <summary>Mapeo automático. Sin encabezado: P, Lat, Lon, H, D en ese orden.</summary>
        private static Dictionary<string, int> AutoMap(List<string[]> rows, bool hasHeader)
        {
            int nCols = rows.Max(r => r.Length);
            string[] header = hasHeader ? rows[0] : new string[0];
            bool headerKnown = hasHeader && (Guess(header, "LAT") >= 0 || Guess(header, "LON") >= 0);
            var map = new Dictionary<string, int>();
            for (int i = 0; i < Fields.Length; i++)
            {
                int g = hasHeader ? Guess(header, Fields[i]) : -1;
                map[Fields[i]] = g >= 0 ? g : (headerKnown ? -1 : (i < nCols ? i : -1));
            }
            return map;
        }

        private void LoadSelectedFile()
        {
            if (_loading || !(lstFiles.SelectedItem is string path)) return;
            try
            {
                _loading = true;
                char delim = InDelimiter(path);
                _rows = CsvPointReader.ReadRows(path, delim);
                if (_rows.Count == 0) { Log($"{Path.GetFileName(path)}: archivo vacío."); return; }

                // Encabezado: si los dos primeros campos de lat/lon no son ángulos, es encabezado
                chkHeader.Checked = _rows[0].Count(f => Projection.TryParseAngle(f, out _)) < 2;
                FillColumnCombos();
                Log($"{Path.GetFileName(path)}: {_rows.Count} filas.");
            }
            catch (Exception ex) { Log("Error leyendo archivo: " + ex.Message); }
            finally { _loading = false; }
            RefreshPreview();
        }

        private void FillColumnCombos()
        {
            if (_rows.Count == 0) return;
            int nCols = _rows.Max(r => r.Length);
            string[] header = chkHeader.Checked ? _rows[0] : new string[0];
            string[] names = Enumerable.Range(0, nCols)
                .Select(i => $"Col {i + 1}" + (i < header.Length && header[i] != "" ? $": {header[i]}" : ""))
                .ToArray();

            var map = AutoMap(_rows, chkHeader.Checked);
            bool wasLoading = _loading;
            _loading = true;
            foreach (var kv in colCombos)
            {
                kv.Value.Items.Clear();
                kv.Value.Items.Add("(ninguna)");
                kv.Value.Items.AddRange(names);
                kv.Value.SelectedIndex = map[kv.Key] + 1;
            }
            _loading = wasLoading;
        }

        // =====================================================================
        // Proyección
        // =====================================================================
        private class GeoResult
        {
            public List<string[]> Projected = new List<string[]>();   // P, N, E, Z, D (como texto, para reutilizar el formateador)
            public List<double[]> LatLon = new List<double[]>();
            public List<string> Warnings = new List<string>();
            public int Skipped;
            public bool NoHeight;
        }

        private GeoResult Project(List<string[]> rows, bool hasHeader, Dictionary<string, int> map)
        {
            var res = new GeoResult();
            var crs = (ProjectedCrs)cboCrs.SelectedItem;
            double geoidN = chkGeoid.Checked ? (double)numGeoid.Value : 0;
            res.NoHeight = map["H"] < 0;
            string Get(string[] r, string f) => map[f] >= 0 && map[f] < r.Length ? r[map[f]] : "";

            for (int i = hasHeader ? 1 : 0; i < rows.Count; i++)
            {
                var r = rows[i];
                string sLat = Get(r, "LAT"), sLon = Get(r, "LON");

                if (!Projection.TryParseAngle(sLat, out double lat) || !Projection.TryParseAngle(sLon, out double lon))
                {
                    res.Skipped++;
                    res.Warnings.Add($"Línea {i + 1}: lat/long no válidas \"{sLat}\" / \"{sLon}\"");
                    continue;
                }
                if (Math.Abs(lat) > 90 || Math.Abs(lon) > 180)
                {
                    res.Skipped++;
                    res.Warnings.Add(Math.Abs(lat) > 1000 || Math.Abs(lon) > 1000
                        ? $"Línea {i + 1}: parecen coordenadas planas, usa la pestaña 1"
                        : $"Línea {i + 1}: fuera de rango (¿latitud y longitud invertidas?)");
                    continue;
                }

                double z = 0;
                if (!res.NoHeight)
                {
                    if (!CsvPointReader.TryParseNumber(Get(r, "H"), out z))
                    {
                        res.Skipped++;
                        res.Warnings.Add($"Línea {i + 1}: altura no numérica \"{Get(r, "H")}\"");
                        continue;
                    }
                    z -= geoidN;
                }

                Projection.Forward(crs, lat, lon, out double north, out double east);
                res.LatLon.Add(new[] { lat, lon });
                res.Projected.Add(new[]
                {
                    Get(r, "P"),
                    north.ToString("R", CultureInfo.InvariantCulture),
                    east.ToString("R", CultureInfo.InvariantCulture),
                    z.ToString("R", CultureInfo.InvariantCulture),
                    Get(r, "D")
                });
            }
            return res;
        }

        /// <summary>Pasa las filas proyectadas por el mismo formateador de la pestaña 1.</summary>
        private CsvPointReader.ConvertResult Format(GeoResult geo)
        {
            var opt = new CsvPointReader.ConvertOptions
            {
                HasHeader = false,
                Format = (string)cboFormat.SelectedItem,
                OutputDelimiter = OutDelimiter(),
                Decimals = (int)numDecimals.Value,
                AutoNumberStart = (int)numAutoStart.Value,
                ColumnMap = new Dictionary<char, int> { { 'P', 0 }, { 'N', 1 }, { 'E', 2 }, { 'Z', 3 }, { 'D', 4 } }
            };
            return CsvPointReader.Convert(geo.Projected, opt);
        }

        private Dictionary<string, int> UiMap() => colCombos.ToDictionary(kv => kv.Key, kv => kv.Value.SelectedIndex - 1);

        private string ValidateMapping(Dictionary<string, int> map)
        {
            if (map["LAT"] < 0) return "Asigna la columna de latitud.";
            if (map["LON"] < 0) return "Asigna la columna de longitud.";
            return null;
        }

        private void RefreshPreview()
        {
            if (_loading || _rows.Count == 0 || lstFiles.SelectedItem == null) return;

            gridPreview.SuspendLayout();
            gridPreview.Columns.Clear();
            gridPreview.Rows.Clear();

            var map = UiMap();
            string err = ValidateMapping(map);
            if (err != null) { lblStatus.Text = "⚠ " + err; gridPreview.ResumeLayout(); return; }

            var geo = Project(_rows, chkHeader.Checked, map);
            var outRes = Format(geo);
            string format = (string)cboFormat.SelectedItem;
            string sep = OutDelimiter().ToString();

            gridPreview.Columns.Add("lat", "Latitud (°)");
            gridPreview.Columns.Add("lon", "Longitud (°)");
            gridPreview.Columns.Add("line", "Línea en el TXT");
            foreach (char f in format) gridPreview.Columns.Add("f" + f, f.ToString());

            for (int i = 0; i < Math.Min(500, outRes.OutputRows.Count); i++)
            {
                var cells = new List<object>
                {
                    geo.LatLon[i][0].ToString("F8", CultureInfo.InvariantCulture),
                    geo.LatLon[i][1].ToString("F8", CultureInfo.InvariantCulture),
                    string.Join(sep, outRes.OutputRows[i])
                };
                cells.AddRange(outRes.OutputRows[i]);
                gridPreview.Rows.Add(cells.ToArray());
            }
            gridPreview.ResumeLayout();

            lblStatus.Text = $"{outRes.OutputRows.Count} puntos → {((ProjectedCrs)cboCrs.SelectedItem).Name}" +
                             (geo.Skipped > 0 ? $"  ·  {geo.Skipped} omitidos" : "") +
                             (geo.NoHeight ? "  ·  sin columna de altura: Z = 0" : "") +
                             $"  ·  En Civil 3D: {format} ({(sep == " " ? "space" : "comma")} delimited)";
        }

        private void ConvertAll()
        {
            var uiMap = UiMap();
            string err = ValidateMapping(uiMap);
            if (err != null) { MessageBox.Show(this, err, "Falta mapeo", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (!chkSameFolder.Checked && !Directory.Exists(txtOutFolder.Text))
            {
                MessageBox.Show(this, "Elige una carpeta de salida válida.", "Carpeta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var crs = (ProjectedCrs)cboCrs.SelectedItem;
            string format = (string)cboFormat.SelectedItem;
            string suffix = $"_{format}_EPSG{crs.Epsg}" + (OutDelimiter() == ' ' ? "_space" : "_comma");
            string selected = lstFiles.SelectedItem as string;
            int ok = 0, total = lstFiles.Items.Count;

            Cursor = Cursors.WaitCursor;
            Log($"--- Proyectando {total} archivo(s) a {crs} ---");
            if (chkGeoid.Checked) Log($"    Cota = h − {numGeoid.Value} m");

            foreach (string path in lstFiles.Items)
            {
                try
                {
                    var rows = CsvPointReader.ReadRows(path, InDelimiter(path));
                    if (rows.Count == 0) { Log($"✘ {Path.GetFileName(path)}: archivo vacío"); continue; }

                    bool header; Dictionary<string, int> map;
                    if (path == selected) { header = chkHeader.Checked; map = uiMap; }
                    else
                    {
                        header = rows[0].Count(f => Projection.TryParseAngle(f, out _)) < 2;
                        map = AutoMap(rows, header);
                        if (ValidateMapping(map) != null)
                        {
                            Log($"✘ {Path.GetFileName(path)}: no se reconocieron latitud/longitud. Selecciónalo y asígnalas a mano.");
                            continue;
                        }
                    }

                    var geo = Project(rows, header, map);
                    var outRes = Format(geo);
                    string folder = chkSameFolder.Checked ? Path.GetDirectoryName(path) : txtOutFolder.Text;
                    string outPath = Path.Combine(folder, Path.GetFileNameWithoutExtension(path) + suffix + ".txt");
                    CsvPointReader.WriteTxt(outPath, outRes.OutputRows, OutDelimiter());
                    ok++;

                    Log($"✔ {Path.GetFileName(outPath)}  ({outRes.OutputRows.Count} puntos, {geo.Skipped} omitidos)");
                    if (geo.NoHeight) Log("    Sin columna de altura: Z = 0");
                    foreach (var w in geo.Warnings.Take(20)) Log("    " + w);
                    if (geo.Warnings.Count > 20) Log($"    ... y {geo.Warnings.Count - 20} avisos más");
                }
                catch (Exception ex) { Log($"✘ {Path.GetFileName(path)}: {ex.Message}"); }
            }
            Cursor = Cursors.Default;
            lblStatus.Text = $"Listo: {ok} de {total} archivo(s) proyectados.";
        }

        private void Log(string msg) => txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
    }
}
