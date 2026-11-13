using System.Drawing;
using System.Windows.Forms;

namespace CsvToCivilTxt
{
    /// <summary>
    /// Ventana principal: encabezado (icono + nombre + navegación), contenido y pie con versión.
    /// Cada método vive en su propio panel: <see cref="PlanasPanel"/> y <see cref="GeoPanel"/>.
    /// </summary>
    public class MainForm : Form
    {
        private readonly PlanasPanel _planas = new PlanasPanel { Dock = DockStyle.Fill };
        private readonly GeoPanel _geo = new GeoPanel { Dock = DockStyle.Fill };
        private readonly SegmentBar _nav = new SegmentBar();

        public MainForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            Text = "CSV a TXT para Civil 3D · AGRDB";
            Icon = AgrdbTheme.AppIcon;
            Size = new Size(1240, 900);
            MinimumSize = new Size(1040, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = AgrdbTheme.Bg;
            ForeColor = AgrdbTheme.Text;
            Font = AgrdbTheme.Body;
            Padding = new Padding(AgrdbTheme.S24, AgrdbTheme.S16, AgrdbTheme.S24, AgrdbTheme.S8);

            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) =>
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (_nav.SelectedIndex == 1) _geo.AddFiles(files); else _planas.AddFiles(files);
            };

            // ---- Contenido ----
            var content = new Panel { Dock = DockStyle.Fill, BackColor = AgrdbTheme.Bg };
            content.Controls.Add(_planas);
            content.Controls.Add(_geo);

            // ---- Encabezado ----
            var header = BuildHeader();
            _nav.Add("Coordenadas planas", _planas);
            _nav.Add("Geográficas (lat/long)", _geo);

            // ---- Pie ----
            var footer = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = AgrdbTheme.Footer,
                ForeColor = AgrdbTheme.TextMuted,
                Text = $"{AgrdbTheme.Version}  ·  AGRDB — AGR Digital Building  ·  agrdb.dev"
            };

            Controls.Add(content);
            Controls.Add(new Panel { Dock = DockStyle.Top, Height = AgrdbTheme.S16, BackColor = AgrdbTheme.Bg });
            Controls.Add(header);
            Controls.Add(footer);

            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>Icono de la app + nombre + subtítulo a la izquierda; navegación entre métodos a la derecha.</summary>
        private Panel BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = AgrdbTheme.Bg };

            var icon = new PictureBox
            {
                Image = AgrdbTheme.AppIconImage(48),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(40, 40),
                Location = new Point(0, 4),
                BackColor = AgrdbTheme.Bg
            };

            var title = new Label
            {
                Text = "CSV a TXT para Civil 3D",
                Font = AgrdbTheme.Title,
                ForeColor = AgrdbTheme.Text,
                AutoSize = true,
                Location = new Point(52, 0),
                BackColor = AgrdbTheme.Bg
            };

            var subtitle = new Label
            {
                Text = "Convierte puntos de cualquier origen a los formatos de Point File Formats",
                Font = AgrdbTheme.Help,
                ForeColor = AgrdbTheme.TextMuted,
                AutoSize = true,
                Location = new Point(54, 28),
                BackColor = AgrdbTheme.Bg
            };

            // La navegación se ancla a la derecha y centrada verticalmente
            var navHost = new Panel { Dock = DockStyle.Right, Width = 360, BackColor = AgrdbTheme.Bg };
            _nav.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            navHost.Controls.Add(_nav);
            navHost.Layout += (s, e) =>
                _nav.Location = new Point(navHost.Width - _nav.Width, (navHost.Height - _nav.Height) / 2);

            header.Controls.Add(navHost);
            header.Controls.Add(icon);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            return header;
        }
    }
}
