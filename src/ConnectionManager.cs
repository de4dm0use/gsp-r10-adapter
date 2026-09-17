using System.Text.Json;
using System.Text.Json.Serialization;
using gspro_r10.OpenConnect;
using Microsoft.Extensions.Configuration;
using LaunchMonitor.Proto;

namespace gspro_r10
{
  public class ConnectionManager: IDisposable
  {
    private R10ConnectionServer? R10Server;
    private OpenConnectClient OpenConnectClient;
    private NovaWebSocketServer? NovaServer;
    public BluetoothConnection? BluetoothConnection { get; private set; }
    internal HttpPuttingServer? PuttingConnection { get; }
    public event ClubChangedEventHandler? ClubChanged;
    public event Action<Metrics>? ShotReceived;
    public delegate void ClubChangedEventHandler(object sender, ClubChangedEventArgs e);
    public class ClubChangedEventArgs: EventArgs { public Club Club { get; set; } }

    private JsonSerializerOptions serializerSettings = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private int shotNumber = 0;
    private bool disposedValue;

    public ConnectionManager(IConfigurationRoot configuration)
    {
      OpenConnectClient = new OpenConnectClient(this, configuration.GetSection("openConnect"));
      OpenConnectClient.ConnectAsync();

      if (bool.Parse(configuration.GetSection("r10E6Server")["enabled"] ?? "false"))
      {
        R10Server = new R10ConnectionServer(this, configuration.GetSection("r10E6Server"));
        R10Server.Start();
      }

      var novaConfig = configuration.GetSection("novaWebSocket");
      if (bool.Parse(novaConfig["enabled"] ?? "true"))
      {
        int port = int.Parse(novaConfig["port"] ?? "2920");
        NovaServer = new NovaWebSocketServer(
          port,
          "Garmin Approach R10",
          () => BluetoothConnection?.LaunchMonitor?.Battery ?? 0
        );
        NovaServer.Start();
      }

      if (bool.Parse(configuration.GetSection("bluetooth")["enabled"] ?? "false"))
      {
        BluetoothConnection = new BluetoothConnection(this, configuration.GetSection("bluetooth"));
        BluetoothConnection.ShotReceived += e =>
        {
          ShotReceived?.Invoke(e);
          NovaServer?.PublishShot(e);
        };
      }

      if (bool.Parse(configuration.GetSection("putting")["enabled"] ?? "false"))
      {
        PuttingConnection = new HttpPuttingServer(this, configuration.GetSection("putting"));
        PuttingConnection.Start();
      }
    }

    internal void SendShot(OpenConnect.BallData? ballData, OpenConnect.ClubData? clubData)
    {
      string openConnectMessage = JsonSerializer.Serialize(OpenConnectApiMessage.CreateShotData(shotNumber++, ballData, clubData), serializerSettings);
      OpenConnectClient.SendAsync(openConnectMessage);
    }

    public void ClubUpdate(Club club) => Task.Run(() => ClubChanged?.Invoke(this, new ClubChangedEventArgs { Club = club }));
    internal void SendLaunchMonitorReadyUpdate(bool deviceReady) => OpenConnectClient.SetDeviceReady(deviceReady);

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          NovaServer?.Dispose();
          R10Server?.Dispose();
          PuttingConnection?.Dispose();
          BluetoothConnection?.Dispose();
          OpenConnectClient?.DisconnectAndStop();
          OpenConnectClient?.Dispose();
        }
        disposedValue = true;
      }
    }

    public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
  }
}