#if DEBUG
namespace Voucha.Client.App;

internal static partial class CatalystGuiPreflight
{
  internal static void LogStartupFailure(Exception exception)
  {
    if (IsActive()) Log($"startup {exception.GetType().Name}: {exception.Message}");
  }

  private static void Log(string message)
  {
    Console.Error.WriteLine($"catalyst-gui {message}");
    var result = Environment.GetEnvironmentVariable("CATALYST_GUI_RESULT");
    if (string.IsNullOrEmpty(result)) return;
    try
    {
      var directory = Path.GetDirectoryName(result);
      if (string.IsNullOrEmpty(directory)) return;
      File.AppendAllText(Path.Combine(directory, "nav.log"), message + Environment.NewLine);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
    }
  }
}
#endif
