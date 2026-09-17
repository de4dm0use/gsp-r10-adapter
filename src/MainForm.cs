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
    private readonly Label ballSpeedLabel = new();
    private readonly Label launchLabel = new();
    private readonly Label spinLabel = new();
    private readonly DataGridView shots = new();
    private int shotCount;

    public MainForm(ConnectionManager manager)
    {
      this.manager = manager;
      Text = "R10 Shot Tracker";
      Width = 1100;
      Height = 720;
      MinimumSize = new Size(850, 560);
      StartPosition = FormStartPosition.CenterScreen;
      BackColor = Color.FromArgb(20, 24, 28);
      ForeColor = Color.White;

      BuildUi();
      manager.ShotReceived += OnShotReceived;
      var bt = manager.BluetoothConnection;
      if (bt != null) bt.BatteryUpdated += OnBatteryUpdated;
      FormClosed += (_, _) => manager.Dispose();
      var timer = new System.Windows.Forms.Timer { Interval = 500 };
      timer.Tick += (_, _) => UpdateConnectionState();
      timer.Start();
    }

    private void BuildUi()
    {
      var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(18), BackColor = BackColor };
      root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
      root.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
      root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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
      deviceLabel.Location = new Point(790, 14);
      header.Controls.Add(title);
      header.Controls.Add(connectionLabel);
      header.Controls.Add(deviceLabel);
      root.Controls.Add(header, 0, 0);

      var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = BackColor };
      for (int i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
      root.Controls.Add(cards, 0, 1);

      carryLabel.Text = "— yd";
      AddCard(cards, 0, "ESTIMATED CARRY", carryLabel, 28);
      ballSpeedLabel.Text = "— mph";
      AddCard(cards, 1, "BALL SPEED", ballSpeedLabel, 22);
      launchLabel.Text = "—°";
      AddCard(cards, 2, "LAUNCH ANGLE", launchLabel, 22);
      spinLabel.Text = "— rpm";
      AddCard(cards, 3, "BACKSPIN", spinLabel, 22);

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
      shots.Columns.Add("Carry", "Carry (est.)");
      shots.Columns.Add("Launch", "Launch");
      shots.Columns.Add("Direction", "Direction");
      shots.Columns.Add("Spin", "Spin");
      shots.Columns.Add("ClubSpeed", "Club speed");
      shots.Columns.Add("AoA", "Attack angle");
      root.Controls.Add(shots, 0, 2);
    }

    private static void AddCard(TableLayoutPanel parent, int column, string caption, Label value, int size)
    {
      var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 36, 41), Margin = new Padding(5) };
      var cap = new Label { Text = caption, AutoSize = true, ForeColor = Color.Silver, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(14, 12) };
      value.AutoSize = true;
      value.ForeColor = Color.White;
      value.Font = new Font("Segoe UI", size, FontStyle.Bold);
      value.Location = new Point(14, 45);
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
      }
      else
      {
        connectionLabel.Text = "● Connecting...";
        connectionLabel.ForeColor = Color.Gold;
      }
    }

    private void OnBatteryUpdated(int battery)
    {
      if (InvokeRequired) { BeginInvoke(() => OnBatteryUpdated(battery)); return; }
      UpdateConnectionState();
    }

    private void OnShotReceived(Metrics metrics)
    {
      if (InvokeRequired) { BeginInvoke(() => OnShotReceived(metrics)); return; }
      var ball = metrics.BallMetrics;
      var club = metrics.ClubMetrics;
      double mph = (ball?.BallSpeed ?? 0) * 2.236936;
      double carry = CarryCalculator.EstimateCarryYards(mph, ball?.LaunchAngle ?? 0, ball?.TotalSpin ?? 0);
      double spin = ball?.TotalSpin ?? 0;
      shotCount++;

      carryLabel.Text = $"{carry:0} yd";
      ballSpeedLabel.Text = $"{mph:0.0} mph";
      launchLabel.Text = $"{ball?.LaunchAngle ?? 0:0.0}°";
      spinLabel.Text = $"{spin:0} rpm";

      shots.Rows.Insert(0, shotCount, $"{mph:0.0} mph", $"{carry:0} yd", $"{ball?.LaunchAngle ?? 0:0.0}°", $"{ball?.LaunchDirection ?? 0:0.0}°", $"{spin:0} rpm", $"{(club?.ClubHeadSpeed ?? 0) * 2.236936:0.0} mph", $"{club?.AttackAngle ?? 0:0.0}°");
      if (shots.Rows.Count > 200) shots.Rows.RemoveAt(shots.Rows.Count - 1);
    }
  }
}
