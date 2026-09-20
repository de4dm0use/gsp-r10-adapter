using LaunchMonitor.Proto;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing;
using System.Windows.Forms;

namespace gspro_r10
{
    public class MainForm : Form
    {
        private sealed class ShotRecord
        {
            public int Number { get; init; }
            public TracerShot Tracer { get; init; }
            public string Summary => $"Shot {Number} • {Tracer.CarryYards:0} yd • {Tracer.LaunchAngleDeg:0.0}° launch";
        }

        private readonly ConnectionManager manager;
        private readonly Label connectionLabel = new();
        private readonly Label deviceLabel = new();
        private readonly Label carryLabel = new();
        private readonly Label apexLabel = new();
        private readonly Label flightLabel = new();
        private readonly Label landingSpeedLabel = new();
        private readonly Label ballSpeedLabel = new();
        private readonly Label launchLabel = new();
        private readonly Label spinLabel = new();
        private readonly DataGridView shots = new();
        private readonly RichTextBox terminal = new();
        private readonly Button connectButton = new();

        private readonly PictureBox cameraPreview = new();
        private readonly ComboBox cameraMode = new();
        private readonly ComboBox shotSelector = new();
        private readonly TextBox videoPath = new();
        private readonly NumericUpDown impactTime = new();
        private readonly ComboBox tracerStyle = new();
        private readonly Label tracerStatus = new();

        private readonly List<ShotRecord> shotRecords = new();
        private VideoCapture? camera;
        private System.Windows.Forms.Timer? cameraTimer;
        private int shotCount;
        private bool tracerBusy;

        public MainForm(ConnectionManager manager)
        {
            this.manager = manager;
            Text = "R10 Golf Tracker";
            Width = 1420;
            Height = 920;
            MinimumSize = new Size(1100, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(11, 20, 32);
            ForeColor = Color.White;
            BuildUi();

            manager.ShotReceived += OnShotReceived;
            var bt = manager.BluetoothConnection;
            if (bt != null)
            {
                bt.BatteryUpdated += OnBatteryUpdated;
                bt.StatusChanged += OnStatusChanged;
            }

            FormClosed += (_, _) =>
            {
                StopCamera();
                manager.Dispose();
            };

            AppendTerminal("R10 Golf Tracker started.");
            AppendTerminal("ARC Physics Lab / trajectory data can drive the tracer.");
            AppendTerminal("Nova WebSocket: ws://127.0.0.1:2920/");
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(14),
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var header = new Panel { Dock = DockStyle.Fill };
            var title = new Label
            {
                Text = "R10 GOLF TRACKER",
                AutoSize = true,
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                Location = new Point(0, 2)
            };
            connectionLabel.Text = "● Connecting...";
            connectionLabel.AutoSize = true;
            connectionLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            connectionLabel.ForeColor = Color.Gold;
            connectionLabel.Location = new Point(3, 42);

            deviceLabel.Text = "Garmin Approach R10";
            deviceLabel.AutoSize = true;
            deviceLabel.ForeColor = Color.LightGray;
            deviceLabel.Location = new Point(875, 10);

            connectButton.Text = "CONNECT R10";
            connectButton.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            connectButton.Size = new Size(140, 34);
            connectButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            connectButton.Location = new Point(1245, 30);
            connectButton.Click += (_, _) => ConnectR10();

            header.Controls.Add(title);
            header.Controls.Add(connectionLabel);
            header.Controls.Add(deviceLabel);
            header.Controls.Add(connectButton);
            root.Controls.Add(header, 0, 0);

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10)
            };
            tabs.TabPages.Add(BuildDashboardTab());
            tabs.TabPages.Add(BuildTracerTab());
            tabs.TabPages.Add(BuildCameraTab());
            root.Controls.Add(tabs, 0, 1);
        }

        private TabPage BuildDashboardTab()
        {
            var page = new TabPage("Live Shot") { BackColor = BackColor, Padding = new Padding(10) };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = BackColor };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            page.Controls.Add(root);

