using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CsvToCivilTxt
{
    /// <summary>Ventana principal. La interfaz se arma en código (sin diseñador) para que sea un solo archivo fácil de editar.</summary>
    public class MainForm : Form
    {
        // Archivos
        private readonly ListBox lstFiles = new ListBox();
        // Lectura
        private readonly ComboBox cboInDelim = new ComboBox();
        private readonly CheckBox chkHeader = new CheckBox();
        // Mapeo de columnas
        private readonly Dictionary<char, ComboBox> colCombos = new Dictionary<char, ComboBox>();
        // Salida
        private readonly ComboBox cboFormat = new ComboBox();
        private readonly ComboBox cboOutDelim = new ComboBox();
        private readonly NumericUpDown numDecimals = new NumericUpDown();
        private readonly NumericUpDown numAutoStart = new NumericUpDown();
        private readonly CheckBox chkSameFolder = new CheckBox();
        private readonly TextBox txtOutFolder = new TextBox();
        private readonly Button btnOutFolder = new Button();
        // Vista previa y log
        private readonly DataGridView gridSource = new DataGridView();
        private readonly DataGridView gridPreview = new DataGridView();
        private readonly TextBox txtLog = new TextBox();
        private readonly Label lblStatus = new Label();
        private readonly Button btnConvert = new Button();

        private readonly TabControl _mainTabs = new TabControl();
        private readonly GeoPanel _geoPanel = new GeoPanel { Dock = DockStyle.Fill };

        private List<string[]> _rows = new List<string[]>();
        private bool _loading;

        private static readonly string[] InDelimNames = { "Automático", "Punto y coma ( ; )", "Coma ( , )", "Tabulador", "Espacio" };
        private static readonly char[] InDelimChars = { '\0', ';', ',', '\t', ' ' };

        public MainForm()
        {
            Text = "CSV → TXT para Civil 3D  ·  AGRDB";
            Size = new Size(1150, 800);
            MinimumSize = new Size(950, 680);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) =>
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (_mainTabs.SelectedIndex == 1) _geoPanel.AddFiles(files); else AddFiles(files);
            };

            BuildLayout();
            UpdateEnabledState();
        }

        // =====================================================================
        // Interfaz
        // =====================================================================
        private void BuildLayout()
        {
            // ---- Panel izquierdo (opciones) ----
            var left = new FlowLayoutPanel
            {
                Dock = DockStyle.Left, Width = 340, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(8)
            };

            // 1. Archivos
            var gFiles = Group("1. Archivos CSV (o arrástralos a la ventana)", 190);
            lstFiles.SetBounds(10, 22, 300, 120);
            lstFiles.HorizontalScrollbar = true;
            lstFiles.SelectedIndexChanged += (s, e) => LoadSelectedFile();
            var btnAdd = MakeButton("Agregar...", 10, 150, 95, (s, e) => BrowseFiles());
            var btnRemove = MakeButton("Quitar", 112, 150, 95, (s, e) =>
            {
                if (lstFiles.SelectedIndex >= 0) lstFiles.Items.RemoveAt(lstFiles.SelectedIndex);
                if (lstFiles.Items.Count > 0) lstFiles.SelectedIndex = 0; else ClearData();
                UpdateEnabledState();
            });
            var btnClear = MakeButton("Limpiar", 214, 150, 95, (s, e) => { lstFiles.Items.Clear(); ClearData(); UpdateEnabledState(); });
            gFiles.Controls.AddRange(new Control[] { lstFiles, btnAdd, btnRemove, btnClear });

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
            var gCols = Group("3. Columnas (qué columna del CSV es cada dato)", 175);
            string[] labels = { "P  Número punto", "N  Norte (Y)", "E  Este (X)", "Z  Cota", "D  Descripción" };
            char[] keys = { 'P', 'N', 'E', 'Z', 'D' };
            for (int i = 0; i < keys.Length; i++)
            {
                int y = 22 + i * 29;
                gCols.Controls.Add(MakeLabel(labels[i], 10, y + 3));
                var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                cb.SetBounds(130, y, 180, 23);
                cb.SelectedIndexChanged += (s, e) => { if (!_loading) RefreshPreview(); };
                colCombos[keys[i]] = cb;
                gCols.Controls.Add(cb);
            }

            // 4. Salida
            var gOut = Group("4. Formato de salida (igual al de Civil 3D)", 205);
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

            left.Controls.AddRange(new Control[] { gFiles, gRead, gCols, gOut });

            // ---- Barra inferior ----
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8) };
            btnConvert.Text = "Convertir a TXT";
            btnConvert.Font = new Font(Font, FontStyle.Bold);
            btnConvert.Dock = DockStyle.Right;
            btnConvert.Width = 180;
            btnConvert.Click += (s, e) => ConvertAll();
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Text = "Agrega uno o varios CSV para empezar.";
            bottom.Controls.Add(lblStatus);
            bottom.Controls.Add(btnConvert);

            // ---- Zona derecha: CSV original | TXT resultante | log ----
            SetupGrid(gridSource);
            SetupGrid(gridPreview);
            gridPreview.DefaultCellStyle.Font = new Font("Consolas", 9F);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var tpPreview = new TabPage("Vista previa del TXT");
            var tpSource = new TabPage("CSV original");
            tpPreview.Controls.Add(gridPreview);
            tpSource.Controls.Add(gridSource);
            tabs.TabPages.Add(tpPreview);
            tabs.TabPages.Add(tpSource);

            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Dock = DockStyle.Fill;
            txtLog.Font = new Font("Consolas", 8.5F);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            split.Panel1.Controls.Add(tabs);
            split.Panel2.Controls.Add(txtLog);
            split.Panel2.Padding = new Padding(0, 4, 0, 0);

            var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 8, 8, 0) };
            right.Controls.Add(split);

            // ---- Pestañas principales: dos métodos separados ----
            var tabPlanas = new TabPage("1. CSV → TXT (coordenadas planas)");
            tabPlanas.Controls.Add(right);
            tabPlanas.Controls.Add(left);
            tabPlanas.Controls.Add(bottom);

            var tabGeo = new TabPage("2. Geográficas (lat/long) → planas → TXT");
            tabGeo.Controls.Add(_geoPanel);

            _mainTabs.Dock = DockStyle.Fill;
            _mainTabs.TabPages.Add(tabPlanas);
            _mainTabs.TabPages.Add(tabGeo);
            Controls.Add(_mainTabs);

            Shown += (s, e) => { try { split.SplitterDistance = (int)(split.Height * 0.72); } catch { } };
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

        private static void SetupGrid(DataGridView g)
        {
            g.Dock = DockStyle.Fill;
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.RowHeadersVisible = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            g.BackgroundColor = SystemColors.Window;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        }

        private void UpdateEnabledState()
        {
            txtOutFolder.Enabled = btnOutFolder.Enabled = !chkSameFolder.Checked;
            btnConvert.Enabled = lstFiles.Items.Count > 0;
        }

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

        private void AddFiles(IEnumerable<string> paths)
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
            lblStatus.Text = "Agrega uno o varios CSV para empezar.";
        }

        private char CurrentInDelimiter(string path)
        {
            char c = InDelimChars[cboInDelim.SelectedIndex];
            return c == '\0' ? CsvPointReader.DetectDelimiter(path) : c;
        }

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
                if (_rows.Count == 0) { Log($"{Path.GetFileName(path)}: archivo vacío."); return; }

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
                if (colCombos[f].SelectedIndex <= 0) return $"Asigna la columna {f} (requerida por el formato {format}).";
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
                lblStatus.Text = "⚠ " + err;
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

            lblStatus.Text = $"{res.OutputRows.Count} puntos válidos" +
                             (res.Skipped > 0 ? $"  ·  {res.Skipped} filas omitidas (ver registro)" : "") +
                             $"  ·  En Civil 3D usa: {format} ({(sep == " " ? "space" : "comma")} delimited)";
        }

        private void ConvertAll()
        {
            string format = (string)cboFormat.SelectedItem;
            string err = ValidateMapping(format);
            if (err != null) { MessageBox.Show(this, err, "Falta mapeo", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (!chkSameFolder.Checked && !Directory.Exists(txtOutFolder.Text))
            {
                MessageBox.Show(this, "Elige una carpeta de salida válida.", "Carpeta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // El archivo seleccionado usa el mapeo que el usuario ajustó en pantalla.
            // Los demás (pueden venir de otro software) se detectan cada uno por su cuenta.
            var uiMap = colCombos.ToDictionary(kv => kv.Key, kv => kv.Value.SelectedIndex - 1);
            bool uiHeader = chkHeader.Checked;
            string selected = lstFiles.SelectedItem as string;
            int ok = 0, total = lstFiles.Items.Count;
            string suffix = "_" + format + (CurrentOutDelimiter() == ' ' ? "_space" : "_comma");

            Cursor = Cursors.WaitCursor;
            Log($"--- Convirtiendo {total} archivo(s) a {format} ---");
            foreach (string path in lstFiles.Items)
            {
                try
                {
                    char inDelim = CurrentInDelimiter(path);
                    var rows = CsvPointReader.ReadRows(path, inDelim);
                    if (rows.Count == 0) { Log($"✘ {Path.GetFileName(path)}: archivo vacío"); continue; }
                    var opt = BuildOptions(inDelim);

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
                            Log($"✘ {Path.GetFileName(path)}: no se reconocieron las columnas {missing}. Selecciónalo y asígnalas a mano.");
                            continue;
                        }
                        Log($"  {Path.GetFileName(path)}: mapeo automático " +
                            string.Join(" ", opt.ColumnMap.Select(kv => $"{kv.Key}=" + (kv.Value < 0 ? "-" : "Col" + (kv.Value + 1)))));
                    }

                    var res = CsvPointReader.Convert(rows, opt);
                    string folder = chkSameFolder.Checked ? Path.GetDirectoryName(path) : txtOutFolder.Text;
                    string outPath = Path.Combine(folder, Path.GetFileNameWithoutExtension(path) + suffix + ".txt");

                    CsvPointReader.WriteTxt(outPath, res.OutputRows, opt.OutputDelimiter);
                    ok++;

                    Log($"✔ {Path.GetFileName(outPath)}  ({res.OutputRows.Count} puntos, {res.Skipped} omitidos)");
                    foreach (var w in res.Warnings.Take(20)) Log("    " + w);
                    if (res.Warnings.Count > 20) Log($"    ... y {res.Warnings.Count - 20} avisos más");
                }
                catch (Exception ex)
                {
                    Log($"✘ {Path.GetFileName(path)}: {ex.Message}");
                }
            }
            Cursor = Cursors.Default;

            lblStatus.Text = $"Listo: {ok} de {total} archivo(s) convertidos.";
            if (ok > 0 && !chkSameFolder.Checked &&
                MessageBox.Show(this, "¿Abrir la carpeta de salida?", "Listo", MessageBoxButtons.YesNo) == DialogResult.Yes)
                System.Diagnostics.Process.Start("explorer.exe", txtOutFolder.Text);
        }

        private void Log(string msg) => txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
    }
}
