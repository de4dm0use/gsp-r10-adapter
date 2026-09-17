using Microsoft.Extensions.Configuration;
using System.Windows.Forms;

namespace gspro_r10
{
  internal static class Program
  {
    [STAThread]
    public static void Main()
    {
      IConfigurationBuilder builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory());

      if (File.Exists(Path.Join(Directory.GetCurrentDirectory(), "settings.json")))
        builder.AddJsonFile("settings.json");
      else
        BaseLogger.LogMessage($"settings.json file not found or could not be opened in {Directory.GetCurrentDirectory()}", "Main", LogMessageType.Error);

      IConfigurationRoot configuration = builder.Build();
      ApplicationConfiguration.Initialize();
      ConnectionManager manager = new ConnectionManager(configuration);
      Application.Run(new MainForm(manager));
    }
  }
}
