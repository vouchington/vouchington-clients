#if DEBUG
using System.Text.Json;

namespace Voucha.Client.App;

internal static class CatalystGuiPreflight
{
  internal const string LaunchArgument = "--ui-testing-launch-smoke";
  internal const string RootShellAutomationId = "root-shell";
  internal const string SharedIdentityFixtureId = "swift.my.identity.default";

  internal static Page? CreateRoot()
  {
    if (!Environment.GetCommandLineArgs().Contains(LaunchArgument))
      return null;
    if (!SharedIdentityFixtureExists())
      return null;

    return new ContentPage
    {
      Content = new Button
      {
        AutomationId = RootShellAutomationId,
        WidthRequest = 44,
        HeightRequest = 44,
      },
    };
  }

  private static bool SharedIdentityFixtureExists()
  {
    var root = Environment.GetEnvironmentVariable("VOUCHA_FILAMENTS_CONTRACT_ROOT");
    if (string.IsNullOrEmpty(root) || !root.StartsWith('/') || root.Contains("$(") || root.Contains(".."))
      return false;

    var fixtureRoot = Path.Combine(root, "api-fixtures", "v1");
    var manifestPath = Path.Combine(fixtureRoot, "manifest.json");
    if (!File.Exists(manifestPath))
      return false;

    try
    {
      using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
      if (!document.RootElement.TryGetProperty("fixtures", out var fixtures))
        return false;
      foreach (var fixture in fixtures.EnumerateArray())
      {
        if (!fixture.TryGetProperty("id", out var id) || id.GetString() != SharedIdentityFixtureId)
          continue;
        if (!fixture.TryGetProperty("method", out var method) || method.GetString() != "GET")
          return false;
        if (!fixture.TryGetProperty("path", out var path) || path.GetString() != "/api/v1/my/identity")
          return false;
        if (!fixture.TryGetProperty("status", out var status) ||
            !status.TryGetInt32(out var statusCode) ||
            statusCode != 200)
          return false;
        if (!fixture.TryGetProperty("bodyFile", out var bodyFile))
          return false;
        var relativeBody = bodyFile.GetString();
        if (relativeBody is null || !relativeBody.StartsWith("responses/") || relativeBody.Contains(".."))
          return false;
        var bodyPath = Path.GetFullPath(Path.Combine(fixtureRoot, relativeBody));
        var fixtureRootPath = Path.GetFullPath(fixtureRoot) + Path.DirectorySeparatorChar;
        return bodyPath.StartsWith(fixtureRootPath, StringComparison.Ordinal) && File.Exists(bodyPath);
      }
    }
    catch (JsonException)
    {
      return false;
    }

    return false;
  }
}
#endif
