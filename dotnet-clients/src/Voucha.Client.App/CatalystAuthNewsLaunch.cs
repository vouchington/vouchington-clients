#if DEBUG
using System.Text.Json;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

// Explicit deterministic UI-backend seam. These legacy auth responses are client-owned,
// not producer fixture parity or a live email/Turnstile integration.
internal static class CatalystAuthNewsLaunch
{
  internal const string Argument = "--ui-testing-auth-news-flows";
  internal const string Email = "fixture@example.test";
  internal const string Code = "ABCDEFGHJK";
  internal const string TurnstileToken = "catalyst-ui-test-token";
  internal static bool IsRequested => Environment.GetCommandLineArgs().Contains(Argument);

  internal static IDeviceLanguageProvider DeviceLanguages(IDeviceLanguageProvider normal, bool active) =>
      active ? new EnglishFlowLanguages() : normal;

  internal static void RequireEnglishIdentity(string body)
  {
    using var document = JsonDocument.Parse(body);
    var identity = document.RootElement.GetProperty("identity");
    if (identity.TryGetProperty("ui_locale", out var locale) && locale.GetString() is not (null or "" or "en"))
      throw new InvalidOperationException("Auth/news identity fixture must not override the English flow locale.");
  }

  private sealed class EnglishFlowLanguages : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["en"];
  }

  internal static string ReadFixture(string id, string method, string path)
  {
    var root = Environment.GetEnvironmentVariable("VOUCHA_CONTRACT_ROOT");
    if (string.IsNullOrEmpty(root) || !root.StartsWith('/') || root.Contains("..") || root.Contains("$("))
      throw new InvalidOperationException("Auth/news GUI requires an absolute contract root.");
    var fixtureRoot = Path.Combine(root, "api-fixtures", "v1");
    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtureRoot, "manifest.json")));
    var fixture = document.RootElement.GetProperty("fixtures").EnumerateArray()
        .Single(item => item.GetProperty("id").GetString() == id);
    var relative = fixture.GetProperty("bodyFile").GetString();
    if (fixture.GetProperty("method").GetString() != method || fixture.GetProperty("path").GetString() != path ||
        fixture.GetProperty("status").GetInt32() != 200 || relative is null ||
        !relative.StartsWith("responses/", StringComparison.Ordinal) || relative.Contains(".."))
      throw new InvalidOperationException($"Invalid auth/news GUI fixture {id}.");
    var body = File.ReadAllText(Path.Combine(fixtureRoot, relative));
    if (id == "swift.my.identity.default") RequireEnglishIdentity(body);
    return body;
  }
}

internal sealed class CatalystAuthNewsTurnstile(bool active) : ITurnstileTokenProvider
{
  public Task<string> GetTokenAsync(string routeAction, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    if (!active || routeAction != "auth.email-address-tokens")
      throw new InvalidOperationException("Unexpected auth/news Turnstile request.");
    return Task.FromResult(CatalystAuthNewsLaunch.TurnstileToken);
  }
}

internal sealed class CatalystAuthNewsSessionPersistence : ISessionCookiePersistence
{
  private string? cookies;
  public Task<string?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(cookies);
  public Task WriteAsync(string serializedCookies, CancellationToken cancellationToken = default)
  {
    cookies = serializedCookies;
    return Task.CompletedTask;
  }
  public Task ClearAsync(CancellationToken cancellationToken = default)
  {
    cookies = null;
    return Task.CompletedTask;
  }
}
#endif
