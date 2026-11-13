using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace CsvToCivilTxt
{
    /// <summary>
    /// Tokens de diseño AGRDB y estilos reutilizables para WinForms.
    /// Las vistas no deben usar colores ni fuentes sueltos: todo sale de aquí.
    /// </summary>
    public static class AgrdbTheme
    {
        // ---------------- Colores (tema claro) ----------------
        public static readonly Color Bg = ColorTranslator.FromHtml("#F6F6F9");
        public static readonly Color Surface = ColorTranslator.FromHtml("#FFFFFF");
        public static readonly Color Border = ColorTranslator.FromHtml("#E2E2EA");
        public static readonly Color Text = ColorTranslator.FromHtml("#1F1F2B");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#6B6B7B");
        public static readonly Color Primary = ColorTranslator.FromHtml("#6A4DBA");
        public static readonly Color PrimaryHover = ColorTranslator.FromHtml("#5B3FA8");
        public static readonly Color Accent = ColorTranslator.FromHtml("#8B6FD6");
        public static readonly Color AccentSoft = ColorTranslator.FromHtml("#EFEAFB");
        public static readonly Color Success = ColorTranslator.FromHtml("#1E8E5A");
        public static readonly Color SuccessSoft = ColorTranslator.FromHtml("#E6F5EC");
        public static readonly Color Warning = ColorTranslator.FromHtml("#B86A12");
        public static readonly Color WarningSoft = ColorTranslator.FromHtml("#FDF0E3");
        public static readonly Color Error = ColorTranslator.FromHtml("#C93A3A");
        public static readonly Color ErrorSoft = ColorTranslator.FromHtml("#FBE9E9");

        // ---------------- Tipografía ----------------
        // 1 pt = 1.333 px a 96 dpi: 20px≈15pt, 14px≈10.5pt, 13px≈9.75pt, 12px≈9pt
        public static readonly Font Body = new Font("Segoe UI", 9.75F);
        public static readonly Font BodySemibold = new Font("Segoe UI Semibold", 9.75F);
        public static readonly Font Title = new Font("Segoe UI Semibold", 15F);
        public static readonly Font Section = new Font("Segoe UI Semibold", 10.5F);
        public static readonly Font Help = new Font("Segoe UI", 9F);
        public static readonly Font Footer = new Font("Segoe UI", 8.25F);
        public static readonly Font Mono = new Font("Consolas", 9.75F);
        public static readonly Font MonoSmall = new Font("Consolas", 9F);

        // ---------------- Espaciado (escala 4 · 8 · 12 · 16 · 24 · 32) ----------------
        public const int S4 = 4, S8 = 8, S12 = 12, S16 = 16, S24 = 24, S32 = 32;
        public const int ControlHeight = 32;
        public const int PrimaryHeight = 36;

        // ---------------- Icono y versión ----------------
        private static Icon _appIcon;

        /// <summary>Icono de la app (recurso incrustado Assets/app.ico).</summary>
        public static Icon AppIcon
        {
            get
            {
                if (_appIcon != null) return _appIcon;
                var asm = Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("CsvToCivilTxt.Assets.app.ico"))
                    _appIcon = s != null ? new Icon(s) : SystemIcons.Application;
                return _appIcon;
            }
        }

        /// <summary>Imagen del icono a un tamaño concreto (toma la capa más cercana del .ico).</summary>
        public static Bitmap AppIconImage(int size) => new Icon(AppIcon, size, size).ToBitmap();

        public static string Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return $"v{v.Major}.{v.Minor}.{v.Build}";
            }
        }

        // ---------------- Estilos de controles ----------------
        public static Button PrimaryButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Height = PrimaryHeight,
                Font = BodySemibold,
                ForeColor = Color.White,
                BackColor = Primary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Padding = new Padding(S16, 0, S16, 0),
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = PrimaryHover;
            b.FlatAppearance.MouseDownBackColor = PrimaryHover;
            // Deshabilitado: WinForms pinta el texto gris sobre el fondo; se aclara el fondo para que se lea como inactivo
            b.EnabledChanged += (s, e) => b.BackColor = b.Enabled ? Primary : Blend(Primary, Bg, 0.55);
            return b;
        }

        public static Button SecondaryButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Height = ControlHeight,
                Font = Body,
                ForeColor = Text,
                BackColor = Surface,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Bg;
            b.FlatAppearance.MouseDownBackColor = Border;
            return b;
        }

        public static Label Label(string text, bool muted = false) => new Label
        {
            Text = text,
            AutoSize = true,
            Font = muted ? Help : Body,
            ForeColor = muted ? TextMuted : Text,
            BackColor = Color.Transparent
        };

        public static ComboBox Combo(params object[] items)
        {
            var c = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = Body,
                BackColor = Surface,
                ForeColor = Text
            };
            if (items.Length > 0) c.Items.AddRange(items);
            return c;
        }

        public static void StyleCombo(ComboBox c)
        {
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.Font = Body;
            c.BackColor = Surface;
            c.ForeColor = Text;
        }

        public static void StyleNumeric(NumericUpDown n)
        {
            n.BorderStyle = BorderStyle.FixedSingle;
            n.Font = Body;
            n.BackColor = Surface;
            n.ForeColor = Text;
        }

        public static void StyleTextBox(TextBox t)
        {
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = Body;
            t.BackColor = Surface;
            t.ForeColor = Text;
        }

        public static void StyleCheck(CheckBox c)
        {
            c.Font = Body;
            c.ForeColor = Text;
            c.BackColor = Color.Transparent;
            c.FlatStyle = FlatStyle.Standard;
            c.Cursor = Cursors.Hand;
        }

        public static void StyleList(ListBox l)
        {
            l.BorderStyle = BorderStyle.None;
            l.Font = Help;
            l.BackColor = Surface;
            l.ForeColor = Text;
            l.IntegralHeight = false;
            l.DrawMode = DrawMode.OwnerDrawFixed;
            l.ItemHeight = 26;
            // Fila seleccionada en accent-soft, nombre del archivo y carpeta en gris
            l.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool sel = (e.State & DrawItemState.Selected) != 0;
                using (var bg = new SolidBrush(sel ? AccentSoft : Surface)) e.Graphics.FillRectangle(bg, e.Bounds);
                if (sel) using (var bar = new SolidBrush(Accent)) e.Graphics.FillRectangle(bar, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
                string full = l.Items[e.Index].ToString();
                string name = System.IO.Path.GetFileName(full);
                var r = new Rectangle(e.Bounds.X + S8, e.Bounds.Y, e.Bounds.Width - S12, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, name, sel ? BodySemibold : Body, r, Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
        }

        public static void StyleGrid(DataGridView g, bool mono)
        {
            g.Dock = DockStyle.Fill;
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.BorderStyle = BorderStyle.None;
            g.BackgroundColor = Surface;
            g.GridColor = Border;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;   // sin cuadrícula vertical
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 32;
            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Bg, ForeColor = Text, Font = BodySemibold,
                SelectionBackColor = Bg, SelectionForeColor = Text,
                Padding = new Padding(S8, 0, S8, 0)
            };
            g.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Surface, ForeColor = Text, Font = mono ? MonoSmall : Body,
                SelectionBackColor = AccentSoft, SelectionForeColor = Text,
                Padding = new Padding(S8, 0, S8, 0)
            };
            g.RowTemplate.Height = 28;
        }

        public static void StyleLog(TextBox t)
        {
            t.Multiline = true;
            t.ReadOnly = true;
            t.ScrollBars = ScrollBars.Vertical;
            t.BorderStyle = BorderStyle.None;
            t.BackColor = Surface;
            t.ForeColor = Text;
            t.Font = MonoSmall;
            t.Dock = DockStyle.Fill;
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Blend(Color a, Color b, double t) => Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    // =========================================================================
    // Tarjeta: fondo surface, borde 1px, radio 8, título 14 semibold, padding 16
    // =========================================================================
    public class CardPanel : Panel
    {
        public const int Pad = AgrdbTheme.S16;
        public const int TitleHeight = 36;   // espacio que ocupa el título + separación
        private readonly string _title;
        private readonly string _subtitle;

        /// <summary>Origen del contenido (debajo del título) para colocar controles con <see cref="Place"/>.</summary>
        public int ContentTop => string.IsNullOrEmpty(_title) ? Pad : Pad + (_subtitle == null ? 26 : 44);
        public int ContentWidth => Width - 2 * Pad;

        public CardPanel(string title, string subtitle = null)
        {
            _title = title;
            _subtitle = subtitle;
            DoubleBuffered = true;
            BackColor = AgrdbTheme.Bg;   // las esquinas fuera del radio muestran el fondo de la ventana
            ResizeRedraw = true;
            Margin = new Padding(0, 0, 0, AgrdbTheme.S16);
            Padding = new Padding(Pad, ContentTop, Pad, Pad);
        }

        /// <summary>Coloca un control en coordenadas relativas al área de contenido.</summary>
        public T Place<T>(T c, int x, int y, int w, int h = -1) where T : Control
        {
            c.SetBounds(Pad + x, ContentTop + y, w, h < 0 ? c.Height : h);
            if (c.BackColor == Color.Transparent || c is Label || c is CheckBox) c.BackColor = AgrdbTheme.Surface;
            Controls.Add(c);
            return c;
        }

        private float _k = 1f;   // factor de escala DPI acumulado (AutoScaleMode.Dpi)

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            _k *= factor.Height;
            Padding = new Padding((int)(Padding.Left * factor.Width), (int)(Padding.Top * factor.Height),
                                  (int)(Padding.Right * factor.Width), (int)(Padding.Bottom * factor.Height));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = AgrdbTheme.RoundRect(r, (int)(8 * _k)))
            using (var fill = new SolidBrush(AgrdbTheme.Surface))
            using (var pen = new Pen(AgrdbTheme.Border))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
            if (!string.IsNullOrEmpty(_title))
                TextRenderer.DrawText(g, _title, AgrdbTheme.Section, new Point((int)((Pad - 3) * _k), (int)((Pad - 2) * _k)), AgrdbTheme.Text);
            if (!string.IsNullOrEmpty(_subtitle))
                TextRenderer.DrawText(g, _subtitle, AgrdbTheme.Help, new Point((int)((Pad - 2) * _k), (int)((Pad + 20) * _k)), AgrdbTheme.TextMuted);
        }
    }

    // =========================================================================
    // Barra segmentada (navegación entre vistas): reemplaza a TabControl
    // =========================================================================
    public class SegmentBar : FlowLayoutPanel
    {
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Control> _views = new List<Control>();
        public int SelectedIndex { get; private set; } = -1;
        public event EventHandler SelectedIndexChanged;

        public SegmentBar()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            WrapContents = false;
            BackColor = AgrdbTheme.Border;            // 1px de borde alrededor de los segmentos
            Padding = new Padding(1);
            Margin = new Padding(0);
        }

        /// <summary>Agrega un segmento; la vista asociada se muestra solo cuando está seleccionado.</summary>
        public void Add(string text, Control view)
        {
            int index = _buttons.Count;
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, AgrdbTheme.ControlHeight - 2),
                Padding = new Padding(AgrdbTheme.S12, 0, AgrdbTheme.S12, 0),
                Margin = new Padding(index == 0 ? 0 : 1, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                Font = AgrdbTheme.Body,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => SelectSegment(index);
            _buttons.Add(b);
            _views.Add(view);
            Controls.Add(b);
            if (SelectedIndex < 0) SelectSegment(0); else ApplyState(b, false, view);
        }

        public void SelectSegment(int index)
        {
            if (index == SelectedIndex) return;
            SelectedIndex = index;
            for (int i = 0; i < _buttons.Count; i++) ApplyState(_buttons[i], i == index, _views[i]);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }

        private static void ApplyState(Button b, bool selected, Control view)
        {
            b.BackColor = selected ? AgrdbTheme.AccentSoft : AgrdbTheme.Surface;
            b.ForeColor = selected ? AgrdbTheme.Primary : AgrdbTheme.TextMuted;
            b.Font = selected ? AgrdbTheme.BodySemibold : AgrdbTheme.Body;
            b.FlatAppearance.MouseOverBackColor = selected ? AgrdbTheme.AccentSoft : AgrdbTheme.Bg;
            if (view != null) { view.Visible = selected; if (selected) view.BringToFront(); }
        }
    }

    // =========================================================================
    // Barra de acción: banner de estado + progreso a la izquierda, primario a la derecha
    // =========================================================================
    public enum StatusKind { Info, Success, Warning, Error }

    public class ActionBar : Panel
    {
        public readonly Button Primary;
        public readonly Button Secondary;
        private readonly Label _status = new Label();
        private readonly ProgressBar _progress = new ProgressBar();

        public ActionBar(string primaryText, string secondaryText = null)
        {
            Dock = DockStyle.Bottom;
            Height = AgrdbTheme.PrimaryHeight + AgrdbTheme.S16;
            Padding = new Padding(0, AgrdbTheme.S16, 0, 0);
            BackColor = AgrdbTheme.Bg;

            Primary = AgrdbTheme.PrimaryButton(primaryText);
            Primary.Dock = DockStyle.Right;
            Primary.AutoSize = true;
            Primary.MinimumSize = new Size(180, AgrdbTheme.PrimaryHeight);

            if (secondaryText != null)
            {
                Secondary = AgrdbTheme.SecondaryButton(secondaryText);
                Secondary.Dock = DockStyle.Right;
                Secondary.AutoSize = true;
                Secondary.Visible = false;
            }

            _progress.Dock = DockStyle.Left;
            _progress.Width = 160;
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Visible = false;

            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Font = AgrdbTheme.Body;
            _status.AutoEllipsis = true;
            _status.Padding = new Padding(AgrdbTheme.S12, 0, AgrdbTheme.S12, 0);

            Controls.Add(_status);
            Controls.Add(_progress);
            if (Secondary != null)
            {
                Controls.Add(new Panel { Dock = DockStyle.Right, Width = AgrdbTheme.S8, BackColor = AgrdbTheme.Bg });
                Controls.Add(Secondary);
            }
            Controls.Add(new Panel { Dock = DockStyle.Right, Width = AgrdbTheme.S8, BackColor = AgrdbTheme.Bg });
            Controls.Add(Primary);
            Primary.SendToBack();   // el primario queda en el extremo derecho
            SetStatus("", StatusKind.Info);
        }

        /// <summary>Mensaje de estado como banner: fondo suave del color del estado + texto oscuro.</summary>
        public void SetStatus(string text, StatusKind kind = StatusKind.Info)
        {
            _status.Text = text;
            switch (kind)
            {
                case StatusKind.Success: _status.BackColor = AgrdbTheme.SuccessSoft; _status.ForeColor = AgrdbTheme.Success; break;
                case StatusKind.Warning: _status.BackColor = AgrdbTheme.WarningSoft; _status.ForeColor = AgrdbTheme.Warning; break;
                case StatusKind.Error: _status.BackColor = AgrdbTheme.ErrorSoft; _status.ForeColor = AgrdbTheme.Error; break;
                default: _status.BackColor = AgrdbTheme.Bg; _status.ForeColor = AgrdbTheme.TextMuted; break;
            }
        }

        public void StartProgress(int max)
        {
            _progress.Maximum = Math.Max(1, max);
            _progress.Value = 0;
            _progress.Visible = true;
            Primary.Enabled = false;
            if (Secondary != null) Secondary.Visible = false;
        }

        public void ReportProgress(int value, string text)
        {
            _progress.Value = Math.Min(_progress.Maximum, Math.Max(0, value));
            SetStatus(text, StatusKind.Info);
        }

        public void EndProgress()
        {
            _progress.Visible = false;
            Primary.Enabled = true;
        }
    }
}
