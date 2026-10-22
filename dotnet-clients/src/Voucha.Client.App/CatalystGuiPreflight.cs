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

  internal static Window CreateProductionWindow(IServiceProvider services)
  {
    var shell = services.GetRequiredService<AppShell>();
    var window = new Window(shell);
    var opened = 0;
    void OpenOnce(object? sender, EventArgs eventArgs)
    {
      if (Interlocked.Exchange(ref opened, 1) != 0) return;
      _ = OpenNotificationSettingsWhenAuthenticatedAsync(services, shell);
    }

    window.Activated += OpenOnce;
    return window;
  }

  private static async Task OpenNotificationSettingsWhenAuthenticatedAsync(IServiceProvider services, AppShell shell)
  {
    var store = services.GetRequiredService<ISessionStore>();
    var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    void OnChanged(object? sender, SessionChangedEventArgs args)
    {
      if (args.Snapshot.IsAuthenticated) ready.TrySetResult();
    }

    store.SessionChanged += OnChanged;
    if (store.Current.IsAuthenticated) ready.TrySetResult();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    using var registration = timeout.Token.Register(() => ready.TrySetResult());
    try
    {
      await ready.Task.ConfigureAwait(false);
    }
    finally
    {
      store.SessionChanged -= OnChanged;
    }

    await MainThread.InvokeOnMainThreadAsync(() => shell.OpenNativePathAsync(NotificationSettingsPath)).ConfigureAwait(false);
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
