using System.Text.Json;

namespace Voucha.Client.Core.Auth;

public sealed partial class SessionCookieJar
{
  /// <summary>
  /// Installs the <c>dt</c>/<c>st</c> body tokens a native email login returns, through the same
  /// persisted cookie path as renewal. Tokens already in the jar (for example from <c>Set-Cookie</c>)
  /// are left untouched; new ones take their expiry from the signed JWT <c>exp</c> claim and fail
  /// closed without one. A response whose <paramref name="authenticationIntent"/> was superseded by
  /// a sign-out or newer authentication is dropped, exactly like its <c>Set-Cookie</c> headers.
  /// </summary>
  public async Task InstallNativeLoginTokensAsync(
      string deviceToken,
      string sessionToken,
      long? authenticationIntent = null,
      CancellationToken cancellationToken = default)
  {
    await RestoreAsync(cancellationToken).ConfigureAwait(false);
    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      var now = DateTimeOffset.UtcNow;
      var pending = new List<(string Name, string Value, DateTimeOffset Expires)>();
      foreach (var (name, value) in new[] { ("dt", deviceToken), ("st", sessionToken) })
      {
        if (ReadCookie(name)?.Value == value) continue;
        if (string.IsNullOrWhiteSpace(value) || JwtExpiration(value) is not { } expires || expires <= now)
          throw new UnauthorizedAccessException("Native login returned an unusable session credential.");
        pending.Add((name, value, expires));
      }
      if (pending.Count == 0) return;

      lock (authenticationLock)
      {
        if (authenticationIntent is { } intent && !IsAuthenticationCurrent(intent)) return;
        foreach (var (name, value, expires) in pending) SetCookie(name, value, expires);
        authenticatedSession = null;
        CommitCredentialChange(preserveAuthentication: true);
      }
      try
      {
        await PersistUnlockedAsync(cancellationToken).ConfigureAwait(false);
      }
      catch
      {
        await ClearUnlockedAsync(preserveAuthentication: true, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        throw;
      }
    }
    finally
    {
      sync.Release();
    }
  }

  /// <summary>Reads the body credentials of a successful native email login; null when absent.</summary>
  internal static (string DeviceToken, string SessionToken)? ReadNativeLoginTokens(string body)
  {
    try
    {
      using var document = JsonDocument.Parse(body);
      var root = document.RootElement;
      return root.ValueKind == JsonValueKind.Object &&
          root.TryGetProperty("dt", out var dt) && dt.ValueKind == JsonValueKind.Object &&
          dt.TryGetProperty("token", out var device) && device.ValueKind == JsonValueKind.String &&
          root.TryGetProperty("st", out var st) && st.ValueKind == JsonValueKind.Object &&
          st.TryGetProperty("token", out var session) && session.ValueKind == JsonValueKind.String
          ? (device.GetString()!, session.GetString()!)
          : null;
    }
    catch (JsonException)
    {
      return null;
    }
  }

  internal static DateTimeOffset? JwtExpiration(string token)
  {
    var parts = token.Split('.');
    if (parts.Length != 3) return null;
    try
    {
      var segment = parts[1].Replace('-', '+').Replace('_', '/');
      segment = segment.PadRight(segment.Length + (4 - segment.Length % 4) % 4, '=');
      using var document = JsonDocument.Parse(Convert.FromBase64String(segment));
      return document.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
          ? DateTimeOffset.FromUnixTimeSeconds(seconds)
          : null;
    }
    catch (Exception ex) when (ex is FormatException or JsonException or ArgumentOutOfRangeException or InvalidOperationException)
    {
      return null;
    }
  }
}
