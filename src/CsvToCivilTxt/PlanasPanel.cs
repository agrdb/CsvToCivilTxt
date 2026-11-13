using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CsvToCivilTxt
{
    /// <summary>
    /// Método 1: CSV con coordenadas planas → TXT en formato de Civil 3D.
    /// Flujo: Entrada (archivos) → Configuración (columnas, salida) → Acción (Convertir) → Resultado (vista previa, registro).
    /// </summary>
    public class PlanasPanel : UserControl
    {
        // Entrada
        private readonly ListBox lstFiles = new ListBox();
        private readonly ComboBox cboInDelim = AgrdbTheme.Combo();
        private readonly CheckBox chkHeader = new CheckBox();
        // Mapeo de columnas
        private readonly Dictionary<char, ComboBox> colCombos = new Dictionary<char, ComboBox>();
        // Salida
        private readonly ComboBox cboFormat = AgrdbTheme.Combo("PENZD", "PNEZD", "PENZ", "PNEZ", "ENZ", "NEZ");
        private readonly ComboBox cboOutDelim = AgrdbTheme.Combo("comma delimited ( , )", "space delimited (espacio)");
        private readonly NumericUpDown numDecimals = new NumericUpDown();
        private readonly NumericUpDown numAutoStart = new NumericUpDown();
        private readonly CheckBox chkSameFolder = new CheckBox();
        private readonly TextBox txtOutFolder = new TextBox();
        private readonly Button btnOutFolder = AgrdbTheme.SecondaryButton("Examinar…");
        // Resultado
        private readonly DataGridView gridSource = new DataGridView();
        private readonly DataGridView gridPreview = new DataGridView();
        private readonly TextBox txtLog = new TextBox();
        private readonly ActionBar _bar = new ActionBar("Convertir a TXT", "Abrir carpeta");

        private List<string[]> _rows = new List<string[]>();
        private bool _loading;
        private string _lastOutFolder;

        private static readonly string[] InDelimNames = { "Automático", "Punto y coma ( ; )", "Coma ( , )", "Tabulador", "Espacio" };
        private static readonly char[] InDelimChars = { '\0', ';', ',', '\t', ' ' };
        private const string EmptyHint = "Agrega uno o varios CSV para empezar.";

        public PlanasPanel()
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
            int w = cardW - 2 * CardPanel.Pad;   // ancho útil dentro de la tarjeta

            // ---- Columna izquierda: Entrada + Configuración ----
            var left = new FlowLayoutPanel
            {
                Dock = DockStyle.Left, Width = cardW + 20, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, BackColor = AgrdbTheme.Bg, Padding = new Padding(0, 0, 4, 0)
            };

            // 1 · Archivos
            var cFiles = new CardPanel("Archivos", "Arrastra los CSV a la ventana o usa Examinar…") { Width = cardW };
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
            var cCols = new CardPanel("Columnas", "Qué columna del CSV corresponde a cada dato") { Width = cardW };
            string[] labels = { "P · Número de punto", "N · Norte (Y)", "E · Este (X)", "Z · Cota (m)", "D · Descripción" };
            char[] keys = { 'P', 'N', 'E', 'Z', 'D' };
            for (int i = 0; i < keys.Length; i++)
            {
                int y = i * 38;
                cCols.Place(AgrdbTheme.Label(labels[i]), 0, y + 7, 130);
                var cb = AgrdbTheme.Combo();
                cb.SelectedIndexChanged += (s, e) => { if (!_loading) RefreshPreview(); };
                colCombos[keys[i]] = cb;
                cCols.Place(cb, 140, y, w - 140);
            }
            cCols.Height = cCols.ContentTop + 4 * 38 + 30 + CardPanel.Pad;

            // 3 · Salida
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

            left.Controls.AddRange(new Control[] { cFiles, cCols, cOut });

            // ---- Columna derecha: Resultado ----
            AgrdbTheme.StyleGrid(gridPreview, true);
            AgrdbTheme.StyleGrid(gridSource, false);

            var cPreview = new CardPanel("Vista previa") { Dock = DockStyle.Fill };
            var gridHost = new Panel { Dock = DockStyle.Fill, BackColor = AgrdbTheme.Surface };
            gridHost.Controls.Add(gridPreview);
            gridHost.Controls.Add(gridSource);
            var seg = new SegmentBar();
            seg.Add("Archivo TXT", gridPreview);
            seg.Add("CSV original", gridSource);
            var segStrip = new Panel { Dock = DockStyle.Top, Height = AgrdbTheme.ControlHeight + AgrdbTheme.S12, BackColor = AgrdbTheme.Surface };
            segStrip.Controls.Add(seg);
            cPreview.Controls.Add(gridHost);
            cPreview.Controls.Add(segStrip);

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

        /// <summary>Envuelve un control sin borde en un marco de 1 px color Border.</summary>
        private static Panel Bordered(Control inner)
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
        // Archivos
        // =====================================================================
        private void BrowseFiles()
        {
            using (var ofd = new OpenFileDialog
            {
                Title = "Selecciona los CSV de puntos",
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
            gridSource.Columns.Clear();
            gridPreview.Columns.Clear();
            foreach (var cb in colCombos.Values) cb.Items.Clear();
            _bar.SetStatus(EmptyHint);
        }

        private static char ResolveDelimiter(char choice, string path) =>
            choice == '\0' ? CsvPointReader.DetectDelimiter(path) : choice;

        private char CurrentInDelimiter(string path) => ResolveDelimiter(InDelimChars[cboInDelim.SelectedIndex], path);

        private char CurrentOutDelimiter() => cboOutDelim.SelectedIndex == 1 ? ' ' : ',';

        /// <summary>Carga el CSV seleccionado: lo lee, detecta encabezado y propone el mapeo de columnas.</summary>
        private void LoadSelectedFile()
        {
            if (_loading) return;
            if (!(lstFiles.SelectedItem is string path)) return;

            try
            {
                _loading = true;
                char delim = CurrentInDelimiter(path);
                _rows = CsvPointReader.ReadRows(path, delim);
                if (_rows.Count == 0) { Log($"{Path.GetFileName(path)}: archivo vacío."); _bar.SetStatus("El archivo está vacío.", StatusKind.Warning); return; }

                chkHeader.Checked = CsvPointReader.LooksLikeHeader(_rows[0]);
                FillColumnCombos();
                FillSourceGrid();

                string dName = delim == '\t' ? "tabulador" : delim == ' ' ? "espacio" : $"'{delim}'";
                Log($"{Path.GetFileName(path)}: {_rows.Count} filas, separador {dName}, " +
                    $"{(chkHeader.Checked ? "con" : "sin")} encabezado.");
            }
            catch (Exception ex)
            {
                Log("Error leyendo archivo: " + ex.Message);
                _bar.SetStatus("No se pudo leer el archivo: " + ex.Message + " ¿Está abierto en Excel?", StatusKind.Error);
            }
            finally
            {
                _loading = false;
            }
            RefreshPreview();
        }

        /// <summary>Llena los combos con las columnas del CSV y adivina cuál es P, N, E, Z, D.</summary>
        private void FillColumnCombos()
        {
            if (_rows.Count == 0) return;
            int nCols = _rows.Max(r => r.Length);
            string[] header = chkHeader.Checked ? _rows[0] : new string[0];

            string[] names = Enumerable.Range(0, nCols)
                .Select(i => $"Col {i + 1}" + (i < header.Length && header[i] != "" ? $": {header[i]}" : ""))
                .ToArray();

            bool wasLoading = _loading;
            _loading = true;
            foreach (var kv in colCombos)
            {
                kv.Value.Items.Clear();
                kv.Value.Items.Add("(ninguna)");
                kv.Value.Items.AddRange(names);

                int guess = chkHeader.Checked ? CsvPointReader.GuessColumn(header, kv.Key.ToString()) : -1;
                if (guess < 0) guess = DefaultColumnByPosition(kv.Key, nCols);
                kv.Value.SelectedIndex = guess + 1; // +1 por "(ninguna)"
            }
            _loading = wasLoading;
        }

        /// <summary>Mapeo automático de un archivo (para lotes con CSV de distintos programas).</summary>
        private static Dictionary<char, int> AutoMap(List<string[]> rows, bool hasHeader)
        {
            int nCols = rows.Max(r => r.Length);
            string[] header = hasHeader ? rows[0] : new string[0];
            var map = new Dictionary<char, int>();
            foreach (char f in "PNEZD")
            {
                int g = hasHeader ? CsvPointReader.GuessColumn(header, f.ToString()) : -1;
                // Con encabezado reconocible pero sin esa columna, no inventar posición
                bool anyHeaderMatch = hasHeader && "NEZ".Any(k => CsvPointReader.GuessColumn(header, k.ToString()) >= 0);
                map[f] = g >= 0 ? g : (anyHeaderMatch ? -1 : DefaultColumnByPosition(f, nCols));
            }
            return map;
        }

        /// <summary>Sin encabezado reconocible se asume el orden típico P, N, E, Z, D (PNEZD).</summary>
        private static int DefaultColumnByPosition(char field, int nCols)
        {
            int idx = "PNEZD".IndexOf(field);
            return idx < nCols ? idx : -1;
        }

        private void FillSourceGrid()
        {
            gridSource.SuspendLayout();
            gridSource.Columns.Clear();
            gridSource.Rows.Clear();
            int nCols = _rows.Max(r => r.Length);
            for (int i = 0; i < nCols; i++) gridSource.Columns.Add("c" + i, "Col " + (i + 1));
            foreach (var r in _rows.Take(500)) gridSource.Rows.Add(r.Cast<object>().ToArray());
            gridSource.ResumeLayout();
        }

        // =====================================================================
        // Conversión
        // =====================================================================
        private CsvPointReader.ConvertOptions BuildOptions(char inDelim)
        {
            var opt = new CsvPointReader.ConvertOptions
            {
                InputDelimiter = inDelim,
                HasHeader = chkHeader.Checked,
                Format = (string)cboFormat.SelectedItem,
                OutputDelimiter = CurrentOutDelimiter(),
                Decimals = (int)numDecimals.Value,
                AutoNumberStart = (int)numAutoStart.Value
            };
            foreach (var kv in colCombos)
                opt.ColumnMap[kv.Key] = kv.Value.SelectedIndex - 1; // -1 = ninguna
            return opt;
        }

        /// <summary>Valida que las columnas que pide el formato estén asignadas. P y D son opcionales.</summary>
        private string ValidateMapping(string format)
        {
            foreach (char f in format)
            {
                if (f == 'P' || f == 'D') continue;
                if (colCombos[f].SelectedIndex <= 0) return $"Falta asignar la columna {f}: el formato {format} la necesita. Elígela en «Columnas».";
            }
            return null;
        }

        private void RefreshPreview()
        {
            if (_loading || _rows.Count == 0 || !(lstFiles.SelectedItem is string path)) return;

            gridPreview.SuspendLayout();
            gridPreview.Columns.Clear();
            gridPreview.Rows.Clear();

            string format = (string)cboFormat.SelectedItem;
            string err = ValidateMapping(format);
            if (err != null)
            {
                _bar.SetStatus(err, StatusKind.Warning);
                gridPreview.ResumeLayout();
                return;
            }

            var res = CsvPointReader.Convert(_rows, BuildOptions(CurrentInDelimiter(path)));

            // Columna con la línea tal cual quedará en el TXT + columnas separadas
            gridPreview.Columns.Add("line", "Línea en el TXT");
            foreach (char f in format) gridPreview.Columns.Add("f" + f, f.ToString());
            string sep = CurrentOutDelimiter().ToString();
            foreach (var r in res.OutputRows.Take(500))
            {
                var cells = new List<object> { string.Join(sep, r) };
                cells.AddRange(r);
                gridPreview.Rows.Add(cells.ToArray());
            }
            gridPreview.ResumeLayout();

            string msg = $"{res.OutputRows.Count} puntos válidos" +
                         (res.Skipped > 0 ? $"  ·  {res.Skipped} filas omitidas (ver registro)" : "") +
                         $"  ·  En Civil 3D usa: {format} ({(sep == " " ? "space" : "comma")} delimited)";
            _bar.SetStatus(msg, res.Skipped > 0 ? StatusKind.Warning : StatusKind.Info);
        }

        private async Task ConvertAllAsync()
        {
            string format = (string)cboFormat.SelectedItem;
            string err = ValidateMapping(format);
            if (err != null) { _bar.SetStatus(err, StatusKind.Error); return; }

            if (!chkSameFolder.Checked && !Directory.Exists(txtOutFolder.Text))
            {
                _bar.SetStatus("La carpeta de salida no existe. Elige una con Examinar… o marca «Guardar junto a cada CSV».", StatusKind.Error);
                return;
            }

            // Todo lo que viene de la interfaz se lee aquí, en el hilo de la UI.
            // El archivo seleccionado usa el mapeo ajustado en pantalla; los demás se detectan cada uno.
            var files = lstFiles.Items.Cast<string>().ToList();
            var uiMap = colCombos.ToDictionary(kv => kv.Key, kv => kv.Value.SelectedIndex - 1);
            bool uiHeader = chkHeader.Checked;
            string selected = lstFiles.SelectedItem as string;
            char inChoice = InDelimChars[cboInDelim.SelectedIndex];
            char outDelim = CurrentOutDelimiter();
            int decimals = (int)numDecimals.Value;
            int autoStart = (int)numAutoStart.Value;
            bool sameFolder = chkSameFolder.Checked;
            string outFolder = txtOutFolder.Text;
            string suffix = "_" + format + (outDelim == ' ' ? "_space" : "_comma");

            var log = new Progress<string>(Log);
            var step = new Progress<int>(i => _bar.ReportProgress(i, $"Convirtiendo {Math.Min(i + 1, files.Count)} de {files.Count}…"));

            _bar.StartProgress(files.Count);
            Log($"--- Convirtiendo {files.Count} archivo(s) a {format} ---");

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
                        char inDelim = ResolveDelimiter(inChoice, path);
                        var rows = CsvPointReader.ReadRows(path, inDelim);
                        if (rows.Count == 0) { L.Report($"✘ {Path.GetFileName(path)}: archivo vacío"); continue; }

                        var opt = new CsvPointReader.ConvertOptions
                        {
                            InputDelimiter = inDelim, Format = format, OutputDelimiter = outDelim,
                            Decimals = decimals, AutoNumberStart = autoStart
                        };

                        if (path == selected)
                        {
                            opt.ColumnMap = uiMap;
                            opt.HasHeader = uiHeader;
                        }
                        else
                        {
                            opt.HasHeader = CsvPointReader.LooksLikeHeader(rows[0]);
                            opt.ColumnMap = AutoMap(rows, opt.HasHeader);
                            string missing = new string(format.Where(f => f != 'P' && f != 'D' && opt.ColumnMap[f] < 0).ToArray());
                            if (missing.Length > 0)
                            {
                                L.Report($"✘ {Path.GetFileName(path)}: no se reconocieron las columnas {missing}. Selecciónalo y asígnalas a mano.");
                                continue;
                            }
                            L.Report($"  {Path.GetFileName(path)}: mapeo automático " +
                                string.Join(" ", opt.ColumnMap.Select(kv => $"{kv.Key}=" + (kv.Value < 0 ? "-" : "Col" + (kv.Value + 1)))));
                        }

                        var res = CsvPointReader.Convert(rows, opt);
                        string folder = sameFolder ? Path.GetDirectoryName(path) : outFolder;
                        string outPath = Path.Combine(folder, Path.GetFileNameWithoutExtension(path) + suffix + ".txt");

                        CsvPointReader.WriteTxt(outPath, res.OutputRows, opt.OutputDelimiter);
                        done++;
                        _lastOutFolder = folder;

                        L.Report($"✔ {Path.GetFileName(outPath)}  ({res.OutputRows.Count} puntos, {res.Skipped} omitidos)");
                        foreach (var w in res.Warnings.Take(20)) L.Report("    " + w);
                        if (res.Warnings.Count > 20) L.Report($"    ... y {res.Warnings.Count - 20} avisos más");
                    }
                    catch (Exception ex)
                    {
                        L.Report($"✘ {Path.GetFileName(path)}: {ex.Message}");
                    }
                }
                return done;
            });

            _bar.EndProgress();
            int total = files.Count;
            if (ok == total) _bar.SetStatus($"Listo: {ok} archivo(s) convertidos.", StatusKind.Success);
            else if (ok > 0) _bar.SetStatus($"{ok} de {total} archivo(s) convertidos. Revisa el registro para los demás.", StatusKind.Warning);
            else _bar.SetStatus("No se convirtió ningún archivo. Revisa el registro.", StatusKind.Error);
            _bar.Secondary.Visible = ok > 0 && Directory.Exists(_lastOutFolder);
        }
    }
}
