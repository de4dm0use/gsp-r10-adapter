using LaunchMonitor.Proto;
using System.Drawing;
using System.Windows.Forms;

namespace gspro_r10
{
    public class MainForm : Form
    {
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
        private int shotCount;

        public MainForm(ConnectionManager manager)
        {
            this.manager = manager;
            Text = "R10 Shot Tracker";
            Width = 1180;
            Height = 900;
            MinimumSize = new Size(950, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(20, 24, 28);
            ForeColor = Color.White;
            BuildUi();
            manager.ShotReceived += OnShotReceived;
            var bt = manager.BluetoothConnection;
            if (bt != null)
            {
                bt.BatteryUpdated += OnBatteryUpdated;
                bt.StatusChanged += OnStatusChanged;
            }
            FormClosed += (_, _) => manager.Dispose();
            var timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += (_, _) => UpdateConnectionState();
            timer.Start();
            AppendTerminal("R10 Shot Tracker started.");
            AppendTerminal("Nova WebSocket: ws://127.0.0.1:2920/");
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(18), BackColor = BackColor };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 185));
            Controls.Add(root);

            var header = new Panel { Dock = DockStyle.Fill };
            var title = new Label { Text = "R10 SHOT TRACKER", AutoSize = true, Font = new Font("Segoe UI", 22, FontStyle.Bold), Location = new Point(0, 2) };
            connectionLabel.Text = "● Connecting...";
            connectionLabel.AutoSize = true;
            connectionLabel.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            connectionLabel.ForeColor = Color.Gold;
            connectionLabel.Location = new Point(3, 43);
            deviceLabel.Text = "Approach R10";
            deviceLabel.AutoSize = true;
            deviceLabel.ForeColor = Color.LightGray;
            deviceLabel.Font = new Font("Segoe UI", 10);
            deviceLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            deviceLabel.Location = new Point(870, 14);

            connectButton.Text = "CONNECT R10";
            connectButton.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            connectButton.Size = new Size(135, 34);
            connectButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            connectButton.Location = new Point(1005, 38);
            connectButton.Click += (_, _) => ConnectR10();

            header.Controls.Add(title);
            header.Controls.Add(connectionLabel);
            header.Controls.Add(deviceLabel);
            header.Controls.Add(connectButton);
            root.Controls.Add(header, 0, 0);

