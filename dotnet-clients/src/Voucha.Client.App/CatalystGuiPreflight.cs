#if DEBUG
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Auth;

namespace Voucha.Client.App;

internal static class CatalystGuiPreflight
{
  internal const string LaunchArgument = "--ui-testing-launch-smoke";
  internal const string SharedIdentityFixtureId = "swift.my.identity.default";
  internal const string EmailPreferencesFixtureId = "native.my.email-preferences.default";
  private const string NotificationSettingsPath = "/my/notification-settings";

  internal static bool IsActive() => TryReadFixtures(out _, out _);

  internal static bool TryReadFixtures(out string identityBody, out string emailPreferencesBody)
  {
    identityBody = string.Empty;
    emailPreferencesBody = string.Empty;
    if (!Environment.GetCommandLineArgs().Contains(LaunchArgument)) return false;
    var root = Environment.GetEnvironmentVariable("VOUCHA_FILAMENTS_CONTRACT_ROOT");
    if (!IsAbsoluteFixtureRoot(root)) return false;
    var fixtureRoot = Path.Combine(root!, "api-fixtures", "v1");
    try
    {
      using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtureRoot, "manifest.json")));
      if (!document.RootElement.TryGetProperty("fixtures", out var fixtures)) return false;
      return TryReadFixtureBody(fixtures, fixtureRoot, SharedIdentityFixtureId, "/api/v1/my/identity", out identityBody) &&
          TryReadFixtureBody(fixtures, fixtureRoot, EmailPreferencesFixtureId, "/api/v1/my/email-preferences", out emailPreferencesBody);
    }
    catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
    {
      return false;
    }
  }

  private static int openStarted;
  private static int windowReady;

  internal static Window CreateProductionWindow(IServiceProvider services)
  {
    var shell = services.GetRequiredService<AppShell>();
    var window = new Window(shell);
    EventHandler markReady = (_, _) =>
    {
      Interlocked.Exchange(ref windowReady, 1);
      ScheduleOpen(services, shell);
    };
    window.Created += markReady;
    window.Activated += markReady;
    shell.Loaded += markReady;
    ScheduleOpen(services, shell);
    return window;
  }

  private static void ScheduleOpen(IServiceProvider services, AppShell shell)
  {
    if (Interlocked.Exchange(ref openStarted, 1) != 0) return;
    _ = OpenNotificationSettingsWithRetriesAsync(services, shell);
  }

  private static async Task OpenNotificationSettingsWithRetriesAsync(IServiceProvider services, AppShell shell)
  {
    var store = services.GetRequiredService<ISessionStore>();
    var giveUp = DateTime.UtcNow.AddSeconds(40);
    while (DateTime.UtcNow < giveUp)
    {
      if (Volatile.Read(ref windowReady) == 1 && store.Current.IsAuthenticated)
      {
        try
        {
          var location = await NavigateAsync(shell).ConfigureAwait(false);
          Log($"location {location}");
          if (IsSettingsLocation(location))
          {
            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
            var confirmed = await LocationAsync(shell).ConfigureAwait(false);
            Log($"confirmed {confirmed}");
            if (IsSettingsLocation(confirmed))
            {
              ArmReassert(shell);
              return;
            }
          }
        }
        catch (Exception exception)
        {
          Log($"navigation {exception.GetType().Name}: {exception.Message}");
        }
      }

      await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
    }

    Log("navigation gave up");
  }

  private static void ArmReassert(AppShell shell)
  {
    var until = DateTime.UtcNow.AddSeconds(10);
    EventHandler<ShellNavigatedEventArgs>? handler = null;
    handler = (_, _) =>
    {
      if (DateTime.UtcNow > until)
      {
        shell.Navigated -= handler;
        return;
      }

      var location = shell.CurrentState?.Location.ToString() ?? "";
      if (IsSettingsLocation(location)) return;
      shell.Navigated -= handler;
      Log($"left settings for {location}");
      _ = NavigateAsync(shell);
    };
    shell.Navigated += handler;
  }

  private static Task<string> NavigateAsync(AppShell shell) =>
      MainThread.InvokeOnMainThreadAsync(async () =>
      {
        await shell.OpenNativePathAsync(NotificationSettingsPath).ConfigureAwait(true);
        var location = shell.CurrentState?.Location.ToString() ?? "";
        if (IsSettingsLocation(location)) return location;
        SelectSettings(shell);
        await shell.OpenNativePathAsync(NotificationSettingsPath).ConfigureAwait(true);
        return shell.CurrentState?.Location.ToString() ?? "";
      });

  private static Task<string> LocationAsync(AppShell shell) =>
      MainThread.InvokeOnMainThreadAsync(() => shell.CurrentState?.Location.ToString() ?? "");

  private static void SelectSettings(AppShell shell)
  {
    foreach (var item in shell.Items)
    {
      foreach (var section in item.Items)
      {
        foreach (var content in section.Items)
        {
          if (content.Route != "settings") continue;
          shell.CurrentItem = item;
          return;
        }
      }
    }
  }

  private static bool IsSettingsLocation(string location) =>
      location.Contains("settings", StringComparison.OrdinalIgnoreCase);

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

  private static bool TryReadFixtureBody(
      JsonElement fixtures, string fixtureRoot, string id, string path, out string body)
  {
    body = string.Empty;
    foreach (var fixture in fixtures.EnumerateArray())
    {
      if (Text(fixture, "id") != id) continue;
      if (Text(fixture, "method") != "GET" || Text(fixture, "path") != path) return false;
      if (!fixture.TryGetProperty("status", out var status) || !status.TryGetInt32(out var code) || code != 200)
        return false;
      var relative = Text(fixture, "bodyFile");
      if (relative is null || !relative.StartsWith("responses/", StringComparison.Ordinal) ||
          relative.Contains("..", StringComparison.Ordinal)) return false;
      var bodyPath = Path.GetFullPath(Path.Combine(fixtureRoot, relative));
      var rootPrefix = Path.GetFullPath(fixtureRoot) + Path.DirectorySeparatorChar;
      if (!bodyPath.StartsWith(rootPrefix, StringComparison.Ordinal) || !File.Exists(bodyPath)) return false;
      body = File.ReadAllText(bodyPath);
      return body.Length > 0;
    }

    return false;
  }

  private static bool IsAbsoluteFixtureRoot(string? root) =>
      !string.IsNullOrEmpty(root) && root.StartsWith('/') && !root.Contains("$(") && !root.Contains("..");

  private static string? Text(JsonElement fixture, string name) =>
      fixture.TryGetProperty(name, out var value) ? value.GetString() : null;
}
#endif
