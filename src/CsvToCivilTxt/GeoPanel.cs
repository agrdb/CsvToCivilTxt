using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CsvToCivilTxt
{
    /// <summary>
    /// Método 2: CSV con latitud/longitud → proyección a coordenadas planas → TXT para Civil 3D.
    /// Independiente del método 1 (que recibe coordenadas ya planas).
    /// </summary>
    public class GeoPanel : UserControl
    {
        // Entrada
        private readonly ListBox lstFiles = new ListBox();
        private readonly ComboBox cboInDelim = AgrdbTheme.Combo();
        private readonly CheckBox chkHeader = new CheckBox();
        // Columnas
        private readonly Dictionary<string, ComboBox> colCombos = new Dictionary<string, ComboBox>();
        // Proyección
        private readonly ComboBox cboCrs = AgrdbTheme.Combo();
        private readonly CheckBox chkGeoid = new CheckBox();
        private readonly NumericUpDown numGeoid = new NumericUpDown();
        // Salida
        private readonly ComboBox cboFormat = AgrdbTheme.Combo("PENZD", "PNEZD", "PENZ", "PNEZ", "ENZ", "NEZ");
        private readonly ComboBox cboOutDelim = AgrdbTheme.Combo("comma delimited ( , )", "space delimited (espacio)");
        private readonly NumericUpDown numDecimals = new NumericUpDown();
        private readonly NumericUpDown numAutoStart = new NumericUpDown();
        private readonly CheckBox chkSameFolder = new CheckBox();
        private readonly TextBox txtOutFolder = new TextBox();
        private readonly Button btnOutFolder = AgrdbTheme.SecondaryButton("Examinar…");
        // Resultado
        private readonly DataGridView gridPreview = new DataGridView();
        private readonly TextBox txtLog = new TextBox();
        private readonly ActionBar _bar = new ActionBar("Proyectar y exportar TXT", "Abrir carpeta");

        private List<string[]> _rows = new List<string[]>();
        private bool _loading;
        private string _lastOutFolder;

        // Campos de entrada de esta pestaña
        private static readonly string[] Fields = { "P", "LAT", "LON", "H", "D" };
        private static readonly string[] FieldLabels = { "P · Número de punto", "Latitud (°)", "Longitud (°)", "Altura (m)", "D · Descripción" };

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
        private const string EmptyHint = "Agrega CSV con latitud y longitud para empezar.";

        public GeoPanel()
        {
            AutoScaleMode = AutoScaleMode.Inherit;
            BackColor = AgrdbTheme.Bg;
            Font = AgrdbTheme.Body;
            BuildLayout();
            UpdateEnabledState();
        }

        // =====================================================================
        // Interfaz
        // =====================================================================
        private void BuildLayout()
        {
            const int cardW = 336;
            int w = cardW - 2 * CardPanel.Pad;

            var left = new FlowLayoutPanel
            {
                Dock = DockStyle.Left, Width = cardW + 20, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, BackColor = AgrdbTheme.Bg, Padding = new Padding(0, 0, 4, 0)
            };

            // 1 · Archivos
            var cFiles = new CardPanel("Archivos", "CSV con latitud y longitud (decimal o GMS)") { Width = cardW };
            AgrdbTheme.StyleList(lstFiles);
            lstFiles.SelectedIndexChanged += (s, e) => LoadSelectedFile();
            cFiles.Place(Bordered(lstFiles), 0, 0, w, 98);
            var btnAdd = cFiles.Place(AgrdbTheme.SecondaryButton("Examinar…"), 0, 110, 116);
            var btnRemove = cFiles.Place(AgrdbTheme.SecondaryButton("Quitar"), 124, 110, 74);
            var btnClear = cFiles.Place(AgrdbTheme.SecondaryButton("Limpiar"), 206, 110, w - 206);
            btnAdd.Click += (s, e) => BrowseFiles();
            btnRemove.Click += (s, e) =>
            {
                if (lstFiles.SelectedIndex >= 0) lstFiles.Items.RemoveAt(lstFiles.SelectedIndex);
                if (lstFiles.Items.Count > 0) lstFiles.SelectedIndex = 0; else ClearData();
                UpdateEnabledState();
            };
            btnClear.Click += (s, e) => { lstFiles.Items.Clear(); ClearData(); UpdateEnabledState(); };

            cFiles.Place(AgrdbTheme.Label("Separador"), 0, 160 + 7, 100);
            cboInDelim.Items.AddRange(InDelimNames);
            cboInDelim.SelectedIndex = 0;
            cboInDelim.SelectedIndexChanged += (s, e) => LoadSelectedFile();
            cFiles.Place(cboInDelim, 100, 160, w - 100);
            chkHeader.Text = "La primera fila es encabezado";
            AgrdbTheme.StyleCheck(chkHeader);
            chkHeader.CheckedChanged += (s, e) => { if (!_loading) { FillColumnCombos(); RefreshPreview(); } };
            cFiles.Place(chkHeader, 0, 198, w, 24);
            cFiles.Height = cFiles.ContentTop + 222 + CardPanel.Pad;

            // 2 · Columnas
            var cCols = new CardPanel("Columnas", "Ej.: 4.6097, -74.0817 o 4°36'35\"N") { Width = cardW };
            for (int i = 0; i < Fields.Length; i++)
            {
                int y = i * 38;
                cCols.Place(AgrdbTheme.Label(FieldLabels[i]), 0, y + 7, 130);
                var cb = AgrdbTheme.Combo();
                cb.SelectedIndexChanged += (s, e) => { if (!_loading) RefreshPreview(); };
                colCombos[Fields[i]] = cb;
                cCols.Place(cb, 140, y, w - 140);
            }
            cCols.Height = cCols.ContentTop + 4 * 38 + 30 + CardPanel.Pad;

            // 3 · Proyección
            var cProj = new CardPanel("Proyección", "Sistema de coordenadas planas de destino") { Width = cardW };
            cboCrs.Items.AddRange(ProjectedCrs.All.Cast<object>().ToArray());
            cboCrs.SelectedIndex = 0;   // Origen Nacional
            cboCrs.DropDownWidth = 360;
            cboCrs.SelectedIndexChanged += (s, e) => RefreshPreview();
            cProj.Place(cboCrs, 0, 0, w);

            chkGeoid.Text = "Convertir altura elipsoidal a cota";
            AgrdbTheme.StyleCheck(chkGeoid);
            chkGeoid.CheckedChanged += (s, e) => { numGeoid.Enabled = chkGeoid.Checked; RefreshPreview(); };
            cProj.Place(chkGeoid, 0, 40, w, 24);

            cProj.Place(AgrdbTheme.Label("Ondulación N (m)"), 0, 74 + 7, 130);
            AgrdbTheme.StyleNumeric(numGeoid);
            numGeoid.Minimum = -150; numGeoid.Maximum = 150; numGeoid.DecimalPlaces = 3; numGeoid.Increment = 0.1M;
            numGeoid.Enabled = false;
            numGeoid.ValueChanged += (s, e) => RefreshPreview();
            cProj.Place(numGeoid, 140, 74, 96);
            cProj.Place(AgrdbTheme.Label("Cota = h − N, con N del modelo geoidal del sitio", true), 0, 112, w);
            cProj.Height = cProj.ContentTop + 130 + CardPanel.Pad;

            // 4 · Salida
            var cOut = new CardPanel("Salida", "Usa el mismo formato al importar en Civil 3D") { Width = cardW };
            cOut.Place(AgrdbTheme.Label("Formato"), 0, 7, 130);
            cboFormat.SelectedIndex = 0;
            cboFormat.SelectedIndexChanged += (s, e) => RefreshPreview();
            cOut.Place(cboFormat, 140, 0, w - 140);

            cOut.Place(AgrdbTheme.Label("Separador"), 0, 38 + 7, 130);
            cboOutDelim.SelectedIndex = 0;
            cboOutDelim.SelectedIndexChanged += (s, e) => RefreshPreview();
            cOut.Place(cboOutDelim, 140, 38, w - 140);

            cOut.Place(AgrdbTheme.Label("Decimales"), 0, 76 + 7, 130);
            AgrdbTheme.StyleNumeric(numDecimals);
            numDecimals.Minimum = 0; numDecimals.Maximum = 8; numDecimals.Value = 3;
            numDecimals.ValueChanged += (s, e) => RefreshPreview();
            cOut.Place(numDecimals, 140, 76, 72);

            cOut.Place(AgrdbTheme.Label("Numerar desde"), 0, 114 + 7, 130);
            AgrdbTheme.StyleNumeric(numAutoStart);
            numAutoStart.Minimum = 1; numAutoStart.Maximum = 99999999; numAutoStart.Value = 1;
            numAutoStart.ValueChanged += (s, e) => RefreshPreview();
            cOut.Place(numAutoStart, 140, 114, 110);
            cOut.Place(AgrdbTheme.Label("si no hay columna P", true), 140 + 116, 114 + 8, w - 256);

            chkSameFolder.Text = "Guardar junto a cada CSV";
            chkSameFolder.Checked = true;
            AgrdbTheme.StyleCheck(chkSameFolder);
            chkSameFolder.CheckedChanged += (s, e) => UpdateEnabledState();
            cOut.Place(chkSameFolder, 0, 152, w, 24);

            AgrdbTheme.StyleTextBox(txtOutFolder);
            cOut.Place(txtOutFolder, 0, 186, w - 108, 30);
            btnOutFolder.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog { Description = "Carpeta de salida para los TXT" })
                    if (fbd.ShowDialog(this) == DialogResult.OK) txtOutFolder.Text = fbd.SelectedPath;
            };
            cOut.Place(btnOutFolder, w - 100, 185, 100);
            cOut.Height = cOut.ContentTop + 217 + CardPanel.Pad;

            left.Controls.AddRange(new System.Windows.Forms.Control[] { cFiles, cCols, cProj, cOut });

            // ---- Resultado ----
            AgrdbTheme.StyleGrid(gridPreview, true);
            var cPreview = new CardPanel("Vista previa", "Coordenadas de entrada y línea resultante del TXT") { Dock = DockStyle.Fill };
            cPreview.Controls.Add(gridPreview);

            var cLog = new CardPanel("Registro") { Dock = DockStyle.Bottom, Height = 150 };
            AgrdbTheme.StyleLog(txtLog);
            cLog.Controls.Add(txtLog);

            var right = new Panel { Dock = DockStyle.Fill, BackColor = AgrdbTheme.Bg, Padding = new Padding(AgrdbTheme.S16, 0, 0, 0) };
            right.Controls.Add(cPreview);
            right.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = AgrdbTheme.S16, BackColor = AgrdbTheme.Bg });
            right.Controls.Add(cLog);

            // ---- Barra de acción ----
            _bar.Primary.Click += async (s, e) => await ConvertAllAsync();
            _bar.Secondary.Click += (s, e) =>
            {
                if (Directory.Exists(_lastOutFolder)) System.Diagnostics.Process.Start("explorer.exe", _lastOutFolder);
            };
            _bar.SetStatus(EmptyHint);

            Controls.Add(right);
            Controls.Add(left);
            Controls.Add(_bar);
        }

        private static Panel Bordered(System.Windows.Forms.Control inner)
        {
            var frame = new Panel { BackColor = AgrdbTheme.Border, Padding = new Padding(1) };
            inner.Dock = DockStyle.Fill;
            frame.Controls.Add(inner);
            return frame;
        }

        private void UpdateEnabledState()
        {
            txtOutFolder.Enabled = btnOutFolder.Enabled = !chkSameFolder.Checked;
            _bar.Primary.Enabled = lstFiles.Items.Count > 0;
        }

        private void Log(string msg) => txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");

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
            _bar.SetStatus(EmptyHint);
        }

        private static char ResolveDelimiter(char choice, string path) =>
            choice == '\0' ? CsvPointReader.DetectDelimiter(path) : choice;

        private char InDelimiter(string path) => ResolveDelimiter(InDelimChars[cboInDelim.SelectedIndex], path);

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
                if (_rows.Count == 0) { Log($"{Path.GetFileName(path)}: archivo vacío."); _bar.SetStatus("El archivo está vacío.", StatusKind.Warning); return; }

                // Encabezado: si los dos primeros campos de lat/lon no son ángulos, es encabezado
                chkHeader.Checked = _rows[0].Count(f => Projection.TryParseAngle(f, out _)) < 2;
                FillColumnCombos();
                Log($"{Path.GetFileName(path)}: {_rows.Count} filas.");
            }
            catch (Exception ex)
            {
                Log("Error leyendo archivo: " + ex.Message);
                _bar.SetStatus("No se pudo leer el archivo: " + ex.Message + " ¿Está abierto en Excel?", StatusKind.Error);
            }
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
        // Proyección (sin acceso a la interfaz: puede correr en otro hilo)
        // =====================================================================
        private class GeoResult
        {
            public List<string[]> Projected = new List<string[]>();   // P, N, E, Z, D (como texto, para reutilizar el formateador)
            public List<double[]> LatLon = new List<double[]>();
            public List<string> Warnings = new List<string>();
            public int Skipped;
            public bool NoHeight;
        }

        private static GeoResult Project(List<string[]> rows, bool hasHeader, Dictionary<string, int> map,
                                         ProjectedCrs crs, double geoidN)
        {
            var res = new GeoResult();
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
                        ? $"Línea {i + 1}: parecen coordenadas planas, usa «Coordenadas planas»"
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

        /// <summary>Pasa las filas proyectadas por el mismo formateador del método 1.</summary>
        private static CsvPointReader.ConvertResult Format(GeoResult geo, string format, char outDelim, int decimals, int autoStart)
        {
            var opt = new CsvPointReader.ConvertOptions
            {
                HasHeader = false,
                Format = format,
                OutputDelimiter = outDelim,
                Decimals = decimals,
                AutoNumberStart = autoStart,
                ColumnMap = new Dictionary<char, int> { { 'P', 0 }, { 'N', 1 }, { 'E', 2 }, { 'Z', 3 }, { 'D', 4 } }
            };
            return CsvPointReader.Convert(geo.Projected, opt);
        }

        private Dictionary<string, int> UiMap() => colCombos.ToDictionary(kv => kv.Key, kv => kv.Value.SelectedIndex - 1);

        private double GeoidN => chkGeoid.Checked ? (double)numGeoid.Value : 0;

        private static string ValidateMapping(Dictionary<string, int> map)
        {
            if (map["LAT"] < 0) return "Falta asignar la columna de latitud. Elígela en «Columnas».";
            if (map["LON"] < 0) return "Falta asignar la columna de longitud. Elígela en «Columnas».";
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
            if (err != null) { _bar.SetStatus(err, StatusKind.Warning); gridPreview.ResumeLayout(); return; }

            var crs = (ProjectedCrs)cboCrs.SelectedItem;
            string format = (string)cboFormat.SelectedItem;
            var geo = Project(_rows, chkHeader.Checked, map, crs, GeoidN);
            var outRes = Format(geo, format, OutDelimiter(), (int)numDecimals.Value, (int)numAutoStart.Value);
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

            string msg = $"{outRes.OutputRows.Count} puntos → {crs.Name}" +
                         (geo.Skipped > 0 ? $"  ·  {geo.Skipped} omitidos" : "") +
                         (geo.NoHeight ? "  ·  sin columna de altura: Z = 0" : "") +
                         $"  ·  En Civil 3D: {format} ({(sep == " " ? "space" : "comma")} delimited)";
            _bar.SetStatus(msg, geo.Skipped > 0 || geo.NoHeight ? StatusKind.Warning : StatusKind.Info);
        }

        private async Task ConvertAllAsync()
        {
            var uiMap = UiMap();
            string err = ValidateMapping(uiMap);
            if (err != null) { _bar.SetStatus(err, StatusKind.Error); return; }
            if (!chkSameFolder.Checked && !Directory.Exists(txtOutFolder.Text))
            {
                _bar.SetStatus("La carpeta de salida no existe. Elige una con Examinar… o marca «Guardar junto a cada CSV».", StatusKind.Error);
                return;
            }

            // Lectura de la interfaz en el hilo de la UI
            var files = lstFiles.Items.Cast<string>().ToList();
            var crs = (ProjectedCrs)cboCrs.SelectedItem;
            double geoidN = GeoidN;
            bool uiHeader = chkHeader.Checked;
            string selected = lstFiles.SelectedItem as string;
            char inChoice = InDelimChars[cboInDelim.SelectedIndex];
            string format = (string)cboFormat.SelectedItem;
            char outDelim = OutDelimiter();
            int decimals = (int)numDecimals.Value;
            int autoStart = (int)numAutoStart.Value;
            bool sameFolder = chkSameFolder.Checked;
            string outFolder = txtOutFolder.Text;
            string suffix = $"_{format}_EPSG{crs.Epsg}" + (outDelim == ' ' ? "_space" : "_comma");

            var log = new Progress<string>(Log);
            var step = new Progress<int>(i => _bar.ReportProgress(i, $"Proyectando {Math.Min(i + 1, files.Count)} de {files.Count}…"));

            _bar.StartProgress(files.Count);
            Log($"--- Proyectando {files.Count} archivo(s) a {crs} ---");
            if (geoidN != 0) Log($"    Cota = h − {geoidN.ToString(CultureInfo.InvariantCulture)} m");

            int ok = await Task.Run(() =>
            {
                int done = 0;
                IProgress<string> L = log;
                IProgress<int> P = step;
                for (int i = 0; i < files.Count; i++)
                {
                    string path = files[i];
                    P.Report(i);
                    try
                    {
                        var rows = CsvPointReader.ReadRows(path, ResolveDelimiter(inChoice, path));
                        if (rows.Count == 0) { L.Report($"✘ {Path.GetFileName(path)}: archivo vacío"); continue; }

                        bool header; Dictionary<string, int> map;
                        if (path == selected) { header = uiHeader; map = uiMap; }
                        else
                        {
                            header = rows[0].Count(f => Projection.TryParseAngle(f, out _)) < 2;
                            map = AutoMap(rows, header);
                            if (ValidateMapping(map) != null)
                            {
                                L.Report($"✘ {Path.GetFileName(path)}: no se reconocieron latitud/longitud. Selecciónalo y asígnalas a mano.");
                                continue;
                            }
                        }

                        var geo = Project(rows, header, map, crs, geoidN);
                        var outRes = Format(geo, format, outDelim, decimals, autoStart);
                        string folder = sameFolder ? Path.GetDirectoryName(path) : outFolder;
                        string outPath = Path.Combine(folder, Path.GetFileNameWithoutExtension(path) + suffix + ".txt");
                        CsvPointReader.WriteTxt(outPath, outRes.OutputRows, outDelim);
                        done++;
                        _lastOutFolder = folder;

                        L.Report($"✔ {Path.GetFileName(outPath)}  ({outRes.OutputRows.Count} puntos, {geo.Skipped} omitidos)");
                        if (geo.NoHeight) L.Report("    Sin columna de altura: Z = 0");
                        foreach (var w in geo.Warnings.Take(20)) L.Report("    " + w);
                        if (geo.Warnings.Count > 20) L.Report($"    ... y {geo.Warnings.Count - 20} avisos más");
                    }
                    catch (Exception ex) { L.Report($"✘ {Path.GetFileName(path)}: {ex.Message}"); }
                }
                return done;
            });

            _bar.EndProgress();
            int total = files.Count;
            if (ok == total) _bar.SetStatus($"Listo: {ok} archivo(s) proyectados a {crs.Name}.", StatusKind.Success);
            else if (ok > 0) _bar.SetStatus($"{ok} de {total} archivo(s) proyectados. Revisa el registro para los demás.", StatusKind.Warning);
            else _bar.SetStatus("No se proyectó ningún archivo. Revisa el registro.", StatusKind.Error);
            _bar.Secondary.Visible = ok > 0 && Directory.Exists(_lastOutFolder);
        }
    }
}