            var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = BackColor };
            for (int i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            root.Controls.Add(cards, 0, 1);
            carryLabel.Text = "— yd";
            AddCard(cards, 0, "ESTIMATED CARRY", carryLabel, 27);
            apexLabel.Text = "— yd";
            AddCard(cards, 1, "APEX", apexLabel, 25);
            flightLabel.Text = "— s";
            AddCard(cards, 2, "FLIGHT TIME", flightLabel, 25);
            landingSpeedLabel.Text = "— mph";
            AddCard(cards, 3, "LANDING SPEED", landingSpeedLabel, 22);

            var detail = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = BackColor };
            for (int i = 0; i < 3; i++) detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            AddSmallCard(detail, 0, "BALL SPEED", ballSpeedLabel, "— mph");
            AddSmallCard(detail, 1, "LAUNCH ANGLE", launchLabel, "—°");
            AddSmallCard(detail, 2, "BACKSPIN", spinLabel, "— rpm");
            root.Controls.Add(detail, 0, 2);

            shots.Dock = DockStyle.Fill;
            shots.BackgroundColor = Color.FromArgb(27, 32, 37);
            shots.GridColor = Color.FromArgb(55, 62, 68);
            shots.BorderStyle = BorderStyle.None;
            shots.RowHeadersVisible = false;
            shots.AllowUserToAddRows = false;
            shots.ReadOnly = true;
            shots.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            shots.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            shots.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(42, 48, 54), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            shots.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(27, 32, 37), ForeColor = Color.White, SelectionBackColor = Color.FromArgb(50, 90, 70), SelectionForeColor = Color.White };
            shots.Columns.Add("Shot", "#");
            shots.Columns.Add("BallSpeed", "Ball speed");
            shots.Columns.Add("Carry", "Carry");
            shots.Columns.Add("Apex", "Apex");
            shots.Columns.Add("Launch", "Launch");
            shots.Columns.Add("Direction", "Direction");
            shots.Columns.Add("Spin", "Backspin");
            shots.Columns.Add("ClubSpeed", "Club speed");
            shots.Columns.Add("AoA", "Attack angle");
            root.Controls.Add(shots, 0, 3);

            var terminalPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(14, 17, 20), Padding = new Padding(8) };
            var terminalTitle = new Label { Text = "TERMINAL / R10 LOG", AutoSize = true, ForeColor = Color.Silver, Font = new Font("Consolas", 9, FontStyle.Bold), Dock = DockStyle.Top, Height = 22 };
            terminal.Font = new Font("Consolas", 9);
            terminal.BackColor = Color.FromArgb(8, 10, 12);
            terminal.ForeColor = Color.LightGreen;
            terminal.BorderStyle = BorderStyle.None;
            terminal.Dock = DockStyle.Fill;
            terminal.ReadOnly = true;
            terminal.WordWrap = false;
            terminal.ScrollBars = RichTextBoxScrollBars.Vertical;
            terminalPanel.Controls.Add(terminal);
            terminalPanel.Controls.Add(terminalTitle);
            root.Controls.Add(terminalPanel, 0, 4);
        }

        private void ConnectR10()
        {
            AppendTerminal("Manual R10 connection requested...");
            connectButton.Enabled = false;
            var bt = manager.BluetoothConnection;
            if (bt == null)
            {
                AppendTerminal("Bluetooth connection is disabled in settings.");
            }
            else
            {
                bt.Reconnect();
            }
            var timer = new System.Windows.Forms.Timer { Interval = 1500 };
            timer.Tick += (_, _) => { connectButton.Enabled = true; timer.Stop(); timer.Dispose(); };
            timer.Start();
        }

        private static void AddCard(TableLayoutPanel parent, int column, string caption, Label value, int size)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 36, 41), Margin = new Padding(5) };
            var cap = new Label { Text = caption, AutoSize = true, ForeColor = Color.Silver, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(14, 12) };
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
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(25, 30, 35), Margin = new Padding(5) };
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

        private void UpdateConnectionState()
        {
            if (IsDisposed) return;
            var bt = manager.BluetoothConnection;
            if (bt?.LaunchMonitor?.Device?.Gatt?.IsConnected == true)
            {
                connectionLabel.Text = $"● Connected  •  {bt.LaunchMonitor.CurrentState}";
                connectionLabel.ForeColor = Color.LightGreen;
                deviceLabel.Text = $"{bt.LaunchMonitor.Model}  •  FW {bt.LaunchMonitor.Firmware}  •  {bt.LaunchMonitor.Battery}%";
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
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (terminal.InvokeRequired) { terminal.BeginInvoke(() => AppendTerminal(message)); return; }
            terminal.AppendText(line + Environment.NewLine);
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
            double backSpin = totalSpin * System.Math.Cos(-spinAxisDeg * System.Math.PI / 180.0);
            backSpin = System.Math.Max(0, backSpin);
            var trajectory = CarryCalculator.Calculate(mph, ball?.LaunchAngle ?? 0, backSpin);
            shotCount++;
            carryLabel.Text = $"{trajectory.CarryYards:0} yd";
            apexLabel.Text = $"{trajectory.ApexYards:0.0} yd";
            flightLabel.Text = $"{trajectory.FlightTimeSeconds:0.00} s";
            landingSpeedLabel.Text = $"{trajectory.LandingSpeedMph:0.0} mph";
            ballSpeedLabel.Text = $"{mph:0.0} mph";
            launchLabel.Text = $"{ball?.LaunchAngle ?? 0:0.0}°";
            spinLabel.Text = $"{backSpin:0} rpm";
            shots.Rows.Insert(0, shotCount, $"{mph:0.0} mph", $"{trajectory.CarryYards:0} yd", $"{trajectory.ApexYards:0.0} yd", $"{ball?.LaunchAngle ?? 0:0.0}°", $"{ball?.LaunchDirection ?? 0:0.0}°", $"{backSpin:0} rpm", $"{(club?.ClubHeadSpeed ?? 0) * 2.236936:0.0} mph", $"{club?.AttackAngle ?? 0:0.0}°");
            AppendTerminal($"SHOT #{shotCount}: {mph:0.0} mph | Launch {ball?.LaunchAngle ?? 0:0.0}° | Spin {totalSpin:0} rpm | Carry {trajectory.CarryYards:0} yd");
            if (shots.Rows.Count > 200) shots.Rows.RemoveAt(shots.Rows.Count - 1);
        }
    }
}