            var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            for (int i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            AddCard(cards, 0, "ESTIMATED CARRY", carryLabel, "— yd", 27);
            AddCard(cards, 1, "APEX", apexLabel, "— yd", 25);
            AddCard(cards, 2, "FLIGHT TIME", flightLabel, "— s", 25);
            AddCard(cards, 3, "LANDING SPEED", landingSpeedLabel, "— mph", 22);
            root.Controls.Add(cards, 0, 0);

            var detail = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            for (int i = 0; i < 3; i++) detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            AddSmallCard(detail, 0, "BALL SPEED", ballSpeedLabel, "— mph");
            AddSmallCard(detail, 1, "LAUNCH ANGLE", launchLabel, "—°");
            AddSmallCard(detail, 2, "BACKSPIN", spinLabel, "— rpm");
            root.Controls.Add(detail, 0, 1);

            ConfigureGrid();
            root.Controls.Add(shots, 0, 2);

            var terminalPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(8, 12, 16), Padding = new Padding(8) };
            var terminalTitle = new Label
            {
                Text = "TERMINAL / R10 LOG",
                AutoSize = true,
                ForeColor = Color.Silver,
                Font = new Font("Consolas", 9, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 22
            };
            terminal.Font = new Font("Consolas", 9);
            terminal.BackColor = Color.FromArgb(5, 8, 10);
            terminal.ForeColor = Color.LightGreen;
            terminal.BorderStyle = BorderStyle.None;
            terminal.Dock = DockStyle.Fill;
            terminal.ReadOnly = true;
            terminal.WordWrap = false;
            terminal.ScrollBars = RichTextBoxScrollBars.Vertical;
            terminalPanel.Controls.Add(terminal);
            terminalPanel.Controls.Add(terminalTitle);
            root.Controls.Add(terminalPanel, 0, 3);
            return page;
        }

        private TabPage BuildTracerTab()
        {
            var page = new TabPage("Video Tracer") { BackColor = BackColor, Padding = new Padding(10) };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = BackColor };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            page.Controls.Add(root);

            cameraPreview.Dock = DockStyle.Fill;
            cameraPreview.BackColor = Color.Black;
            cameraPreview.SizeMode = PictureBoxSizeMode.Zoom;
            root.Controls.Add(cameraPreview, 0, 0);

