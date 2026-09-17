using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LaunchMonitor.Proto;

namespace gspro_r10
{
  /// <summary>
  /// Local Nova-compatible WebSocket output. Clients connect and receive JSON shot/status events.
  /// Default endpoint: ws://127.0.0.1:2920/
  /// </summary>
  public sealed class NovaWebSocketServer : IDisposable
  {
    private readonly HttpListener listener = new();
    private readonly List<WebSocket> clients = new();
    private readonly object clientsLock = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly string firmwareVersion;
    private readonly Func<int> batteryProvider;
    private int shotCount;
    private bool disposed;

    public int Port { get; }

    public NovaWebSocketServer(int port, string firmwareVersion, Func<int>? batteryProvider = null)
    {
      Port = port;
      this.firmwareVersion = firmwareVersion;
      this.batteryProvider = batteryProvider ?? (() => 0);
      listener.Prefixes.Add($"http://127.0.0.1:{port}/");
      listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Start()
    {
      if (disposed) throw new ObjectDisposedException(nameof(NovaWebSocketServer));
      listener.Start();
      _ = AcceptLoopAsync(cancellation.Token);
      NovaLogger.Info($"Nova WebSocket output listening on ws://127.0.0.1:{Port}/");
    }

    public void PublishShot(Metrics metrics)
    {
      if (metrics.BallMetrics == null) return;

      int number = Interlocked.Increment(ref shotCount);
      var b = metrics.BallMetrics;
      var c = metrics.ClubMetrics;

      double spinAxis = -b.SpinAxis;
      var message = new
      {
        type = "shot",
        shot_number = number,
        shot_id = metrics.ShotId,
        timestamp = DateTimeOffset.UtcNow.ToString("O"),
        ball_speed_meters_per_second = b.BallSpeed,
        vertical_launch_angle_degrees = b.LaunchAngle,
        horizontal_launch_angle_degrees = b.LaunchDirection,
        total_spin_rpm = b.TotalSpin,
        spin_axis_degrees = spinAxis,
        backspin_rpm = b.TotalSpin * Math.Cos(spinAxis * Math.PI / 180.0),
        sidespin_rpm = b.TotalSpin * Math.Sin(spinAxis * Math.PI / 180.0),
        club_speed_meters_per_second = c?.ClubHeadSpeed,
        club_path_degrees = c?.ClubAnglePath,
        club_face_to_target_degrees = c?.ClubAngleFace,
        angle_of_attack_degrees = c?.AttackAngle
      };

      Broadcast(JsonSerializer.Serialize(message));
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
      while (!token.IsCancellationRequested)
      {
        HttpListenerContext? context = null;
        try
        {
          context = await listener.GetContextAsync().WaitAsync(token);
          if (!context.Request.IsWebSocketRequest)
          {
            context.Response.StatusCode = 400;
            context.Response.Close();
            continue;
          }

          HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
          WebSocket socket = wsContext.WebSocket;
          lock (clientsLock) clients.Add(socket);
          _ = ClientLoopAsync(socket, token);
          await SendStatusAsync(socket);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
        catch (Exception ex)
        {
          NovaLogger.Error($"Nova WebSocket error: {ex.Message}");
          try { context?.Response.Close(); } catch { }
        }
      }
    }

    private async Task SendStatusAsync(WebSocket socket)
    {
      var status = new
      {
        type = "status",
        uptime_seconds = Environment.TickCount64 / 1000.0,
        firmware_version = firmwareVersion,
        shot_count = Volatile.Read(ref shotCount),
        battery_percent = batteryProvider()
      };
      await SendAsync(socket, JsonSerializer.Serialize(status));
    }

    private async Task ClientLoopAsync(WebSocket socket, CancellationToken token)
    {
      var buffer = new byte[1024];
      try
      {
        while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
          WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, token);
          if (result.MessageType == WebSocketMessageType.Close) break;
        }
      }
      catch { }
      finally
      {
        lock (clientsLock) clients.Remove(socket);
        try { socket.Dispose(); } catch { }
      }
    }

    private void Broadcast(string message)
    {
      WebSocket[] current;
      lock (clientsLock) current = clients.ToArray();
      foreach (WebSocket socket in current)
        _ = SendToClientAsync(socket, message);
    }

    private async Task SendToClientAsync(WebSocket socket, string message)
    {
      try { await SendAsync(socket, message); }
      catch
      {
        lock (clientsLock) clients.Remove(socket);
        try { socket.Dispose(); } catch { }
      }
    }

    private static async Task SendAsync(WebSocket socket, string message)
    {
      if (socket.State != WebSocketState.Open) return;
      byte[] bytes = Encoding.UTF8.GetBytes(message);
      await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      cancellation.Cancel();
      try { listener.Stop(); } catch { }
      try { listener.Close(); } catch { }
      lock (clientsLock)
      {
        foreach (WebSocket socket in clients)
        {
          try { socket.Abort(); socket.Dispose(); } catch { }
        }
        clients.Clear();
      }
      cancellation.Dispose();
    }
  }

  internal static class NovaLogger
  {
    public static void Info(string message) => BaseLogger.LogMessage(message, "NOVA-WS", LogMessageType.Informational, ConsoleColor.Cyan);
    public static void Error(string message) => BaseLogger.LogMessage(message, "NOVA-WS", LogMessageType.Error, ConsoleColor.Cyan);
  }
}
