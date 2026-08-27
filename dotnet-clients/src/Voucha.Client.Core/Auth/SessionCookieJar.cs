using System.Net;
using System.Text.Json;

namespace Voucha.Client.Core.Auth;

public sealed partial class SessionCookieJar : IDisposable
{
  private static readonly string[] SessionCookieNames = ["dt", "st"];
  private readonly CookieContainer cookieContainer;
  private readonly Uri apiBaseUrl;
  private readonly ISessionCookiePersistence persistence;
  private readonly SemaphoreSlim sync = new(1, 1);
  private volatile bool isRestored;
  private string? lastSerializedCookies;

  public SessionCookieJar(
      CookieContainer cookieContainer,
      Uri apiBaseUrl,
      ISessionCookiePersistence persistence)
  {
    this.cookieContainer = cookieContainer ?? throw new ArgumentNullException(nameof(cookieContainer));
    this.apiBaseUrl = apiBaseUrl ?? throw new ArgumentNullException(nameof(apiBaseUrl));
    this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
  }

  public CookieContainer CookieContainer => cookieContainer;

  public async Task RestoreAsync(CancellationToken cancellationToken = default)
  {
    if (isRestored) return;

    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (isRestored) return;

      var serializedCookies = await persistence.ReadAsync(cancellationToken).ConfigureAwait(false);
      if (string.IsNullOrWhiteSpace(serializedCookies))
      {
        isRestored = true;
        return;
      }

      SessionCookieState? state;
      try
      {
        state = JsonSerializer.Deserialize<SessionCookieState>(serializedCookies);
      }
      catch (JsonException)
      {
        await ClearUnlockedAsync(cancellationToken).ConfigureAwait(false);
        return;
      }

      if (state?.Cookies is null || !HasCompleteSession(state.Cookies))
      {
        await ClearUnlockedAsync(cancellationToken).ConfigureAwait(false);
        return;
      }

      ClearMemoryUnlocked();
      foreach (var cookie in state.Cookies.Where(IsKnownCookie))
      {
        SetCookie(cookie.Name, cookie.Value, cookie.ExpiresUtc);
      }
      lastSerializedCookies = serializedCookies;
      isRestored = true;
    }
    finally
    {
      sync.Release();
    }
  }

  public async Task PersistAsync(CancellationToken cancellationToken = default)
  {
    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      await PersistUnlockedAsync(cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      sync.Release();
    }
  }

  public async Task ClearAsync(CancellationToken cancellationToken = default)
  {
    await sync.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      await ClearUnlockedAsync(cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      sync.Release();
    }
  }

  public void StoreSetCookieHeaders(Uri? requestUri, IEnumerable<string> setCookieHeaders)
  {
    ArgumentNullException.ThrowIfNull(setCookieHeaders);
    if (requestUri is null || !IsSameOrigin(requestUri)) return;

    foreach (var header in setCookieHeaders)
    {
      try
      {
        cookieContainer.SetCookies(apiBaseUrl, header);
      }
      catch (CookieException)
      {
        // Ignore malformed Set-Cookie headers instead of failing the API response.
      }
    }
  }

  public void Dispose() => sync.Dispose();

  private async Task ClearUnlockedAsync(CancellationToken cancellationToken)
  {
    ClearMemoryUnlocked();
    await persistence.ClearAsync(cancellationToken).ConfigureAwait(false);
    lastSerializedCookies = null;
    isRestored = true;
  }

  private async Task PersistUnlockedAsync(CancellationToken cancellationToken)
  {
    var cookies = SessionCookieNames
        .Select(ReadCookie)
        .OfType<SessionCookie>()
        .ToArray();
    if (cookies.Length != SessionCookieNames.Length)
    {
      await ClearUnlockedAsync(cancellationToken).ConfigureAwait(false);
      return;
    }

    var state = new SessionCookieState(cookies);
    var serialized = JsonSerializer.Serialize(state);
    if (serialized == lastSerializedCookies)
    {
      isRestored = true;
      return;
    }

    await persistence
        .WriteAsync(serialized, cancellationToken)
        .ConfigureAwait(false);
    lastSerializedCookies = serialized;
    isRestored = true;
  }

  private void ClearMemoryUnlocked()
  {
    foreach (var name in SessionCookieNames)
    {
      ExpireCookie(name);
    }
  }

  private SessionCookie? ReadCookie(string name)
  {
    var cookie = cookieContainer.GetCookies(apiBaseUrl)[name];
    return cookie is null || string.IsNullOrEmpty(cookie.Value)
        ? null
        : new SessionCookie(
            name,
            cookie.Value,
            cookie.Expires == DateTime.MinValue ? null : new DateTimeOffset(cookie.Expires.ToUniversalTime()));
  }

  private void SetCookie(string name, string value, DateTimeOffset? expiresUtc = null) =>
      cookieContainer.Add(apiBaseUrl, new Cookie(name, value, "/")
      {
        HttpOnly = true,
        Secure = apiBaseUrl.Scheme == Uri.UriSchemeHttps,
        Expires = expiresUtc?.UtcDateTime ?? DateTime.MinValue,
      });

  private void ExpireCookie(string name) =>
      cookieContainer.Add(apiBaseUrl, new Cookie(name, "", "/") { Expired = true });

  private bool IsSameOrigin(Uri requestUri) =>
      string.Equals(requestUri.Scheme, apiBaseUrl.Scheme, StringComparison.OrdinalIgnoreCase) &&
      string.Equals(requestUri.Host, apiBaseUrl.Host, StringComparison.OrdinalIgnoreCase) &&
      requestUri.Port == apiBaseUrl.Port;

  private static bool IsKnownCookie(SessionCookie cookie) =>
      SessionCookieNames.Contains(cookie.Name, StringComparer.Ordinal) &&
      !string.IsNullOrEmpty(cookie.Value);

  private static bool HasCompleteSession(IReadOnlyList<SessionCookie> cookies) =>
      SessionCookieNames.All(name => cookies.Any(cookie =>
          string.Equals(cookie.Name, name, StringComparison.Ordinal) &&
          !string.IsNullOrEmpty(cookie.Value)));

}