            var controls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 12, Padding = new Padding(12), BackColor = Color.FromArgb(18, 29, 44) };
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            controls.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            controls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            controls.Controls.Add(HeaderLabel("SHOT TRACER"), 0, 0);
            controls.Controls.Add(HeaderLabel("ARC Physics Lab trajectory → camera overlay"), 0, 1);

            controls.Controls.Add(MakeLabel("Video file"), 0, 2);
            var videoRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            videoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            videoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            videoPath.Dock = DockStyle.Fill;
            videoPath.BackColor = Color.FromArgb(8, 15, 24);
            videoPath.ForeColor = Color.White;
            var browse = Button("BROWSE", (_, _) => BrowseVideo());
            videoRow.Controls.Add(videoPath, 0, 0);
            videoRow.Controls.Add(browse, 1, 0);
            controls.Controls.Add(videoRow, 0, 3);

            controls.Controls.Add(MakeLabel("R10 shot"), 0, 4);
            shotSelector.Dock = DockStyle.Fill;
            shotSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            controls.Controls.Add(shotSelector, 0, 5);

            controls.Controls.Add(MakeLabel("Impact time in video (seconds)"), 0, 6);
            impactTime.DecimalPlaces = 2;
            impactTime.Increment = 0.05M;
            impactTime.Maximum = 600;
            impactTime.Value = 1.50M;
            impactTime.Dock = DockStyle.Fill;
            controls.Controls.Add(impactTime, 0, 7);

            controls.Controls.Add(MakeLabel("Tracer style"), 0, 8);
            tracerStyle.Items.AddRange(new object[] { "Ball trail + landing", "Arc only", "Arc + shot HUD" });
            tracerStyle.SelectedIndex = 0;
            tracerStyle.Dock = DockStyle.Fill;
            controls.Controls.Add(tracerStyle, 0, 9);

            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actions.Controls.Add(Button("GENERATE PREVIEW", (_, _) => GenerateTracerPreview()), 0, 0);
            actions.Controls.Add(Button("EXPORT MP4", (_, _) => ExportTracer()), 1, 0);
            controls.Controls.Add(actions, 0, 10);

            tracerStatus.Text = "Load a video and select an R10 shot.";
            tracerStatus.ForeColor = Color.LightGreen;
            tracerStatus.Dock = DockStyle.Fill;
            controls.Controls.Add(tracerStatus, 0, 11);

            root.Controls.Add(controls, 1, 0);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 21, 33), Padding = new Padding(10) };
            bottom.Controls.Add(Button("START CAMERA", (_, _) => StartCamera()));
            bottom.Controls.Add(Button("STOP CAMERA", (_, _) => StopCamera()));
            bottom.Controls.Add(Button("CAPTURE FRAME", (_, _) => CaptureFrame()));
            bottom.Controls.Add(Button("PUTTING CAMERA", (_, _) => SetCameraMode("Putting Camera")));
            bottom.Controls.Add(Button("SHOT TRACER CAMERA", (_, _) => SetCameraMode("Shot Tracer")));
            root.Controls.Add(bottom, 0, 1);
            return page;
        }

        private TabPage BuildCameraTab()
        {
            var page = new TabPage("Putting Camera") { BackColor = BackColor, Padding = new Padding(10) };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            page.Controls.Add(root);

            var view = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.Black, SizeMode = PictureBoxSizeMode.Zoom };
            root.Controls.Add(view, 0, 0);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 29, 44), Padding = new Padding(10) };
            bar.Controls.Add(new Label { Text = "Camera mode:", AutoSize = true, ForeColor = Color.White, Margin = new Padding(5, 9, 5, 0) });
            cameraMode.Items.AddRange(new object[] { "Putting Camera", "Shot Tracer Camera" });
            cameraMode.SelectedIndex = 0;
            cameraMode.Width = 180;
            bar.Controls.Add(cameraMode);
            bar.Controls.Add(Button("START CAMERA", (_, _) => { cameraPreview = view; StartCamera(); }));
            bar.Controls.Add(Button("STOP CAMERA", (_, _) => StopCamera()));
            root.Controls.Add(bar, 0, 1);
            return page;
        }

        private static Label HeaderLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12, FontStyle.Bold)
        };

        private static Label MakeLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.Silver,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };

        private static Button Button(string text, EventHandler action)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = false,
                Width = 150,
                Height = 34,
                BackColor = Color.FromArgb(24, 112, 220),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Margin = new Padding(5)
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += action;
            return b;
        }

        private static void AddCard(TableLayoutPanel parent, int column, string caption, Label value, string initial, int size)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(25, 38, 53), Margin = new Padding(5) };
            var cap = new Label { Text = caption, AutoSize = true, ForeColor = Color.Silver, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(14, 12) };
            value.Text = initial;
            value.AutoSize = true;
            value.ForeColor = Color.White;
            value.Font = new Font("Segoe UI", size, FontStyle.Bold);
            value.Location = new Point(14, 43);
            panel.Controls.Add(cap);
            panel.Controls.Add(value);
            parent.Controls.Add(panel, column, 0);
        }

        private static void AddSmallCard(TableLayoutPanel parent, int column, string caption, Label value, string initial)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 31, 44), Margin = new Padding(5) };
            var cap = new Label { Text = caption, AutoSize = true, ForeColor = Color.Silver, Font = new Font("Segoe UI", 8, FontStyle.Bold), Location = new Point(12, 9) };
            value.Text = initial;
            value.AutoSize = true;
            value.ForeColor = Color.White;
            value.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            value.Location = new Point(12, 34);
            panel.Controls.Add(cap);
            panel.Controls.Add(value);
            parent.Controls.Add(panel, column, 0);
        }

        private void ConfigureGrid()
        {
            shots.Dock = DockStyle.Fill;
            shots.BackgroundColor = Color.FromArgb(18, 29, 42);
            shots.GridColor = Color.FromArgb(48, 62, 76);
            shots.BorderStyle = BorderStyle.None;
            shots.RowHeadersVisible = false;
            shots.AllowUserToAddRows = false;
            shots.ReadOnly = true;
            shots.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            shots.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            shots.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(31, 49, 68), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            shots.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(18, 29, 42), ForeColor = Color.White, SelectionBackColor = Color.FromArgb(24, 112, 220), SelectionForeColor = Color.White };
            shots.Columns.Add("Shot", "#");
            shots.Columns.Add("BallSpeed", "Ball speed");
            shots.Columns.Add("Carry", "Carry");
            shots.Columns.Add("Apex", "Apex");
            shots.Columns.Add("Launch", "Launch");
            shots.Columns.Add("Direction", "Direction");
            shots.Columns.Add("Spin", "Backspin");
            shots.Columns.Add("ClubSpeed", "Club speed");
            shots.Columns.Add("AoA", "Attack angle");
        }

        private void ConnectR10()
        {
            AppendTerminal("Manual R10 connection requested...");
            connectButton.Enabled = false;
            var bt = manager.BluetoothConnection;
            if (bt == null)
                AppendTerminal("Bluetooth connection is disabled in settings.");
            else
                bt.Reconnect();

            var timer = new System.Windows.Forms.Timer { Interval = 1500 };
            timer.Tick += (_, _) => { connectButton.Enabled = true; timer.Stop(); timer.Dispose(); };
            timer.Start();
        }

        private void UpdateConnectionState()
        {
            if (IsDisposed) return;
            var bt = manager.BluetoothConnection;
            if (bt?.LaunchMonitor?.Device?.Gatt?.IsConnected == true)
            {
                connectionLabel.Text = $"● Connected • {bt.LaunchMonitor.CurrentState}";
                connectionLabel.ForeColor = Color.LightGreen;
                deviceLabel.Text = $"{bt.LaunchMonitor.Model} • FW {bt.LaunchMonitor.Firmware} • {bt.LaunchMonitor.Battery}%";
                connectButton.Text = "RECONNECT R10";
            }
            else
            {
                connectionLabel.Text = "● Connecting...";
                connectionLabel.ForeColor = Color.Gold;
                connectButton.Text = "CONNECT R10";
            }
        }

        private void OnBatteryUpdated(int battery)
        {
            if (InvokeRequired) { BeginInvoke(() => OnBatteryUpdated(battery)); return; }
            AppendTerminal($"Battery: {battery}%");
            UpdateConnectionState();
        }

        private void OnStatusChanged(string status)
        {
            if (InvokeRequired) { BeginInvoke(() => OnStatusChanged(status)); return; }
            AppendTerminal(status);
            UpdateConnectionState();
        }

        private void AppendTerminal(string message)
        {
            if (terminal.IsDisposed) return;
            if (terminal.InvokeRequired) { terminal.BeginInvoke(() => AppendTerminal(message)); return; }
            terminal.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            terminal.SelectionStart = terminal.TextLength;
            terminal.ScrollToCaret();
        }

        private void OnShotReceived(Metrics metrics)
        {
            if (InvokeRequired) { BeginInvoke(() => OnShotReceived(metrics)); return; }

            var ball = metrics.BallMetrics;
            var club = metrics.ClubMetrics;
            double mph = (ball?.BallSpeed ?? 0) * 2.236936;
            double totalSpin = ball?.TotalSpin ?? 0;
            double spinAxisDeg = ball?.SpinAxis ?? 0;
            double backSpin = Math.Max(0, totalSpin * Math.Cos(-spinAxisDeg * Math.PI / 180.0));
            var trajectory = CarryCalculator.Calculate(mph, ball?.LaunchAngle ?? 0, backSpin);

            shotCount++;
            carryLabel.Text = $"{trajectory.CarryYards:0} yd";
            apexLabel.Text = $"{trajectory.ApexYards:0.0} yd";
            flightLabel.Text = $"{trajectory.FlightTimeSeconds:0.00} s";
            landingSpeedLabel.Text = $"{trajectory.LandingSpeedMph:0.0} mph";
            ballSpeedLabel.Text = $"{mph:0.0} mph";
            launchLabel.Text = $"{ball?.LaunchAngle ?? 0:0.0}°";
            spinLabel.Text = $"{backSpin:0} rpm";

            shots.Rows.Insert(0, shotCount,
                $"{mph:0.0} mph",
                $"{trajectory.CarryYards:0} yd",
                $"{trajectory.ApexYards:0.0} yd",
                $"{ball?.LaunchAngle ?? 0:0.0}°",
                $"{ball?.LaunchDirection ?? 0:0.0}°",
                $"{backSpin:0} rpm",
                $"{(club?.ClubHeadSpeed ?? 0) * 2.236936:0.0} mph",
                $"{club?.AttackAngle ?? 0:0.0}°");

            var record = new ShotRecord
            {
                Number = shotCount,
                Tracer = new TracerShot(
                    trajectory.CarryYards,
                    trajectory.ApexYards,
                    trajectory.FlightTimeSeconds,
                    ball?.LaunchAngle ?? 0,
                    ball?.LaunchDirection ?? 0,
                    spinAxisDeg)
            };
            shotRecords.Insert(0, record);
            shotSelector.Items.Insert(0, record);
            shotSelector.SelectedIndex = 0;

            AppendTerminal($"SHOT #{shotCount}: {mph:0.0} mph | Launch {ball?.LaunchAngle ?? 0:0.0}° | Spin {totalSpin:0} rpm | Carry {trajectory.CarryYards:0} yd");
            if (shots.Rows.Count > 200) shots.Rows.RemoveAt(shots.Rows.Count - 1);
            if (shotRecords.Count > 200) shotRecords.RemoveAt(shotRecords.Count - 1);
            if (shotSelector.Items.Count > 200) shotSelector.Items.RemoveAt(shotSelector.Items.Count - 1);
        }

        private void BrowseVideo()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Video files|*.mp4;*.mov;*.avi;*.mkv;*.m4v|All files|*.*",
                Title = "Select golf swing video"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                videoPath.Text = dialog.FileName;
                tracerStatus.Text = "Video loaded. Choose the impact time and generate a preview.";
                AppendTerminal($"Video selected: {Path.GetFileName(dialog.FileName)}");
            }
        }

        private ShotRecord? SelectedShot() => shotSelector.SelectedItem as ShotRecord;

        private void GenerateTracerPreview()
        {
            if (tracerBusy) return;
            var shot = SelectedShot();
            if (shot == null || !File.Exists(videoPath.Text))
            {
                MessageBox.Show(this, "Select a video and an R10 shot first.", "Tracer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                tracerBusy = true;
                using var capture = new VideoCapture(videoPath.Text);
                if (!capture.IsOpened()) throw new InvalidOperationException("OpenCV could not open the video.");
                double fps = capture.Fps > 0 ? capture.Fps : 30;
                capture.PosMsec = (double)impactTime.Value * 1000;
                using var frame = new Mat();
                if (!capture.Read(frame) || frame.Empty()) throw new InvalidOperationException("Could not read the requested video frame.");

                TracerEngine.Draw(frame, shot.Tracer, 1.0);
                if (tracerStyle.SelectedIndex == 2) TracerEngine.DrawHud(frame, shot.Tracer);
                ShowFrame(frame);
                tracerStatus.Text = "Tracer preview generated.";
                AppendTerminal($"Tracer preview: shot #{shot.Number}, impact {impactTime.Value:0.00}s.");
            }
            catch (Exception ex)
            {
                tracerStatus.Text = $"Tracer error: {ex.Message}";
                AppendTerminal($"TRACER ERROR: {ex.Message}");
                MessageBox.Show(this, ex.Message, "Tracer error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { tracerBusy = false; }
        }

        private async void ExportTracer()
        {
            if (tracerBusy) return;
            var shot = SelectedShot();
            if (shot == null || !File.Exists(videoPath.Text))
            {
                MessageBox.Show(this, "Select a video and an R10 shot first.", "Tracer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "MP4 video|*.mp4",
                FileName = $"R10_Shot_{shot.Number}_Tracer.mp4",
                Title = "Export shot tracer video"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            tracerBusy = true;
            tracerStatus.Text = "Rendering tracer video...";
            try
            {
                await Task.Run(() => RenderVideo(videoPath.Text, dialog.FileName, shot.Tracer, (double)impactTime.Value, tracerStyle.SelectedIndex));
                tracerStatus.Text = $"Export complete: {Path.GetFileName(dialog.FileName)}";
                AppendTerminal($"Tracer exported: {dialog.FileName}");
                MessageBox.Show(this, $"Tracer video exported to:\n{dialog.FileName}", "Tracer complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                tracerStatus.Text = $"Export failed: {ex.Message}";
                AppendTerminal($"TRACER EXPORT ERROR: {ex.Message}");
                MessageBox.Show(this, ex.Message, "Tracer export error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { tracerBusy = false; }
        }

        private static void RenderVideo(string input, string output, TracerShot shot, double impactSeconds, int style)
        {
            using var capture = new VideoCapture(input);
            if (!capture.IsOpened()) throw new InvalidOperationException("OpenCV could not open the source video.");

            double fps = capture.Fps > 0 ? capture.Fps : 30;
            int width = (int)capture.FrameWidth;
            int height = (int)capture.FrameHeight;
            if (width <= 0 || height <= 0) throw new InvalidOperationException("The source video has no usable dimensions.");

            using var writer = new VideoWriter(output, FourCC.MP4V, fps, new OpenCvSharp.Size(width, height));
            if (!writer.IsOpened()) throw new InvalidOperationException("OpenCV could not create the MP4 output. Try a different output filename.");

            using var frame = new Mat();
            while (capture.Read(frame))
            {
                if (frame.Empty()) break;
                double time = capture.PosMsec / 1000.0;
                double progress = shot.FlightTimeSeconds <= 0
                    ? 1.0
                    : Math.Clamp((time - impactSeconds) / shot.FlightTimeSeconds, 0, 1);

                if (time >= impactSeconds)
                    TracerEngine.Draw(frame, shot, progress);

                if (style == 2)
                    TracerEngine.DrawHud(frame, shot);

                writer.Write(frame);
            }
        }

        private void StartCamera()
        {
            StopCamera();
            camera = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
            if (!camera.IsOpened())
            {
                camera.Dispose();
                camera = null;
                AppendTerminal("Camera: unable to open webcam.");
                MessageBox.Show(this, "Could not open camera 0. Check Windows camera permissions and that another app is not using it.", "Camera", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            camera.Set(VideoCaptureProperties.FrameWidth, 1280);
            camera.Set(VideoCaptureProperties.FrameHeight, 720);
            cameraTimer = new System.Windows.Forms.Timer { Interval = 33 };
            cameraTimer.Tick += (_, _) => ReadCameraFrame();
            cameraTimer.Start();
            AppendTerminal($"Camera started: {cameraMode.SelectedItem ?? "Putting Camera"}");
        }

        private void ReadCameraFrame()
        {
            if (camera == null || !camera.IsOpened()) return;
            using var frame = new Mat();
            if (!camera.Read(frame) || frame.Empty()) return;
            ShowFrame(frame);
        }

        private void StopCamera()
        {
            cameraTimer?.Stop();
            cameraTimer?.Dispose();
            cameraTimer = null;
            camera?.Release();
            camera?.Dispose();
            camera = null;
        }

        private void SetCameraMode(string mode)
        {
            cameraMode.SelectedItem = mode;
            AppendTerminal($"Camera mode: {mode}");
            StartCamera();
        }

        private void CaptureFrame()
        {
            if (cameraPreview.Image == null)
            {
                MessageBox.Show(this, "Start the camera first.", "Camera", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "PNG image|*.png",
                FileName = $"R10_Camera_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                cameraPreview.Image.Save(dialog.FileName, System.Drawing.Imaging.ImageFormat.Png);
                AppendTerminal($"Camera frame captured: {dialog.FileName}");
            }
        }

        private void ShowFrame(Mat frame)
        {
            if (IsDisposed) return;
            Bitmap bitmap = BitmapConverter.ToBitmap(frame);
            var old = cameraPreview.Image;
            cameraPreview.Image = bitmap;
            old?.Dispose();
        }
    }
}
