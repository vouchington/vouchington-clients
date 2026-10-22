#if DEBUG
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Navigation;

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
    catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
    {
      return false;
    }
  }

  private static int openStarted;
  private static int windowReady;

  internal static Window CreateProductionWindow(IServiceProvider services)
  {
    DisablePreflightAnimations();
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

  private static void DisablePreflightAnimations()
  {
#if MACCATALYST
    UIKit.UIView.AnimationsEnabled = false;
    foreach (var window in UIKit.UIApplication.SharedApplication.Windows)
      window.Layer.RemoveAllAnimations();
#endif
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
          var location = await NavigateAsync(services, shell).ConfigureAwait(false);
          Log(location);
          if (location.Contains("focused", StringComparison.Ordinal) && IsSettingsLocation(location))
          {
            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
            var confirmed = await LocationAsync(shell).ConfigureAwait(false);
            Log($"confirmed {confirmed}");
            if (IsSettingsLocation(confirmed)) return;
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

  private static Task<string> NavigateAsync(IServiceProvider services, AppShell shell) =>
      MainThread.InvokeOnMainThreadAsync(async () =>
      {
        DisablePreflightAnimations();
        await shell.OpenNativePathAsync(NotificationSettingsPath).ConfigureAwait(true);
        var location = shell.CurrentState?.Location.ToString() ?? "";
        if (!IsSettingsLocation(location))
        {
          SelectSettings(shell);
          await shell.OpenNativePathAsync(NotificationSettingsPath).ConfigureAwait(true);
          location = shell.CurrentState?.Location.ToString() ?? "";
        }

        if (shell.CurrentPage is not SettingsPage settings) return location;
        var viewer = services.GetRequiredService<INavigationViewerProvider>().CurrentViewer;
        var match = NativeDeepLinkResolver.Resolve(NotificationSettingsPath, viewer).Match;
        if (match is null) return location + " unmatched";
        await settings.ApplyRouteMatchAsync(match).ConfigureAwait(true);
        return location + " focused";
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
