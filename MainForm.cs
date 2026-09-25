using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DarPing
{
    public sealed class MainForm : Form
    {
        private readonly AppSettings settings = AppSettings.Load();
        private readonly HistoryStore historyStore = new HistoryStore();
        private MonitorService monitor;
        private readonly Timer timer = new Timer();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly Label status = new Label();
        private readonly Label ping = new Label();
        private readonly Label traffic = new Label();
        private readonly Label target = new Label();
        private readonly HistoryChart chart = new HistoryChart();
        private readonly TrafficWidget widget = new TrafficWidget();
        private readonly ComboBox chartRange = new ComboBox();
        private bool reading;

        public MainForm()
        {
            Text = "DarPing";
            MinimumSize = new Size(500, 360);
            Size = new Size(700, 470);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(247, 249, 251);
            Font = new Font("Segoe UI", 9F);
            monitor = new MonitorService(settings);
            StartupManager.Apply(settings.StartWithWindows);
            Icon = TrafficWidget.CreateTrayIcon(Color.FromArgb(54, 190, 111));
            BuildUi();
            widget.OpenRequested = ShowWindow;
            widget.TextColor = Color.FromArgb(settings.TaskbarTextColorArgb);
            widget.Show();
            tray.Icon = TrafficWidget.CreateTrayIcon(Color.FromArgb(54, 190, 111));
            tray.Text = "DarPing";
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowWindow(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir DarPing", null, delegate { ShowWindow(); });
            menu.Items.Add("Configuración", null, delegate { OpenSettings(); });
            menu.Items.Add("Salir", null, delegate { Close(); });
            tray.ContextMenuStrip = menu;
            timer.Interval = 1000;
            timer.Tick += async delegate { await ReadOnce(); };
            timer.Start();
            FormClosing += delegate { widget.Close(); tray.Visible = false; tray.Dispose(); };
        }

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = Color.White, Padding = new Padding(22, 15, 22, 12) };
            Controls.Add(header);
            var title = new Label { Text = "DarPing", Font = new Font("Segoe UI Semibold", 19F), ForeColor = Color.FromArgb(31, 42, 55), AutoSize = true, Location = new Point(20, 12) };
            header.Controls.Add(title);
            target.Text = "Destino: " + settings.Host;
            target.ForeColor = Color.FromArgb(105, 116, 129);
            target.AutoSize = true;
            target.Location = new Point(23, 50);
            header.Controls.Add(target);
            var chartRangeLabel = new Label { Text = "Gráfico:", AutoSize = true, ForeColor = Color.FromArgb(105, 116, 129), Location = new Point(330, 31) };
            header.Controls.Add(chartRangeLabel);
            chartRange.DropDownStyle = ComboBoxStyle.DropDownList;
            chartRange.Items.AddRange(new object[] { "Último minuto", "Última hora", "Últimas 6 h", "Últimas 12 h", "Últimas 24 h" });
            chartRange.SelectedIndex = 0;
            chartRange.Width = 105;
            chartRange.Location = new Point(385, 26);
            chartRange.SelectedIndexChanged += delegate { chart.SetRangeMinutes(chartRange.SelectedIndex == 0 ? 1 : chartRange.SelectedIndex == 1 ? 60 : chartRange.SelectedIndex == 2 ? 360 : chartRange.SelectedIndex == 3 ? 720 : 1440); };
            header.Controls.Add(chartRange);
            var settingsButton = new Button { Text = "Configuración", FlatStyle = FlatStyle.Flat, Width = 110, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(544, 25) };
            settingsButton.FlatAppearance.BorderColor = Color.FromArgb(210, 216, 224);
            settingsButton.Click += delegate { OpenSettings(); };
            header.Controls.Add(settingsButton);
            var cards = new TableLayoutPanel { Dock = DockStyle.Top, Height = 104, ColumnCount = 3, RowCount = 1, Padding = new Padding(18, 14, 18, 8) };
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            Controls.Add(cards);
            cards.Controls.Add(MakeCard("CONEXIÓN", status, Color.FromArgb(35, 158, 91)), 0, 0);
            cards.Controls.Add(MakeCard("PING", ping, Color.FromArgb(33, 91, 145)), 1, 0);
            cards.Controls.Add(MakeCard("TRÁFICO", traffic, Color.FromArgb(214, 127, 29)), 2, 0);
            chart.Dock = DockStyle.Fill;
            chart.BackColor = Color.White;
            chart.Values = historyStore.Load();
            chart.SetRangeMinutes(1);
            Controls.Add(chart);
            status.Text = "Iniciando"; status.Font = new Font("Segoe UI Semibold", 14F); status.AutoSize = true;
            ping.Text = "--"; ping.Font = new Font("Segoe UI Semibold", 14F); ping.AutoSize = true;
            traffic.Text = "↓ --  ↑ --"; traffic.Font = new Font("Segoe UI Semibold", 12F); traffic.AutoSize = true;
        }

        private Panel MakeCard(string caption, Label value, Color accent)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(5), Padding = new Padding(15, 10, 8, 5) };
            var line = new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent };
            panel.Controls.Add(line);
            var label = new Label { Text = caption, ForeColor = Color.FromArgb(121, 132, 144), AutoSize = true, Location = new Point(18, 12), Font = new Font("Segoe UI", 8F) };
            panel.Controls.Add(label);
            value.Location = new Point(18, 34);
            panel.Controls.Add(value);
            return panel;
        }

        private async Task ReadOnce()
        {
            if (reading || IsDisposed) return;
            reading = true;
            try
            {
                var sample = await monitor.ReadAsync();
                status.Text = sample.State == ConnectionState.Online ? "Conectado" : sample.State == ConnectionState.Slow ? "Lento" : "Sin conexión";
                status.ForeColor = sample.State == ConnectionState.Online ? Color.FromArgb(35, 158, 91) : sample.State == ConnectionState.Slow ? Color.FromArgb(205, 135, 13) : Color.FromArgb(201, 55, 55);
                ping.Text = sample.PingMs < 0 ? "--" : sample.PingMs + " ms";
                traffic.Text = "↓ " + FormatRate(sample.Download) + "  ↑ " + FormatRate(sample.Upload);
                historyStore.Add(sample);
                chart.Add(sample);
                widget.UpdateReading(sample, FormatRate(sample.Download), FormatRate(sample.Upload));
                var previousIcon = tray.Icon;
                tray.Icon = TrafficWidget.CreateTrayIcon(widget.StatusColor);
                if (previousIcon != null) previousIcon.Dispose();
                var previousWindowIcon = Icon;
                Icon = TrafficWidget.CreateTrayIcon(widget.StatusColor);
                if (previousWindowIcon != null) previousWindowIcon.Dispose();
                tray.Text = "DarPing | " + status.Text + " | ↓ " + FormatRate(sample.Download) + " ↑ " + FormatRate(sample.Upload);
            }
            finally { reading = false; }
        }

        private static string FormatRate(long bytes)
        {
            if (bytes < 1024) return bytes + " B/s";
            if (bytes < 1024 * 1024) return (bytes / 1024D).ToString("0.0") + " KB/s";
            return (bytes / 1024D / 1024D).ToString("0.0") + " MB/s";
        }

        private void OpenSettings()
        {
            using (var form = new SettingsForm(settings))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    settings.Save(); monitor = new MonitorService(settings); target.Text = "Destino: " + settings.Host;
                    widget.TextColor = Color.FromArgb(settings.TaskbarTextColorArgb);
                    StartupManager.Apply(form.StartWithWindows);
                    if (form.HistoryCleared) { historyStore.Clear(); chart.Clear(); }
                }
            }
        }
        private void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if (WindowState == FormWindowState.Minimized) Hide(); }
    }

    public sealed class TrafficWidget : Form
    {
        private const int GwlStyle = -16;
        private const int WsChild = 0x40000000;
        private const int WsPopup = unchecked((int)0x80000000);
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpShowWindow = 0x0040;
        private const uint SwpNoOwnerZOrder = 0x0200;
        private const uint SwpNoSendChanging = 0x0400;
        private static readonly IntPtr HwndTop = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr handle, int index);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr handle, int index, int value);
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr handle, out RECT rect);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr handle, out RECT rect);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        private string down = "--";
        private string up = "--";
        private string connection = "Iniciando";
        private Color stateColor = Color.FromArgb(121, 132, 144);
        private IntPtr taskbarHandle;
        public Action OpenRequested { get; set; }
        public Color StatusColor { get { return stateColor; } }
        public Color TextColor { get; set; }

        public TrafficWidget()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            Width = 118;
            Height = 40;
            BackColor = Color.FromArgb(54, 190, 111);
            TextColor = Color.White;
            Opacity = 1.0;
            Cursor = Cursors.Hand;
            Click += delegate { if (OpenRequested != null) OpenRequested(); };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            AttachToTaskbar();
        }

        private void AttachToTaskbar()
        {
            taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle == IntPtr.Zero) return;
            var style = GetWindowLong(Handle, GwlStyle);
            SetWindowLong(Handle, GwlStyle, (style & ~WsPopup) | WsChild);
            SetParent(Handle, taskbarHandle);
            PositionInTaskbar();
        }

        private void PositionInTaskbar()
        {
            if (taskbarHandle == IntPtr.Zero) return;
            RECT taskbarRect;
            if (!GetWindowRect(taskbarHandle, out taskbarRect)) return;
            var taskbarWidth = taskbarRect.Right - taskbarRect.Left;
            var taskbarHeight = taskbarRect.Bottom - taskbarRect.Top;
            var x = Math.Max(0, taskbarWidth - Width - 4);
            var notifyHandle = FindWindowEx(taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);
            RECT notifyRect;
            if (notifyHandle != IntPtr.Zero && GetWindowRect(notifyHandle, out notifyRect))
                x = Math.Max(0, notifyRect.Left - taskbarRect.Left - Width - 2);
            var y = Math.Max(0, (taskbarHeight - Height) / 2);
            SetWindowPos(Handle, HwndTop, x, y, Width, Height, SwpNoActivate | SwpShowWindow | SwpNoOwnerZOrder | SwpNoSendChanging);
        }

        public static Icon CreateTrayIcon(Color color)
        {
            var bitmap = new Bitmap(32, 32);
            using (var graphics = Graphics.FromImage(bitmap)) using (var brush = new SolidBrush(color)) using (var pen = new Pen(Color.White, 2F))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.FillEllipse(brush, 3, 3, 26, 26);
                graphics.DrawEllipse(pen, 4, 4, 24, 24);
                graphics.DrawArc(pen, 9, 4, 14, 24, 90, 180);
                graphics.DrawArc(pen, 9, 4, 14, 24, 270, 180);
                graphics.DrawLine(pen, 5, 16, 27, 16);
            }
            var icon = Icon.FromHandle(bitmap.GetHicon());
            bitmap.Dispose();
            return icon;
        }

        public void UpdateReading(MonitorReading reading, string download, string upload)
        {
            down = download;
            up = upload;
            connection = reading.State == ConnectionState.Online ? "Conectado" : reading.State == ConnectionState.Slow ? "Lento" : "Sin conexión";
            stateColor = reading.State == ConnectionState.Online ? Color.FromArgb(54, 190, 111) : reading.State == ConnectionState.Slow ? Color.FromArgb(235, 177, 45) : Color.FromArgb(220, 76, 76);
            BackColor = stateColor;
            ResizeToText();
            PositionInTaskbar();
            Invalidate();
        }

        private void ResizeToText()
        {
            using (var font = new Font("Segoe UI Semibold", 9F))
            {
                var upper = TextRenderer.MeasureText("↑ " + up, font).Width;
                var lower = TextRenderer.MeasureText("↓ " + down, font).Width;
                Width = Math.Max(82, Math.Min(150, Math.Max(upper, lower) + 12));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var text = new SolidBrush(TextColor))
            using (var valueFont = new Font("Segoe UI Semibold", 9F))
            {
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString("↑ " + up, valueFont, text, new RectangleF(0, 1, Width, 18), format);
                e.Graphics.DrawString("↓ " + down, valueFont, text, new RectangleF(0, 20, Width, 18), format);
            }
        }
    }

    public sealed class SettingsForm : Form
    {
        private readonly AppSettings settings; private readonly TextBox host = new TextBox(); private readonly NumericUpDown threshold = new NumericUpDown(); private readonly ComboBox soundOnlineProfile = new ComboBox(); private readonly ComboBox soundSlowProfile = new ComboBox(); private readonly ComboBox soundOfflineProfile = new ComboBox(); private readonly CheckBox soundOnline = new CheckBox(); private readonly CheckBox soundSlow = new CheckBox(); private readonly CheckBox soundOffline = new CheckBox(); private readonly Button textColorButton = new Button(); private readonly CheckBox startWithWindows = new CheckBox();
        public bool StartWithWindows { get { return startWithWindows.Checked; } }
        public bool HistoryCleared { get; private set; }
        public SettingsForm(AppSettings source)
        {
            settings = source; Text = "Configuración"; Size = new Size(410, 555); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; Font = new Font("Segoe UI", 9F);
            Controls.Add(new Label { Text = "IP o nombre de destino", AutoSize = true, Location = new Point(25, 24) }); host.Location = new Point(25, 47); host.Width = 290; host.Text = settings.Host; Controls.Add(host);
            Controls.Add(new Label { Text = "Considerar lento por encima de (ms)", AutoSize = true, Location = new Point(25, 82) }); threshold.Location = new Point(25, 105); threshold.Minimum = 50; threshold.Maximum = 5000; threshold.Value = settings.SlowPingMs; Controls.Add(threshold);
            Controls.Add(new Label { Text = "Sonido para cada resultado", AutoSize = true, Location = new Point(25, 143) });
            AddSoundRow("Ping correcto", soundOnline, soundOnlineProfile, settings.SoundOnOnline, settings.SoundProfileOnline, 168);
            AddSoundRow("Ping lento", soundSlow, soundSlowProfile, settings.SoundOnSlow, settings.SoundProfileSlow, 242);
            AddSoundRow("Ping fallido", soundOffline, soundOfflineProfile, settings.SoundOnOffline, settings.SoundProfileOffline, 316);
            Controls.Add(new Label { Text = "Color de la tipografía en la taskbar", AutoSize = true, Location = new Point(25, 350) });
            textColorButton.Text = "Elegir color"; textColorButton.Width = 180; textColorButton.Location = new Point(25, 372); textColorButton.BackColor = Color.FromArgb(settings.TaskbarTextColorArgb); textColorButton.ForeColor = settings.TaskbarTextColorArgb == Color.White.ToArgb() ? Color.Black : Color.White; textColorButton.Click += delegate { using (var picker = new ColorDialog { Color = Color.FromArgb(settings.TaskbarTextColorArgb), FullOpen = true }) { if (picker.ShowDialog(this) == DialogResult.OK) { settings.TaskbarTextColorArgb = picker.Color.ToArgb(); textColorButton.BackColor = picker.Color; textColorButton.ForeColor = picker.Color.GetBrightness() > 0.55F ? Color.Black : Color.White; } } }; Controls.Add(textColorButton);
            var clearHistory = new Button { Text = "Vaciar histórico de pings", Width = 180, Location = new Point(25, 410) }; clearHistory.Click += delegate { if (MessageBox.Show(this, "¿Vaciar todo el histórico de pings?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) { HistoryCleared = true; MessageBox.Show(this, "Histórico vaciado.", "DarPing", MessageBoxButtons.OK, MessageBoxIcon.Information); } }; Controls.Add(clearHistory);
            startWithWindows.Text = "Arrancar DarPing al iniciar sesión en Windows"; startWithWindows.AutoSize = true; startWithWindows.Location = new Point(25, 445); startWithWindows.Checked = settings.StartWithWindows; Controls.Add(startWithWindows);
            var save = new Button { Text = "Guardar", DialogResult = DialogResult.OK, Location = new Point(225, 490), Width = 75 }; var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(310, 490), Width = 75 }; Controls.Add(save); Controls.Add(cancel); AcceptButton = save; CancelButton = cancel;
            save.Click += delegate { settings.Host = host.Text.Trim(); settings.SlowPingMs = (int)threshold.Value; settings.SoundProfileOnline = soundOnlineProfile.SelectedIndex; settings.SoundProfileSlow = soundSlowProfile.SelectedIndex; settings.SoundProfileOffline = soundOfflineProfile.SelectedIndex; settings.SoundProfile = settings.SoundProfileOnline; settings.SoundOnOnline = soundOnline.Checked; settings.SoundOnSlow = soundSlow.Checked; settings.SoundOnOffline = soundOffline.Checked; settings.PlaySound = settings.SoundOnOnline || settings.SoundOnSlow || settings.SoundOnOffline; settings.StartWithWindows = startWithWindows.Checked; };
        }

        private void AddSoundRow(string caption, CheckBox enabled, ComboBox profile, bool isEnabled, int selectedProfile, int top)
        {
            enabled.Text = caption; enabled.AutoSize = true; enabled.Location = new Point(25, top); enabled.Checked = isEnabled; Controls.Add(enabled);
            profile.DropDownStyle = ComboBoxStyle.DropDownList; profile.Items.AddRange(MonitorService.SoundNames); profile.SelectedIndex = Math.Max(0, Math.Min(9, selectedProfile)); profile.Location = new Point(155, top - 3); profile.Width = 150; Controls.Add(profile);
            var test = new Button { Text = "Probar", Width = 65, Location = new Point(315, top - 4) }; test.Click += delegate { MonitorService.PlaySound(profile.SelectedIndex); }; Controls.Add(test);
        }
    }

    public sealed class HistoryChart : Control
    {
        public List<MonitorReading> Values = new List<MonitorReading>();
        private int rangeMinutes = 1;

        public void SetRangeMinutes(int minutes) { rangeMinutes = Math.Max(1, Math.Min(1440, minutes)); Invalidate(); }
        public void Clear() { Values.Clear(); Invalidate(); }
        public void Add(MonitorReading value)
        {
            Values.Add(value);
            var cutoff = DateTime.Now.AddHours(-24);
            Values.RemoveAll(item => item.Time < cutoff);
            if (Values.Count > 100000) Values.RemoveRange(0, Values.Count - 100000);
            Invalidate();
        }

        private List<MonitorReading> GetVisibleValues()
        {
            var cutoff = DateTime.Now.AddMinutes(-rangeMinutes);
            return Values.Where(item => item.Time >= cutoff).ToList();
        }

        private static List<MonitorReading> RemoveIsolatedSpikes(List<MonitorReading> source)
        {
            var cleaned = new List<MonitorReading>(source.Count);
            for (var i = 0; i < source.Count; i++)
            {
                var current = source[i];
                if (i > 0 && i + 1 < source.Count && current.PingMs >= 0 && source[i - 1].PingMs >= 0 && source[i + 1].PingMs >= 0)
                {
                    var neighbours = Math.Max(source[i - 1].PingMs, source[i + 1].PingMs);
                    if (current.PingMs > neighbours + 120 && current.PingMs > neighbours * 2.5)
                    {
                        current = new MonitorReading { Time = current.Time, Download = current.Download, Upload = current.Upload, PingMs = (source[i - 1].PingMs + source[i + 1].PingMs) / 2, State = current.State };
                    }
                }
                cleaned.Add(current);
            }
            return cleaned;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(Color.White);
            var visibleValues = RemoveIsolatedSpikes(GetVisibleValues());
            var rangeText = rangeMinutes == 1 ? "último minuto" : rangeMinutes == 60 ? "última hora" : "últimas " + (rangeMinutes / 60) + " horas";
            using (var font = new Font("Segoe UI", 9F)) using (var titleBrush = new SolidBrush(Color.FromArgb(70, 82, 96))) using (var grid = new Pen(Color.FromArgb(231, 235, 239)))
            { e.Graphics.DrawString("Histórico de ping / latencia · " + rangeText, font, titleBrush, 20, 14); e.Graphics.DrawLine(grid, 20, 48, Width - 20, 48); e.Graphics.DrawLine(grid, 20, Height - 30, Width - 20, Height - 30); }
            if (visibleValues.Count < 2) return;
            var left = 20F; var top = 58F; var width = Math.Max(1, Width - 40); var height = Math.Max(1, Height - 92); var maxPing = Math.Max(100, visibleValues.Where(v => v.PingMs > 0).Select(v => v.PingMs).DefaultIfEmpty(100).Max());
            using (var online = new Pen(Color.FromArgb(43, 165, 99), 2F)) using (var slow = new Pen(Color.FromArgb(220, 157, 34), 2F)) using (var offline = new SolidBrush(Color.FromArgb(207, 67, 67)))
            { for (var i = 1; i < visibleValues.Count; i++) { var a = visibleValues[i - 1]; var b = visibleValues[i]; var x1 = left + width * (i - 1) / (visibleValues.Count - 1); var x2 = left + width * i / (visibleValues.Count - 1); if (b.PingMs < 0) e.Graphics.FillEllipse(offline, x2 - 2, top + height - 4, 5, 5); else e.Graphics.DrawLine(b.State == ConnectionState.Slow ? slow : online, x1, top + height - (float)(Math.Min(maxPing, Math.Max(0, a.PingMs)) / maxPing * height), x2, top + height - (float)(Math.Min(maxPing, Math.Max(0, b.PingMs)) / maxPing * height)); } }
        }
    }
}